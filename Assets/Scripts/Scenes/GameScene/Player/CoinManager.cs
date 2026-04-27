using UnityEngine;

/// <summary>
/// 金币管理器：存储与消费，并通过 PlayerEvents 广播变化。
/// </summary>
[RequireComponent(typeof(PlayerEvents))]
public class CoinManager : MonoBehaviour
{
    [Header("组件引用")]
    //PlayerEvents
    private PlayerEvents playerEvents;

    [Header("金币状态")]
    //当前金币数量
    private int currentCoins = 0;

    private void Awake()
    {
        playerEvents = GetComponent<PlayerEvents>();
    }

    private void Start()
    {
        InitializeCoins();
    }

    /// <summary>
    /// 每局开局将金币置零。
    /// </summary>
    private void InitializeCoins()
    {
        currentCoins = 0;
    }

    /// <summary>
    /// 增加金币（float 四舍五入为 int；amount 数量）。
    /// </summary>
    public void AddCoins(float amount)
    {
        if (amount <= 0f) return;

        int coinsToAdd = Mathf.RoundToInt(amount);
        if (coinsToAdd > 0)
        {
            currentCoins += coinsToAdd;
            if (playerEvents != null)
            {
                playerEvents.TriggerCoinsChanged(currentCoins);
            }
        }
    }

    /// <summary>
    /// 消费金币（amount 数量；返回是否扣款成功）。
    /// </summary>
    public bool SpendCoins(int amount)
    {
        if (amount <= 0) return false;

        if (currentCoins >= amount)
        {
            currentCoins -= amount;
            if (playerEvents != null)
            {
                playerEvents.TriggerCoinsChanged(currentCoins);
            }
            return true;
        }

        return false;
    }

    /// <summary>
    /// 当前金币数量。
    /// </summary>
    public int GetCurrentCoins() => currentCoins;

    /// <summary>
    /// 是否不少于指定数量（amount 所需金币）。
    /// </summary>
    public bool HasEnoughCoins(int amount) => currentCoins >= amount;

    /// <summary>
    /// 设置金币（测试或特殊流程）。
    /// </summary>
    public void SetCoins(int amount)
    {
        currentCoins = Mathf.Max(0, amount);

        if (playerEvents != null)
        {
            playerEvents.TriggerCoinsChanged(currentCoins);
        }
    }

    /// <summary>
    /// 清空金币（如局末）。
    /// </summary>
    public void ClearCoins()
    {
        currentCoins = 0;
        if (playerEvents != null)
        {
            playerEvents.TriggerCoinsChanged(currentCoins);
        }
    }

}
