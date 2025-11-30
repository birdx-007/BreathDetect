using BirdSky;
using BreathDetect;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class BreathDisplayer : MonoBehaviour
{
    public bool selfUpdate = false;
    [SerializeField] private Image fill;
    [SerializeField] private Color emptyColor;
    [SerializeField] private Color fullColor;
    [SerializeField] private Color huColor;
    void Update()
    {
        if (selfUpdate)
        {
            if (MicrophoneDevice.microphoneStarted)
            {
                UpdateBreathDisplay(MicrophoneDevice.samples, BreathReceiver.isHuNow);
            }
            else
            {
                Reset();
            }
        }
    }
    public void UpdateBreathDisplay(float[] samples, bool isHu = false)
    {
        float rms = SignalProcessor.CalculateRMS(samples);
        float lowBound = NoiseDetect.quietThreshold;
        float upBound = lowBound * 3.162f;
        fill.fillAmount = Mathf.Clamp01((rms / lowBound) / (upBound / lowBound));
        fill.color = isHu ? huColor : Color.Lerp(emptyColor, fullColor, fill.fillAmount);
    }
    public void Reset()
    {
        fill.fillAmount = 0;
    }
}
