using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// 游戏结束面板：失败、普通胜利、无尽模式结算（死亡时仍显示胜利文案 + 无尽轮次）
/// Addressables 键名需与类名一致：GameEndPanel
/// </summary>
public class GameEndPanel : BasePanel
{
    [Header("文案")]
    [SerializeField]
    private Text textTitle;

    [SerializeField]
    [Tooltip("无尽胜利时显示的无尽轮次")]
    private Text textEndlessRound;

    [SerializeField]
    [Tooltip("玩家结束时等级显示")]
    private Text textLevel;

    [Header("背景")]
    [SerializeField]
    [Tooltip("结算面板背景图")]
    private Image imageBK;
    [SerializeField]
    [Tooltip("胜利背景色")]
    private Color victoryBackgroundColor = new Color(0.16f, 0.45f, 0.24f, 1f);
    [SerializeField]
    [Tooltip("失败背景色")]
    private Color defeatBackgroundColor = new Color(0.45f, 0.16f, 0.16f, 1f);

    [Header("结算快照")]
    [SerializeField]
    [Tooltip("玩家所选角色图")]
    private Image imageCharacter;

    [SerializeField]
    [Tooltip("玩家所选难度图")]
    private Image imageMode;

    [SerializeField]
    [Tooltip("道具列表容器")]
    private Transform itemContent;

    [SerializeField]
    [Tooltip("武器列表容器")]
    private Transform weaponContent;

    [SerializeField]
    [Tooltip("列表条目预制体可寻址键")]
    private string svButtonKey = "SVButton";

    [Header("返回")]
    [SerializeField]
    private Button buttonBackToMenu;

    [SerializeField]
    [Tooltip("主菜单场景可寻址地址")]
    private string beginSceneAddress = "BeginScene";

    private InventoryManager inventoryManager;
    private WeaponManager weaponManager;
    private LevelSystem levelSystem;
    private GameObject svButtonPrefab;
    private bool svButtonLoadRequested;
    private bool isLeavingScene;

    /// <summary>
    /// 绑定序列化引用并注册按钮
    /// </summary>
    public override void Init()
    {
        if (buttonBackToMenu != null)
        {
            buttonBackToMenu.onClick.AddListener(OnBackToMenuClicked);
        }
    }

    /// <summary>
    /// 由 GameMgr 在弹出面板时注入展示数据
    /// </summary>
    public void BindFromGameMgr(GameMgr mgr)
    {
        if (mgr == null)
        {
            return;
        }

        GameMgr.GameEndPresentationKind kind = mgr.GetLastEndGamePresentationKind();
        int endlessRound = mgr.GetLastEndlessRoundDisplay();

        if (textEndlessRound != null)
        {
            textEndlessRound.gameObject.SetActive(kind == GameMgr.GameEndPresentationKind.VictoryEndless);
            if (kind == GameMgr.GameEndPresentationKind.VictoryEndless)
            {
                textEndlessRound.text = $"Reached Endless Mode Wave {endlessRound}";
            }
        }

        if (textTitle != null)
        {
            switch (kind)
            {
                case GameMgr.GameEndPresentationKind.Defeat:
                    textTitle.text = "Defeat";
                    break;
                case GameMgr.GameEndPresentationKind.VictoryNormal:
                case GameMgr.GameEndPresentationKind.VictoryEndless:
                    textTitle.text = "Victory";
                    break;
                default:
                    textTitle.text = "End";
                    break;
            }
        }

        ApplyBackgroundColor(kind);

        UpdateLevelText();
        RefreshSnapshotUI();
    }

    /// <summary>
    /// 返回主菜单：重置会话、清池并加载 Begin 场景
    /// </summary>
    private void OnBackToMenuClicked()
    {
        if (isLeavingScene)
        {
            return;
        }
        isLeavingScene = true;

        Time.timeScale = 1f;
        if (GameSessionManager.Instance != null)
        {
            GameSessionManager.Instance.ResetSession();
        }

        if (GameObjectPoolManager.Instance != null)
        {
            GameObjectPoolManager.Instance.ClearAllPools();
        }

        UIManager.Instance.HidePanel<GameEndPanel>(false);
        AddressablesMgr.Instance.LoadScene(beginSceneAddress, LoadSceneMode.Single, null);
    }

    /// <summary>
    /// 刷新会话图与运行时管理器并重建道具/武器列表
    /// </summary>
    private void RefreshSnapshotUI()
    {
        BindSessionImages();
        ResolveRuntimeManagers();
        LoadSVButtonPrefabIfNeeded(RebuildInventorySnapshot);
    }

    /// <summary>
    /// 从 GameSession 绑定角色与难度图标
    /// </summary>
    private void BindSessionImages()
    {
        GameSession session = GameSessionManager.Instance != null ? GameSessionManager.Instance.GetSession() : null;

        Sprite characterSprite = session != null && session.selectedCharacter != null ? session.selectedCharacter.icon : null;
        if (imageCharacter != null)
        {
            imageCharacter.sprite = characterSprite;
            imageCharacter.enabled = characterSprite != null;
        }

        Sprite modeSprite = session != null && session.selectedMode != null ? session.selectedMode.modeIcon : null;
        if (imageMode != null)
        {
            imageMode.sprite = modeSprite;
            imageMode.enabled = modeSprite != null;
        }
    }

    /// <summary>
    /// 从 GameContext 或玩家节点解析背包/武器/等级组件
    /// </summary>
    private void ResolveRuntimeManagers()
    {
        if (GameContext.HasInstance)
        {
            GameContext.Instance.TryGetInventoryManager(out inventoryManager);
            GameContext.Instance.TryGetWeaponManager(out weaponManager);
            GameContext.Instance.TryGetLevelSystem(out levelSystem);
        }

        if (inventoryManager == null || weaponManager == null || levelSystem == null)
        {
            GameObject player = null;
            if (GameContext.HasInstance)
            {
                GameContext.Instance.TryGetPlayer(out player);
            }
            if (player != null)
            {
                if (inventoryManager == null)
                {
                    inventoryManager = player.GetComponent<InventoryManager>();
                }
                if (weaponManager == null)
                {
                    weaponManager = player.GetComponent<WeaponManager>();
                }
                if (levelSystem == null)
                {
                    levelSystem = player.GetComponent<LevelSystem>();
                }
            }
        }
    }

    /// <summary>
    /// 更新等级文本
    /// </summary>
    private void UpdateLevelText()
    {
        if (textLevel == null)
        {
            return;
        }

        ResolveRuntimeManagers();
        int level = levelSystem != null ? Mathf.Max(1, levelSystem.GetCurrentLevel()) : 1;
        textLevel.text = level.ToString();
    }

    /// <summary>
    /// 按需异步加载 SVButton 预制体
    /// </summary>
    private void LoadSVButtonPrefabIfNeeded(System.Action onLoaded)
    {
        if (svButtonPrefab != null)
        {
            onLoaded?.Invoke();
            return;
        }

        if (svButtonLoadRequested)
        {
            return;
        }

        svButtonLoadRequested = true;
        AddressablesMgr.Instance.LoadAsset<GameObject>(svButtonKey, prefab =>
        {
            svButtonLoadRequested = false;
            svButtonPrefab = prefab;
            if (svButtonPrefab == null)
            {
                return;
            }
            onLoaded?.Invoke();
        });
    }

    /// <summary>
    /// 重建道具与武器快照
    /// </summary>
    private void RebuildInventorySnapshot()
    {
        RebuildItemSnapshot();
        RebuildWeaponSnapshot();
    }

    /// <summary>
    /// 重建道具栏汇总显示
    /// </summary>
    private void RebuildItemSnapshot()
    {
        ClearChildren(itemContent);
        if (itemContent == null || inventoryManager == null || svButtonPrefab == null)
        {
            return;
        }

        List<ItemConfig> items = inventoryManager.GetInventory();
        Dictionary<ItemConfig, int> itemCounts = new();
        for (int i = 0; i < items.Count; i++)
        {
            ItemConfig item = items[i];
            if (item == null) continue;

            if (itemCounts.ContainsKey(item))
            {
                itemCounts[item]++;
            }
            else
            {
                itemCounts[item] = 1;
            }
        }

        foreach (KeyValuePair<ItemConfig, int> pair in itemCounts)
        {
            ItemConfig item = pair.Key;
            GameObject entry = CreateSVButtonEntry(itemContent, item.itemIcon, pair.Value, true);
            if (entry == null) continue;

            SVButtonView view = entry.GetComponent<SVButtonView>();
            if (view != null)
            {
                view.SetLevelBackground(true, item.GetThemeColor());
            }
        }
    }

    /// <summary>
    /// 重建武器栏显示
    /// </summary>
    private void RebuildWeaponSnapshot()
    {
        ClearChildren(weaponContent);
        if (weaponContent == null || weaponManager == null || svButtonPrefab == null)
        {
            return;
        }

        List<WeaponManager.WeaponSlot> weapons = weaponManager.GetAllWeapons();
        for (int i = 0; i < weapons.Count; i++)
        {
            WeaponManager.WeaponSlot slot = weapons[i];
            if (slot == null || slot.weaponConfig == null) continue;

            WeaponConfig weapon = slot.weaponConfig;
            GameObject entry = CreateSVButtonEntry(weaponContent, weapon.weaponIcon, 1, false);
            if (entry == null) continue;

            SVButtonView view = entry.GetComponent<SVButtonView>();
            if (view != null)
            {
                view.SetLevelBackground(true, weapon.GetThemeColor());
            }
        }
    }

    /// <summary>
    /// 实例化一条 SVButton 条目并设置图标与数量
    /// </summary>
    private GameObject CreateSVButtonEntry(Transform parent, Sprite sprite, int stackCount, bool enableCountDisplay)
    {
        if (parent == null || svButtonPrefab == null)
        {
            return null;
        }

        GameObject entry = Instantiate(svButtonPrefab, parent);
        SVButtonView view = entry.GetComponent<SVButtonView>();
        if (view != null)
        {
            view.SetIcon(sprite);
            view.SetStackCount(stackCount, enableCountDisplay);
            view.SetSellAction(null, 0);
            view.SetMergeAction(null);
            view.SetLevelBackground(false, Color.clear);
        }
        return entry;
    }

    /// <summary>
    /// 销毁子物体
    /// </summary>
    private void ClearChildren(Transform parent)
    {
        if (parent == null) return;

        for (int i = parent.childCount - 1; i >= 0; i--)
        {
            Destroy(parent.GetChild(i).gameObject);
        }
    }

    /// <summary>
    /// 解除监听并释放预制体引用
    /// </summary>
    private void OnDestroy()
    {
        if (buttonBackToMenu != null)
        {
            buttonBackToMenu.onClick.RemoveListener(OnBackToMenuClicked);
        }

        if (svButtonPrefab != null)
        {
            AddressablesMgr.Instance?.Release(svButtonPrefab);
            svButtonPrefab = null;
        }
    }

    /// <summary>
    /// 按胜负切换背景色
    /// </summary>
    private void ApplyBackgroundColor(GameMgr.GameEndPresentationKind kind)
    {
        if (imageBK == null)
        {
            return;
        }

        switch (kind)
        {
            case GameMgr.GameEndPresentationKind.Defeat:
                imageBK.color = defeatBackgroundColor;
                break;
            case GameMgr.GameEndPresentationKind.VictoryNormal:
            case GameMgr.GameEndPresentationKind.VictoryEndless:
                imageBK.color = victoryBackgroundColor;
                break;
            default:
                imageBK.color = victoryBackgroundColor;
                break;
        }
    }
}
