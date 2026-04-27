using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 全部波次完成后：选择进入无尽模式或直接胜利结算。
/// Addressables 键名需与类名一致：PostWavesChoicePanel
/// </summary>
public class PostWavesChoicePanel : BasePanel
{
    [Header("文案")]
    [SerializeField]
    private Text textHint;

    [Header("按钮")]
    [SerializeField]
    private Button buttonEndless;

    [SerializeField]
    private Button buttonVictory;

    private WaveManager waveManager;

    /// <summary>
    /// 绑定按钮与提示文案
    /// </summary>
    public override void Init()
    {
        ResolveWaveManager();

        WaveConfig waveConfig = waveManager != null ? waveManager.GetWaveConfig() : null;
        bool endlessEnabled = waveConfig != null && waveConfig.enableEndlessMode;

        if (buttonEndless != null)
        {
            buttonEndless.gameObject.SetActive(endlessEnabled);
            buttonEndless.onClick.AddListener(OnEndlessClicked);
        }

        if (buttonVictory != null)
        {
            buttonVictory.onClick.AddListener(OnVictoryClicked);
        }

        if (textHint != null)
        {
            textHint.text = endlessEnabled
                ? "All waves cleared: Enter Endless Mode?"
                : "All waves cleared: finish with Victory";
        }
    }

    /// <summary>
    /// 进入无尽模式并关闭面板
    /// </summary>
    private void OnEndlessClicked()
    {
        ResolveWaveManager();
        if (waveManager == null)
        {
            return;
        }

        UIManager.Instance.HidePanel<PostWavesChoicePanel>(false);
        waveManager.ChooseEndlessMode();
    }

    /// <summary>
    /// 选择普通胜利并关闭面板
    /// </summary>
    private void OnVictoryClicked()
    {
        ResolveWaveManager();
        if (waveManager == null)
        {
            return;
        }

        UIManager.Instance.HidePanel<PostWavesChoicePanel>(false);
        waveManager.ChooseVictory();
    }

    /// <summary>
    /// 从 GameContext 解析 WaveManager
    /// </summary>
    private void ResolveWaveManager()
    {
        if (waveManager != null)
        {
            return;
        }

        if (GameContext.HasInstance)
        {
            GameContext.Instance.TryGetWaveManager(out waveManager);
        }
    }
}
