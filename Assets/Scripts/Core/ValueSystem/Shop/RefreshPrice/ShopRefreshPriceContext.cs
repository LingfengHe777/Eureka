using UnityEngine;

/// <summary>
/// 商店刷新价格计算上下文（强类型、无分配）。
/// 业务层负责组装上下文，具体公式由 ShopRefreshPriceStrategy 实现。
/// </summary>
public readonly struct ShopRefreshPriceContext
{
    //波次（至少为1）
    public readonly int wave;
    //本局商店内已刷新次数（非负）
    public readonly int refreshCountThisStore;
    //模式配置
    public readonly ModeConfig modeConfig;

    /// <summary>
    /// 构造刷新价格上下文。
    /// </summary>
    public ShopRefreshPriceContext(int wave, int refreshCountThisStore, ModeConfig modeConfig)
    {
        this.wave = Mathf.Max(1, wave);
        this.refreshCountThisStore = Mathf.Max(0, refreshCountThisStore);
        this.modeConfig = modeConfig;
    }
}
