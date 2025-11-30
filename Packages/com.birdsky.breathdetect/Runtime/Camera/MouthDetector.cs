using Mediapipe.Tasks.Components.Containers;
using Mediapipe.Tasks.Vision.FaceLandmarker;
using Mediapipe.Unity;
using Mediapipe.Unity.Experimental;
using NaughtyAttributes;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using UnityEngine;
using UnityEngine.Device;

namespace BreathDetect
{
    public class MouthDetector : MonoBehaviour
    {
        private static MouthDetector instance;
        void Awake()
        {
            if (instance != null)
            {
                Destroy(gameObject);
                return;
            }
            DontDestroyOnLoad(gameObject);
            instance = this;

            StartCoroutine(Init());
        }
        public static bool isDetecting = false;
        public static bool isReady = false;
        public static bool hasDetectedFullMouth = false;
        public static int webCamDeviceIndex = 0;
        private WebCamDevice webCamDevice;
        [NonSerialized] public WebCamTexture webCamTexture;
        private TextureFrame textureFrame;
        [SerializeField] private TextAsset modelAsset;
        private FaceLandmarker faceLandmarker;
        private FaceLandmarkerResult result;
        private Stopwatch stopwatch;
        [ReadOnly, SerializeField] private float mouthOpeningDegree;
        public static float MouthOpeningDegree => instance != null ? instance.mouthOpeningDegree : 0;
        private IEnumerator Init()
        {
            webCamTexture?.Stop();
            isReady = false;
            if (WebCamTexture.devices.Length == 0)
            {
                throw new System.Exception("Web Camera devices are not found");
            }
            webCamDevice = WebCamTexture.devices[webCamDeviceIndex];
            int width = 1280;
            int height = 720;
            int fps = 30;
            webCamTexture = new WebCamTexture(webCamDevice.name, width, height, fps);
            stopwatch = new Stopwatch();

            // NOTE: On macOS, the contents of webCamTexture may not be readable immediately, so wait until it is readable
            yield return new WaitUntil(() => webCamTexture.width > 16);

            var options = new FaceLandmarkerOptions(
                baseOptions: new Mediapipe.Tasks.Core.BaseOptions(
                    Mediapipe.Tasks.Core.BaseOptions.Delegate.CPU,
                    modelAssetBuffer: modelAsset.bytes
                ),
                runningMode: Mediapipe.Tasks.Vision.Core.RunningMode.VIDEO
            );
            faceLandmarker = FaceLandmarker.CreateFromOptions(options);
            result = default(FaceLandmarkerResult);

            textureFrame = new TextureFrame(webCamTexture.width, webCamTexture.height, TextureFormat.RGBA32);
            isReady = true;
        }
        private void Update()
        {
            mouthOpeningDegree = 0f;
            if (!isDetecting || !isReady)
            {
                return;
            }
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

            hasDetectedFullMouth = false;
            if (faceLandmarker.TryDetectForVideo(image, stopwatch.ElapsedMilliseconds, imageProcessingOptions, ref result))
            {
                if (result.faceLandmarks?.Count > 0)
                {
                    var landmarks = result.faceLandmarks[0].landmarks;
                    var topOfMouth = landmarks[13];
                    var bottomOfMouth = landmarks[14];
                    var side1OfMouth = landmarks[78];
                    var side2OfMouth = landmarks[308];
                    hasDetectedFullMouth = !(IsOutOfWebCam(topOfMouth) || IsOutOfWebCam(bottomOfMouth) || IsOutOfWebCam(side1OfMouth) || IsOutOfWebCam(side2OfMouth));
                    var lipWidth = (new Vector3(side1OfMouth.x, side1OfMouth.y, side1OfMouth.z) - new Vector3(side2OfMouth.x, side2OfMouth.y, side2OfMouth.z)).magnitude;
                    var lipOpenHeight = (new Vector3(topOfMouth.x, topOfMouth.y, topOfMouth.z) - new Vector3(bottomOfMouth.x, bottomOfMouth.y, bottomOfMouth.z)).magnitude;
                    mouthOpeningDegree = Mathf.Clamp01(lipOpenHeight / lipWidth);
                }
            }
        }
        private void OnDestroy()
        {
            webCamTexture?.Stop();
        }
        private bool IsOutOfWebCam(NormalizedLandmark landmark)
        {
            float threshold = 0.025f;
            return !(landmark.x >= threshold && landmark.x <= 1 - threshold && landmark.y >= threshold && landmark.y <= 1 - threshold);
        }
        public static void StartMouthDetect()
        {
            if (instance == null) return;
            if (isDetecting) return;
            isDetecting = true;
            instance.webCamTexture.Play();
            instance.stopwatch.Start();
        }
        public static void StopMouthDetect()
        {
            if (instance == null) return;
            if (!isDetecting) return;
            isDetecting = false;
            instance.webCamTexture.Pause();
            instance.stopwatch.Stop();
        }
        public static Vector3 CalculateMouthConfidence(float huAverage, float xiAverage, float bingAverage)
        {
            if (!isReady || !hasDetectedFullMouth)
            {
                return Vector3.zero;
            }
            var res = Vector3.zero;
            // hu
            res.x = MouthOpeningDegree >= huAverage * 0.8f ? 1 : Mathf.Exp(-4 * Mathf.Abs(MouthOpeningDegree - huAverage));
            // xi
            res.y = Mathf.Exp(-8 * Mathf.Abs(MouthOpeningDegree - xiAverage));
            // bing
            res.z = Mathf.Exp(-8 * Mathf.Abs(MouthOpeningDegree - bingAverage));
            return res;
        }
    }
}
