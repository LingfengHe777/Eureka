using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.AddressableAssets;

/// <summary>
/// 玩家加载、游戏状态、静态状态事件
/// </summary>
public class GameMgr : MonoBehaviour
{
    // 全局就绪状态与事件
    public static bool IsGameplayReady { get; private set; }
    public static event Action OnGameplayReady;
    public static event Action<GameState> OnGameStateChanged;

    // 运行时上下文
    private GameContext gameContext;

    /// <summary>
    /// 局内状态枚举
    /// </summary>
    public enum GameState
    {
        Waiting,
        Playing,
        Paused,
        Victory,
        Defeat
    }

    /// <summary>
    /// 结算UI展示枚举
    /// </summary>
    public enum GameEndPresentationKind
    {
        None,
        Defeat,
        VictoryNormal,
        VictoryEndless
    }
    
    // 当前游戏状态
    private GameState currentState = GameState.Waiting;

    // 配置加载键
    [SerializeField]
    [Tooltip("GlobalStatConfig键")]
    private string globalConfigKey = "GlobalStatConfig";
    [SerializeField]
    [Tooltip("StatClampConfig键")]
    private string statClampConfigKey = "StatClampConfig";
    [SerializeField]
    [Tooltip("战斗飘字预制体引用")]
    private AssetReferenceGameObject combatPopupPrefabRef;

    // 运行时配置缓存
    private GameSession gameSession;
    private GlobalStatConfig globalConfig;
    private StatClampConfig statClampConfig;

    // 玩家与关联组件缓存
    private GameObject player;
    private PlayerEvents playerEvents;
    private CoinManager coinManager;
    private CombatPopupManager combatPopupManager;

    // 结算展示状态缓存
    private GameEndPresentationKind lastEndGamePresentationKind = GameEndPresentationKind.None;
    private int lastEndlessRoundDisplay;

    // 生命周期与异步请求控制
    private bool isDestroyed;
    private int playerLoadRequestId;

    /// <summary>
    /// 初始化上下文、飘字管理器与界面初始状态。
    /// </summary>
    private void Awake()
    {
        IsGameplayReady = false;
        gameContext = GetComponent<GameContext>();
        if (gameContext == null)
        {
            gameContext = gameObject.AddComponent<GameContext>();
        }
        combatPopupManager = GetComponent<CombatPopupManager>();
        if (combatPopupManager == null)
        {
            combatPopupManager = gameObject.AddComponent<CombatPopupManager>();
        }

        gameContext.RegisterGameMgr(this);

        UIManager ui = UIManager.Instance;
        ui?.TeardownPersistentGameplayPanels();
    }

    /// <summary>
    /// 启动时加载配置并触发玩家初始化。
    /// </summary>
    private async void Start()
    {
        var initHandle = Addressables.InitializeAsync();
        await initHandle.Task;
        if (IsInstanceUnavailable()) return;

        gameSession = await GameSessionManager.Instance.GetSessionAsync();
        if (IsInstanceUnavailable() || gameSession == null) return;

        globalConfig = await LoadGlobalConfigTask();
        if (IsInstanceUnavailable() || globalConfig == null) return;

        statClampConfig = await LoadStatClampConfigTask();
        if (IsInstanceUnavailable() || !ValidateStartupDependencies()) return;

        LoadAndInitializePlayer();
    }


    /// <summary>
    /// 加载GlobalStatConfig
    /// </summary>
    private Task<GlobalStatConfig> LoadGlobalConfigTask()
    {
        TaskCompletionSource<GlobalStatConfig> tcs = new();
        AddressablesMgr.Instance.LoadAsset<GlobalStatConfig>(globalConfigKey, (config) =>
        {
            tcs.SetResult(config);
        });
        return tcs.Task;
    }

    /// <summary>
    /// 加载StatClampConfig
    /// </summary>
    private Task<StatClampConfig> LoadStatClampConfigTask()
    {
        TaskCompletionSource<StatClampConfig> tcs = new();
        AddressablesMgr.Instance.LoadAsset<StatClampConfig>(statClampConfigKey, (config) =>
        {
            tcs.SetResult(config);
        });
        return tcs.Task;
    }

    /// <summary>
    /// 加载Player Prefab并完成初始化
    /// </summary>
    private void LoadAndInitializePlayer()
    {
        int requestId = ++playerLoadRequestId;
        AddressablesMgr.Instance.LoadAsset<GameObject>("Player", (_player) =>
        {
            if (isDestroyed || this == null || requestId != playerLoadRequestId)
            {
                return;
            }

            if (_player == null)
            {
                return;
            }

            player = Instantiate(_player);
            player.SetActive(false);

            SetupCamera();

            InitializePlayer();

            gameContext?.RegisterPlayer(player);

            player.SetActive(true);

            InitializeCombatPopupSystem();

            IsGameplayReady = true;
            OnGameplayReady?.Invoke();

            LoadGamePanel();

            player.transform.position = Vector3.zero;

            SetGameState(ShouldStartPaused() ? GameState.Paused : GameState.Playing);
        });
    }

    /// <summary>
    /// 打开GamePanel
    /// </summary>
    private void LoadGamePanel()
    {
        UIManager ui = UIManager.Instance;
        if (ui == null) return;
        ui.ShowPanel<GamePanel>(_ => { });
    }

    /// <summary>
    /// 组件、外观、属性、背包、事件
    /// </summary>
    private void InitializePlayer()
    {
        if (player == null) return;

        EnsurePlayerComponents();
        SetupCharacterVisuals();
        InitializeStatSystem();
        InitializeInventorySystem();
        SubscribeToPlayerEvents();
    }

    /// <summary>
    /// 补全玩家所需组件
    /// </summary>
    private void EnsurePlayerComponents()
    {
        EnsureComponent<StatHandler>();
        EnsureComponent<EffectManager>();
        EnsureComponent<InventoryManager>();
        EnsureComponent<PlayerEvents>();
        EnsureComponent<PlayerHealth>();
        EnsureComponent<PlayerMovement>();

        EnsureComponent<WeaponController>();
        EnsureComponent<WeaponManager>();

        EnsureComponent<CoinCollector>();
        EnsureComponent<CoinManager>();
        EnsureComponent<ExperienceManager>();
        EnsureComponent<LevelSystem>();

        playerEvents = player.GetComponent<PlayerEvents>();
        coinManager = player.GetComponent<CoinManager>();
        coinManager.SetCoins(0);
    }

    /// <summary>
    /// 获取组件
    /// </summary>
    private T EnsureComponent<T>() where T : Component
    {
        return player.GetComponent<T>() ?? player.AddComponent<T>();
    }

    /// <summary>
    /// 主相机跟随玩家
    /// </summary>
    private void SetupCamera()
    {
        Camera main = Camera.main;
        if (main == null) return;

        MainCamera mainCamera = main.GetComponent<MainCamera>();
        if (mainCamera != null)
        {
            mainCamera.SetCameraTarget(player.transform);
        }
    }

    /// <summary>
    /// 按会话角色设置 Sprite / Animator。
    /// </summary>
    private void SetupCharacterVisuals()
    {
        SpecialStatConfig character = gameSession?.selectedCharacter;
        if (character == null) return;

        PlayerVisuals visuals = player.GetComponent<PlayerVisuals>();
        if (visuals == null)
        {
            return;
        }

        SpriteRenderer sr = visuals.BodySpriteRenderer;
        if (sr == null)
        {
            return;
        }

        if (character.icon != null)
        {
            sr.sprite = character.icon;
        }

        if (character.animatorController != null)
        {
            AddressablesMgr.Instance.LoadAsset<RuntimeAnimatorController>(character.animatorController, (controller) =>
            {
                Animator anim = visuals.BodyAnimator;
                if (anim == null)
                {
                    return;
                }

                //失败时 controller 为 null，不覆盖预制体上已有 AnimatorController
                if (controller != null)
                {
                    anim.runtimeAnimatorController = controller;
                }
            });
        }
    }

    /// <summary>
    /// 注入全局配置、角色与钳制
    /// </summary>
    private void InitializeStatSystem()
    {
        StatHandler statHandler = player.GetComponent<StatHandler>();
        if (statHandler == null) return;
        statHandler.globalConfig = globalConfig;
        statHandler.characterConfig = gameSession.selectedCharacter;
        statHandler.statClampConfig = statClampConfig;
    }

    /// <summary>
    /// 初始化背包并应用角色天生 LogicEffect
    /// </summary>
    private void InitializeInventorySystem()
    {
        InventoryManager inventoryManager = player.GetComponent<InventoryManager>();
        if (inventoryManager == null) return;
        inventoryManager.InitializeFromSession(gameSession);

        SpecialStatConfig character = gameSession?.selectedCharacter;
        if (character == null) return;

        EffectManager effectManager = player.GetComponent<EffectManager>();
        if (effectManager == null) return;

        List<LogicEffectSO> innateLogicEffects = character.innateLogicEffects;
        if (innateLogicEffects == null || innateLogicEffects.Count == 0)
        {
            return;
        }

        for (int i = 0; i < innateLogicEffects.Count; i++)
        {
            LogicEffectSO logicEffect = innateLogicEffects[i];
            if (logicEffect != null)
            {
                effectManager.ApplyEffect(logicEffect);
            }
        }
    }

    /// <summary>
    /// 飘字：订阅玩家事件。
    /// </summary>
    private void InitializeCombatPopupSystem()
    {
        if (combatPopupManager == null)
        {
            return;
        }

        if (combatPopupPrefabRef == null || !combatPopupPrefabRef.RuntimeKeyIsValid())
        {
            return;
        }

        if (playerEvents == null || player == null)
        {
            return;
        }

        combatPopupManager.Initialize(playerEvents, player.transform, combatPopupPrefabRef);
    }

    /// <summary>
    /// 订阅死亡等由本类处理的事件。
    /// </summary>
    private void SubscribeToPlayerEvents()
    {
        if (playerEvents == null) return;
        playerEvents.OnDeath += HandlePlayerDeath;
    }

    /// <summary>
    /// 死亡：解析结算展示并设为 Defeat。
    /// </summary>
    private void HandlePlayerDeath()
    {
        ResolveEndGamePresentationOnDeath();
        SetGameState(GameState.Defeat);
    }

    /// <summary>
    /// 死亡时根据当前模式解析最终结算展示类型。
    /// </summary>
    private void ResolveEndGamePresentationOnDeath()
    {
        if (TryGetWaveManager(out WaveManager waveManager) && waveManager.IsEndlessMode())
        {
            lastEndGamePresentationKind = GameEndPresentationKind.VictoryEndless;
            lastEndlessRoundDisplay = Mathf.Max(1, waveManager.GetEndlessWaveDisplayIndex());
        }
        else
        {
            lastEndGamePresentationKind = GameEndPresentationKind.Defeat;
            lastEndlessRoundDisplay = 0;
        }
    }

    /// <summary>
    /// 按状态同步 Time.timeScale；状态未变时也可调用，修正外部改回 1 导致的不同步。
    /// </summary>
    private static void SyncTimeScaleWithState(GameState state)
    {
        switch (state)
        {
            case GameState.Waiting:
            case GameState.Playing:
                Time.timeScale = 1f;
                break;
            case GameState.Paused:
            case GameState.Victory:
            case GameState.Defeat:
                Time.timeScale = 0f;
                break;
        }
    }

    /// <summary>
    /// 切换状态并触发 OnGameStateChanged。
    /// </summary>
    public void SetGameState(GameState newState)
    {
        if (newState == currentState)
        {
            SyncTimeScaleWithState(currentState);
            return;
        }

        currentState = newState;
        HandleStateSideEffects(newState);

        OnGameStateChanged?.Invoke(newState);

        if (newState == GameState.Defeat || newState == GameState.Victory)
        {
            TryShowEndGamePresentation();
        }
    }

    /// <summary>
    /// 普通胜利结算前设置展示类型。
    /// </summary>
    public void PrepareVictoryNormal()
    {
        lastEndGamePresentationKind = GameEndPresentationKind.VictoryNormal;
        lastEndlessRoundDisplay = 0;
    }

    /// <summary>
    /// 获取最近一次结算展示类型。
    /// </summary>
    public GameEndPresentationKind GetLastEndGamePresentationKind() => lastEndGamePresentationKind;

    /// <summary>
    /// 获取无尽模式结算中显示的轮次。
    /// </summary>
    public int GetLastEndlessRoundDisplay() => lastEndlessRoundDisplay;

    /// <summary>
    /// 胜利时兜底设置结算展示类型。
    /// </summary>
    private void EnsureEndGamePresentationForVictory()
    {
        if (lastEndGamePresentationKind != GameEndPresentationKind.None)
        {
            return;
        }

        if (TryGetWaveManager(out WaveManager waveManager) && waveManager.IsEndlessMode())
        {
            lastEndGamePresentationKind = GameEndPresentationKind.VictoryEndless;
            lastEndlessRoundDisplay = Mathf.Max(1, waveManager.GetEndlessWaveDisplayIndex());
            return;
        }

        lastEndGamePresentationKind = GameEndPresentationKind.VictoryNormal;
        lastEndlessRoundDisplay = 0;
    }

    /// <summary>
    /// 重置结算展示缓存到默认状态。
    /// </summary>
    private void ResetEndGamePresentation()
    {
        lastEndGamePresentationKind = GameEndPresentationKind.None;
        lastEndlessRoundDisplay = 0;
    }

    /// <summary>
    /// 展示结算界面并关闭战斗流程相关界面。
    /// </summary>
    private void TryShowEndGamePresentation()
    {
        if (lastEndGamePresentationKind == GameEndPresentationKind.None)
        {
            return;
        }

        UIManager ui = UIManager.Instance;
        if (ui == null) return;

        ui.HidePanel<GamePanel>(false);
        ui.HidePanel<StorePanel>(false);
        ui.HidePanel<UpgradePanel>(false);
        ui.HidePanel<PostWavesChoicePanel>(false);

        ui.ShowPanel<GameEndPanel>(panel =>
        {
            if (panel != null)
            {
                panel.BindFromGameMgr(this);
            }
        });
    }

    /// <summary>
    /// 当前状态。
    /// </summary>
    public GameState GetGameState() => currentState;

    /// <summary>
    /// 失败等结束时清空局内金币。
    /// </summary>
    private void ClearGameCoins()
    {
        coinManager?.ClearCoins();
    }

    /// <summary>
    /// 根据选项面板显示状态决定是否以暂停状态开局。
    /// </summary>
    private static bool ShouldStartPaused()
    {
        UIManager ui = UIManager.Instance;
        if (ui == null) return false;
        if (!ui.TryGetPanel<OptionsPanel>(out OptionsPanel optionsPanel) || optionsPanel == null)
        {
            return false;
        }

        return optionsPanel.isShow;
    }

    /// <summary>
    /// 判断当前实例是否已销毁或不可用。
    /// </summary>
    private bool IsInstanceUnavailable()
    {
        return isDestroyed || this == null;
    }

    /// <summary>
    /// 校验启动阶段必须依赖是否完整可用。
    /// </summary>
    private bool ValidateStartupDependencies()
    {
        if (statClampConfig == null || !statClampConfig.ValidateComplete(out _))
        {
            return false;
        }

        return gameContext == null || gameContext.ValidateRequiredServices(out _);
    }

    /// <summary>
    /// 处理状态切换带来的时间流速与结算副作用。
    /// </summary>
    private void HandleStateSideEffects(GameState newState)
    {
        switch (newState)
        {
            case GameState.Playing:
                SyncTimeScaleWithState(GameState.Playing);
                ResetEndGamePresentation();
                return;
            case GameState.Paused:
                SyncTimeScaleWithState(GameState.Paused);
                return;
            case GameState.Victory:
                EnsureEndGamePresentationForVictory();
                SyncTimeScaleWithState(GameState.Victory);
                return;
            case GameState.Defeat:
                SyncTimeScaleWithState(GameState.Defeat);
                ClearGameCoins();
                return;
        }
    }
    /// <summary>
    /// 销毁时清理标记并取消事件订阅。
    /// </summary>
    private void OnDestroy()
    {
        isDestroyed = true;
        IsGameplayReady = false;
        if (playerEvents != null)
        {
            playerEvents.OnDeath -= HandlePlayerDeath;
        }
    }

    /// <summary>
    /// 从上下文中获取可用的WaveManager。
    /// </summary>
    private bool TryGetWaveManager(out WaveManager waveManager)
    {
        waveManager = null;
        return gameContext != null && gameContext.TryGetWaveManager(out waveManager) && waveManager != null;
    }
}