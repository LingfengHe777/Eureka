using LitJson;
using System.IO;
using UnityEngine;

/// <summary>
/// JSON 序列化方案。
/// </summary>
public enum JsonType
{
    JsonUtlity,
    LitJson,
}

/// <summary>
/// JSON 持久化：序列化写入磁盘、反序列化读取。
/// </summary>
public class JsonMgr
{
    private static JsonMgr instance = new JsonMgr();
    public static JsonMgr Instance => instance;

    /// <summary>
    /// 单例。
    /// </summary>
    private JsonMgr() { }

    /// <summary>
    /// 序列化并写入持久化路径。
    /// </summary>
    public void SaveData(object data, string fileName, JsonType type = JsonType.LitJson)
    {
        string path = Application.persistentDataPath + "/" + fileName + ".json";
        string jsonStr = "";
        switch (type)
        {
            case JsonType.JsonUtlity:
                jsonStr = JsonUtility.ToJson(data);
                break;
            case JsonType.LitJson:
                jsonStr = JsonMapper.ToJson(data);
                break;
        }
        File.WriteAllText(path, jsonStr);
    }

    /// <summary>
    /// 从 StreamingAssets 或持久化路径读取；无文件则返回 new T()。
    /// </summary>
    public T LoadData<T>(string fileName, JsonType type = JsonType.LitJson) where T : new()
    {
        string path = Application.streamingAssetsPath + "/" + fileName + ".json";
        if (!File.Exists(path))
            path = Application.persistentDataPath + "/" + fileName + ".json";
        if (!File.Exists(path))
            return new T();

        string jsonStr = File.ReadAllText(path);
        T data = default(T);
        switch (type)
        {
            case JsonType.JsonUtlity:
                data = JsonUtility.FromJson<T>(jsonStr);
                break;
            case JsonType.LitJson:
                data = JsonMapper.ToObject<T>(jsonStr);
                break;
        }

        return data;
    }
}
