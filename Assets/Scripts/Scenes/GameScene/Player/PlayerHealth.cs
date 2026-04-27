using UnityEngine;

/// <summary>
/// 玩家生命：受伤、闪避、护甲、回复、死亡与事件广播。
/// </summary>
[RequireComponent(typeof(StatHandler), typeof(PlayerEvents))]
public class PlayerHealth : MonoBehaviour
{
    [Header("组件引用")]
    //StatHandler
    private StatHandler statHandler;
    //PlayerEvents
    private PlayerEvents playerEvents;

    [Header("生命值状态")]
    //当前生命
    private int currentHealth;

    //最大生命（来自 StatHandler）
    private int maxHealth;

    [Header("生命回复")]
    //上次回复时刻
    private float lastRegenTime = 0f;

    //回复间隔秒
    private const float regenInterval = 1f;
    //无敌计数（>0 视为免疫伤害）
    private int invulnerableCounter;

    private void Awake()
    {
        statHandler = GetComponent<StatHandler>();
        playerEvents = GetComponent<PlayerEvents>();
    }

    private void Start()
    {
        InitializeHealth();

        if (playerEvents != null)
        {
            playerEvents.OnDamaged += HandleDamage;
        }
    }

    private void Update()
    {
        HandleHealthRegen();
        UpdateMaxHealth();
    }

    /// <summary>
    /// 从 StatHandler 读取最大生命并广播初始血量。
    /// </summary>
    private void InitializeHealth()
    {
        if (statHandler == null)
        {
            return;
        }

        maxHealth = Mathf.Max(1, Mathf.RoundToInt(statHandler.GetStat(StatType.MaxHealth)));
        currentHealth = maxHealth;

        if (playerEvents != null)
        {
            playerEvents.TriggerHealthChanged(currentHealth, maxHealth);
        }
    }

    /// <summary>
    /// MaxHealth 变化时按比例夹紧当前生命并广播。
    /// </summary>
    private void UpdateMaxHealth()
    {
        if (statHandler == null) return;

        int newMaxHealth = Mathf.Max(1, Mathf.RoundToInt(statHandler.GetStat(StatType.MaxHealth)));
        if (newMaxHealth != maxHealth)
        {
            maxHealth = newMaxHealth;
            if (currentHealth > maxHealth)
                currentHealth = maxHealth;

            if (playerEvents != null)
            {
                playerEvents.TriggerHealthChanged(currentHealth, maxHealth);
            }
        }
    }

    /// <summary>
    /// 闪避、护甲抵扣后扣血，必要时死亡。
    /// </summary>
    private void HandleDamage(float damage, GameObject source)
    {
        if (currentHealth <= 0) return;
        if (IsInvulnerable()) return;

        float dodgeChance = statHandler != null ? statHandler.GetStat(StatType.Dodge) : 0f;
        if (Random.Range(0f, 1f) < dodgeChance)
        {
            playerEvents?.TriggerDodgeSuccess();
            return;
        }

        float armor = statHandler != null ? statHandler.GetStat(StatType.Armor) : 0f;
        int actualDamage = Mathf.Max(0, Mathf.RoundToInt(damage * GetArmorDamageTakenMultiplier(armor)));

        currentHealth -= actualDamage;
        currentHealth = Mathf.Max(0, currentHealth);

        if (playerEvents != null)
        {
            if (actualDamage > 0)
            {
                playerEvents.TriggerDamageTaken(actualDamage, source);
            }

            playerEvents.TriggerHealthChanged(currentHealth, maxHealth);
        }

        if (currentHealth <= 0)
        {
            Die();
        }
    }

    /// <summary>
    /// Brotato 风格护甲模型（Armor 为点数）：
    /// - armor >= 0: 受伤倍率 = 1 / (1 + armor / 15)
    /// - armor < 0 : 受伤倍率 = (15 - 2*armor) / (15 - armor)
    /// </summary>
    private static float GetArmorDamageTakenMultiplier(float armor)
    {
        if (armor >= 0f)
        {
            return 1f / (1f + armor / 15f);
        }

        float denominator = 15f - armor;
        if (denominator <= 0.0001f)
        {
            return 1f;
        }

        return (15f - 2f * armor) / denominator;
    }

    /// <summary>
    /// 按 HealthRegen 与间隔回复生命。
    /// </summary>
    private void HandleHealthRegen()
    {
        if (statHandler == null || currentHealth >= maxHealth) return;

        float healthRegen = statHandler.GetStat(StatType.HealthRegen);
        if (healthRegen <= 0f) return;

        if (Time.time - lastRegenTime >= regenInterval)
        {
            int regenAmount = Mathf.Max(0, Mathf.RoundToInt(healthRegen));
            if (regenAmount <= 0)
            {
                return;
            }

            currentHealth += regenAmount;
            currentHealth = Mathf.Min(currentHealth, maxHealth);
            lastRegenTime = Time.time;

            if (playerEvents != null)
            {
                playerEvents.TriggerHealthChanged(currentHealth, maxHealth);

                if (currentHealth >= maxHealth)
                {
                    playerEvents.TriggerHealthFull();
                }
            }
        }
    }

    /// <summary>
    /// 广播死亡事件。
    /// </summary>
    private void Die()
    {
        if (playerEvents != null)
        {
            playerEvents.TriggerDeath();
        }
    }

    /// <summary>
    /// 治疗并夹紧到最大生命。
    /// </summary>
    public void Heal(float healAmount)
    {
        if (currentHealth <= 0) return;

        int healValue = Mathf.Max(0, Mathf.RoundToInt(healAmount));
        if (healValue <= 0)
        {
            return;
        }

        currentHealth += healValue;
        currentHealth = Mathf.Min(currentHealth, maxHealth);

        if (playerEvents != null)
        {
            playerEvents.TriggerHealthChanged(currentHealth, maxHealth);

            if (currentHealth >= maxHealth)
            {
                playerEvents.TriggerHealthFull();
            }
        }
    }

    /// <summary>
    /// 立即回满到当前最大生命（如波次切换）。
    /// </summary>
    public void FillToMaxHealth()
    {
        if (currentHealth <= 0) return;
        UpdateMaxHealth();
        currentHealth = maxHealth;

        if (playerEvents != null)
        {
            playerEvents.TriggerHealthChanged(currentHealth, maxHealth);
            playerEvents.TriggerHealthFull();
        }
    }

    /// <summary>
    /// 当前生命。
    /// </summary>
    public int GetCurrentHealth() => currentHealth;

    /// <summary>
    /// 最大生命。
    /// </summary>
    public int GetMaxHealth() => maxHealth;

    /// <summary>
    /// 生命比例 0–1。
    /// </summary>
    public float GetHealthPercentage() => maxHealth > 0 ? (float)currentHealth / maxHealth : 0f;

    /// <summary>
    /// 是否已死亡。
    /// </summary>
    public bool IsDead() => currentHealth <= 0;

    /// <summary>
    /// 增加一层无敌计数。
    /// </summary>
    public void AddInvulnerability()
    {
        invulnerableCounter++;
    }

    /// <summary>
    /// 移除一层无敌计数。
    /// </summary>
    public void RemoveInvulnerability()
    {
        invulnerableCounter = Mathf.Max(0, invulnerableCounter - 1);
    }

    /// <summary>
    /// 当前是否处于无敌状态。
    /// </summary>
    public bool IsInvulnerable()
    {
        return invulnerableCounter > 0;
    }

    private void OnDestroy()
    {
        if (playerEvents != null)
        {
            playerEvents.OnDamaged -= HandleDamage;
        }
    }
}
