using UnityEngine;

/// <summary>
/// 敌人可缩放属性枚举。
/// </summary>
public enum EnemyStat
{
    Health,//血量
    Damage,//攻击值
    MoveSpeed,//移动速度
    AttackCooldown//攻击间隔
}

/// <summary>
/// 敌人配置：基础属性、Prefab 引用与掉落。视觉表现（Sprite、Animator 等）在 Prefab 中配置。
/// 通用数值（生命、伤害、移速、攻击间隔、资源键、掉落等）放于此资产。
/// attackRange 表示「可发起攻击」的判定距离，语义统一：近战为挥击范围，鱼叉等为可投掷/出手距离。
/// 若某类型还需要更大外圈（如兴奋追击），在对应 EnemyConfig 子类中增加字段（如 EnemyHarpoonFishConfig.excitedRange）。
/// Animator 参数名、投射物等仍放在 EnemyMeleeConfig、EnemyHarpoonFishConfig 等子类。
/// </summary>
[CreateAssetMenu(fileName = "EnemyConfig", menuName = "Eureka/Enemy/EnemyConfig")]
public class EnemyConfig : ScriptableObject
{
    [Header("展示")]
    [Tooltip("名称，界面与调试")]
    public string enemyName;

    [TextArea(2, 5)]
    [Tooltip("描述文案")]
    public string enemyDescription;

    [Tooltip("主题色，界面高亮等")]
    public Color themeColor;

    [Header("资源")]
    [Tooltip("敌人预制体可寻址键")]
    public string prefabKey;

    [Header("预警")]
    [Tooltip("脚下预警圈预制体可寻址键，仅样式")]
    public string spawnTelegraphPrefabKey;

    [Header("战斗数值")]
    [Tooltip("基础生命")]
    public float baseHealth = 100f;

    [Tooltip("基础攻击伤害")]
    public float baseDamage = 10f;

    [Tooltip("基础移动速度")]
    public float baseMoveSpeed = 3f;

    [Tooltip("攻击距离：近战为挥击半径，鱼叉等为投掷距离；兴奋外圈在子类配置 excitedRange")]
    public float attackRange = 1.5f;

    [Tooltip("两次攻击间隔，单位秒")]
    public float attackCooldown = 1.5f;

    [Header("生成")]
    [Min(0f)]
    [Tooltip("出生后静止等待，秒，期间不移动不攻击")]
    public float spawnWakeDelay = 0f;

    [Header("奖励")]
    [Tooltip("击杀获得经验")]
    public float expReward = 10f;

    [Range(0f, 1f)]
    [Tooltip("掉落金币概率，0～1")]
    public float coinDropChance = 0.5f;

    /// <summary>
    /// 单条金币掉落：预制体与权重。
    /// </summary>
    [System.Serializable]
    public class CoinDrop
    {
        [Tooltip("金币预制体")]
        public GameObject coinPrefab;

        [Range(0.1f, 10f)]
        [Tooltip("多条掉落之间的相对权重")]
        public float dropWeight = 1f;
    }

    [Tooltip("按权重随机的一种或多种金币预制体")]
    public CoinDrop[] coinDrops = new CoinDrop[0];

    private void OnValidate()
    {
        attackRange = Mathf.Max(0f, attackRange);
    }
}
