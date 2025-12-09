using BreathDetect;
using Cysharp.Threading.Tasks;
using NaughtyAttributes;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace BirdSky
{
    public enum BreathSampleState
    {
        None,
        Hu,
        Xi,
        Bing,
        Test,
    }
    public class BreathSample : MonoBehaviour
    {
        [Header("basic")]
        public SerializableDictionary<BreathSampleState, BreathSampleStage_IOS> breathSampleStages;
        [ReadOnly] public BreathSampleState state = BreathSampleState.None;
        public UnityEvent onSampleFinish;
        public BreathDisplayer breathDisplayer;
        [Header("UI")]
        public GameObject startPanel;
        public Text startText;
        public GameObject mainPanel;
        public Text mainGuideText;
        public Button mainLeftButton;
        public Button mainRightButton;
        public GameObject mainSampleProcessBar;
        public Image mainSampleProcess;
        public GameObject testPanel;

        public void QuitBreathSample()
        {
            state = BreathSampleState.None;
            startPanel.SetActive(false);
            testPanel.SetActive(false);
        }
        public void EnterBreathSample()
        {
            if(state != BreathSampleState.None)
            {
                Debug.LogWarning("Cannot enter breath sample. Current state: " + state);
                return;
            }
            state = BreathSampleState.Hu;
            breathSampleStages[state].EnterStage(this);
        }
        public void NextStage()
        {
            if (breathSampleStages.ContainsKey(state))
            {
                breathSampleStages[state].QuitStage();
            }
            switch (state)
            {
                case BreathSampleState.Hu:
                    state = BreathSampleState.Xi; break;
                case BreathSampleState.Xi:
                    state = BreathSampleState.Bing; break;
                case BreathSampleState.Bing:
                    state = BreathSampleState.Test; break;
                case BreathSampleState.Test:
                    FinishBreathSample(); return;
                default:
                    return;
            }
            if(state == BreathSampleState.Test)
            {
                SaveData.Instance.hasTeachedBreathSample = true;
                mainPanel.SetActive(false);
                testPanel.SetActive(true);
                StartTestProcess();
                return;
            }
            breathSampleStages[state].EnterStage(this);
        }
        public async void NextStep()
        {
            if (state == BreathSampleState.Test)
            {
                StopTestProcess();
                NextStage();
                return;
            }
            bool stageFinished = await breathSampleStages[state].NextStep();
            if (stageFinished)
            {
                NextStage();
            }
        }
        public void PrevStep()
        {
            if (state == BreathSampleState.Test)
            {
                testPanel.SetActive(false);
                // 重新回到呼气采样
                state = BreathSampleState.Hu;
                breathSampleStages[state].EnterStage(this);
                return;
            }
            breathSampleStages[state].PrevStep();
        }
        public void SkipGuide()
        {
            breathSampleStages[state].SkipGuide();
        }
        public void RestartGuide()
        {
            breathSampleStages[state].RestartGuide();
        }
        public void FinishBreathSample()
        {
            SaveData.Instance.hasFinishedBreathSample = true;
            QuitBreathSample();
            onSampleFinish.Invoke();
        }
        #region Press To Next Step
        private readonly float pressThreshold = 0.2f;
        private float pressStartTime;
        private bool tryPress = false;
        private bool pressTriggered = false;
        private void Update()
        {
            // 此处处理UI相关逻辑
            if (state == BreathSampleState.Hu || state == BreathSampleState.Xi || state == BreathSampleState.Bing)
            {
                if (breathSampleStages[state].CanPressToNextStep)
                {
                    bool touch = false;
#if UNITY_IOS
                    touch = Input.touchCount > 0;
#else
                    touch = Input.GetMouseButton(0);
#endif
                    if (touch)
                    {
                        if (AnyTouchOnUIButton())
                        {
                            tryPress = false;
                            pressTriggered = false;
                            return;
                        }
                        if (!tryPress)
                        {
                            pressStartTime = Time.time;
                            tryPress = true;
                            pressTriggered = false;
                        }
                        if (tryPress && !pressTriggered)
                        {
                            if (Time.time - pressStartTime >= pressThreshold)
                            {
                                pressTriggered = true;
                            }
                        }
                    }
                    else
                    {
                        tryPress = false;
                        pressTriggered = false;
                    }
                    if (PressDetect())
                    {
                        NextStep();
                    }
                }
                else
                {
                    tryPress = false;
                    pressTriggered = false;
                }
            }
        }
        private bool PressDetect()
        {
            bool res = false;
            res = pressTriggered;
            return res;
        }
        private bool AnyTouchOnUIButton()
        {
#if UNITY_IOS
            for (int i = 0; i < Input.touchCount; i++)
            {
                Touch touch = Input.GetTouch(i);

                PointerEventData eventData = new PointerEventData(EventSystem.current)
                {
                    position = touch.position
                };

                List<RaycastResult> results = new List<RaycastResult>();
                EventSystem.current.RaycastAll(eventData, results);

                foreach (var result in results)
                {
                    if (result.gameObject.GetComponent<Button>() != null)
                    {
                        return true;
                    }
                }
            }
            return false;
#else
            PointerEventData eventData = new PointerEventData(EventSystem.current)
            {
                position = Input.mousePosition
            };

            List<RaycastResult> results = new List<RaycastResult>();
            EventSystem.current.RaycastAll(eventData, results);

            foreach (RaycastResult result in results)
            {
                if (result.gameObject.GetComponent<Button>() != null)
                {
                    return true;
                }
            }
            return false;
#endif
        }
#endregion
        #region Sample Function
        private struct SampleData
        {
            public float energy;
            public float deltaTime;
            public float mainFrequency;
        }
        private Queue<SampleData> sampleDataQueue;
        private float averageMouthOpeningDegree;
        private float[] averageSpectrum;
        private float sampleTimer = 0;
        private readonly float sampleMaxTime = 5f;
        private readonly int sampleNum = 700;
        private bool isSampling = false;
        [ReadOnly] public bool enableSample = true;
        public string sampleCompleteHint;
        public void StartSampleProcess()
        {
            BreathReceiver.StartBreathSample(UpdateSampleProcess);
            MouthDetector.StartMouthDetect();
            sampleTimer = 0;
            sampleDataQueue = new Queue<SampleData>();
            averageSpectrum = new float[MicrophoneDevice.sampleWindow];
            averageMouthOpeningDegree = 0f;
        }
        public void RestartSampleProcess()
        {
            StartSampleProcess();
            mainSampleProcess.fillAmount = 0f; 
            breathDisplayer.gameObject.SetActive(true);
        }
        public void StopSampleProcess()
        {
            BreathReceiver.StopBreathSample();
            MouthDetector.StopMouthDetect();
            sampleDataQueue.Clear();
            breathDisplayer.Reset();
        }
        public void CompleteSampleProcess()
        {
            BreathReceiver.StopBreathSample();
            MouthDetector.StopMouthDetect();
            mainSampleProcess.fillAmount = 1f;
            breathDisplayer.Reset();
            mainRightButton.gameObject.SetActive(true);
            if (breathSampleStages[state].NeedSampleData)
            {
                float averageEnergy = 0;
                float averageMainFrequency = 0;
                foreach (SampleData data in sampleDataQueue)
                {
                    averageEnergy += data.energy;
                    averageMainFrequency += data.mainFrequency;
                }
                averageEnergy = averageEnergy / sampleDataQueue.Count;
                averageMainFrequency = averageMainFrequency / sampleDataQueue.Count;
                for (int i = 0; i < MicrophoneDevice.sampleWindow; i++)
                {
                    averageSpectrum[i] /= sampleDataQueue.Count;
                }
                SignalProcessor.Normalize(ref averageSpectrum);
                averageMouthOpeningDegree = averageMouthOpeningDegree / sampleDataQueue.Count;
                switch (state)
                {
                    case BreathSampleState.Hu:
                        BreathClassifier.huAverageEnergy = averageEnergy;
                        BreathClassifier.huAverageSpectrum = averageSpectrum;
                        BreathClassifier.huAverageMouthOpeningDegree = averageMouthOpeningDegree;
                        break;
                    case BreathSampleState.Xi:
                        BreathClassifier.xiAverageEnergy = averageEnergy;
                        BreathClassifier.xiAverageSpectrum = averageSpectrum;
                        BreathClassifier.xiAverageMouthOpeningDegree = averageMouthOpeningDegree;
                        break;
                    case BreathSampleState.Bing:
                        BreathClassifier.bingAverageEnergy = averageEnergy;
                        BreathClassifier.bingAverageSpectrum = averageSpectrum;
                        BreathClassifier.bingAverageMouthOpeningDegree = averageMouthOpeningDegree;
                        OnAllSampleProcessesComplete();
                        break;
                    default:
                        break;
                }
                mainGuideText.text = sampleCompleteHint;
            }
        }
        public void OnAllSampleProcessesComplete()
        {
            if (breathSampleStages[state].NeedSampleData)
            {
                BreathClassifier.SaveBreathDetectParameters();
                //SaveData.Instance.thresholdEnergy = (HuEnergy + XiEnergy + BingEnergy) / 3.4f;
                //SaveData.Instance.thresholdFrequency = (HuMainFrequency + XiMainFrequency + BingMainFrequency) * 3;
                // ...
            }
        }
        private void UpdateSampleProcess(float energy, float deltaTime, float mainFrequency)
        {
            //breathBar.UpdateBreathBar(MicrophoneDevice.samples, false);
            breathDisplayer.UpdateBreathDisplay(MicrophoneDevice.samples, false);
            bool shouldSample = false;
#if UNITY_IOS
            shouldSample = Input.touchCount > 0;
#else
            shouldSample = Input.GetMouseButton(0);
#endif
            shouldSample &= enableSample;
            if (!shouldSample)
            {
                if (isSampling)
                {
                    //onSamplePause();
                }
                isSampling = false;
                return;
            }
            if (!isSampling)
            {
                //onSampleStart();
                isSampling = true;
            }
            sampleDataQueue.Enqueue(new SampleData
            {
                energy = energy,
                deltaTime = deltaTime,
                mainFrequency = mainFrequency
            });
            var spectrum = SignalProcessor.CalculateSpectrum_FFT(MicrophoneDevice.samples);
            for (int i = 0; i < MicrophoneDevice.sampleWindow; i++)
            {
                averageSpectrum[i] += spectrum[i];
            }
            averageMouthOpeningDegree += MouthDetector.MouthOpeningDegree;
            float sampleNumRate = (float)sampleDataQueue.Count / sampleNum;
            float sampleTimeRate = sampleTimer / sampleMaxTime;
            mainSampleProcess.fillAmount = Mathf.Max(sampleNumRate, sampleTimeRate);

            sampleTimer += deltaTime;
            if (sampleDataQueue.Count >= sampleNum || sampleTimer > sampleMaxTime)
            {
                CompleteSampleProcess();
            }
        }
        #endregion
        #region Test Function
        [Header("Test")]
        private bool isHu = false;
        public void StartTestProcess()
        {
            BreathReceiver.StartBreathSample(UpdateTestProcess);
            MouthDetector.StartMouthDetect();
        }
        public void StopTestProcess()
        {
            BreathReceiver.StopBreathSample();
            MouthDetector.StopMouthDetect();
            isHu = false;
            breathDisplayer.Reset();
        }
        private void UpdateTestProcess(float energy, float deltaTime, float mainFrequency)
        {
            if (BreathClassifier.JudgeIsHu(energy,mainFrequency) && (!isHu))
            {
                isHu = true;
            }
            else if ((!BreathClassifier.JudgeIsHu(energy, mainFrequency)) && isHu)
            {
                isHu = false;
            }
            breathDisplayer.UpdateBreathDisplay(MicrophoneDevice.samples, isHu);
        }
        #endregion
    }
    public enum BreathSampleStageState
    {
        None,
        Start,
        BasicTeach,
        Guide,
        Sample,
    }
    [Serializable]
    public class BreathSampleStage_IOS
    {
        [ReadOnly, AllowNesting] public BreathSampleStageState stageState = BreathSampleStageState.None;
        private BreathSample manager;
        public string startTextContent;
        public GameObject mainAvatar;
        public string mainGuideTextContent;
        public GameObject[] teachSteps;
        public GameObject[] guideSteps;
        private static readonly int[] guidePressToSampleIndices = { 0, 2 };
        private static readonly int guideReleaseToPauseIndex = 1;
        private static readonly int guideFinalIndex = 3;
        private int currentTeachStepIndex = 0;
        private int currentGuideStepIndex = 0;
        // private bool guideSkipped = false;
        public bool NeedSampleData => stageState == BreathSampleStageState.Sample;
        public bool CanPressToNextStep
        {
            get
            {
                if(stageState == BreathSampleStageState.Guide)
                {
                    foreach(var index in guidePressToSampleIndices)
                    {
                        if (currentGuideStepIndex == index)
                        {
                            return true;
                        }
                    }
                }
                return false;
            }
        }
        public void EnterStage(BreathSample breathSample_IOS)
        {
            manager = breathSample_IOS;
            manager.startText.text = startTextContent;
            mainAvatar.SetActive(true);
            manager.mainGuideText.text = mainGuideTextContent;
            foreach (var step in teachSteps)
            {
                step.SetActive(false);
            }
            stageState = BreathSampleStageState.Start;
            manager.startPanel.SetActive(true);
            manager.mainPanel.SetActive(false);
            manager.breathDisplayer.gameObject.SetActive(false);

            // judge save state: teached or not
            if (SaveData.Instance.hasTeachedBreathSample)
            {
                GotoSample();
            }
        }
        public async UniTask<bool> NextStep()
        {
            bool stageFinished = false;
            switch (stageState)
            {
                case BreathSampleStageState.Start:
                    stageState = BreathSampleStageState.BasicTeach;
                    manager.startPanel.SetActive(false);
                    manager.mainPanel.SetActive(true);
                    manager.mainSampleProcess.fillAmount = 0f;
                    manager.mainLeftButton.gameObject.SetActive(false);
                    manager.mainRightButton.gameObject.SetActive(false);
                    mainAvatar.SetActive(true);
                    currentTeachStepIndex = 0;
                    teachSteps[currentTeachStepIndex].SetActive(true);
                    break;
                case BreathSampleStageState.BasicTeach:
                    teachSteps[currentTeachStepIndex].SetActive(false);
                    currentTeachStepIndex++;
                    if (currentTeachStepIndex < teachSteps.Length)
                    {
                        teachSteps[currentTeachStepIndex].SetActive(true);
                    }
                    else
                    {
                        stageState = BreathSampleStageState.Guide;
                        currentGuideStepIndex = 0;
                        guideSteps[currentGuideStepIndex].SetActive(true);
                        manager.breathDisplayer.gameObject.SetActive(true);
                        manager.StartSampleProcess();
                    }
                    break;
                case BreathSampleStageState.Guide:
                    guideSteps[currentGuideStepIndex].SetActive(false);
                    currentGuideStepIndex++;
                    if(currentGuideStepIndex < guideSteps.Length)
                    {
                        int nextIndex = currentGuideStepIndex;
                        if (nextIndex == guideReleaseToPauseIndex)
                        {
                            manager.enableSample = true;
                            await UniTask.WaitUntil(()=>manager.mainSampleProcess.fillAmount >= 0.25f);
                            manager.enableSample = false;
                        }
                        else if (nextIndex == guideFinalIndex)
                        {
                            manager.enableSample = true;
                            await UniTask.WaitUntil(() => manager.mainSampleProcess.fillAmount == 1f);
                            manager.enableSample = false;
                        }
                        guideSteps[currentGuideStepIndex].SetActive(true);
                    }
                    else
                    {
                        stageState = BreathSampleStageState.Sample;
                        manager.enableSample = true;
                        manager.mainGuideText.text = mainGuideTextContent;
                        manager.mainSampleProcess.fillAmount = 0f;
                        manager.mainLeftButton.gameObject.SetActive(true);
                        manager.mainRightButton.gameObject.SetActive(false);
                        manager.StartSampleProcess();
                    }
                    break;
                case BreathSampleStageState.Sample:
                    stageState = BreathSampleStageState.None;
                    stageFinished = true;
                    break;
                default:
                    break;
            }
            return stageFinished;
        }
        public void PrevStep()
        {
            switch (stageState)
            {
                case BreathSampleStageState.BasicTeach:
                    if (currentTeachStepIndex > 0)
                    {
                        teachSteps[currentTeachStepIndex].SetActive(false);
                        currentTeachStepIndex--;
                        teachSteps[currentTeachStepIndex].SetActive(true);
                    }
                    break;
                case BreathSampleStageState.Guide:
                    // never used
                    break;
                case BreathSampleStageState.Sample:
                    manager.mainGuideText.text = mainGuideTextContent;
                    manager.mainLeftButton.gameObject.SetActive(true);
                    manager.mainRightButton.gameObject.SetActive(false);
                    manager.RestartSampleProcess();
                    break;
                default:
                    break;
            }
        }
        public void SkipGuide()
        {
            if (stageState == BreathSampleStageState.Guide)
            {
                guideSteps[currentGuideStepIndex].SetActive(false);
                currentGuideStepIndex = guideSteps.Length - 1;
                guideSteps[currentGuideStepIndex].SetActive(true);
                manager.StopSampleProcess();
            }
        }
        public void RestartGuide()
        {
            if(stageState == BreathSampleStageState.Guide)
            {
                guideSteps[currentGuideStepIndex].SetActive(false);
                currentGuideStepIndex = 0;
                guideSteps[currentGuideStepIndex].SetActive(true);
                manager.RestartSampleProcess();
            }
        }
        public void GotoSample()
        {
            stageState = BreathSampleStageState.Sample;
            manager.startPanel.SetActive(false);
            manager.mainPanel.SetActive(true);
            manager.testPanel.SetActive(false);
            manager.mainGuideText.text = mainGuideTextContent;
            manager.mainSampleProcess.fillAmount = 0f;
            manager.mainLeftButton.gameObject.SetActive(true);
            manager.mainRightButton.gameObject.SetActive(false);
            manager.breathDisplayer.gameObject.SetActive(true);
            mainAvatar.SetActive(true);
            manager.StartSampleProcess();
        }
        public void QuitStage()
        {
            mainAvatar.SetActive(false);
        }
    }
}
