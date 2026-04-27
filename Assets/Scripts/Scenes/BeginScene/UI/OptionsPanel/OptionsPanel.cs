using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// 设置面板：音频/视频/系统操作页签，读写 GameDataMgr 存档与场景切换。
/// </summary>
public class OptionsPanel : BasePanel
{
    //tab颜色
    private Color32 normalColor = new Color32(200, 200, 200, 255);
    private Color32 selectedColor = new Color32(255, 255, 255, 255);

    //控件
    public Button btnClose;

    public Toggle togAudioSettings;
    public Toggle togVideoSettings;
    public Toggle togSystemActions;
    public Toggle togMusic;
    public Toggle togSound;
    public Slider sliderMusic;
    public Slider sliderSound;
    public Button btnRestartGame;
    public Button btnQuitGame;

    private Image imgAudioSettings;
    private Image imgVideoSettings;
    private Image imgSystemActions;

    //页签根节点
    public GameObject audioSettings;
    public GameObject videoSettings;
    public GameObject systemActions;

    [Header("场景地址")]
    [SerializeField] private string gameSceneAddress = "GameScene";
    [SerializeField] private string beginSceneAddress = "BeginScene";
    [SerializeField] private string gameplaySceneName = "GameScene";
    [Header("确认提示文案")]
    [SerializeField] private string restartConfirmTips = "Restart current run?";
    [SerializeField] private string quitConfirmTips = "Quit to main menu?";

    /// <summary>
    /// 缓存页签背景图引用并默认隐藏各页。
    /// </summary>
    protected override void Awake()
    {
        base.Awake();

        imgAudioSettings = togAudioSettings.transform.Find("Background").GetComponentInChildren<Image>();
        imgVideoSettings = togVideoSettings.transform.Find("Background").GetComponentInChildren<Image>();
        if (togSystemActions != null)
        {
            imgSystemActions = togSystemActions.transform.Find("Background").GetComponentInChildren<Image>();
        }

        audioSettings.SetActive(false);
        videoSettings.SetActive(false);
        if (systemActions != null)
        {
            systemActions.SetActive(false);
        }
    }

    /// <summary>
    /// 绑定关闭、页签、音量控件与系统按钮。
    /// </summary>
    public override void Init()
    {
        print("OptionsPanel打开");
        imgAudioSettings.color = selectedColor;
        imgVideoSettings.color = normalColor;
        if (imgSystemActions != null)
        {
            imgSystemActions.color = normalColor;
        }
        audioSettings.SetActive(true);
        videoSettings.SetActive(false);
        if (systemActions != null)
        {
            systemActions.SetActive(false);
        }

        btnClose.onClick.AddListener(() =>
        {
            GameDataMgr.Instance.SaveAudioData();
            GameDataMgr.Instance.SaveVideoData();
            UIManager.Instance.HidePanel<OptionsPanel>();
        });

        togAudioSettings.onValueChanged.AddListener((isOn) =>
        {
            imgAudioSettings.color = isOn ? selectedColor : normalColor;
            if (isOn)
            {
                audioSettings.SetActive(true);
                videoSettings.SetActive(false);
                if (systemActions != null)
                {
                    systemActions.SetActive(false);
                }
            }
        });

        togVideoSettings.onValueChanged.AddListener((isOn) =>
        {
            imgVideoSettings.color = isOn ? selectedColor : normalColor;
            if (isOn)
            {
                audioSettings.SetActive(false);
                videoSettings.SetActive(true);
                if (systemActions != null)
                {
                    systemActions.SetActive(false);
                }
            }
        });

        if (togSystemActions != null)
        {
            togSystemActions.onValueChanged.AddListener((isOn) =>
            {
                if (imgSystemActions != null)
                {
                    imgSystemActions.color = isOn ? selectedColor : normalColor;
                }

                if (isOn)
                {
                    audioSettings.SetActive(false);
                    videoSettings.SetActive(false);
                    if (systemActions != null)
                    {
                        systemActions.SetActive(true);
                    }
                }
                else if (systemActions != null)
                {
                    systemActions.SetActive(false);
                }
            });
        }

        togMusic.isOn = GameDataMgr.Instance.audioData.music;
        sliderMusic.value = GameDataMgr.Instance.audioData.musicVolume;

        togMusic.onValueChanged.AddListener((isOn) =>
        {
            GameDataMgr.Instance.audioData.music = isOn;
            GameDataMgr.Instance.RefreshAudio();
        });

        sliderMusic.onValueChanged.AddListener((value) =>
        {
            GameDataMgr.Instance.audioData.musicVolume = value;
            GameDataMgr.Instance.RefreshAudio();
        });

        togSound.isOn = GameDataMgr.Instance.audioData.sound;
        sliderSound.value = GameDataMgr.Instance.audioData.soundVolume;

        togSound.onValueChanged.AddListener((isOn) =>
        {
            GameDataMgr.Instance.audioData.sound = isOn;
            GameDataMgr.Instance.RefreshAudio();
        });

        sliderSound.onValueChanged.AddListener((value) =>
        {
            GameDataMgr.Instance.audioData.soundVolume = value;
            GameDataMgr.Instance.RefreshAudio();
        });

        ConfigureSystemActionsVisibilityByScene();
        BindSystemActionButtons();
    }

    /// <summary>
    /// 仅在游戏场景显示系统操作页签。
    /// </summary>
    private void ConfigureSystemActionsVisibilityByScene()
    {
        bool isGameplayScene = SceneManager.GetActiveScene().name == gameplaySceneName;

        if (togSystemActions != null)
        {
            togSystemActions.gameObject.SetActive(isGameplayScene);
        }

        if (!isGameplayScene)
        {
            if (systemActions != null)
            {
                systemActions.SetActive(false);
            }

            if (togSystemActions != null)
            {
                togSystemActions.isOn = false;
            }
        }
    }

    /// <summary>
    /// 绑定重启与退回主菜单按钮。
    /// </summary>
    private void BindSystemActionButtons()
    {
        if (btnRestartGame != null)
        {
            btnRestartGame.onClick.AddListener(RestartGame);
        }

        if (btnQuitGame != null)
        {
            btnQuitGame.onClick.AddListener(QuitGame);
        }
    }

    /// <summary>
    /// 确认后重启当前游戏场景。
    /// </summary>
    private void RestartGame()
    {
        ShowTipsConfirm(restartConfirmTips, () =>
        {
            Time.timeScale = 1f;
            CleanupGameplayRuntimeObjects(false);
            UIManager.Instance.HidePanel<OptionsPanel>(false);
            GameObjectPoolManager.Instance.ClearAllPools();
            AddressablesMgr.Instance.LoadScene(gameSceneAddress, LoadSceneMode.Single, (scene) =>
            {
                GameDataMgr.Instance.PlayMusic("GameMusic");
            });
        });
    }

    /// <summary>
    /// 确认后加载开始场景。
    /// </summary>
    private void QuitGame()
    {
        ShowTipsConfirm(quitConfirmTips, () =>
        {
            Time.timeScale = 1f;
            CleanupGameplayRuntimeObjects(true);
            UIManager.Instance.HidePanel<OptionsPanel>(false);
            GameObjectPoolManager.Instance.ClearAllPools();
            AddressablesMgr.Instance.LoadScene(beginSceneAddress, LoadSceneMode.Single);
        });
    }

    /// <summary>
    /// 弹出 TipsPanel，确认后执行 onConfirm。
    /// </summary>
    private void ShowTipsConfirm(string tips, System.Action onConfirm)
    {
        UIManager.Instance.ShowPanel<TipsPanel>(panel =>
        {
            if (panel == null)
            {
                return;
            }

            panel.Setup(
                tips,
                () => UIManager.Instance.HidePanel<TipsPanel>(false),
                () =>
                {
                    UIManager.Instance.HidePanel<TipsPanel>(false);
                    onConfirm?.Invoke();
                });
        });
    }

    /// <summary>
    /// 回收 UI 与池化对象，可选重置会话。
    /// </summary>
    private static void CleanupGameplayRuntimeObjects(bool resetSession)
    {
        UIManager.Instance.TeardownPersistentGameplayPanels();

        if (GameObjectPoolManager.Instance != null)
        {
            GameObjectPoolManager.Instance.ReleaseAllActivePooledInstances();
        }

        if (GameContext.HasInstance && GameContext.Instance.TryGetSpawnManager(out SpawnManager spawnManager) && spawnManager != null)
        {
            spawnManager.ClearActiveEnemyListOnly();
        }

        if (resetSession)
        {
            GameSessionManager.Instance.ResetSession();
        }
    }
}
