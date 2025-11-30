using System;
using System.Collections;
using System.Collections.Generic;

// usage: SaveData.instance.currentRoomName = "exampleRoom";
[Serializable]
public class SaveData
{
    private static SaveData instance;
    public static SaveData Instance
    {
        get
        {
            if (instance == null)
            {
                instance = new SaveData();
            }
            return instance;
        }
        set { instance = value; }
    }

    // add things you want to save
    // e.g. public string currentRoomName;
    #region 呼吸训练部分
    public int currentDay;
    public int currentBreathMode;
    public int currentMap;
    public int finishedDays;
    #endregion
    #region 采集基线部分
    public bool hasTeachedAudioSetup;
    public float microphoneAudioMultiplier_dB = 0f;
    public bool hasTeachedBreathSample;
    public bool hasFinishedBreathSample;
    #endregion
    #region 地图探险部分
    public bool hasTeachedPlayerControl;
    #endregion
    #region 呼吸检测部分
    public float huAverageEnergy, xiAverageEnergy, bingAverageEnergy;
    public float[] huAverageSpectrum, xiAverageSpectrum, bingAverageSpectrum; 
    public float huAverageMouthOpeningDegree, xiAverageMouthOpeningDegree, bingAverageMouthOpeningDegree;
    #endregion
}
