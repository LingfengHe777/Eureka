using UnityEngine;

/// <summary>
/// 鱼叉鱼专用配置：在 EnemyConfig 之上增加兴奋圈与鱼叉专属字段。
/// 投掷距离使用基类 EnemyConfig.attackRange（与近战共用「攻击距离」语义）；更大一圈的追击/兴奋距离为 excitedRange。
/// </summary>
[CreateAssetMenu(fileName = "EnemyHarpoonFishConfig", menuName = "Eureka/Enemy/EnemyHarpoonFishConfig")]
public class EnemyHarpoonFishConfig : EnemyConfig
{
    [Header("鱼叉鱼 · 距离")]
    [Tooltip("玩家进入此距离后开始追击；须不小于基类攻击距离")]
    public float excitedRange = 8f;

    [Header("鱼叉鱼 · 移动")]
    [Tooltip("兴奋区内、攻击距离外追击用的基础速度，再乘难度移速倍率")]
    [Min(0.0001f)]
    public float excitedRunSpeed = 5f;

    [Tooltip("闲逛时随机换向的间隔，秒")]
    [Min(0.0001f)]
    public float wanderDirectionChangeInterval = 2f;

    [Header("鱼叉鱼 · 投掷")]
    [Tooltip("鱼叉预制体，需挂 HarpoonFishProjectile、刚体与触发碰撞体")]
    public GameObject harpoonPrefab;

    [Tooltip("鱼叉飞行速度")]
    public float harpoonProjectileSpeed = 12f;

    [Tooltip("自生成点起最大飞行距离，超出回收")]
    public float harpoonMaxTravelDistance = 24f;

    [Header("鱼叉鱼 · Animator")]
    [Tooltip("奔跑态布尔参数名")]
    public string harpoonRunningParamName = "IsRunning";

    [Tooltip("投掷态布尔参数名")]
    public string harpoonThrowingParamName = "IsThrowing";

    private void OnValidate()
    {
        if (excitedRange < 0f) excitedRange = 0f;
        attackRange = Mathf.Max(0f, attackRange);
        if (excitedRange > 0f && attackRange > excitedRange)
        {
            attackRange = excitedRange;
        }
    }
}
