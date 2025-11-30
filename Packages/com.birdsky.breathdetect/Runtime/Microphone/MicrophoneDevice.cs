using System;
using System.Collections.Generic;
using UnityEngine;
using System.Runtime.InteropServices;
//using Microphone = FrostweepGames.MicrophonePro.Microphone;

namespace BreathDetect
{
    /// <summary>
    /// 枚举、切换、开关麦克风，以一定的帧率提供 PCM 样本数据
    /// </summary>
    public class MicrophoneDevice : MonoBehaviour
    {
        private static MicrophoneDevice instance;
        void Awake()
        {
            if (instance != null)
            {
                Destroy(gameObject);
                return;
            }
            DontDestroyOnLoad(gameObject);
            instance = this;

            Refresh();
            samples = new float[sampleWindow];
        }

        #region Device Management
        /*
#if UNITY_IOS && !UNITY_EDITOR
        /// <summary>
        /// IOS Plugins, see Assets/Plugins/IOS/
        /// </summary>
        [DllImport("__Internal")] private static extern IntPtr AIM_GetInputsJson();
        [DllImport("__Internal")] private static extern int    AIM_SetPreferredInput(string uid);
        [DllImport("__Internal")] private static extern void   AIM_Free(IntPtr p);
        [DllImport("__Internal")] private static extern void   AIM_SetUnityReceiver(string goName);
#endif
        */
        public static int DevicesNum => inputDevices.Count;
        public static readonly List<(string name, string uid)> inputDevices = new();
        private static int deviceIndex = 0;
        public static string CurrentDeviceName => inputDevices[deviceIndex].name;

        [Serializable]
        private struct MicrophoneInfo
        {
            public string name; public string uid;
        }
        [Serializable]
        private class MicrophoneListWrapper
        {
            public MicrophoneInfo[] items;
        }

        public static void Refresh()
        {
            /*
#if UNITY_IOS && !UNITY_EDITOR
            AIM_SetUnityReceiver(instance.gameObject.name);
#endif
            */
            RefreshDeviceList();
        }

        /* -------- 解析 & 刷新麦克风列表 -------- */
        private static void RefreshDeviceList(string jsonOverride = null)
        {
            string json;
            /*
#if UNITY_IOS && !UNITY_EDITOR
            IntPtr ptr = IntPtr.Zero;
            if (jsonOverride == null)
            {
                ptr  = AIM_GetInputsJson();
                json = Marshal.PtrToStringAnsi(ptr);
            }
            else
                json = jsonOverride;
#else
            json = NonIosDevicesJson();
#endif
            */
            json = NonIosDevicesJson();
            var wrapper = JsonUtility.FromJson<MicrophoneListWrapper>("{\"items\":" + json + "}");
            inputDevices.Clear();
            foreach (var info in wrapper.items) inputDevices.Add((info.name, info.uid));
            /*
#if UNITY_IOS && !UNITY_EDITOR
            if (ptr != IntPtr.Zero) AIM_Free(ptr);
#endif
            */
        }

        /* -------- 选项切换 -------- */
        public static void ChangeDevice(int index)
        {
#if UNITY_WEBGL
            return;
#endif
            if (index < 0 || index >= inputDevices.Count) return;
            /*
#if UNITY_IOS && !UNITY_EDITOR
            AIM_SetPreferredInput(inputDevices[index].uid);
#endif
            */
            deviceIndex = index;
        }

        public static void SwitchToNextDevice()
        {
#if UNITY_IOS && !UNITY_EDITOR
#else
            RefreshDeviceList();
#endif
            int nextIndex = (deviceIndex + 1) % inputDevices.Count;
            ChangeDevice(nextIndex);
        }

        /// <summary>
        /// used by IOS plugins, see Assets/Plugins/IOS/
        /// </summary>
        /// <param name="json"></param>
        public void OnMicRouteChanged(string json)
        {
            RefreshDeviceList(json);
            if (deviceIndex >= inputDevices.Count)
            {
                deviceIndex = inputDevices.Count - 1;
            }
        }

        private static string NonIosDevicesJson()
        {
            var list = new MicrophoneInfo[Microphone.devices.Length];
            for (int i = 0; i < list.Length; i++)
            {
                list[i].name = list[i].uid = Microphone.devices[i];
            }
            return JsonUtility.ToJson(new MicrophoneListWrapper { items = list }).Replace("{\"items\":", "").TrimEnd('}');
        }
        #endregion
        #region Microphone Function
        public static readonly int sampleWindow = 4096;
        public static readonly int sampleFrequency = 44100;
        public static bool microphoneStarted = false;
        public static float[] samples;
        public static AudioClip microphoneClip;
        public static float microphoneAudioMultiplier_dB = 0f;
        public static void StartMicrophone(int audioLength = 512)
        {
            if (microphoneStarted)
                return;
            microphoneStarted = true;
            var microphoneName = CurrentDeviceName;
            //microphoneName = Microphone.devices[0];
#if UNITY_WEBGL
            // webgl录音不可超过10min
            microphoneClip = Microphone.Start(microphoneName, true, Math.Clamp(audioLength, 1, 600), sampleFrequency, true);
#else
            microphoneClip = Microphone.Start(microphoneName, true, audioLength, sampleFrequency);
#endif
        }
        public static void StopMicrophone()
        {
            microphoneStarted = false;
            Microphone.End(CurrentDeviceName);
        }
        public static bool UpdateMicrophone()
        {
            if (microphoneStarted)
            {
                var microphoneName = CurrentDeviceName;
                // 获取麦克风录音数据
                int pos = Microphone.GetPosition(microphoneName) - (sampleWindow + 1); // null means the first microphone
                if (pos < 0)
                    return false;
                // 获取音频数据
#if UNITY_WEBGL
                bool condition = Microphone.GetData(samples, pos);
                if (!condition)
                {
                    condition = microphoneClip.GetData(samples, pos);
                }
#else
                bool condition = microphoneClip.GetData(samples, pos);
#endif
                if (condition)
                {
                    //音量增幅
                    for (int i = 0; i < samples.Length; i++)
                    {
                        samples[i] *= Mathf.Pow(10f, microphoneAudioMultiplier_dB / 20f);
                    }
                }
                return condition;
            }
            return false;
        }
        #endregion
    }
}
