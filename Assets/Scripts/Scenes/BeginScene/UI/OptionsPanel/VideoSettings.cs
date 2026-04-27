using UnityEngine;

/// <summary>
/// VideoSettings 面板：四个 Dropdown 管理分辨率/显示模式/帧率/画质，实时写 videoData。
/// </summary>
public class VideoSettings : MonoBehaviour
{
    private const int FullscreenIndex = 0;
    private const int WindowedIndex = 1;
    private const string CustomResolutionText = "Custom";

    [Header("下拉菜单")]
    public OptionDropdown resolutionDP;
    public OptionDropdown displayModeDP;
    public OptionDropdown frameRateDP;
    public OptionDropdown qualityDP;

    //当前预览中的值（未单独保存JSON前）
    private int resolutionIndex;
    private int displayModeIndex;
    private int frameRateIndex;
    private int qualityIndex;

    /// <summary>
    /// 从存档刷新 Dropdown 并绑定监听。
    /// </summary>
    void OnEnable()
    {
        VideoData data = GameDataMgr.Instance.videoData;

        resolutionIndex = data.resolutionIndex;
        displayModeIndex = data.displayModeIndex;
        frameRateIndex = data.frameRateIndex;
        qualityIndex = data.qualityIndex;

        resolutionDP.SetValueWithoutNotify(resolutionIndex);
        displayModeDP.SetValueWithoutNotify(displayModeIndex);
        frameRateDP.SetValueWithoutNotify(frameRateIndex);
        qualityDP.SetValueWithoutNotify(qualityIndex);
        RefreshResolutionCaption();

        resolutionDP.onValueChanged.AddListener(OnResolutionChanged);
        displayModeDP.onValueChanged.AddListener(OnDisplayModeChanged);
        frameRateDP.onValueChanged.AddListener(OnFrameRateChanged);
        qualityDP.onValueChanged.AddListener(OnQualityChanged);
    }

    /// <summary>
    /// 解绑监听，避免重复订阅。
    /// </summary>
    void OnDisable()
    {
        resolutionDP.onValueChanged.RemoveListener(OnResolutionChanged);
        displayModeDP.onValueChanged.RemoveListener(OnDisplayModeChanged);
        frameRateDP.onValueChanged.RemoveListener(OnFrameRateChanged);
        qualityDP.onValueChanged.RemoveListener(OnQualityChanged);
    }

    /// <summary>
    /// 分辨率变更：窗口模式下选预设则切回全屏并应用分辨率。
    /// </summary>
    void OnResolutionChanged(int index)
    {
        resolutionIndex = index;
        GameDataMgr.Instance.videoData.resolutionIndex = index;

        if (displayModeIndex == WindowedIndex)
        {
            displayModeIndex = FullscreenIndex;
            GameDataMgr.Instance.videoData.displayModeIndex = displayModeIndex;
            displayModeDP.SetValueWithoutNotify(displayModeIndex);
        }

        RefreshResolutionCaption();
        ApplyResolution();
    }

    /// <summary>
    /// 显示模式变更并应用分辨率。
    /// </summary>
    void OnDisplayModeChanged(int index)
    {
        displayModeIndex = index;
        GameDataMgr.Instance.videoData.displayModeIndex = index;
        RefreshResolutionCaption();
        ApplyResolution();
    }

    /// <summary>
    /// 帧率索引变更并应用 targetFrameRate。
    /// </summary>
    void OnFrameRateChanged(int index)
    {
        frameRateIndex = index;
        GameDataMgr.Instance.videoData.frameRateIndex = index;
        ApplyFrameRate();
    }

    /// <summary>
    /// 画质等级变更并应用 QualitySettings。
    /// </summary>
    void OnQualityChanged(int index)
    {
        qualityIndex = index;
        GameDataMgr.Instance.videoData.qualityIndex = index;
        ApplyQuality();
    }

    /// <summary>
    /// 按当前索引设置 Screen 分辨率与全屏。
    /// </summary>
    void ApplyResolution()
    {
        int w = resolutionIndex == 0 ? 2560 : 1920;
        int h = resolutionIndex == 0 ? 1440 : 1080;

        bool fullscreen = displayModeIndex == 0;
        Screen.SetResolution(w, h, fullscreen);
    }

    /// <summary>
    /// 按帧率索引设置 Application.targetFrameRate。
    /// </summary>
    void ApplyFrameRate()
    {
        switch (frameRateIndex)
        {
            case 0: Application.targetFrameRate = 120; break;
            case 1: Application.targetFrameRate = 90; break;
            case 2: Application.targetFrameRate = 60; break;
            default: Application.targetFrameRate = -1; break;
        }
    }

    /// <summary>
    /// 设置 QualitySettings 当前等级。
    /// </summary>
    void ApplyQuality()
    {
        QualitySettings.SetQualityLevel(qualityIndex);
    }

    /// <summary>
    /// 将当前 videoData 写入磁盘。
    /// </summary>
    public void SaveCurrentSettings()
    {
        GameDataMgr.Instance.SaveVideoData();
    }

    /// <summary>
    /// 窗口模式下显示 Custom 文案；全屏下显示选项原文。
    /// </summary>
    private void RefreshResolutionCaption()
    {
        if (resolutionDP == null || resolutionDP.dropdown == null || resolutionDP.dropdown.captionText == null)
        {
            return;
        }

        if (displayModeIndex == WindowedIndex)
        {
            resolutionDP.dropdown.captionText.text = CustomResolutionText;
        }
        else
        {
            int optionCount = resolutionDP.dropdown.options != null ? resolutionDP.dropdown.options.Count : 0;
            if (optionCount <= 0)
            {
                return;
            }

            int safeIndex = Mathf.Clamp(resolutionIndex, 0, optionCount - 1);
            resolutionDP.dropdown.captionText.text = resolutionDP.dropdown.options[safeIndex].text;
        }
    }
}
