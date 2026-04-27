using UnityEngine;

/// <summary>
/// 持有期间持续流血：
/// - 游戏处于 Playing 时，每秒触发一次受伤
/// - 单次固定扣血（默认 1 点）
/// </summary>
public class LogicEffect_BloodDrain : MonoBehaviour, IEffectComponent
{
    private float tickIntervalSeconds = 1f;
    private float damagePerTick = 1f;

    private float nextTickTime;
    private bool isInitialized;
    private PlayerEvents playerEvents;

    /// <summary>
    /// 由 LogicEffect_BloodDrainSO 注入参数。
    /// </summary>
    public void ApplyConfig(LogicEffect_BloodDrainSO config)
    {
        if (config == null)
        {
            Debug.LogError("[LogicEffect_BloodDrain] 配置为空。", this);
            enabled = false;
            return;
        }

        tickIntervalSeconds = Mathf.Max(0.01f, config.tickIntervalSeconds);
        damagePerTick = Mathf.Max(1f, config.damagePerTick);
        playerEvents = GetComponent<PlayerEvents>();
        if (playerEvents == null)
        {
            Debug.LogError("[LogicEffect_BloodDrain] 缺少 PlayerEvents 组件。", this);
            enabled = false;
            return;
        }

        nextTickTime = Time.time + tickIntervalSeconds;
        isInitialized = true;
    }

    /// <summary>
    /// 效果移除时清理运行时状态。
    /// </summary>
    public void CleanupEffect()
    {
        isInitialized = false;
    }

    /// <summary>
    /// 按固定间隔触发一次流血伤害。
    /// </summary>
    private void Update()
    {
        if (!enabled || !isInitialized)
        {
            return;
        }

        if (!IsGameplayRunning())
        {
            return;
        }

        if (Time.time < nextTickTime)
        {
            return;
        }

        nextTickTime += tickIntervalSeconds;
        playerEvents.TriggerDamaged(damagePerTick, gameObject);
    }

    /// <summary>
    /// 仅在 Playing 状态下执行流血逻辑。
    /// </summary>
    private bool IsGameplayRunning()
    {
        GameContext ctx = GameContext.Instance;
        if (ctx == null || !ctx.TryGetGameMgr(out GameMgr gameMgr) || gameMgr == null)
        {
            return true;
        }

        return gameMgr.GetGameState() == GameMgr.GameState.Playing;
    }
}
