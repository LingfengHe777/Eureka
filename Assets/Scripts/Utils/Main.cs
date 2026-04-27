using UnityEngine;

/// <summary>
/// 入口场景：打开开始面板并应用视频设置。
/// </summary>
public class Main : MonoBehaviour
{
    /// <summary>
    /// 显示 BeginPanel，回调中应用存档视频设置。
    /// </summary>
    void Start()
    {
        UIManager.Instance.ShowPanel<BeginPanel>((panel) =>
        {
            GameDataMgr.Instance.ApplyVideoSettings();
        });
    }
}
