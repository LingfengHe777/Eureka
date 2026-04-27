using UnityEngine;

/// <summary>
/// 近战型敌人
///
/// 行为：
/// 1. 持续追踪玩家
/// 2. 进入攻击范围后近战攻击
/// 3. 攻击范围内缓慢靠近
/// 4. 与其他敌人保持分离
///
/// 动画事件：
/// OnAttackHit() 攻击动画命中帧调用，造成伤害
/// OnAttackAnimEnd() 攻击动画结束帧调用，退出攻击状态
///
/// 配置须使用 EnemyMeleeConfig（含移动/攻击 Animator 参数名）；不可用纯 EnemyConfig。
///
/// Prefab要求：
/// Rigidbody2D
/// SpriteRenderer
/// Animator（参数名与 EnemyMeleeConfig 一致，攻击动画中配置 Animation Event）
/// Collider2D
/// </summary>
public class EnemyMelee : Enemy
{
    private EnemyMeleeConfig meleeConfig;

    //是否正在追踪
    private bool isChasing = false;
    //是否正在攻击动画
    private bool isAttacking = false;
    //下次可攻击时间
    private float nextAttackTime = 0f;

    private bool _knockbackActive;
    private Vector2 _knockbackStartPos;
    private Vector2 _knockbackTotalDelta;
    private float _knockbackDuration;
    private float _knockbackElapsed;

    /// <summary>
    /// 停止距离：小于此距离则停止平移，为攻击范围的0.85倍。
    /// </summary>
    private float stopDistance => meleeConfig != null ? meleeConfig.attackRange * 0.85f : 0f;

    protected override void Awake()
    {
        base.Awake();
    }

    protected override void OnInit()
    {
        base.OnInit();

        meleeConfig = config as EnemyMeleeConfig;
        if (meleeConfig == null)
        {
            Debug.LogError(
                $"[EnemyMelee] 必须使用 EnemyMeleeConfig，当前为 {(config != null ? $"{config.GetType().Name} ({config.name})" : "null")}。（已禁用本组件）",
                this);
            enabled = false;
            return;
        }

        if (string.IsNullOrWhiteSpace(meleeConfig.movingParamName)
            || string.IsNullOrWhiteSpace(meleeConfig.attackingParamName))
        {
            Debug.LogError(
                $"[EnemyMelee] EnemyMeleeConfig「{meleeConfig.name}」须填写 movingParamName 与 attackingParamName。（已禁用本组件）",
                this);
            enabled = false;
            return;
        }

        isAttacking = false;
        nextAttackTime = 0f;
        rb.velocity = Vector2.zero;
        isChasing = false;
        _knockbackActive = false;

        if (player != null)
        {
            isChasing = true;
        }
    }

    private void Update()
    {
        if (!enabled || meleeConfig == null) return;
        if (isDead || player == null) return;
        if (!IsWakeReady())
        {
            rb.velocity = Vector2.zero;
            UpdateAnimator();
            return;
        }

        HandleAI();
        HandleAttack();
        UpdateAnimator();
    }

    private void FixedUpdate()
    {
        if (!enabled || meleeConfig == null) return;
        if (isDead) return;

        try
        {
            if (!isChasing || player == null) return;
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

            if (isAttacking)
            {
                rb.velocity = Vector2.zero;
                return;
            }

            HandleMovement();
        }
        finally
        {
            if (enabled && !isDead && rb != null)
            {
                ClampPositionToMapBounds();
            }
        }
    }

    /// <summary>
    /// 近战击退：在 duration 内线性移动 distance，期间不追踪。
    /// </summary>
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
    /// 解析玩家并开始追踪。
    /// </summary>
    private void HandleAI()
    {
        if (player == null)
        {
            FindPlayer();
            if (player != null) isChasing = true;
            return;
        }
        if (!isChasing) isChasing = true;
    }

    /// <summary>
    /// 追踪力与分离力合成速度。
    /// </summary>
    private void HandleMovement()
    {
        Vector2 selfPos = rb.position;
        Vector2 playerPos = (Vector2)player.transform.position;
        Vector2 toPlayer = playerPos - selfPos;

        float distance = GetDistanceToPlayer();
        float stopDist = stopDistance;

        Vector2 seekForce;
        if (distance <= stopDist)
        {
            seekForce = Vector2.zero;
        }
        else if (distance > meleeConfig.attackRange)
        {
            seekForce = toPlayer.normalized * currentMoveSpeed;
        }
        else
        {
            seekForce = toPlayer.normalized * (currentMoveSpeed * 0.2f);
        }

        Vector2 separationForce = ComputeSeparationForce(selfPos);

        Vector2 finalVelocity = seekForce + separationForce;

        if (finalVelocity.magnitude > currentMoveSpeed)
        {
            finalVelocity = finalVelocity.normalized * currentMoveSpeed;
        }

        rb.velocity = finalVelocity;

        if (spriteRenderer != null)
        {
            if (Mathf.Abs(toPlayer.x) > 0.01f)
            {
                spriteRenderer.flipX = toPlayer.x < 0;
            }
        }
    }

    /// <summary>
    /// 进入攻击范围且冷却结束则开始攻击。
    /// </summary>
    private void HandleAttack()
    {
        if (player == null || isDead || isAttacking) return;
        if (Time.time < nextAttackTime) return;

        float dist = GetDistanceToPlayer();
        if (dist <= meleeConfig.attackRange)
        {
            StartAttack();
        }
    }

    /// <summary>
    /// 进入攻击状态并刷新冷却。
    /// </summary>
    private void StartAttack()
    {
        isAttacking = true;
        nextAttackTime = Time.time + currentAttackCooldown;

        rb.velocity = Vector2.zero;
        UpdateAnimator();
    }

    /// <summary>
    /// 攻击动画命中帧造成伤害。
    /// </summary>
    private void PerformAttack()
    {
        if (isDead || player == null || !isAttacking) return;

        float distanceToPlayer = GetDistanceToPlayer();
        if (distanceToPlayer <= meleeConfig.attackRange * 1.5f)
        {
            if (player.TryGetComponent<PlayerEvents>(out PlayerEvents playerEvents))
            {
                playerEvents.TriggerDamaged(currentDamage, this.gameObject);
            }
        }
    }

    /// <summary>
    /// 攻击命中事件（动画事件）。
    /// </summary>
    public void OnAttackHit()
    {
        PerformAttack();
    }

    /// <summary>
    /// 攻击动画结束事件。
    /// </summary>
    public void OnAttackAnimEnd()
    {
        isAttacking = false;
    }

    /// <summary>
    /// 更新移动/攻击 Animator 参数。
    /// </summary>
    private void UpdateAnimator()
    {
        if (animator == null || meleeConfig == null) return;

        bool isMoving = isChasing && !isAttacking && rb.velocity.magnitude > 0.1f;

        if (HasAnimatorParameter(meleeConfig.movingParamName))
        {
            animator.SetBool(meleeConfig.movingParamName, isMoving);
        }

        if (HasAnimatorParameter(meleeConfig.attackingParamName))
        {
            animator.SetBool(meleeConfig.attackingParamName, isAttacking);
        }
    }
}