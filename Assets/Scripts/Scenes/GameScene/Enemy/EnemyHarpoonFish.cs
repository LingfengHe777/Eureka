using UnityEngine;

/// <summary>
/// 鱼叉鱼：
/// - 兴奋区外：以 Enemy.currentMoveSpeed（基类 baseMoveSpeed×难度）随意闲逛，不朝向玩家；
/// - 兴奋区内、基类攻击距离外：以 EnemyHarpoonFishConfig.excitedRunSpeed×难度 朝向玩家奔跑；
/// - 进入基类 EnemyConfig.attackRange（投掷距离）：停下；冷却结束则投掷。
///
/// 基类 EnemyConfig.attackRange 表示可投掷距离；外圈兴奋距离为 EnemyHarpoonFishConfig.excitedRange。
/// 投掷方向在 StartThrow（动画开始时）锁定，动画事件 OnHarpoonRelease 只负责按该方向生成鱼叉。
/// </summary>
public class EnemyHarpoonFish : Enemy
{
    [Header("场景引用")]
    [Tooltip("鱼叉生成点，可空，空则用自身刚体位置")]
    [SerializeField] private Transform harpoonSpawnPoint;

    private EnemyHarpoonFishConfig harpoonConfig;
    /// <summary>
    /// 缓存自 EnemyHarpoonFishConfig.excitedRange。
    /// </summary>
    private float excitedRange;
    /// <summary>
    /// 缓存自 EnemyConfig.attackRange（投掷距离）。
    /// </summary>
    private float attackRange;
    private string runningParamName;
    private string throwingParamName;

    /// <summary>
    /// 兴奋追击速度（excitedRunSpeed × 难度速度倍率）。
    /// </summary>
    private float currentExcitedRunSpeed;

    private Vector2 wanderDirection = Vector2.right;
    private float nextWanderDirectionChangeTime;

    private bool isThrowing;
    private float nextThrowTime;

    /// <summary>
    /// 进入投掷时锁定：自出手点指向玩家的方向，出手事件只使用此方向，不再追应当帧玩家位置。
    /// </summary>
    private Vector2 lockedThrowDirection = Vector2.right;

    private bool _knockbackActive;
    private Vector2 _knockbackStartPos;
    private Vector2 _knockbackTotalDelta;
    private float _knockbackDuration;
    private float _knockbackElapsed;

    protected override void OnInit()
    {
        isThrowing = false;
        nextThrowTime = 0f;
        lockedThrowDirection = Vector2.right;
        rb.velocity = Vector2.zero;
        _knockbackActive = false;

        harpoonConfig = config as EnemyHarpoonFishConfig;
        if (harpoonConfig == null)
        {
            FailInvalidConfig(
                $"必须使用 EnemyHarpoonFishConfig，当前为 {(config != null ? $"{config.GetType().Name} ({config.name})" : "null")}。");
            return;
        }

        if (DifficultySystem.Instance == null)
        {
            FailInvalidConfig("DifficultySystem 未就绪，无法计算兴奋追击速度倍率。");
            return;
        }

        if (harpoonConfig.harpoonPrefab == null)
        {
            FailInvalidConfig($"EnemyHarpoonFishConfig「{harpoonConfig.name}」未指定 harpoonPrefab。");
            return;
        }

        if (harpoonConfig.harpoonProjectileSpeed <= 0f)
        {
            FailInvalidConfig($"EnemyHarpoonFishConfig「{harpoonConfig.name}」harpoonProjectileSpeed 必须大于 0。");
            return;
        }

        if (harpoonConfig.harpoonMaxTravelDistance <= 0f)
        {
            FailInvalidConfig($"EnemyHarpoonFishConfig「{harpoonConfig.name}」harpoonMaxTravelDistance 必须大于 0。");
            return;
        }

        if (harpoonConfig.excitedRange <= 0f)
        {
            FailInvalidConfig(
                $"EnemyHarpoonFishConfig「{harpoonConfig.name}」excitedRange 必须大于 0。");
            return;
        }

        if (harpoonConfig.attackRange <= 0f || harpoonConfig.attackRange > harpoonConfig.excitedRange)
        {
            FailInvalidConfig(
                $"EnemyHarpoonFishConfig「{harpoonConfig.name}」基类 attackRange 须在 (0, excitedRange] 内（投掷距离）。");
            return;
        }

        if (harpoonConfig.excitedRunSpeed <= 0f)
        {
            FailInvalidConfig($"EnemyHarpoonFishConfig「{harpoonConfig.name}」excitedRunSpeed 必须大于 0。");
            return;
        }

        if (harpoonConfig.wanderDirectionChangeInterval <= 0f)
        {
            FailInvalidConfig($"EnemyHarpoonFishConfig「{harpoonConfig.name}」wanderDirectionChangeInterval 必须大于 0。");
            return;
        }

        if (string.IsNullOrWhiteSpace(harpoonConfig.harpoonRunningParamName)
            || string.IsNullOrWhiteSpace(harpoonConfig.harpoonThrowingParamName))
        {
            FailInvalidConfig($"EnemyHarpoonFishConfig「{harpoonConfig.name}」须填写 harpoonRunningParamName 与 harpoonThrowingParamName。");
            return;
        }

        DifficultySystem.EnemyMultipliers m = DifficultySystem.Instance.GetEnemyMultipliers();
        currentExcitedRunSpeed = harpoonConfig.excitedRunSpeed * m.speed;

        excitedRange = harpoonConfig.excitedRange;
        attackRange = harpoonConfig.attackRange;
        runningParamName = harpoonConfig.harpoonRunningParamName;
        throwingParamName = harpoonConfig.harpoonThrowingParamName;

        PickNewWanderDirection();
        nextWanderDirectionChangeTime = Time.time + harpoonConfig.wanderDirectionChangeInterval;
    }

    private void PickNewWanderDirection()
    {
        wanderDirection = Random.insideUnitCircle.normalized;
        if (wanderDirection.sqrMagnitude < 0.0001f)
        {
            wanderDirection = Vector2.right;
        }
    }

    private void FailInvalidConfig(string message)
    {
        Debug.LogError($"[EnemyHarpoonFish] {message}（已禁用本组件） GameObject={gameObject.name}", this);
        enabled = false;
    }

    private void Update()
    {
        if (!enabled || harpoonConfig == null) return;
        if (isDead) return;
        if (player == null)
        {
            FindPlayer();
        }

        if (!IsWakeReady())
        {
            rb.velocity = Vector2.zero;
            ApplyAnimatorParams();
            return;
        }

        HandleThrowIntent();
        ApplyAnimatorParams();
    }

    private void FixedUpdate()
    {
        if (!enabled || harpoonConfig == null) return;
        if (isDead) return;

        try
        {
            if (player == null)
            {
                FindPlayer();
            }

            if (!IsWakeReady())
            {
                rb.velocity = Vector2.zero;
                return;
            }

            if (_knockbackActive)
            {
                _knockbackElapsed += Time.fixedDeltaTime;
                float t = _knockbackDuration > 0f
                    ? Mathf.Clamp01(_knockbackElapsed / _knockbackDuration)
                    : 1f;
                rb.MovePosition(_knockbackStartPos + _knockbackTotalDelta * t);
                rb.velocity = Vector2.zero;
                if (t >= 1f)
                {
                    _knockbackActive = false;
                }

                return;
            }

            if (isThrowing)
            {
                rb.velocity = Vector2.zero;
                return;
            }

            float dist = GetDistanceToPlayer();

            if (dist > excitedRange)
            {
                HandleIdleWanderMovement();
                return;
            }

            if (dist > attackRange)
            {
                HandleChaseMovement();
                return;
            }

            rb.velocity = Vector2.zero;
        }
        finally
        {
            if (enabled && !isDead && rb != null)
            {
                ClampPositionToMapBounds();
            }
        }
    }

    public override void ApplyKnockback(Vector2 direction, float distance, float duration)
    {
        if (isDead || rb == null || distance <= 0f) return;
        if (direction.sqrMagnitude <= 0.0001f) return;

        Vector2 dir = direction.normalized;
        rb.velocity = Vector2.zero;

        if (duration <= 0f)
        {
            rb.MovePosition(rb.position + dir * distance);
            return;
        }

        _knockbackStartPos = rb.position;
        _knockbackTotalDelta = dir * distance;
        _knockbackDuration = duration;
        _knockbackElapsed = 0f;
        _knockbackActive = true;
    }

    /// <summary>
    /// 攻击区内且冷却结束则开始投掷（由 Update 调用，避免与 FixedUpdate 移动交错）。
    /// </summary>
    private void HandleThrowIntent()
    {
        if (isThrowing) return;

        float dist = GetDistanceToPlayer();
        if (dist > excitedRange) return;
        if (dist > attackRange) return;
        if (Time.time < nextThrowTime) return;

        StartThrow();
    }

    private void StartThrow()
    {
        isThrowing = true;
        rb.velocity = Vector2.zero;

        Vector2 aimOrigin = harpoonSpawnPoint != null ? (Vector2)harpoonSpawnPoint.position : rb.position;
        Vector2 toPlayer = player != null
            ? (Vector2)player.transform.position - aimOrigin
            : Vector2.right;
        if (toPlayer.sqrMagnitude < 0.0001f)
        {
            toPlayer = Vector2.right;
        }

        lockedThrowDirection = toPlayer.normalized;

        if (spriteRenderer != null && Mathf.Abs(lockedThrowDirection.x) > 0.01f)
        {
            spriteRenderer.flipX = lockedThrowDirection.x < 0;
        }

        if (animator != null && !string.IsNullOrEmpty(throwingParamName) && HasAnimatorParameter(throwingParamName))
        {
            animator.SetBool(throwingParamName, true);
        }

        ApplyAnimatorParams();
    }

    /// <summary>
    /// 兴奋区外：不朝向玩家，周期性随机方向，速度为 currentMoveSpeed。
    /// </summary>
    private void HandleIdleWanderMovement()
    {
        if (Time.time >= nextWanderDirectionChangeTime)
        {
            PickNewWanderDirection();
            nextWanderDirectionChangeTime = Time.time + harpoonConfig.wanderDirectionChangeInterval;
        }

        Vector2 selfPos = rb.position;
        Vector2 wanderForce = wanderDirection * currentMoveSpeed;

        Vector2 separationForce = ComputeSeparationForce(selfPos);
        Vector2 finalVelocity = wanderForce + separationForce;

        if (finalVelocity.magnitude > currentMoveSpeed)
        {
            finalVelocity = finalVelocity.normalized * currentMoveSpeed;
        }

        rb.velocity = finalVelocity;

        if (spriteRenderer != null && Mathf.Abs(finalVelocity.x) > 0.05f)
        {
            spriteRenderer.flipX = finalVelocity.x < 0;
        }
    }

    /// <summary>
    /// 兴奋区内、攻击区外：朝向玩家追击，速度上限为 currentExcitedRunSpeed。
    /// </summary>
    private void HandleChaseMovement()
    {
        Vector2 selfPos = rb.position;
        Vector2 playerPos = (Vector2)player.transform.position;
        Vector2 toPlayer = playerPos - selfPos;

        Vector2 seekForce = toPlayer.sqrMagnitude > 0.0001f ? toPlayer.normalized * currentExcitedRunSpeed : Vector2.zero;

        Vector2 separationForce = ComputeSeparationForce(selfPos);

        Vector2 finalVelocity = seekForce + separationForce;
        if (finalVelocity.magnitude > currentExcitedRunSpeed)
        {
            finalVelocity = finalVelocity.normalized * currentExcitedRunSpeed;
        }

        rb.velocity = finalVelocity;

        if (spriteRenderer != null && Mathf.Abs(toPlayer.x) > 0.01f)
        {
            spriteRenderer.flipX = toPlayer.x < 0;
        }
    }

    /// <summary>
    /// 奔跑：仅兴奋区内且未进攻击区、且非投掷；否则为 idle（含区外闲逛、攻击区内等冷却）。
    /// </summary>
    private void ApplyAnimatorParams()
    {
        if (animator == null) return;

        float dist = player == null ? float.MaxValue : GetDistanceToPlayer();
        bool throwing = isThrowing;
        bool running = !throwing
            && dist <= excitedRange
            && dist > attackRange;

        if (!string.IsNullOrEmpty(runningParamName) && HasAnimatorParameter(runningParamName))
        {
            animator.SetBool(runningParamName, running);
        }

        if (!string.IsNullOrEmpty(throwingParamName) && HasAnimatorParameter(throwingParamName))
        {
            animator.SetBool(throwingParamName, throwing);
        }
    }

    public void OnHarpoonRelease()
    {
        if (harpoonConfig == null)
        {
            Debug.LogError("[EnemyHarpoonFish] OnHarpoonRelease：配置无效，不应调用。", this);
            return;
        }

        GameObject prefab = harpoonConfig.harpoonPrefab;
        if (isDead || prefab == null || player == null) return;

        float speed = harpoonConfig.harpoonProjectileSpeed;
        float maxDist = harpoonConfig.harpoonMaxTravelDistance;

        Vector2 spawn = harpoonSpawnPoint != null
            ? (Vector2)harpoonSpawnPoint.position
            : rb.position;

        Vector2 flightDir = lockedThrowDirection.sqrMagnitude > 0.0001f
            ? lockedThrowDirection
            : Vector2.right;

        GameObject projObj = GameObjectPoolManager.Instance.Spawn(prefab, spawn, Quaternion.identity);
        if (projObj == null) return;

        if (!projObj.TryGetComponent(out HarpoonFishProjectile projectile))
        {
            Debug.LogError(
                $"[EnemyHarpoonFish] 鱼叉预制体「{prefab.name}」缺少 HarpoonFishProjectile。",
                prefab);
            GameObjectPoolManager.Instance.Release(projObj);
            return;
        }

        projectile.Initialize(gameObject, currentDamage, speed, flightDir, maxDist);
    }

    /// <summary>
    /// 动画事件：投掷片段结束。须放在 throw 片段最后一帧；片段请勿勾选 Loop Time，否则会反复触发本事件。
    /// 冷却从本帧起算。此处立即同步 Animator，否则事件多在 Update 之后执行，当帧仍保持 IsThrowing=true，投掷状态会卡住或循环。
    /// </summary>
    public void OnHarpoonThrowEnd()
    {
        isThrowing = false;
        nextThrowTime = Time.time + currentAttackCooldown;

        if (animator != null && !string.IsNullOrEmpty(throwingParamName) && HasAnimatorParameter(throwingParamName))
        {
            animator.SetBool(throwingParamName, false);
        }

        ApplyAnimatorParams();
    }
}
