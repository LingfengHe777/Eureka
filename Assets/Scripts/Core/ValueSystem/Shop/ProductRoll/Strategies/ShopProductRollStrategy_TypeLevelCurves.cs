using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 商店抽卡策略（默认实现）：
/// - 不区分道具/武器类型权重，统一视为“商品”
/// - 通过“波次→等级权重曲线”控制不同等级商品出现概率
///
/// 注意：所有数学/权重计算都在策略 SO 内部，业务脚本禁止出现权重公式。
/// </summary>
[CreateAssetMenu(fileName = "ShopProductRollStrategy_TypeLevelCurves", menuName = "Eureka/ValueStrategy/Shop/Roll/TypeLevelCurves")]
public class ShopProductRollStrategy_TypeLevelCurves : ShopProductRollStrategy
{
    [Header("品阶权重曲线")]
    [Tooltip("横轴波次，纵轴 1 级商品相对权重，须≥0")]
    public AnimationCurve level1WeightByWave = AnimationCurve.Linear(1f, 1f, 30f, 0.35f);

    [Tooltip("横轴波次，纵轴 2 级商品相对权重，须≥0")]
    public AnimationCurve level2WeightByWave = AnimationCurve.Linear(1f, 0.22f, 30f, 0.9f);

    [Tooltip("横轴波次，纵轴 3 级商品相对权重，须≥0")]
    public AnimationCurve level3WeightByWave = AnimationCurve.Linear(1f, 0.05f, 30f, 0.75f);

    [Tooltip("横轴波次，纵轴 4 级商品相对权重，须≥0")]
    public AnimationCurve level4WeightByWave = AnimationCurve.Linear(1f, 0f, 30f, 0.6f);

    /// <summary>
    /// 将浮点权重钳到非负。
    /// </summary>
    private static float ClampNonNegative(float v) => v < 0f ? 0f : v;

    /// <summary>
    /// 按等级权重从候选池加权随机抽取一件商品。
    /// </summary>
    public override ScriptableObject Evaluate(in ShopProductRollContext ctx)
    {
        if (ctx.itemPool == null && ctx.weaponPool == null) return null;

        int wave = ctx.wave < 1 ? 1 : ctx.wave;
        HashSet<ScriptableObject> excluded = ctx.excludedProducts;

        float totalWeight = 0f;

        if (ctx.itemPool != null)
        {
            for (int i = 0; i < ctx.itemPool.Count; i++)
            {
                ItemConfig item = ctx.itemPool[i];
                if (item == null) continue;
                if (excluded != null && excluded.Contains(item)) continue;

                totalWeight += GetLevelWeightByWave(item.itemLevel, wave);
            }
        }

        if (ctx.weaponPool != null)
        {
            for (int i = 0; i < ctx.weaponPool.Count; i++)
            {
                WeaponConfig weapon = ctx.weaponPool[i];
                if (weapon == null) continue;
                if (excluded != null && excluded.Contains(weapon)) continue;

                totalWeight += GetLevelWeightByWave(weapon.GetCurrentLevel(), wave);
            }
        }

        if (totalWeight <= 0f) return null;

        float r = Random.Range(0f, totalWeight);

        if (ctx.itemPool != null)
        {
            for (int i = 0; i < ctx.itemPool.Count; i++)
            {
                ItemConfig item = ctx.itemPool[i];
                if (item == null) continue;
                if (excluded != null && excluded.Contains(item)) continue;

                float w = GetLevelWeightByWave(item.itemLevel, wave);
                r -= w;
                if (r <= 0f) return item;
            }
        }

        if (ctx.weaponPool != null)
        {
            for (int i = 0; i < ctx.weaponPool.Count; i++)
            {
                WeaponConfig weapon = ctx.weaponPool[i];
                if (weapon == null) continue;
                if (excluded != null && excluded.Contains(weapon)) continue;

                float w = GetLevelWeightByWave(weapon.GetCurrentLevel(), wave);
                r -= w;
                if (r <= 0f) return weapon;
            }
        }

        return null;
    }

    /// <summary>
    /// 按等级与波次从曲线取权重并钳到非负。
    /// </summary>
    private float GetLevelWeightByWave(int level, int wave)
    {
        int clampedLevel = Mathf.Clamp(level, 1, 4);
        float weight;
        switch (clampedLevel)
        {
            case 1:
                weight = level1WeightByWave != null ? level1WeightByWave.Evaluate(wave) : 1f;
                break;
            case 2:
                weight = level2WeightByWave != null ? level2WeightByWave.Evaluate(wave) : 1f;
                break;
            case 3:
                weight = level3WeightByWave != null ? level3WeightByWave.Evaluate(wave) : 1f;
                break;
            default:
                weight = level4WeightByWave != null ? level4WeightByWave.Evaluate(wave) : 1f;
                break;
        }

        return ClampNonNegative(weight);
    }
}
