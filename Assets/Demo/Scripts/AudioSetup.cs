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
    public enum AudioSetupState
    {
        None,
        Teaching,
        Setup,
    }
    public class AudioSetup : MonoBehaviour
    {
        [Header("basic")]
        public AudioSetupState state = AudioSetupState.None;
        public AudioSetupStage_IOS[] audioSetupStages;
        public int setupStageStartIndex;
        private int _currentStageIndex = 0;
        public UnityEvent onSetupFinish;
        [Header("UI")]
        public GameObject[] allUiComponents;
        public GameObject underMask;
        public GameObject mask;
        public GameObject overMask;
        public Text deviceName;
        public BreathDisplayer breathDisplayer;
        public void QuitAudioSetup()
        {
            state = AudioSetupState.None;
            underMask.SetActive(false);
            mask.SetActive(false);
            overMask.SetActive(false);
        }
        public void EnterAudioSetup()
        {
            if(state != AudioSetupState.None)
            {
                Debug.LogWarning("Audio setup is already in progress.");
                return;
            }
            deviceName.text = MicrophoneDevice.CurrentDeviceName;
            breathDisplayer.gameObject.SetActive(false);
            // todo: judge save state: teached or not
            if(SaveData.Instance.hasTeachedAudioSetup)
            {
                state = AudioSetupState.Setup;
                _currentStageIndex = setupStageStartIndex;
            }
            else
            {
                state = AudioSetupState.Teaching;
                _currentStageIndex = 0;
            }
            audioSetupStages[_currentStageIndex].EnterStage(allUiComponents, underMask, mask, overMask);
        }
        public void NextStage()
        {
            if (state == AudioSetupState.None)
            {
                Debug.LogWarning("Cannot go to next stage. Current state: " + state);
                return;
            }
            audioSetupStages[_currentStageIndex].QuitStage();
            _currentStageIndex++;
            if (_currentStageIndex >= audioSetupStages.Length)
            {
                Debug.Log("Audio setup teaching finished.");
                FinishAudioSetup();
                return;
            }
            state = _currentStageIndex < setupStageStartIndex ? AudioSetupState.Teaching : AudioSetupState.Setup;
            audioSetupStages[_currentStageIndex].EnterStage(allUiComponents, underMask, mask, overMask);
        }
        public void PrevStage()
        {
            if (state == AudioSetupState.None)
            {
                Debug.LogWarning("Cannot go to next stage. Current state: " + state);
                return;
            }
            audioSetupStages[_currentStageIndex].QuitStage();
            _currentStageIndex--;
            if (_currentStageIndex < 0)
            {
                Debug.LogWarning("Cannot go back to previous stage. Already at the first stage.");
                _currentStageIndex = 0;
                return;
            }
            state = _currentStageIndex < setupStageStartIndex ? AudioSetupState.Teaching : AudioSetupState.Setup;
            audioSetupStages[_currentStageIndex].EnterStage(allUiComponents, underMask, mask, overMask);
        }
        public void RetryAudioSetup()
        {
            if (state == AudioSetupState.None)
            {
                Debug.LogWarning("Cannot retry audio setup. Current state: " + state);
                return;
            }
            state = AudioSetupState.Setup;
            _currentStageIndex = setupStageStartIndex;
            audioSetupStages[_currentStageIndex].EnterStage(allUiComponents, underMask, mask, overMask);
        }
        public void FinishAudioSetup()
        {
            onSetupFinish.Invoke();
            SaveData.Instance.hasTeachedAudioSetup = true;
            QuitAudioSetup();
        }
        #region Audio Function
        public void SwitchToNextDevice()
        {
            MicrophoneDevice.SwitchToNextDevice();
            deviceName.text = MicrophoneDevice.CurrentDeviceName;
            if (state == AudioSetupState.Teaching)
            {
                StopMicrophoneTest();
                StartMicrophoneTest();
            }
            else if (state == AudioSetupState.Setup)
            {
                StopAudioReceiveSetup();
                StartAudioReceiveSetup();
            }
        }
        public void StartMicrophoneTest()
        {
            BreathReceiver.StartMicrophoneAudioTest();
        }
        public void StopMicrophoneTest()
        {
            BreathReceiver.StopMicrophoneAudioTest();
        }
        public void SetMicrophoneMultiplier_dB(float dB)
        {
            BreathReceiver.SetMicrophoneReceiveMultiplier_dB(dB);
        }
        public void StartAudioReceiveSetup()
        {
            BreathReceiver.StartAudioReceiveSetup(UpdateAudioReceiveSetup);
            breathDisplayer.gameObject.SetActive(true);
            breathDisplayer.Reset();
        }
        public void StopAudioReceiveSetup()
        {
            BreathReceiver.StopAudioReceiveSetup();
            breathDisplayer.Reset();
            breathDisplayer.gameObject.SetActive(false);
        }
        private void UpdateAudioReceiveSetup()
        {
            breathDisplayer.UpdateBreathDisplay(MicrophoneDevice.samples);
        }
        #endregion
    }
    [Serializable]
    public class AudioSetupStage_IOS
    {
        public GameObject[] underMaskUiComponents;
        public GameObject[] overMaskUiComponents;
        public bool maskEnabled;
        public UnityEvent onEnterStage;
        public UnityEvent onQuitStage;
        public void EnterStage(GameObject[] allUiComponents, GameObject underMask, GameObject mask, GameObject overMask)
        {
            foreach (var item in allUiComponents)
            {
                item.SetActive(false);
            }
            for(int i = 0; i < underMaskUiComponents.Length; i++)
            {
                underMaskUiComponents[i].SetActive(true);
                underMaskUiComponents[i].transform.SetParent(underMask.transform, false);
                underMaskUiComponents[i].transform.SetSiblingIndex(i);
            }
            underMask.SetActive(true);
            mask.SetActive(maskEnabled);
            for (int i = 0; i < overMaskUiComponents.Length; i++)
            {
                overMaskUiComponents[i].SetActive(true);
                overMaskUiComponents[i].transform.SetParent(overMask.transform, false);
                overMaskUiComponents[i].transform.SetSiblingIndex(i);
            }
            overMask.SetActive(true);
            onEnterStage?.Invoke();
        }
        public void QuitStage()
        {
            onQuitStage?.Invoke();
        }
    }
}
