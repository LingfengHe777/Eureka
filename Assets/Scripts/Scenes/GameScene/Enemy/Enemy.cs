using UnityEngine;
using System.Collections;

/// <summary>
/// 敌人抽象基类
///
/// 职责：
/// 1.生命值管理（TakeDamage、Die）
/// 2.ModeConfig难度倍率应用
/// 3.掉落（金币、经验值）
/// 4.玩家查找
/// 5.组件引用缓存
///
/// 不负责：
/// 具体的移动方式
/// 具体的攻击方式
/// 具体的状态机结构
/// 
/// 共享辅助：
/// ComputeSeparationForce供需要与其他敌人保持间距的子类在合成速度时使用。
///
/// 架构原则：
/// 结构逻辑由代码继承控制，不由配置参数决定
/// EnemyConfig 提供全体敌人共用的数值与资源键（含单层攻击距离 attackRange 等；多层距离由各 EnemyConfig 子类定义）；再按需使用子类资产
/// 不允许通过枚举/布尔分支行为结构
/// 不同敌人类型 = 不同子类 + 不同 Prefab（+ 可选的专用 EnemyConfig 子类）
/// </summary>
[RequireComponent(typeof(Rigidbody2D))]
public abstract class Enemy : MonoBehaviour
{
    //场景上下文
    private GameContext gameContext;
    [Header("配置引用")]
    //敌人配置（实例化后由SpawnManager设置）
    public EnemyConfig config;

    //运行时属性（受难度倍率影响）
    protected int maxHealth;
    protected int currentHealth;
    protected float currentMoveSpeed;
    protected float currentDamage;
    protected float currentAttackCooldown;

    //组件引用
    protected Rigidbody2D rb;
    protected SpriteRenderer spriteRenderer;
    protected Animator animator;
    protected Collider2D enemyCollider;

    //共享状态
    protected GameObject player;
    protected bool isDead = false;
    //是否已完成生成初始化
    private bool hasSpawnInitialized = false;
    //唤醒就绪时间
    private float wakeReadyTime = 0f;
    //查找玩家协程（避免重复启动）
    private Coroutine delayedFindPlayerCoroutine;

    protected virtual void Awake()
    {
        gameContext = GameContext.Instance;
        rb = GetComponent<Rigidbody2D>();
        spriteRenderer = GetComponent<SpriteRenderer>();
        animator = GetComponent<Animator>();
        enemyCollider = GetComponent<Collider2D>();
    }

    protected virtual void Start()
    {
        if (!hasSpawnInitialized)
        {
            InitializeForSpawn(config);
        }
    }

    /// <summary>
    /// 子类自定义初始化（在基类Start末尾调用，确保属性已就绪）。
    /// </summary>
    protected virtual void OnInit() { }

    /// <summary>
    /// 供生成器在每次生成（含池复用）时调用，重置本轮运行时状态。
    /// </summary>
    public void InitializeForSpawn(EnemyConfig spawnConfig)
    {
        gameContext = GameContext.Instance;
        player = null;
        if (delayedFindPlayerCoroutine != null)
        {
            StopCoroutine(delayedFindPlayerCoroutine);
            delayedFindPlayerCoroutine = null;
        }

        config = spawnConfig;
        if (config == null)
        {
            enabled = false;
            return;
        }

        enabled = true;
        isDead = false;
        if (rb != null)
        {
            rb.velocity = Vector2.zero;
            rb.angularVelocity = 0f;
        }

        FindPlayer();

        maxHealth = Mathf.Max(1, Mathf.RoundToInt(config.baseHealth));
        currentMoveSpeed = config.baseMoveSpeed;
        currentDamage = config.baseDamage;
        currentAttackCooldown = config.attackCooldown;

        ApplyDifficultyMultipliers();

        currentHealth = maxHealth;
        wakeReadyTime = Time.time + Mathf.Max(0f, config.spawnWakeDelay);

        OnInit();
        hasSpawnInitialized = true;
    }

    /// <summary>
    /// 敌人是否完成生成唤醒冷却。
    /// </summary>
    protected bool IsWakeReady()
    {
        return Time.time >= wakeReadyTime;
    }

    /// <summary>
    /// 查找玩家。
    /// </summary>
    protected void FindPlayer()
    {
        EnsureGameContext();
        if (TryResolvePlayerFromContext())
        {
            return;
        }

        if (delayedFindPlayerCoroutine == null)
        {
            delayedFindPlayerCoroutine = StartCoroutine(DelayedFindPlayer());
        }
    }

    /// <summary>
    /// 延迟查找玩家。
    /// </summary>
    private IEnumerator DelayedFindPlayer()
    {
        int attempts = 0;
        while (player == null && attempts < 50)
        {
            yield return new WaitForSeconds(0.1f);
            EnsureGameContext();
            TryResolvePlayerFromContext();

            attempts++;
        }

        delayedFindPlayerCoroutine = null;
    }

    protected virtual void OnDisable()
    {
        if (delayedFindPlayerCoroutine != null)
        {
            StopCoroutine(delayedFindPlayerCoroutine);
            delayedFindPlayerCoroutine = null;
        }
    }

    /// <summary>
    /// 计算与玩家的距离（优先用碰撞体边界）。
    /// </summary>
    protected float GetDistanceToPlayer()
    {
        if (player == null) return float.MaxValue;

        if (enemyCollider == null)
        {
            return Vector2.Distance(rb.position, (Vector2)player.transform.position);
        }

        if (!player.TryGetComponent<Collider2D>(out var playerCollider))
        {
            return Vector2.Distance(rb.position, (Vector2)player.transform.position);
        }

        Vector2 enemyClosest = enemyCollider.ClosestPoint(player.transform.position);
        Vector2 playerClosest = playerCollider.ClosestPoint(rb.position);

        return Vector2.Distance(enemyClosest, playerClosest);
    }

    private static readonly float SeparationRadius = 0.5f;
    private static readonly float SeparationStrength = 1.2f;
    private const int SeparationInitialBufferSize = 64;
    private const int SeparationMaxBufferSize = 512;
    private Collider2D[] separationOverlapBuffer = new Collider2D[SeparationInitialBufferSize];

    /// <summary>
    /// 对周围半径内其他 Enemy 做排斥向量（与近战/鱼叉等子类原逻辑一致），用于与追踪力等合成。
    /// </summary>
    protected Vector2 ComputeSeparationForce(Vector2 selfPos)
    {
        Vector2 separationForce = Vector2.zero;
        int neighborCount = 0;
        int count = OverlapNeighbors(selfPos);
        for (int i = 0; i < count; i++)
        {
            TryAccumulateSeparationFromNeighbor(separationOverlapBuffer[i], selfPos, ref separationForce, ref neighborCount);
        }

        if (neighborCount > 0)
        {
            separationForce = (separationForce / neighborCount) * SeparationStrength;
        }

        return separationForce;
    }

    private int OverlapNeighbors(Vector2 selfPos)
    {
        int count = Physics2D.OverlapCircleNonAlloc(selfPos, SeparationRadius, separationOverlapBuffer);
        while (count >= separationOverlapBuffer.Length && separationOverlapBuffer.Length < SeparationMaxBufferSize)
        {
            int newSize = Mathf.Min(separationOverlapBuffer.Length * 2, SeparationMaxBufferSize);
            separationOverlapBuffer = new Collider2D[newSize];
            count = Physics2D.OverlapCircleNonAlloc(selfPos, SeparationRadius, separationOverlapBuffer);
        }

        return Mathf.Min(count, separationOverlapBuffer.Length);
    }

    private void EnsureGameContext()
    {
        if (gameContext == null)
        {
            gameContext = GameContext.Instance;
        }
    }

    private bool TryResolvePlayerFromContext()
    {
        if (player != null || gameContext == null)
        {
            return player != null;
        }

        if (!gameContext.TryGetPlayer(out GameObject contextPlayer))
        {
            return false;
        }

        player = contextPlayer;
        return true;
    }

    private void TryAccumulateSeparationFromNeighbor(
        Collider2D neighbor,
        Vector2 selfPos,
        ref Vector2 separationForce,
        ref int neighborCount)
    {
        if (neighbor == null || neighbor.gameObject == gameObject || !neighbor.TryGetComponent<Enemy>(out _))
        {
            return;
        }

        Vector2 away = selfPos - (Vector2)neighbor.transform.position;
        float dist = away.magnitude;
        if (dist < 0.01f)
        {
            away = Random.insideUnitCircle.normalized;
            dist = 0.01f;
        }

        separationForce += away.normalized * (SeparationRadius / dist);
        neighborCount++;
    }

    /// <summary>
    /// 与地图矩形边界内缩，避免贴边抖动；边界须与 SpawnManager.mapSize / 场景墙体对齐。
    /// </summary>
    private const float MapBoundsInset = 0.25f;

    /// <summary>
    /// 敌人为 Trigger 时不会与墙体发生物理阻挡，需用逻辑边界约束；矩形与 SpawnManager 地图配置一致。
    /// </summary>
    protected bool TryGetMapBounds(out Vector2 min, out Vector2 max)
    {
        min = max = Vector2.zero;
        if (gameContext == null)
        {
            gameContext = GameContext.Instance;
        }

        if (gameContext == null || !gameContext.TryGetSpawnManager(out SpawnManager sm) || sm == null)
        {
            return false;
        }

        if (sm.mapSize.x <= 0f || sm.mapSize.y <= 0f)
        {
            return false;
        }

        Vector2 half = sm.mapSize * 0.5f - MapBoundsInset * Vector2.one;
        if (half.x <= 0f || half.y <= 0f)
        {
            return false;
        }

        min = sm.mapCenter - half;
        max = sm.mapCenter + half;
        return true;
    }

    /// <summary>
    /// 在移动逻辑所在 FixedUpdate 的 finally 中调用：钳制位置并消除指向地图外的速度分量。
    /// </summary>
    protected void ClampPositionToMapBounds()
    {
        if (rb == null || isDead)
        {
            return;
        }

        if (!TryGetMapBounds(out Vector2 min, out Vector2 max))
        {
            return;
        }

        Vector2 p = rb.position;
        bool clamped = false;
        if (p.x < min.x)
        {
            p.x = min.x;
            clamped = true;
        }
        else if (p.x > max.x)
        {
            p.x = max.x;
            clamped = true;
        }

        if (p.y < min.y)
        {
            p.y = min.y;
            clamped = true;
        }
        else if (p.y > max.y)
        {
            p.y = max.y;
            clamped = true;
        }

        if (clamped)
        {
            rb.MovePosition(p);
        }

        Vector2 v = rb.velocity;
        if (p.x <= min.x && v.x < 0f)
        {
            v.x = 0f;
        }

        if (p.x >= max.x && v.x > 0f)
        {
            v.x = 0f;
        }

        if (p.y <= min.y && v.y < 0f)
        {
            v.y = 0f;
        }

        if (p.y >= max.y && v.y > 0f)
        {
            v.y = 0f;
        }

        rb.velocity = v;
    }

    /// <summary>
    /// 应用难度倍率（从DifficultySystem读取结果）。
    /// </summary>
    private void ApplyDifficultyMultipliers()
    {
        DifficultySystem.EnemyMultipliers m = DifficultySystem.Instance.GetEnemyMultipliers();

        maxHealth = Mathf.Max(1, Mathf.RoundToInt(config.baseHealth * m.health));
        currentMoveSpeed = config.baseMoveSpeed * m.speed;
        currentDamage = config.baseDamage * m.damage;
        currentAttackCooldown = config.attackCooldown * m.attackCooldown;
    }

    /// <summary>
    /// 受到伤害。
    /// </summary>
    public virtual void TakeDamage(float damage, GameObject source)
    {
        if (isDead) return;

        int actualDamage = Mathf.Max(0, Mathf.RoundToInt(damage));
        if (actualDamage <= 0)
        {
            return;
        }

        currentHealth -= actualDamage;
        currentHealth = Mathf.Max(0, currentHealth);
        OnDamaged();

        if (currentHealth <= 0)
        {
            Die(source);
        }
    }

    /// <summary>
    /// 受击击退：默认本帧瞬移整段距离（忽略 duration）。
    /// EnemyMelee 等子类可覆写为按时间插值，并与 AI 移动协调。
    /// </summary>
    public virtual void ApplyKnockback(Vector2 direction, float distance, float duration)
    {
        if (isDead || rb == null || distance <= 0f) return;
        if (direction.sqrMagnitude <= 0.0001f) return;

        Vector2 delta = direction.normalized * distance;
        rb.MovePosition(rb.position + delta);
    }

    /// <summary>
    /// 受伤反馈（子类可覆写）。
    /// 基类使用固定参数名 TakeDamage（若 Animator 中存在）；与具体敌人类型的移动/攻击参数名无关，后者在各自 EnemyConfig 子类中配置。
    /// </summary>
    protected virtual void OnDamaged()
    {
        if (animator != null && HasAnimatorParameter("TakeDamage"))
        {
            animator.SetTrigger("TakeDamage");
        }
    }

    /// <summary>
    /// 检查Animator参数是否存在。
    /// </summary>
    protected bool HasAnimatorParameter(string paramName)
    {
        if (animator == null || string.IsNullOrEmpty(paramName)) return false;

        foreach (AnimatorControllerParameter param in animator.parameters)
        {
            if (param.name == paramName) return true;
        }
        return false;
    }

    /// <summary>
    /// 死亡处理（子类可覆写）。
    /// </summary>
    protected virtual void Die(GameObject killer)
    {
        if (isDead) return;

        isDead = true;
        rb.velocity = Vector2.zero;

        if (animator != null)
        {
            if (HasAnimatorParameter("Die"))
            {
                animator.SetTrigger("Die");
            }
            if (HasAnimatorParameter("IsDead"))
            {
                animator.SetBool("IsDead", true);
            }
        }

        PlayerEvents playerEvents = null;

        if (killer != null)
        {
            killer.TryGetComponent<PlayerEvents>(out playerEvents);
        }

        if (playerEvents == null && player != null)
        {
            player.TryGetComponent<PlayerEvents>(out playerEvents);
        }

        if (playerEvents != null)
        {
            playerEvents.TriggerEnemyKilled(this.gameObject);

            if (config != null && config.expReward > 0)
            {
                playerEvents.TriggerExpGained(config.expReward);
            }
        }

        SpawnCoin();

        GameObjectPoolManager.Instance.Release(gameObject, 0.1f);
    }

    /// <summary>
    /// 死亡后按配置与难度掉落金币。
    /// </summary>
    private void SpawnCoin()
    {
        if (config == null) return;

        if (DifficultySystem.Instance == null)
        {
            return;
        }

        float materialDropRate = DifficultySystem.Instance.GetMaterialDropRate();
        float finalDropChance = Mathf.Clamp01(config.coinDropChance * materialDropRate);
        if (Random.Range(0f, 1f) > finalDropChance) return;
        if (config.coinDrops == null || config.coinDrops.Length == 0) return;

        float totalWeight = 0f;

        foreach (EnemyConfig.CoinDrop coinDrop in config.coinDrops)
        {
            if (coinDrop.coinPrefab != null)
            {
                totalWeight += coinDrop.dropWeight;
            }
        }

        if (totalWeight <= 0f) return;

        float randomValue = Random.Range(0f, totalWeight);

        float currentWeight = 0f;

        GameObject selectedCoinPrefab = null;

        foreach (EnemyConfig.CoinDrop coinDrop in config.coinDrops)
        {
            if (coinDrop.coinPrefab == null) continue;

            currentWeight += coinDrop.dropWeight;
            if (randomValue <= currentWeight)
            {
                selectedCoinPrefab = coinDrop.coinPrefab;
                break;
            }
        }

        if (selectedCoinPrefab != null)
        {
            Vector3 spawnPosition = this.transform.position;

            GameObject coin = GameObjectPoolManager.Instance.Spawn(selectedCoinPrefab, spawnPosition, Quaternion.identity);
            if (coin == null)
            {
                return;
            }

            float randomOffset = Random.Range(-0.5f, 0.5f);
            coin.transform.position += new Vector3(randomOffset, randomOffset, 0f);
        }
    }

    /// <summary>
    /// 当前生命比例。
    /// </summary>
    public float GetHealthPercentage() => maxHealth > 0 ? (float)currentHealth / maxHealth : 0f;

    /// <summary>
    /// 当前生命值。
    /// </summary>
    public int GetCurrentHealth() => currentHealth;

    /// <summary>
    /// 最大生命值。
    /// </summary>
    public int GetMaxHealth() => maxHealth;
}
