using UnityEngine;

/// <summary>
/// 商店“抽卡/出货”策略。
///
/// 约束：
/// - 抽取概率/权重/曲线等动态逻辑必须封装在策略 SO 中。
/// - StorePanel 只允许组装 ShopProductRollContext 并调用 Evaluate，不允许写具体权重公式。
/// </summary>
public abstract class ShopProductRollStrategy : ValueStrategy
{
    /// <summary>
    /// 从候选池中抽取 1 个商品（ItemConfig 或 WeaponConfig），若无可用返回 null。
    /// </summary>
    public abstract ScriptableObject Evaluate(in ShopProductRollContext ctx);

    /// <summary>
    /// 便捷方法：直接抽取商品（用于 StorePanel）。
    /// </summary>
    public ScriptableObject RollProduct(in ShopProductRollContext ctx)
    {
        return Evaluate(ctx);
    }
}
