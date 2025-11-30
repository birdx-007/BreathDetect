using BreathDetect;
using NaughtyAttributes;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace BirdSky
{
    [Serializable]
    public enum NoiseDetactState
    {
        None,
        Start,
        Detecting,
        End,
    }
    public class NoiseDetect : MonoBehaviour
    {
        [Header("basic")]
        public NoiseDetactState state = NoiseDetactState.None;
        public SerializableDictionary<NoiseDetactState, GameObject> stateGameobjects;
        public UnityEvent onDetectFinish;
        [Header("detecting")]
        public bool isQuiet = false;
        public GameObject noisy;
        public GameObject quiet;
        public void QuitNoiseDetect()
        {
            state = NoiseDetactState.None;
            foreach (var item in stateGameobjects)
            {
                item.Value.SetActive(false);
            }
        }
        public void EnterNoiseDetect()
        {
            if (state != NoiseDetactState.None)
            {
                Debug.LogWarning("Noise detection is already in progress.");
                return;
            }
            state = NoiseDetactState.Start;
            stateGameobjects[state].SetActive(true);
        }
        #region detecting
        private Queue<float> noiseWindow = new Queue<float>();
        private readonly int windowSize = 50;
        public static readonly float quietThreshold_dB = -30f;
        public static float quietThreshold => Mathf.Pow(10f, quietThreshold_dB / 20f);
        private void SetQuiet(bool quiet)
        {
            isQuiet = quiet;
            noisy.SetActive(!quiet);
            this.quiet.SetActive(quiet);
        }
        private void InitNoiseDetect()
        {
            SetQuiet(false);
            BreathReceiver.StartNoiseDetect(UpdateNoiseDetect);
            noiseWindow.Clear();
            for(int i = 0; i < windowSize; i++)
            {
                noiseWindow.Enqueue(65f);// 初始时假设环境噪音较大
            }
        }
        public void StartDetecting()
        {
            if (state != NoiseDetactState.Start)
            {
                Debug.LogWarning("Cannot start detecting noise. Current state: " + state);
                return;
            }
            stateGameobjects[state].SetActive(false);
            state = NoiseDetactState.Detecting;
            stateGameobjects[state].SetActive(true);
            InitNoiseDetect();
        }
        private void UpdateNoiseDetect(float dB)
        {
            if (state == NoiseDetactState.Detecting)
            {
                noiseWindow.Enqueue(dB);
                if (noiseWindow.Count > windowSize)
                {
                    noiseWindow.Dequeue();
                }
                float average = 0f;
                foreach (var item in noiseWindow)
                {
                    average += item;
                }
                average /= noiseWindow.Count;
                if (average < quietThreshold_dB)
                {
                    SetQuiet(true);
                }
                else
                {
                    SetQuiet(false);
                }
#if UNITY_EDITOR
                //Debug.Log("Current dB: " + dB + ", Average dB: " + average + ", isQuiet: " + isQuiet);
#endif
            }
        }
        public void BackToStart()
        {
            if(state != NoiseDetactState.Detecting)
            {
                Debug.LogWarning("Cannot go back to start. Current state: " + state);
                return;
            }
            BreathReceiver.StopNoiseDetect();
            stateGameobjects[state].SetActive(false);
            state = NoiseDetactState.Start;
            stateGameobjects[state].SetActive(true);
        }
        public void EndDetecting()
        {
            if (state != NoiseDetactState.Detecting)
            {
                Debug.LogWarning("Cannot end detecting noise. Current state: " + state);
                return;
            }
            BreathReceiver.StopNoiseDetect();
            stateGameobjects[state].SetActive(false);
            if (!isQuiet)
            {
                state = NoiseDetactState.End;
                stateGameobjects[state].SetActive(true);
            }
            else
            {
                FinishNoiseDetect();
            }
        }
        #endregion
        public void RetryNoiseDetect()
        {
            if (state != NoiseDetactState.End)
            {
                Debug.LogWarning("Cannot retry noise detection. Current state: " + state);
                return;
            }
            stateGameobjects[state].SetActive(false);
            state = NoiseDetactState.Detecting;
            stateGameobjects[state].SetActive(true);
            InitNoiseDetect();
        }
        public void FinishNoiseDetect()
        {
            onDetectFinish.Invoke();
            QuitNoiseDetect();
        }
    }
}
