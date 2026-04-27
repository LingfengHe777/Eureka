using UnityEngine;

/// <summary>
/// 商店购买规则策略（默认实现）：
/// - 必须有 CoinManager
/// - 金币足够
/// - 道具：必须有 InventoryManager
/// - 武器：必须有 WeaponManager，且 CanPurchaseWeapon 为 true（未满或可合成）
/// </summary>
[CreateAssetMenu(fileName = "ShopPurchaseRuleStrategy_Default", menuName = "Eureka/ValueStrategy/Shop/PurchaseRule/Default")]
public class ShopPurchaseRuleStrategy_Default : ShopPurchaseRuleStrategy
{
    /// <summary>
    /// 按默认规则判断是否可购买。
    /// </summary>
    public override ShopPurchaseDecision Evaluate(in ShopPurchaseContext ctx)
    {
        if (ctx.product == null) return ShopPurchaseDecision.Deny(ctx.productPrice, ShopPurchaseFailReason.ProductNull);
        if (ctx.coinManager == null) return ShopPurchaseDecision.Deny(ctx.productPrice, ShopPurchaseFailReason.CoinManagerMissing);

        if (!ctx.coinManager.HasEnoughCoins(ctx.productPrice))
        {
            return ShopPurchaseDecision.Deny(ctx.productPrice, ShopPurchaseFailReason.NotEnoughCoins);
        }

        if (ctx.product is ItemConfig)
        {
            if (ctx.inventoryManager == null)
            {
                return ShopPurchaseDecision.Deny(ctx.productPrice, ShopPurchaseFailReason.InventoryManagerMissing);
            }
            return ShopPurchaseDecision.Allow(ctx.productPrice);
        }

        if (ctx.product is WeaponConfig weapon)
        {
            if (ctx.weaponManager == null)
            {
                return ShopPurchaseDecision.Deny(ctx.productPrice, ShopPurchaseFailReason.WeaponManagerMissing);
            }

            if (!ctx.weaponManager.CanPurchaseWeapon(weapon))
            {
                return ShopPurchaseDecision.Deny(ctx.productPrice, ShopPurchaseFailReason.WeaponFullCannotMerge);
            }

            return ShopPurchaseDecision.Allow(ctx.productPrice);
        }

        return ShopPurchaseDecision.Deny(ctx.productPrice, ShopPurchaseFailReason.UnknownProductType);
    }
}
