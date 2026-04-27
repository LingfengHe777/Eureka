using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 道具配置：ItemConfig 作为效果的容器，业务逻辑放在各 EffectSO 中。
/// </summary>
[CreateAssetMenu(fileName = "ItemConfig", menuName = "Eureka/Item/ItemConfig")]
public class ItemConfig : ScriptableObject
{
    [Header("展示")]
    [Tooltip("道具名称")]
    public string itemName;

    [Tooltip("商店与背包用图标")]
    public Sprite itemIcon;

    [TextArea(3, 10)]
    [Tooltip("效果说明")]
    public string itemDescription;

    [Header("品阶")]
    [Range(1, 4)]
    [Tooltip("品阶 1～4，影响主题色与抽卡权重")]
    public int itemLevel = 1;

    /// <summary>
    /// 按 itemLevel 返回主题颜色。
    /// </summary>
    public Color GetThemeColor()
    {
        switch (itemLevel)
        {
            case 1: return Color.white;
            case 2: return Color.cyan;
            case 3: return new Color(0.5f, 0f, 0.5f, 1f);
            case 4: return Color.red;
            default: return Color.white;
        }
    }

    /// <summary>
    /// 由等级推导的主题颜色（用于 UI）。
    /// </summary>
    public Color themeColor => GetThemeColor();

    [Header("效果")]
    [Tooltip("挂载的效果资源列表")]
    public List<EffectSO> effects = new List<EffectSO>();

    [Header("商店")]
    [Tooltip("商店基础标价，最终价由模式策略计算")]
    public int shopPrice = 0;
}
