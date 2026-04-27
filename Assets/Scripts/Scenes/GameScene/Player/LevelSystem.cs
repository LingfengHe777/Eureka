using UnityEngine;

/// <summary>
/// 等级系统：从 Addressables 加载 LevelConfig，管理当前等级与升级事件。
/// </summary>
[RequireComponent(typeof(PlayerEvents))]
public class LevelSystem : MonoBehaviour
{
    [Header("可寻址")]
    [Tooltip("LevelConfig 的可寻址标签，批量加载时取首个")]
    //LevelConfig 标签
    public string levelConfigLabel = "level";

    [Header("组件引用")]
    //PlayerEvents
    private PlayerEvents playerEvents;

    //升级配置
    private LevelConfig levelConfig;

    //配置是否已加载
    private bool isConfigLoaded = false;

    [Header("等级状态")]
    //当前等级
    private int currentLevel = 1;

    private void Awake()
    {
        playerEvents = GetComponent<PlayerEvents>();
    }

    private void Start()
    {
        LoadLevelConfig();
    }

    /// <summary>
    /// 按 label 加载第一个 LevelConfig 并初始化等级。
    /// </summary>
    private void LoadLevelConfig()
    {
        AddressablesMgr.Instance.LoadAssets<LevelConfig>(levelConfigLabel, (configs) =>
        {
            if (configs == null || configs.Count == 0)
            {
                isConfigLoaded = false;
                return;
            }

            levelConfig = configs[0];
            isConfigLoaded = true;
            InitializeLevel();
        });
    }

    /// <summary>
    /// 开局等级置 1。
    /// </summary>
    private void InitializeLevel()
    {
        currentLevel = 1;
    }

    /// <summary>
    /// 未满级则升一级并触发 PlayerEvents。
    /// </summary>
    public void LevelUp()
    {
        if (!isConfigLoaded || levelConfig == null) return;

        if (currentLevel >= levelConfig.maxLevel)
        {
            return;
        }

        currentLevel++;

        if (playerEvents != null)
        {
            playerEvents.TriggerLevelUp(currentLevel);
        }

    }

    /// <summary>
    /// 当前等级。
    /// </summary>
    public int GetCurrentLevel() => currentLevel;

    /// <summary>
    /// 最大等级（需已加载配置）。
    /// </summary>
    public int GetMaxLevel()
    {
        if (levelConfig == null)
        {
            return 0;
        }

        return levelConfig.maxLevel;
    }

    /// <summary>
    /// 当前 LevelConfig 引用。
    /// </summary>
    public LevelConfig GetLevelConfig() => levelConfig;

    /// <summary>
    /// 注入或替换 LevelConfig（测试等）。
    /// </summary>
    public void SetLevelConfig(LevelConfig config)
    {
        if (config != null)
        {
            levelConfig = config;
        }
    }

    /// <summary>
    /// 是否已达最大等级。
    /// </summary>
    public bool IsMaxLevel() => currentLevel >= GetMaxLevel();

    /// <summary>
    /// 直接设置等级（夹紧到配置范围）。
    /// </summary>
    public void SetLevel(int level)
    {
        if (!isConfigLoaded || levelConfig == null) return;

        level = Mathf.Clamp(level, 1, levelConfig.maxLevel);

        currentLevel = level;
    }
}
