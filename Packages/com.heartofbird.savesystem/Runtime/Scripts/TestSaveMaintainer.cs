using System.IO;
using UnityEngine;
using System.Text.RegularExpressions;
using System.Linq;

public class TestSaveMaintainer
{
    public static int currentTestSaveIndex = 0;
    private static string[] GetTestSaveNames()
    {
        if (!Directory.Exists(Application.persistentDataPath + "/saves"))
        {
            Directory.CreateDirectory(Application.persistentDataPath + "/saves");
            return new string[0];
        }
        // 取出所有 .save 文件
        string[] files = Directory.GetFiles(Application.persistentDataPath + "/saves", "*.save");

        // 用正则筛选 "test+数字.save"
        Regex regex = new Regex(@"^test\d+\.save$", RegexOptions.IgnoreCase);

        string[] results = files
            .Where(f => regex.IsMatch(Path.GetFileName(f))) // 只匹配文件名部分
            .ToArray();
        return results;
    }
    private static int CalculateLatestTestSaveIndex()
    {
        var saveNames = GetTestSaveNames();
        return saveNames.Length;
    }
    public static void NewTestSave()
    {
        currentTestSaveIndex = CalculateLatestTestSaveIndex();
    }
    public static void TestSave()
    {
        string latestSaveName = "test" + currentTestSaveIndex.ToString();
        SaveManager.Save(latestSaveName,SaveData.Instance);
    }
    public static void TestLoad()
    {
        currentTestSaveIndex = CalculateLatestTestSaveIndex() - 1;
        if(currentTestSaveIndex < 0)
        {
            NewTestSave();
            return;
        }
        string latestSaveName = "test" + currentTestSaveIndex.ToString();
        SaveData.Instance = (SaveData)SaveManager.Load(latestSaveName);
    }
}
