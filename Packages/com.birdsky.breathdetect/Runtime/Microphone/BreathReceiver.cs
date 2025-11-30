using Cysharp.Threading.Tasks;
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;

namespace BreathDetect
{
    public enum BreathReceiverMode
    {
        None,
        NoiseDetect,
        AudioSetup,
        BreathSample,
        BreathDetect
    }
    public class BreathReceiver : MonoBehaviour
    {
        private static BreathReceiver instance;
        void Awake()
        {
            if (instance != null)
            {
                Destroy(gameObject);
                return;
            }
            DontDestroyOnLoad(gameObject);
            instance = this;
            onUpdate = DefaultOnUpdate;
        }
        public static BreathReceiverMode mode = BreathReceiverMode.None;
        private delegate void OnUpdate();
        private static OnUpdate onUpdate;
        private void DefaultOnUpdate() { }
        #region NoiseDetect
        public delegate void OnDetectNoise(float dB);
        private static OnDetectNoise onDetectNoise;
        public static void StartNoiseDetect(OnDetectNoise processor)
        {
            mode = BreathReceiverMode.NoiseDetect;
            MicrophoneDevice.StartMicrophone();
            onUpdate = instance.UpdateNoiseDetect;
            onDetectNoise = processor;
        }
        public static void StopNoiseDetect()
        {
            mode = BreathReceiverMode.None;
            MicrophoneDevice.StopMicrophone();
            onUpdate = instance.DefaultOnUpdate;
            onDetectNoise = null;
        }
        private void UpdateNoiseDetect()
        {
            if (mode != BreathReceiverMode.NoiseDetect || !MicrophoneDevice.microphoneStarted)
                return;
            float originalMultiplier_dB = MicrophoneDevice.microphoneAudioMultiplier_dB;
            MicrophoneDevice.microphoneAudioMultiplier_dB = 0f;
            bool microphoneSamplesPrepared = MicrophoneDevice.UpdateMicrophone();
            MicrophoneDevice.microphoneAudioMultiplier_dB = originalMultiplier_dB;
            if (!microphoneSamplesPrepared)
                return;

            float dB = SignalProcessor.CalculateRMS_dB(MicrophoneDevice.samples);
            onDetectNoise(dB);
        }
        #endregion
        #region AudioSetup
        public AudioSource microphoneAudioPlayer;
        /// <summary>
        /// 开始麦克风声音测试，录入的声音将被播放便于用户评估录制效果
        /// </summary>
        public static async void StartMicrophoneAudioTest()
        {
            mode = BreathReceiverMode.AudioSetup;
            MicrophoneDevice.StartMicrophone(300);
            instance.microphoneAudioPlayer.clip = MicrophoneDevice.microphoneClip;
            await UniTask.WaitUntil(() => Microphone.GetPosition(MicrophoneDevice.CurrentDeviceName) > 0);
            instance.microphoneAudioPlayer.Play();
        }
        public static void StopMicrophoneAudioTest()
        {
            mode = BreathReceiverMode.None;
            MicrophoneDevice.StopMicrophone();
        }
        public delegate void OnUpdateAudioReceiveSetup();
        private static OnUpdateAudioReceiveSetup onUpdateAudioReceiveSetup;
        public static void StartAudioReceiveSetup(OnUpdateAudioReceiveSetup processor)
        {
            mode = BreathReceiverMode.AudioSetup;
            MicrophoneDevice.StartMicrophone();
            onUpdate = instance.UpdateAudioReceiveSetup;
            onUpdateAudioReceiveSetup = processor;
        }
        public static void StopAudioReceiveSetup()
        {
            mode = BreathReceiverMode.None;
            MicrophoneDevice.StopMicrophone();
            onUpdate = instance.DefaultOnUpdate;
            onUpdateAudioReceiveSetup = null;
        }
        private void UpdateAudioReceiveSetup()
        {
            if (mode != BreathReceiverMode.AudioSetup || !MicrophoneDevice.microphoneStarted)
                return;
            bool microphoneSamplesPrepared = MicrophoneDevice.UpdateMicrophone();
            if (!microphoneSamplesPrepared)
                return;

            onUpdateAudioReceiveSetup();
        }
        public static void SetMicrophoneReceiveMultiplier_dB(float dB)
        {
            SaveData.Instance.microphoneAudioMultiplier_dB = MicrophoneDevice.microphoneAudioMultiplier_dB = dB;
        }
        #endregion
        #region BreathSample
        public delegate void OnProcessSampleData(float energy, float deltaTime, float mainFrequency);
        private static OnProcessSampleData onProcessSampleData;
        /// <summary>
        /// 开始采样用户呼吸，以形成呼吸检测所用的阈值 / 采样完成开始进行阈值测试
        /// </summary>
        public static void StartBreathSample(OnProcessSampleData processor)
        {
            mode = BreathReceiverMode.BreathSample;
            MicrophoneDevice.StartMicrophone();
            onUpdate = instance.UpdateBreathSample;
            onProcessSampleData = processor;
        }
        public static void StopBreathSample()
        {
            mode = BreathReceiverMode.None;
            MicrophoneDevice.StopMicrophone();
            onUpdate = instance.DefaultOnUpdate;
            onProcessSampleData = null;
        }
        private void UpdateBreathSample()
        {
            if (mode != BreathReceiverMode.BreathSample || !MicrophoneDevice.microphoneStarted)
                return;
            bool microphoneSamplesPrepared = MicrophoneDevice.UpdateMicrophone();
            if (!microphoneSamplesPrepared)
                return;

            float energy = SignalProcessor.CalculateRMS(MicrophoneDevice.samples);
            float mainFrequency = SignalProcessor.CalculateMainFrequency(MicrophoneDevice.samples);
            float deltaTime = Time.deltaTime;

            onProcessSampleData(energy, deltaTime, mainFrequency);
        }
        #endregion
        #region BreathDetect
        public struct BreathFrameData
        {
            public bool isHu;
            public float deltaTime;
            public float energy;
        }
        [Header("Breath Detect Interface")]
        public Queue<BreathFrameData> allBreathFrameDataQueue = new();
        public Queue<BreathFrameData> storedBreathFrameDataQueue = new();
        private int storedDataLength = 8000;
        private int averageSlideWindowLength = 3;
        private bool isHuLastFrame = false;
        public static bool isHuNow = false;
        public static float averageCircleTime = 0f;
        public static float timeOfLatestSection;
        public float huStateChangeCount = 0f;
        public static float longestHuTime = 0f;
        public static int longHuCount = 0;
        public static void StartBreathDetect()
        {
            mode = BreathReceiverMode.BreathDetect;
            MicrophoneDevice.StartMicrophone();
            onUpdate = instance.UpdateBreathDetect;
        }
        public static void StopBreathDetect()
        {
            mode = BreathReceiverMode.None;
            MicrophoneDevice.StopMicrophone();
            onUpdate = instance.DefaultOnUpdate;
            isHuNow = false;
        }
        private void UpdateBreathDetect()
        {
            if (mode != BreathReceiverMode.BreathDetect || !MicrophoneDevice.microphoneStarted)
                return;
            bool microphoneSamplesPrepared = MicrophoneDevice.UpdateMicrophone();
            if (!microphoneSamplesPrepared)
                return;
            float energy = SignalProcessor.CalculateRMS(MicrophoneDevice.samples);
            float mainFrequency = SignalProcessor.CalculateMainFrequency(MicrophoneDevice.samples);
            float deltaTime = Time.deltaTime;

            BreathFrameData frameData = new()
            {
                isHu = BreathClassifier.JudgeIsHu(energy, mainFrequency),
                energy = energy,
                deltaTime = deltaTime
            };
            allBreathFrameDataQueue.Enqueue(frameData);

            //* 滑动平均导致延迟有点高，还是取消为好
            if (storedBreathFrameDataQueue.Count > averageSlideWindowLength)
            { //做滑动平均降低高频信号的影响
                float energySum = 0;
                BreathFrameData[] array = storedBreathFrameDataQueue.ToArray();
                for (int i = array.Length - 1; i > array.Length - 1 - averageSlideWindowLength; i--)
                {
                    energySum += array[i].energy;
                }
                energySum += frameData.energy;
                frameData.energy = energySum / (averageSlideWindowLength + 1);
                frameData.isHu = BreathClassifier.JudgeIsHu(frameData.energy, mainFrequency);
            }

            storedBreathFrameDataQueue.Enqueue(frameData);
            if (storedBreathFrameDataQueue.Count > storedDataLength)
            {
                storedBreathFrameDataQueue.Dequeue();
            }

            if ((isHuLastFrame && !frameData.isHu) || (!isHuLastFrame && frameData.isHu))
            {
                isHuLastFrame = frameData.isHu;
                huStateChangeCount++;
                if (!frameData.isHu)
                {
                    longestHuTime = Mathf.Max(longestHuTime, timeOfLatestSection);
                    if (BreathClassifier.JudgeIsLongHu(timeOfLatestSection))
                    {
                        longHuCount++;
                    }
                }
            }  
            AnalyseBreathTime();
        }
        private void AnalyseBreathTime()
        {
            BreathFrameData[] datas = storedBreathFrameDataQueue.ToArray();
            List<int> changePoints = new List<int>();

            //find change points
            bool last = datas.Length > 0 ? datas[0].isHu : false;
            for (int i = 1; i < datas.Length; i++)
            {
                if (datas[i].isHu == last)
                    continue;
                changePoints.Add(i);
                last = datas[i].isHu;
            }
            isHuNow = last;

            //calculate time in this section
            timeOfLatestSection = 0;
            if (changePoints.Count >= 1)
                for (int i = changePoints[changePoints.Count - 1]; i < datas.Length; i++)
                {
                    timeOfLatestSection += datas[i].deltaTime;
                }
            else
                for (int i = 0; i < datas.Length; i++)
                {
                    timeOfLatestSection += datas[i].deltaTime;
                }

            //when no change point
            if (changePoints.Count == 0)
            {
                averageCircleTime = timeOfLatestSection;
                return;
            }
            //when only one change point
            if (changePoints.Count == 1)
            {
                averageCircleTime = 0;
                for (int i = 0; i < datas.Length; i++)
                {
                    averageCircleTime += datas[i].deltaTime;
                }
                return;
            }
            //when only 2 change points
            if (changePoints.Count == 2)
            {
                averageCircleTime = 0;
                for (int i = 0; i < datas.Length; i++)
                {
                    averageCircleTime += datas[i].deltaTime;
                }
                averageCircleTime = averageCircleTime / 3.0f * 2.0f;
                return;
            }

            //calculate average cycle time with weights
            float weightsSum = 0;
            averageCircleTime = 0;
            for (int i = 0; i + 2 < changePoints.Count; i += 2)
            {
                float timeOfThisCycle = 0;
                for (int j = changePoints[i]; j < changePoints[i + 2]; j++)
                {
                    timeOfThisCycle += datas[j].deltaTime;
                }
                averageCircleTime += timeOfThisCycle * Mathf.Sqrt((i + 1) / (float)changePoints.Count);
                weightsSum += Mathf.Sqrt((i + 1) / (float)changePoints.Count);
            }
            if (weightsSum > 0)
            {
                averageCircleTime /= weightsSum;
            }
            else
            {
                averageCircleTime = 0;
            }
        }
        #endregion
        private void Update()
        {
            onUpdate();
        }
    }
}
