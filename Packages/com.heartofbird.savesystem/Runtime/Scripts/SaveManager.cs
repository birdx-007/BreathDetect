using System.Collections;
using System.Collections.Generic;
using System.Runtime.Serialization.Formatters.Binary;
using System.IO;
using UnityEngine;
using System.Runtime.Serialization;

// usage:
// SaveManager.Save(..., SaveData.instance);
// SaveData.instance = (SaveData)SaveManager.Load(...);
public class SaveManager
{
    public static bool Save(string saveName, object saveData)
    {
        BinaryFormatter binaryFormatter = GetBinaryFormatter();
        if(!Directory.Exists(Application.persistentDataPath + "/saves"))
        {
            Directory.CreateDirectory(Application.persistentDataPath + "/saves");
        }
        string savePath = Application.persistentDataPath + "/saves/" + saveName + ".save";
        FileStream file = File.Create(savePath);
        binaryFormatter.Serialize(file, saveData);
        file.Close();
        return true;
    }
    public static object Load(string saveName)
    {
        string savePath = Application.persistentDataPath + "/saves/" + saveName + ".save";
        if (!File.Exists(savePath))
        {
            return null;
        }
        FileStream file = File.Open(savePath,FileMode.Open);
        BinaryFormatter binaryFormatter = GetBinaryFormatter();
        try
        {
            object saveData = binaryFormatter.Deserialize(file);
            file.Close();
            return saveData;
        }
        catch
        {
            Debug.LogErrorFormat("Error: Failed to load save file at {0}", savePath);
            file.Close();
            return null;
        }
    }
    private static BinaryFormatter GetBinaryFormatter()
    {
        BinaryFormatter binaryFormatter = new BinaryFormatter();
        SurrogateSelector selector = new SurrogateSelector();

        SerializationSurrogate_Vector3 serializationSurrogate_Vector3 = new SerializationSurrogate_Vector3();
        selector.AddSurrogate(typeof(Vector3), new StreamingContext(StreamingContextStates.All), serializationSurrogate_Vector3);
        SerializationSurrogate_Quaternion serializationSurrogate_Quaternion = new SerializationSurrogate_Quaternion();
        selector.AddSurrogate(typeof(Quaternion), new StreamingContext(StreamingContextStates.All), serializationSurrogate_Quaternion);
        // can add more...

        binaryFormatter.SurrogateSelector = selector;
        return binaryFormatter;
    }
}
