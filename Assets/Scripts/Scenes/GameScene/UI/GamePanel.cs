using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 游戏面板
/// 显示玩家血量、经验值、金币、波次等信息
/// </summary>
public class GamePanel : BasePanel
{
    private const float FALLBACK_REFRESH_INTERVAL = 1f;

    //面板UI引用
    [Header("界面引用")]
    [Header("血量显示")]
    [Tooltip("最大血量背景图片")]
    public Image imageMaxHealth;

    [Tooltip("当前血量填充图片")]
    public Image imageCurrentHealth;

    [Tooltip("血量文本")]
    public Text textHealth;

    [Header("经验值显示")]
    [Tooltip("最大经验值背景图片")]
    public Image imageMaxExperience;

    [Tooltip("当前经验值填充图片")]
    public Image imageCurrentExperience;

    [Tooltip("等级文本")]
    public Text textLevel;

    [Header("金币显示")]
    [Tooltip("金币图标")]
    public Image imageCoin;

    [Tooltip("金币数量文本")]
    public Text textCoinCount;

    [Header("波次显示")]
    [Tooltip("波次文本")]
    public Text textWave;

    [Tooltip("计时器文本")]
    public Text textTimer;

    [Header("受伤反馈")]
    [Tooltip("全屏受伤泛红")]
    [SerializeField] private Image damageVignetteImage;

    [Tooltip("受伤泛红最大透明度，0～1")]
    [SerializeField] private float damageVignetteAlpha = 0.32f;

    [Tooltip("泛红淡出时长，秒")]
    [SerializeField] private float damageVignetteDuration = 0.22f;

    [SerializeField] private string playerHurtSfxKey = "Hurt";

    private Coroutine damageVignetteRoutine;

    //玩家事件监听器
    private PlayerEvents playerEvents;
    //玩家生命值管理器
    private PlayerHealth playerHealth;
    //玩家经验值管理器
    private ExperienceManager experienceManager;
    //玩家等级管理器
    private LevelSystem levelSystem;
    //金币管理器
    private CoinManager coinManager;
    //波次管理器
    private WaveManager waveManager;
    private Coroutine findComponentsCoroutine;
    private bool isBoundToEvents;

    /// <summary>
    /// 初始化进度条并监听游戏状态
    /// </summary>
    public override void Init()
    {
        InitializeUIProgress();
        GameMgr.OnGameStateChanged += HandleGameStateChanged;

        GameMgr gameMgr = null;
        if (GameContext.HasInstance)
        {
            GameContext.Instance.TryGetGameMgr(out gameMgr);
        }
        if (gameMgr != null && gameMgr.GetGameState() == GameMgr.GameState.Playing)
        {
            StartFindPlayerComponents();
        }
    }

    /// <summary>
    /// 初始化生命值和经验值的进度条
    /// </summary>
    private void InitializeUIProgress()
    {
        if (imageCurrentHealth != null)
        {
            imageCurrentHealth.fillAmount = 0f;
        }

        if (imageCurrentExperience != null)
        {
            imageCurrentExperience.fillAmount = 0f;
        }
    }

    /// <summary>
    /// 游戏状态变为 Playing 时开始查找玩家组件
    /// </summary>
    private void HandleGameStateChanged(GameMgr.GameState newState)
    {
        if (newState == GameMgr.GameState.Playing)
        {
            StartFindPlayerComponents();
        }
    }

    /// <summary>
    /// 启动协程查找玩家身上的管理器
    /// </summary>
    private void StartFindPlayerComponents()
    {
        if (findComponentsCoroutine != null)
        {
            StopCoroutine(findComponentsCoroutine);
        }
        findComponentsCoroutine = StartCoroutine(FindPlayerComponents());
    }

    /// <summary>
    /// 等待玩家出现后绑定事件并刷新 UI
    /// </summary>
    private System.Collections.IEnumerator FindPlayerComponents()
    {
        GameObject player = null;
        int attempts = 0;
        int maxAttempts = 100;
        while (player == null && attempts < maxAttempts)
        {
            if (GameContext.HasInstance && GameContext.Instance.TryGetPlayer(out player))
            {
                break;
            }
            if (player == null)
            {
                yield return new WaitForSeconds(0.1f);
                attempts++;
            }
        }

        if (player == null)
        {
            findComponentsCoroutine = null;
            yield break;
        }

        playerEvents = player.GetComponent<PlayerEvents>();
        playerHealth = player.GetComponent<PlayerHealth>();
        experienceManager = player.GetComponent<ExperienceManager>();
        levelSystem = player.GetComponent<LevelSystem>();
        coinManager = player.GetComponent<CoinManager>();
        if (GameContext.HasInstance)
        {
            GameContext.Instance.TryGetPlayerEvents(out playerEvents);
            GameContext.Instance.TryGetPlayerHealth(out playerHealth);
            GameContext.Instance.TryGetExperienceManager(out experienceManager);
            GameContext.Instance.TryGetLevelSystem(out levelSystem);
            GameContext.Instance.TryGetCoinManager(out coinManager);
        }

        waveManager = null;
        if (GameContext.HasInstance && GameContext.Instance.TryGetWaveManager(out WaveManager contextWaveManager))
        {
            waveManager = contextWaveManager;
        }

        UnsubscribeFromEvents();
        SubscribeToEvents();
        StartFallbackRefresh();
        UpdateAllUI();
        findComponentsCoroutine = null;
    }

    /// <summary>
    /// 订阅玩家与波次事件
    /// </summary>
    private void SubscribeToEvents()
    {
        if (isBoundToEvents) return;
        bool subscribed = false;

        if (playerEvents != null)
        {
            playerEvents.OnHealthChanged -= UpdateHealthUI;
            playerEvents.OnExpGained -= OnExpGained;
            playerEvents.OnLevelUp -= OnLevelUp;
            playerEvents.OnCoinsChanged -= OnCoinsChanged;
            playerEvents.OnDamageTaken -= OnPlayerDamageTaken;
            playerEvents.OnHealthChanged += UpdateHealthUI;
            playerEvents.OnExpGained += OnExpGained;
            playerEvents.OnLevelUp += OnLevelUp;
            playerEvents.OnCoinsChanged += OnCoinsChanged;
            playerEvents.OnDamageTaken += OnPlayerDamageTaken;
            subscribed = true;
        }

        if (waveManager != null)
        {
            waveManager.OnWaveStarted -= OnWaveStarted;
            waveManager.OnTimerUpdated -= OnTimerUpdated;
            waveManager.OnWaveStarted += OnWaveStarted;
            waveManager.OnTimerUpdated += OnTimerUpdated;
            subscribed = true;
        }

        isBoundToEvents = subscribed;
    }

    /// <summary>
    /// 取消订阅
    /// </summary>
    private void UnsubscribeFromEvents()
    {
        if (playerEvents != null)
        {
            playerEvents.OnHealthChanged -= UpdateHealthUI;
            playerEvents.OnExpGained -= OnExpGained;
            playerEvents.OnLevelUp -= OnLevelUp;
            playerEvents.OnCoinsChanged -= OnCoinsChanged;
            playerEvents.OnDamageTaken -= OnPlayerDamageTaken;
        }

        if (waveManager != null)
        {
            waveManager.OnWaveStarted -= OnWaveStarted;
            waveManager.OnTimerUpdated -= OnTimerUpdated;
        }

        isBoundToEvents = false;
    }

    /// <summary>
    /// 定时兜底刷新（金币/经验/波次/计时）
    /// </summary>
    private void StartFallbackRefresh()
    {
        CancelInvoke(nameof(FallbackRefreshTick));
        InvokeRepeating(nameof(FallbackRefreshTick), FALLBACK_REFRESH_INTERVAL, FALLBACK_REFRESH_INTERVAL);
    }

    /// <summary>
    /// 停止定时兜底刷新
    /// </summary>
    private void StopFallbackRefresh()
    {
        CancelInvoke(nameof(FallbackRefreshTick));
    }

    /// <summary>
    /// 定时兜底刷新回调
    /// </summary>
    private void FallbackRefreshTick()
    {
        UpdateCoinUI();
        UpdateExperienceUI();
        UpdateWaveUI();
        UpdateTimerUI();
    }

    /// <summary>
    /// 销毁时解除监听
    /// </summary>
    private void OnDestroy()
    {
        GameMgr.OnGameStateChanged -= HandleGameStateChanged;
        if (findComponentsCoroutine != null)
        {
            StopCoroutine(findComponentsCoroutine);
            findComponentsCoroutine = null;
        }
        StopFallbackRefresh();
        UnsubscribeFromEvents();
    }

    /// <summary>
    /// 刷新全部 HUD
    /// </summary>
    private void UpdateAllUI()
    {
        if (playerHealth != null)
        {
            int current = playerHealth.GetCurrentHealth();
            int max = playerHealth.GetMaxHealth();
            UpdateHealthUI(current, max);
        }

        UpdateExperienceUI();
        UpdateLevelUI();
        UpdateCoinUI();
        UpdateWaveUI();
        UpdateTimerUI();
    }

    /// <summary>
    /// 更新血量显示
    /// </summary>
    private void UpdateHealthUI(int currentHealth, int maxHealth)
    {
        if (textHealth != null)
        {
            textHealth.text = $"{currentHealth}/{maxHealth}";
        }

        if (imageCurrentHealth != null)
        {
            if (maxHealth > 0)
            {
                imageCurrentHealth.fillAmount = Mathf.Clamp01((float)currentHealth / maxHealth);
            }
            else
            {
                imageCurrentHealth.fillAmount = 0f;
            }
        }
    }

    /// <summary>
    /// 更新经验与等级文本
    /// </summary>
    private void UpdateExperienceUI()
    {
        if (experienceManager == null || levelSystem == null) return;

        float progress = experienceManager.GetExpProgress();

        if (imageCurrentExperience != null)
        {
            imageCurrentExperience.fillAmount = Mathf.Clamp01(progress);
        }

        if (textLevel != null)
        {
            int currentLevel = levelSystem.GetCurrentLevel();
            textLevel.text = $"LV.{currentLevel}";
        }
    }

    /// <summary>
    /// 仅更新等级文本
    /// </summary>
    private void UpdateLevelUI()
    {
        if (levelSystem == null) return;

        int level = levelSystem.GetCurrentLevel();

        if (textLevel != null)
        {
            textLevel.text = $"LV.{level}";
        }
    }

    /// <summary>
    /// 更新金币显示
    /// </summary>
    private void UpdateCoinUI()
    {
        if (coinManager == null) return;

        int coins = coinManager.GetCurrentCoins();

        if (textCoinCount != null)
        {
            textCoinCount.text = coins.ToString();
        }
    }

    /// <summary>
    /// 更新波次UI
    /// </summary>
    private void UpdateWaveUI()
    {
        if (waveManager == null)
        {
            return;
        }

        int wave = Mathf.Max(1, waveManager.GetCurrentWave());
        bool isEndless = waveManager.IsEndlessMode();

        if (textWave != null)
        {
            textWave.text = isEndless ? $"Round {wave} (EndLess)" : $"Round {wave}";
        }
    }

    /// <summary>
    /// 更新计时器UI
    /// </summary>
    private void UpdateTimerUI()
    {
        if (waveManager == null)
        {
            return;
        }

        float time = waveManager.GetCurrentWaveTime();

        if (textTimer != null)
        {
            textTimer.text = $"{(int)time}";
        }
    }

    /// <summary>
    /// 获得经验时刷新
    /// </summary>
    private void OnExpGained(float exp)
    {
        UpdateExperienceUI();
    }

    /// <summary>
    /// 金币变化时刷新
    /// </summary>
    private void OnCoinsChanged(int currentCoins)
    {
        UpdateCoinUI();
    }

    /// <summary>
    /// 升级时刷新
    /// </summary>
    private void OnLevelUp(int newLevel)
    {
        UpdateLevelUI();
        UpdateExperienceUI();
    }

    /// <summary>
    /// 新波次开始时刷新波次与计时
    /// </summary>
    private void OnWaveStarted(int waveNumber, bool isEndless)
    {
        UpdateWaveUI();
        UpdateTimerUI();
    }

    /// <summary>
    /// 波次计时更新时刷新
    /// </summary>
    private void OnTimerUpdated(float remainingTime)
    {
        UpdateTimerUI();
    }

    private void OnPlayerDamageTaken(float actualDamage, GameObject source)
    {
        if (actualDamage <= 0f) return;

        if (!string.IsNullOrEmpty(playerHurtSfxKey))
        {
            GameDataMgr.Instance?.PlaySound(playerHurtSfxKey);
        }

        if (damageVignetteImage == null) return;

        if (damageVignetteRoutine != null)
        {
            StopCoroutine(damageVignetteRoutine);
        }

        damageVignetteRoutine = StartCoroutine(DamageVignetteFadeRoutine());
    }

    private IEnumerator DamageVignetteFadeRoutine()
    {
        Color c = damageVignetteImage.color;
        float peakAlpha = Mathf.Clamp01(damageVignetteAlpha);
        float duration = Mathf.Max(0.05f, damageVignetteDuration);
        float t = 0f;

        while (t < duration)
        {
            t += Time.unscaledDeltaTime;
            float a = Mathf.Lerp(peakAlpha, 0f, t / duration);
            c.a = a;
            damageVignetteImage.color = c;
            yield return null;
        }

        c.a = 0f;
        damageVignetteImage.color = c;
        damageVignetteRoutine = null;
    }
}
