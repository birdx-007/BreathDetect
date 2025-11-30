using Cysharp.Threading.Tasks;
using NaughtyAttributes;
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace BirdSky
{
    public class DemoBrain : MonoBehaviour
    {
        [Header("basic")]
        public NoiseDetect noiseDetect;
        public AudioSetup audioSetup;
        public BreathSample breathSample;
        private void Start()
        {
            audioSetup.SetMicrophoneMultiplier_dB(SaveData.Instance.microphoneAudioMultiplier_dB);
            BeginNoiseDetect();
        }
        public void BeginNoiseDetect()
        {
            noiseDetect.EnterNoiseDetect();
        }
        public void BeginAudioSetup()
        {
            audioSetup.EnterAudioSetup();
        }
        public void BeginBreathSample()
        {
            breathSample.EnterBreathSample();
        }
        public void Finish()
        {
            Application.Quit();
        }
    }
}