using UnityEngine;

/// <summary>
/// 近战敌人专用配置：在 EnemyConfig 通用数值之上，仅增加 Animator 布尔参数名。
/// 基类 EnemyConfig.attackRange 对本类型为挥击/近战判定距离（与鱼叉类型共用「攻击距离」字段，鱼叉侧为投掷距离）。
/// </summary>
[CreateAssetMenu(fileName = "EnemyMeleeConfig", menuName = "Eureka/Enemy/EnemyMeleeConfig")]
public class EnemyMeleeConfig : EnemyConfig
{
    [Header("近战 · Animator")]
    [Tooltip("移动用布尔参数名")]
    public string movingParamName = "IsMoving";

    [Tooltip("攻击用布尔参数名")]
    public string attackingParamName = "IsAttacking";
}
