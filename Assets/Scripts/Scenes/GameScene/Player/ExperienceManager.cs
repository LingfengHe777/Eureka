using UnityEngine;

/// <summary>
/// 经验管理：累积经验、升级判定，与 LevelSystem、PlayerEvents 协作。
/// </summary>
[RequireComponent(typeof(PlayerEvents), typeof(LevelSystem))]
public class ExperienceManager : MonoBehaviour
{
    [Header("组件引用")]
    //PlayerEvents
    private PlayerEvents playerEvents;
    //LevelSystem
    private LevelSystem levelSystem;

    //升级配置（与 LevelSystem 共享）
    private LevelConfig levelConfig;

    //配置是否已从 LevelSystem 就绪
    private bool isConfigLoaded = false;

    [Header("经验值状态")]
    //当前总经验
    private float currentExp = 0f;

    //当前等级段内升到下一级所需经验（缓存）
    private float expRequiredForNextLevel = 0f;

    private void Awake()
    {
        playerEvents = GetComponent<PlayerEvents>();
        levelSystem = GetComponent<LevelSystem>();
    }

    private void Start()
    {
        StartCoroutine(WaitForLevelConfig());
    }

    /// <summary>
    /// 等待 LevelSystem 加载 LevelConfig 后初始化并订阅经验事件。
    /// </summary>
    private System.Collections.IEnumerator WaitForLevelConfig()
    {
        int attempts = 0;
        while (levelSystem == null || levelSystem.GetLevelConfig() == null)
        {
            yield return new WaitForSeconds(0.1f);
            attempts++;
            if (attempts > 100)
            {
                yield break;
            }
        }

        levelConfig = levelSystem.GetLevelConfig();
        isConfigLoaded = true;

        InitializeExperience();

        if (playerEvents != null)
        {
            playerEvents.OnExpGained += AddExperience;
        }
    }

    /// <summary>
    /// 按当前等级对齐总经验与下一级需求。
    /// </summary>
    private void InitializeExperience()
    {
        if (!isConfigLoaded || levelSystem == null || levelConfig == null) return;

        int currentLevel = levelSystem.GetCurrentLevel();
        currentExp = levelConfig.GetTotalExpForLevel(currentLevel);
        expRequiredForNextLevel = levelConfig.GetExpForLevel(currentLevel);
    }

    /// <summary>
    /// 增加经验并尝试连续升级（exp 获得量）。
    /// </summary>
    public void AddExperience(float exp)
    {
        if (!isConfigLoaded || levelSystem == null || levelConfig == null) return;

        currentExp += exp;

        CheckLevelUp();
    }

    /// <summary>
    /// 经验足够时循环调用 LevelUp 直至不足或满级。
    /// </summary>
    private void CheckLevelUp()
    {
        if (!isConfigLoaded || levelSystem == null || levelConfig == null) return;

        int currentLevel = levelSystem.GetCurrentLevel();

        if (currentLevel >= levelConfig.maxLevel) return;

        float totalExpForNextLevel = levelConfig.GetTotalExpForLevel(currentLevel + 1);

        while (currentExp >= totalExpForNextLevel && currentLevel < levelConfig.maxLevel)
        {
            if (levelSystem != null)
            {
                levelSystem.LevelUp();
            }

            currentLevel = levelSystem.GetCurrentLevel();
            totalExpForNextLevel = levelConfig.GetTotalExpForLevel(currentLevel + 1);
        }

        expRequiredForNextLevel = levelConfig.GetExpForLevel(currentLevel);
    }

    /// <summary>
    /// 当前累计总经验。
    /// </summary>
    public float GetCurrentExp() => currentExp;

    /// <summary>
    /// 当前等级起始累计经验。
    /// </summary>
    public float GetTotalExpForCurrentLevel()
    {
        if (!isConfigLoaded || levelSystem == null || levelConfig == null) return 0f;
        return levelConfig.GetTotalExpForLevel(levelSystem.GetCurrentLevel());
    }

    /// <summary>
    /// 下一等级起始累计经验（满级为 float.MaxValue）。
    /// </summary>
    public float GetTotalExpForNextLevel()
    {
        if (!isConfigLoaded || levelSystem == null || levelConfig == null) return 0f;
        int nextLevel = levelSystem.GetCurrentLevel() + 1;
        if (nextLevel > levelConfig.maxLevel) return float.MaxValue;
        return levelConfig.GetTotalExpForLevel(nextLevel);
    }

    /// <summary>
    /// 当前等级内升到下一级所需经验差。
    /// </summary>
    public float GetExpRequiredForNextLevel() => expRequiredForNextLevel;

    /// <summary>
    /// 当前等级进度 0–1。
    /// </summary>
    public float GetExpProgress()
    {
        if (!isConfigLoaded || levelSystem == null || levelConfig == null) return 0f;

        int currentLevel = levelSystem.GetCurrentLevel();
        if (currentLevel >= levelConfig.maxLevel) return 1f;

        float totalExpForCurrentLevel = levelConfig.GetTotalExpForLevel(currentLevel);
        float totalExpForNextLevel = levelConfig.GetTotalExpForLevel(currentLevel + 1);

        if (totalExpForNextLevel <= totalExpForCurrentLevel) return 1f;

        return (currentExp - totalExpForCurrentLevel) / (totalExpForNextLevel - totalExpForCurrentLevel);
    }

    private void OnDestroy()
    {
        if (playerEvents != null)
        {
            playerEvents.OnExpGained -= AddExperience;
        }
    }
}
