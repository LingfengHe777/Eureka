using UnityEngine;

/// <summary>
/// Esc 开关 Options；Playing 时打开则 Paused，关闭时仅在由本控制器暂停则恢复 Playing。
/// </summary>
public class GameScenePauseController : MonoBehaviour
{
    private GameMgr gameMgr;
    private bool pausedByOptionsPanel;
    private bool wasOptionsPanelOpen;

    private void Update()
    {
        TrackOptionsPanelCloseFromOtherActions();
        EnsurePausedWhileOptionsOpen();

        if (!Input.GetKeyDown(KeyCode.Escape))
        {
            return;
        }

        if (IsOptionsPanelOpen())
        {
            UIManager.Instance.HidePanel<OptionsPanel>();
            TryResumeGameplayAfterClosingOptions();
            return;
        }

        OpenOptionsPanel();
    }

    /// <summary>
    /// 打开设置并按需暂停。
    /// </summary>
    private void OpenOptionsPanel()
    {
        PauseForOptionsIfAllowed();

        UIManager.Instance.ShowPanel<OptionsPanel>();
        wasOptionsPanelOpen = true;
    }

    /// <summary>
    /// 面板被其它逻辑关闭时尝试恢复游玩。
    /// </summary>
    private void TrackOptionsPanelCloseFromOtherActions()
    {
        bool isOpen = IsOptionsPanelOpen();
        if (wasOptionsPanelOpen && !isOpen)
        {
            TryResumeGameplayAfterClosingOptions();
        }

        wasOptionsPanelOpen = isOpen;
    }

    /// <summary>
    /// Options 是否显示中。
    /// </summary>
    private bool IsOptionsPanelOpen()
    {
        return UIManager.Instance.TryGetPanel<OptionsPanel>(out OptionsPanel panel) &&
               panel != null &&
               panel.isShow;
    }

    /// <summary>
    /// 非本控制器暂停原因（如商店打开）则不恢复 Playing。
    /// </summary>
    private void TryResumeGameplayAfterClosingOptions()
    {
        if (!pausedByOptionsPanel)
        {
            return;
        }

        pausedByOptionsPanel = false;
        EnsureGameMgr();
        if (gameMgr == null || gameMgr.GetGameState() != GameMgr.GameState.Paused)
        {
            return;
        }

        if (IsIntermissionPanelOpen())
        {
            return;
        }

        gameMgr.SetGameState(GameMgr.GameState.Playing);
    }

    /// <summary>
    /// 商店或升级是否仍打开。
    /// </summary>
    private bool IsIntermissionPanelOpen()
    {
        bool storeOpen = UIManager.Instance.TryGetPanel<StorePanel>(out StorePanel storePanel) &&
                         storePanel != null &&
                         storePanel.isShow;
        if (storeOpen)
        {
            return true;
        }

        bool upgradeOpen = UIManager.Instance.TryGetPanel<UpgradePanel>(out UpgradePanel upgradePanel) &&
                           upgradePanel != null &&
                           upgradePanel.isShow;
        return upgradeOpen;
    }

    /// <summary>
    /// 从 GameContext 解析 GameMgr。
    /// </summary>
    private void EnsureGameMgr()
    {
        if (gameMgr != null)
        {
            return;
        }

        if (GameContext.HasInstance)
        {
            GameContext.Instance.TryGetGameMgr(out gameMgr);
        }
    }

    private void EnsurePausedWhileOptionsOpen()
    {
        if (!IsOptionsPanelOpen())
        {
            return;
        }

        PauseForOptionsIfAllowed();
    }

    private void PauseForOptionsIfAllowed()
    {
        EnsureGameMgr();
        if (gameMgr == null)
        {
            return;
        }

        GameMgr.GameState state = gameMgr.GetGameState();
        if (state == GameMgr.GameState.Victory || state == GameMgr.GameState.Defeat)
        {
            return;
        }

        gameMgr.SetGameState(GameMgr.GameState.Paused);
        pausedByOptionsPanel = true;
    }
}
