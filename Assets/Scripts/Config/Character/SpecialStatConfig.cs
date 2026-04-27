using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AddressableAssets;

/// <summary>
/// 角色特殊状态与属性配置：在GlobalStatConfig之上区分不同角色的属性与能力
/// </summary>
[CreateAssetMenu(fileName = "SpecialStatConfig", menuName = "Eureka/Character/SpecialStatConfig")]
public class SpecialStatConfig : ScriptableObject
{
    [Tooltip("角色名称")]
    public string characterName;

    [Tooltip("角色头像")]
    public Sprite icon;

    [TextArea(10, 10)]
    [Tooltip("简介或背景说明")]
    public string description;

    [Tooltip("角色动画控制器可寻址引用")]
    public AssetReference animatorController;

    [Tooltip("相对全局默认属性的额外修正")]
    public List<StatModifier> initialStats;

    [Tooltip("角色天生逻辑能力列表（直接应用 LogicEffect，不经过道具）")]
    public List<LogicEffectSO> innateLogicEffects = new();
}