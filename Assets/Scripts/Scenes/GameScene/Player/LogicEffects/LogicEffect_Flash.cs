using System.Collections;
using UnityEngine;

/// <summary>
/// 闪现逻辑效果：
/// 按配置键触发
/// 触发前做碰撞体形状检测，遇到Wall停在碰撞体前
/// 所有可调参数由 LogicEffect_FlashSO 提供
/// </summary>
[RequireComponent(typeof(Rigidbody2D), typeof(Collider2D))]
public class LogicEffect_Flash : MonoBehaviour, IEffectComponent
{
    private float cooldownSeconds;
    private float blinkDistance;
    private float blinkDuration;
    private float stopBuffer;
    private int castBufferSize;
    private LayerMask wallLayerMask;
    private KeyCode activationKey;

    private Rigidbody2D rb;
    private Collider2D selfCollider;
    private PlayerMovement playerMovement;
    private Coroutine blinkRoutine;
    private bool isBlinking;
    private bool isInitialized;
    private float nextBlinkTime;
    private Vector2 lastNonZeroInput = Vector2.right;
    private RaycastHit2D[] castHits;
    private PlayerOverheadCooldownBar overheadCooldownBar;

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        selfCollider = GetComponent<Collider2D>();
        playerMovement = GetComponent<PlayerMovement>();
        overheadCooldownBar = GetComponentInChildren<PlayerOverheadCooldownBar>(true);
        if (overheadCooldownBar != null)
        {
            overheadCooldownBar.HideImmediately();
        }
    }

    private void Update()
    {
        if (!enabled || isBlinking)
        {
            return;
        }

        if (!isInitialized)
        {
            return;
        }

        if (!IsGameplayRunning())
        {
            return;
        }

        if (playerMovement != null)
        {
            Vector2 inputDir = playerMovement.GetInputDir();
            if (inputDir.sqrMagnitude > 0.0001f)
            {
                lastNonZeroInput = inputDir.normalized;
            }
        }

        if (Input.GetKeyDown(activationKey))
        {
            TryBlink();
        }

        UpdateCooldownUI();
    }

    /// <summary>
    /// 由 LogicEffect_FlashSO 注入配置。
    /// </summary>
    public void ApplyConfig(LogicEffect_FlashSO config)
    {
        if (config == null)
        {
            Debug.LogError("[LogicEffect_Flash] 配置为空。", this);
            enabled = false;
            return;
        }

        cooldownSeconds = Mathf.Max(0f, config.cooldownSeconds);
        blinkDistance = Mathf.Max(0f, config.blinkDistance);
        blinkDuration = Mathf.Max(0f, config.blinkDuration);
        stopBuffer = Mathf.Max(0f, config.stopBuffer);
        castBufferSize = Mathf.Max(4, config.castBufferSize);
        wallLayerMask = config.wallLayerMask;
        activationKey = config.activationKey;
        castHits = new RaycastHit2D[castBufferSize];

        if (blinkDistance <= 0f)
        {
            Debug.LogError("[LogicEffect_Flash] blinkDistance 必须大于 0", this);
            enabled = false;
            return;
        }

        if (wallLayerMask.value == 0)
        {
            int wallLayer = LayerMask.NameToLayer("Wall");
            if (wallLayer >= 0)
            {
                wallLayerMask = 1 << wallLayer;
            }
            else
            {
                Debug.LogError("[LogicEffect_Flash] 未配置 wallLayerMask，且找不到名为 Wall 的层。", this);
                enabled = false;
                return;
            }
        }

        isInitialized = true;
        if (overheadCooldownBar != null)
        {
            overheadCooldownBar.HideImmediately();
        }
    }

    public void CleanupEffect()
    {
        if (blinkRoutine != null)
        {
            StopCoroutine(blinkRoutine);
            blinkRoutine = null;
        }

        isBlinking = false;
        isInitialized = false;
        if (playerMovement != null)
        {
            playerMovement.enabled = true;
        }

        if (overheadCooldownBar != null)
        {
            overheadCooldownBar.HideImmediately();
        }
    }

    private void TryBlink()
    {
        if (Time.time < nextBlinkTime)
        {
            return;
        }

        if (blinkDistance <= 0f)
        {
            return;
        }

        Vector2 direction = ResolveBlinkDirection();
        if (direction.sqrMagnitude <= 0.0001f)
        {
            return;
        }

        Vector2 startPos = rb.position;
        float allowedDistance = ComputeAllowedDistance(startPos, direction, blinkDistance);
        if (allowedDistance <= 0.0001f)
        {
            nextBlinkTime = Time.time + cooldownSeconds;
            return;
        }

        Vector2 endPos = startPos + direction * allowedDistance;
        nextBlinkTime = Time.time + cooldownSeconds;

        if (blinkRoutine != null)
        {
            StopCoroutine(blinkRoutine);
        }
        blinkRoutine = StartCoroutine(BlinkRoutine(startPos, endPos, blinkDuration));
    }

    private IEnumerator BlinkRoutine(Vector2 startPos, Vector2 endPos, float duration)
    {
        isBlinking = true;
        if (playerMovement != null)
        {
            playerMovement.enabled = false;
        }

        rb.velocity = Vector2.zero;

        if (duration <= 0.0001f)
        {
            rb.MovePosition(endPos);
            yield return new WaitForFixedUpdate();
        }
        else
        {
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.fixedDeltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                rb.MovePosition(Vector2.Lerp(startPos, endPos, t));
                yield return new WaitForFixedUpdate();
            }

            rb.MovePosition(endPos);
            yield return new WaitForFixedUpdate();
        }

        rb.velocity = Vector2.zero;
        if (playerMovement != null)
        {
            playerMovement.enabled = true;
        }

        isBlinking = false;
        blinkRoutine = null;
    }

    private Vector2 ResolveBlinkDirection()
    {
        if (playerMovement != null)
        {
            Vector2 inputDir = playerMovement.GetInputDir();
            if (inputDir.sqrMagnitude > 0.0001f)
            {
                return inputDir.normalized;
            }
        }

        return lastNonZeroInput.normalized;
    }

    private float ComputeAllowedDistance(Vector2 origin, Vector2 direction, float desiredDistance)
    {
        ContactFilter2D filter = new ContactFilter2D();
        filter.useTriggers = false;
        filter.useLayerMask = false;

        int hitCount = selfCollider.Cast(direction, filter, castHits, desiredDistance);
        float minDistance = desiredDistance;
        bool blocked = false;

        for (int i = 0; i < hitCount; i++)
        {
            Collider2D hitCollider = castHits[i].collider;
            if (hitCollider == null)
            {
                continue;
            }

            if (!IsWallCollider(hitCollider))
            {
                continue;
            }

            if (castHits[i].distance < minDistance)
            {
                minDistance = castHits[i].distance;
                blocked = true;
            }
        }

        if (!blocked)
        {
            return desiredDistance;
        }

        return Mathf.Max(0f, minDistance - stopBuffer);
    }

    private bool IsWallCollider(Collider2D col)
    {
        if (col == null)
        {
            return false;
        }

        return (wallLayerMask.value & (1 << col.gameObject.layer)) != 0;
    }

    private bool IsGameplayRunning()
    {
        GameContext ctx = GameContext.Instance;
        if (ctx == null || !ctx.TryGetGameMgr(out GameMgr gameMgr) || gameMgr == null)
        {
            return true;
        }

        return gameMgr.GetGameState() == GameMgr.GameState.Playing;
    }

    private float GetCooldownRemaining()
    {
        return Mathf.Max(0f, nextBlinkTime - Time.time);
    }

    private void UpdateCooldownUI()
    {
        if (overheadCooldownBar == null || !isInitialized)
        {
            return;
        }

        overheadCooldownBar.SetCooldown(GetCooldownRemaining(), cooldownSeconds);
    }
}