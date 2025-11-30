using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace BreathDetect
{
    /// <summary>
    /// 根据能量、频率等输入，输出“呼气/吸气”标记，可配置阈值、滑动平均窗口等
    /// </summary>
    public class BreathClassifier : MonoBehaviour
    {
        private static bool hasLoadedParameters = false;
        private static float thresholdEnergy;
        public static float huAverageEnergy, xiAverageEnergy, bingAverageEnergy;
        public static float[] huAverageSpectrum, xiAverageSpectrum, bingAverageSpectrum;
        public static float huAverageMouthOpeningDegree, xiAverageMouthOpeningDegree, bingAverageMouthOpeningDegree;
        public static bool JudgeIsHu(float energy, float frequency)
        {
            if (!hasLoadedParameters)
            {
                LoadBreathDetectParameters();
                hasLoadedParameters = true;
            }
            thresholdEnergy = (huAverageEnergy + xiAverageEnergy + bingAverageEnergy) / 3.4f;
            return energy > thresholdEnergy;
        }
        public static float longHuTimeThreshold = 4f;
        public static bool JudgeIsLongHu(float huTime)
        {
            return huTime > longHuTimeThreshold;
        }
        public static void SaveBreathDetectParameters()
        {
            SaveData.Instance.huAverageEnergy = huAverageEnergy;
            SaveData.Instance.xiAverageEnergy = xiAverageEnergy;
            SaveData.Instance.bingAverageEnergy = bingAverageEnergy;
            SaveData.Instance.huAverageSpectrum = huAverageSpectrum;
            SaveData.Instance.xiAverageSpectrum = xiAverageSpectrum;
            SaveData.Instance.bingAverageSpectrum = bingAverageSpectrum;
            SaveData.Instance.huAverageMouthOpeningDegree = huAverageMouthOpeningDegree;
            SaveData.Instance.xiAverageMouthOpeningDegree = xiAverageMouthOpeningDegree;
            SaveData.Instance.bingAverageMouthOpeningDegree = bingAverageMouthOpeningDegree;
        }
        private static void LoadBreathDetectParameters()
        {
            huAverageEnergy = SaveData.Instance.huAverageEnergy;
            xiAverageEnergy = SaveData.Instance.xiAverageEnergy;
            bingAverageEnergy = SaveData.Instance.bingAverageEnergy;
            huAverageSpectrum = SaveData.Instance.huAverageSpectrum;
            xiAverageSpectrum = SaveData.Instance.xiAverageSpectrum;
            bingAverageSpectrum = SaveData.Instance.bingAverageSpectrum;
            huAverageMouthOpeningDegree = SaveData.Instance.huAverageMouthOpeningDegree;
            xiAverageMouthOpeningDegree = SaveData.Instance.xiAverageMouthOpeningDegree;
            bingAverageMouthOpeningDegree = SaveData.Instance.bingAverageMouthOpeningDegree;
        }
    }
}
