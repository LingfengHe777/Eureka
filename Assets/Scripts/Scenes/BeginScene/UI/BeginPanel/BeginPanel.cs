using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 开始界面：背景音乐、进入选角、选项、退出确认。
/// </summary>
public class BeginPanel : BasePanel
{
    public Button btnStart;
    public Button btnOptions;
    public Button btnExit;

    /// <summary>
    /// 播放 BeginMusic。
    /// </summary>
    private void OnEnable()
    {
        GameDataMgr.Instance.PlayMusic("BeginMusic");
    }

    /// <summary>
    /// 绑定开始、选项、退出按钮。
    /// </summary>
    public override void Init()
    {
        btnStart.onClick.AddListener(() =>
        {
            UIManager.Instance.HidePanel<BeginPanel>();
            UIManager.Instance.ShowPanel<SelectPanel>((onCompete) =>
            {
                GameDataMgr.Instance.PlayMusic("SelectMusic");
            });
        });

        btnOptions.onClick.AddListener(() =>
        {
            UIManager.Instance.ShowPanel<OptionsPanel>();
        });

        btnExit.onClick.AddListener(() =>
        {
            UIManager.Instance.ShowPanel<TipsPanel>(panel =>
            {
                panel.Setup("Are you sure you want to quit the game?", () =>
                {
                    UIManager.Instance.HidePanel<TipsPanel>(false);
                }, () =>
                {
                    Application.Quit();
                });
            });
        });
    }
}
