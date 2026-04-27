using UnityEngine;

/// <summary>
/// 商店“购买规则”策略。
///
/// 约束：
/// - 购买限制/合成规则/折扣等动态逻辑必须封装在策略 SO 中。
/// - StorePanel 只允许组装 ShopPurchaseContext 并调用 Evaluate，不允许写购买规则分支和公式。
/// </summary>
public abstract class ShopPurchaseRuleStrategy : ValueStrategy
{
    /// <summary>
    /// 评估是否可购买及应付价格、失败原因。
    /// </summary>
    public abstract ShopPurchaseDecision Evaluate(in ShopPurchaseContext ctx);

    /// <summary>
    /// 便捷方法：直接评估购买决策（用于 StorePanel）。
    /// </summary>
    public ShopPurchaseDecision EvaluatePurchase(in ShopPurchaseContext ctx)
    {
        return Evaluate(ctx);
    }
}
