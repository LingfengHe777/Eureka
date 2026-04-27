using UnityEngine;

/// <summary>
/// 模式配置：难度展示、敌人与经济参数、无尽增长及关联的 WaveConfig；仅提供数据不参与计算。
/// </summary>
[CreateAssetMenu(fileName = "ModeConfig", menuName = "Eureka/Mode/ModeConfig")]
public class ModeConfig : ScriptableObject
{
    [Header("展示")]
    [Tooltip("模式名称，用于选关等界面")]
    public string modeName;

    [Tooltip("模式图标")]
    public Sprite modeIcon;

    [Tooltip("模式说明文案")]
    public string modeDescription;

    [Header("难度")]
    [Range(0, 5)]
    [Tooltip("难度 0～5，影响主题色")]
    public int difficultyLevel = 0;

    [Header("波次")]
    [Tooltip("本模式使用的波次与刷怪结构")]
    public WaveConfig waveConfig;

    [Header("预警时长")]
    [Min(0f)]
    [Tooltip("敌人生成预警时长，秒；样式见敌人配置")]
    public float spawnTelegraphDuration = 0.5f;

    [Header("敌人倍率")]
    [Tooltip("相对敌人基础生命的倍率")]
    public float enemyHealthMultiplier = 1.0f;

    [Tooltip("相对敌人基础攻击的倍率")]
    public float enemyDamageMultiplier = 1.0f;

    [Tooltip("相对敌人基础移速的倍率")]
    public float enemySpeedMultiplier = 1.0f;

    [Tooltip("攻击间隔倍率，小于 1 更密更难，大于 1 更疏更易")]
    public float enemyAttackCooldownMultiplier = 1.0f;

    [Header("数值策略 · 敌人")]
    [Tooltip("敌人属性随波次与无尽缩放，公式仅在此资源")]
    public EnemyScalingStrategy enemyScalingStrategy;

    [Tooltip("每波生成数量倍率，公式仅在此资源")]
    public SpawnCountMultiplierStrategy spawnCountMultiplierStrategy;

    [Tooltip("每波刷怪数量在倍率之后的整数偏移")]
    public int spawnCountOffset = 0;

    [Header("经济与掉落")]
    [Range(0f, 1f)]
    [Tooltip("材料掉落系数，0～1")]
    public float materialDropRate = 1.0f;

    [Tooltip("商店标价的基础倍率")]
    public float shopPriceMultiplier = 1.0f;

    [Header("数值策略 · 商店")]
    [Tooltip("商品最终价格策略，武器与道具共用")]
    public ShopProductPriceStrategy shopProductPriceStrategy;

    [Tooltip("商店刷新价计算策略")]
    public ShopRefreshPriceStrategy shopRefreshPriceStrategy;

    [Tooltip("商店随机商品的权重规则")]
    public ShopProductRollStrategy shopProductRollStrategy;

    [Tooltip("购买与合成等限制规则")]
    public ShopPurchaseRuleStrategy shopPurchaseRuleStrategy;

    [Header("商店 · 价格随波次")]
    [Tooltip("每过一关对基础价的线性加成系数")]
    [Range(0f, 2f)]
    public float shopPriceWaveLinearGrowth = 0.08f;

    [Tooltip("每过一关对基础价的二次加成，后期涨得更快")]
    [Range(0f, 1f)]
    public float shopPriceWaveQuadraticGrowth = 0.01f;

    [Header("玩家 · 治疗")]
    [Range(0f, 1f)]
    [Tooltip("治疗效果乘数，1 不削减，0 无效")]
    public float healingReduction = 1.0f;

    [Header("商店 · 刷新价格")]
    [Tooltip("第 1 波且未刷新时的基础刷新价")]
    public int baseRefreshPrice = 100;

    [Tooltip("每次刷新相对上次价格的倍率增量")]
    [Range(0f, 5f)]
    public float refreshBaseGrowth = 0.25f;

    [Tooltip("随波次增加而叠加的每次刷新倍率增量")]
    [Range(0f, 1f)]
    public float refreshGrowthPerWave = 0.02f;

    [Tooltip("刷新价底数随波次的线性系数")]
    [Range(0f, 2f)]
    public float basePriceWaveLinearGrowth = 0.08f;

    [Tooltip("刷新价底数随波次的二次系数")]
    [Range(0f, 1f)]
    public float basePriceWaveQuadraticGrowth = 0.01f;

    [Tooltip("刷新价下限，防异常")]
    public int minRefreshPrice = 1;

    [Tooltip("刷新价上限，防异常")]
    public int maxRefreshPrice = 999999;

    [Header("无尽 · 增长")]
    [Min(1f)]
    [Tooltip("进入无尽后的难度总倍率起点")]
    public float endlessBaseDifficultyMultiplier = 1.5f;

    [Min(1.01f)]
    [Tooltip("无尽每轮整体难度指数")]
    public float endlessDifficultyGrowthRate = 1.15f;

    [Min(1.01f)]
    [Tooltip("无尽每轮生命倍率乘数")]
    public float endlessHealthMultiplierGrowth = 1.20f;

    [Min(1.01f)]
    [Tooltip("无尽每轮攻击倍率乘数")]
    public float endlessDamageMultiplierGrowth = 1.15f;

    [Min(1.01f)]
    [Tooltip("无尽每轮移速倍率乘数")]
    public float endlessSpeedMultiplierGrowth = 1.10f;

    [Range(0.8f, 1.0f)]
    [Tooltip("无尽每轮攻击间隔乘数，小于 1 攻击更快")]
    public float endlessAttackCooldownMultiplierGrowth = 0.95f;

    [Min(1.01f)]
    [Tooltip("无尽每轮刷怪数量倍率乘数")]
    public float endlessSpawnCountMultiplierGrowth = 1.25f;

    /// <summary>
    /// 按 difficultyLevel 返回难度主题色。
    /// </summary>
    public Color GetThemeColor()
    {
        switch (difficultyLevel)
        {
            case 0: return Color.clear;
            case 1: return Color.white;
            case 2: return Color.cyan;
            case 3: return new Color(0.5f, 0f, 0.5f, 1f);
            case 4: return Color.red;
            case 5: return Color.black;
            default: return Color.clear;
        }
    }

    /// <summary>
    /// 由难度等级推导的主题颜色（用于 UI）。
    /// </summary>
    public Color themeColor => GetThemeColor();
}
