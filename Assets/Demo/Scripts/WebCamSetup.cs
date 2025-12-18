using Mediapipe.Tasks.Vision.FaceLandmarker;
using Mediapipe.Unity;
using Mediapipe.Unity.CoordinateSystem;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace BirdSky
{
    [Serializable]
    public enum WebCamSetupState
    {
        None,
        Setup,
    }
    public class WebCamSetup : MonoBehaviour
    {
        [Header("basic")]
        public WebCamSetupState state = WebCamSetupState.None;
        public SerializableDictionary<WebCamSetupState, GameObject> stateGameobjects;
        public UnityEvent onDetectFinish;
        [Header("UI")]
        [SerializeField] private RawImage screen;
        [SerializeField] private int width = 1280;
        [SerializeField] private int height = 720;
        [SerializeField] private int fps = 30;
        private WebCamTexture webCamTexture;

        public int webCamDeviceIndex = 0;
        [SerializeField] private Text camText;

        [SerializeField] private TextAsset modelAsset;
        private FaceLandmarkerResult result;

        private GameObject mouthTopMark;
        private GameObject mouthBottomMark;
        private GameObject mouthSide1Mark;
        private GameObject mouthSide2Mark;
        public void QuitWebCamSetup()
        {
            state = WebCamSetupState.None;
            foreach (var item in stateGameobjects)
            {
                item.Value.SetActive(false);
            }
            if (webCamTexture != null)
            {
                webCamTexture.Stop();
            }
        }
        public void EnterWebCamSetup()
        {
            if (state != WebCamSetupState.None)
            {
                Debug.LogWarning("WebCam setup is already in progress.");
                return;
            }
            state = WebCamSetupState.Setup;
            stateGameobjects[state].SetActive(true);

            StartSetup();
        }
        public void StartSetup()
        {
            StartCoroutine(Init());
        }
        public void EndSetup()
        {
            stateGameobjects[state].SetActive(false);
            onDetectFinish.Invoke();
            QuitWebCamSetup();
        }
        private IEnumerator Init()
        {
            if (WebCamTexture.devices.Length == 0)
            {
                throw new System.Exception("Web Camera devices are not found");
            }
            var webCamDevice = WebCamTexture.devices[webCamDeviceIndex];
            camText.text = webCamDevice.name;
            webCamTexture = new WebCamTexture(webCamDevice.name, width, height, fps);
            webCamTexture.Play();

            // NOTE: On macOS, the contents of webCamTexture may not be readable immediately, so wait until it is readable
            yield return new WaitUntil(() => webCamTexture.width > 16);

            screen.rectTransform.sizeDelta = new Vector2(width, height);
            screen.texture = webCamTexture;

            result = default(FaceLandmarkerResult);
            var options = new FaceLandmarkerOptions(
                baseOptions: new Mediapipe.Tasks.Core.BaseOptions(
                    Mediapipe.Tasks.Core.BaseOptions.Delegate.CPU,
                    modelAssetBuffer: modelAsset.bytes
                ),
                runningMode: Mediapipe.Tasks.Vision.Core.RunningMode.VIDEO
            );

            using var faceLandmarker = FaceLandmarker.CreateFromOptions(options);

            var stopwatch = new System.Diagnostics.Stopwatch();
            stopwatch.Start();

            var waitForEndOfFrame = new WaitForEndOfFrame();
            using var textureFrame = new Mediapipe.Unity.Experimental.TextureFrame(webCamTexture.width, webCamTexture.height, TextureFormat.RGBA32);

            var screenRect = screen.rectTransform.rect;
            InitMouthMark(ref mouthTopMark);
            InitMouthMark(ref mouthBottomMark);
            InitMouthMark(ref mouthSide1Mark);
            InitMouthMark(ref mouthSide2Mark);

            while (true)
            {
                var imageTransformationOptions = Mediapipe.Unity.Experimental.ImageTransformationOptions.Build(
                    shouldFlipHorizontally: webCamDevice.isFrontFacing,
                    isVerticallyFlipped: webCamTexture.videoVerticallyMirrored,
                    rotation: (RotationAngle)webCamTexture.videoRotationAngle
                );
                var flipHorizontally = !imageTransformationOptions.flipHorizontally;
                var flipVertically = imageTransformationOptions.flipVertically;
                var imageProcessingOptions = new Mediapipe.Tasks.Vision.Core.ImageProcessingOptions(rotationDegrees: (int)imageTransformationOptions.rotationAngle);

                textureFrame.ReadTextureOnCPU(webCamTexture, flipHorizontally, flipVertically);
                using var image = textureFrame.BuildCPUImage();

                if (faceLandmarker.TryDetectForVideo(image, stopwatch.ElapsedMilliseconds, imageProcessingOptions, ref result))
                {
                    if (result.faceLandmarks?.Count > 0)
                    {
                        var landmarks = result.faceLandmarks[0].landmarks;

                        var topOfMouth = landmarks[13];
                        var position = screenRect.GetPoint(in topOfMouth);
                        position.z = 0;
                        mouthTopMark.transform.localPosition = position;
                        mouthTopMark.SetActive(true);

                        var bottomOfMouth = landmarks[14];
                        position = screenRect.GetPoint(in bottomOfMouth);
                        position.z = 0;
                        mouthBottomMark.transform.localPosition = position;
                        mouthBottomMark.SetActive(true);

                        var side1OfMouth = landmarks[78];
                        position = screenRect.GetPoint(in side1OfMouth);
                        position.z = 0;
                        mouthSide1Mark.transform.localPosition = position;
                        mouthSide1Mark.SetActive(true);

                        var side2OfMouth = landmarks[308];
                        position = screenRect.GetPoint(in side2OfMouth);
                        position.z = 0;
                        mouthSide2Mark.transform.localPosition = position;
                        mouthSide2Mark.SetActive(true);

                        var lipWidth = (new Vector3(side1OfMouth.x, side1OfMouth.y, side1OfMouth.z) - new Vector3(side2OfMouth.x, side2OfMouth.y, side2OfMouth.z)).magnitude;
                        var lipOpenHeight = (new Vector3(topOfMouth.x, topOfMouth.y, topOfMouth.z) - new Vector3(bottomOfMouth.x, bottomOfMouth.y, bottomOfMouth.z)).magnitude;
                    }
                    else
                    {
                        mouthTopMark.SetActive(false);
                        mouthBottomMark.SetActive(false);
                        mouthSide1Mark.SetActive(false);
                        mouthSide2Mark.SetActive(false);
                    }
                }

                yield return waitForEndOfFrame;
            }
        }
        private void InitMouthMark(ref GameObject mouthMark)
        {
            if (mouthMark != null) return;
            mouthMark = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            mouthMark.transform.SetParent(screen.transform);
            mouthMark.transform.localPosition = new Vector3(0, 0, 0);
            mouthMark.transform.localScale = new Vector3(10f, 10f, 10f);
            mouthMark.SetActive(false);
        }

        public void SwitchWebCamDevice()
        {
            int deviceNum = WebCamTexture.devices.Length;
            if (deviceNum > 0)
            {
                webCamDeviceIndex = (webCamDeviceIndex + 1) % deviceNum;
                if (webCamTexture != null)
                {
                    webCamTexture.Stop();
                }
                StartCoroutine(Init());
            }
        }
        private void OnDestroy()
        {
            if (webCamTexture != null)
            {
                webCamTexture.Stop();
            }
        }
    }
}