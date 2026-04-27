/// <summary>
/// 游戏中角色属性的类型枚举。
/// </summary>
public enum StatType
{
    MaxHealth,          //最大生命值
    HealthRegen,        //生命再生
    HealthSteal,        //生命偷取(%)
    Armor,              //护盾值
    Dodge,              //闪避率(%)
    Damage,             //所有伤害(%)
    MeleeDamage,        //近战伤害
    RangedDamage,       //远程伤害
    ElementalDamage,    //元素伤害
    AttackSpeed,        //攻击速度(%)
    CritChance,         //暴击率(%)
    Range,              //攻击范围
    MoveSpeed,          //移动速度
    PickRange,          //拾取范围
}