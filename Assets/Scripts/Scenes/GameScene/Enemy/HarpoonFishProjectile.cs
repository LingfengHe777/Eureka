using UnityEngine;

/// <summary>
/// 鱼叉鱼投掷物：直线飞行，命中玩家造成伤害并回收；命中 Wall 或超出最大飞行距离则销毁/回收。
/// Prefab：Rigidbody2D（Dynamic）、Collider2D（Is Trigger）、本脚本。
/// </summary>
[RequireComponent(typeof(Rigidbody2D))]
public class HarpoonFishProjectile : MonoBehaviour
{
    private Rigidbody2D rb;
    private Collider2D selfCollider;

    private GameObject owner;
    private Collider2D ownerCollider;
    private Vector2 spawnPosition;
    private float maxDistanceSq;
    private float damage;
    private bool initialized;

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        selfCollider = GetComponent<Collider2D>();
    }

    private void OnDisable()
    {
        if (rb != null)
        {
            rb.velocity = Vector2.zero;
            rb.angularVelocity = 0f;
        }

        if (selfCollider != null && ownerCollider != null)
        {
            Physics2D.IgnoreCollision(selfCollider, ownerCollider, false);
        }

        owner = null;
        ownerCollider = null;
        initialized = false;
    }

    /// <summary>
    /// 从敌人处发射时调用；方向为世界空间。
    /// </summary>
    public void Initialize(
        GameObject ownerEnemy,
        float damageAmount,
        float speed,
        Vector2 direction,
        float maxTravelDistance)
    {
        owner = ownerEnemy;
        damage = damageAmount;
        spawnPosition = rb.position;
        maxDistanceSq = maxTravelDistance * maxTravelDistance;

        Vector2 dir = direction.sqrMagnitude > 0.0001f ? direction.normalized : Vector2.right;
        rb.velocity = dir * speed;

        float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
        transform.rotation = Quaternion.AngleAxis(angle, Vector3.forward);

        if (owner != null)
        {
            owner.TryGetComponent(out ownerCollider);
        }

        if (selfCollider != null && ownerCollider != null)
        {
            Physics2D.IgnoreCollision(selfCollider, ownerCollider, true);
        }

        initialized = true;
    }

    private void FixedUpdate()
    {
        if (!initialized) return;

        if ((rb.position - spawnPosition).sqrMagnitude >= maxDistanceSq)
        {
            ReturnToPool();
        }
    }

    private void OnTriggerEnter2D(Collider2D collision)
    {
        if (!initialized) return;
        if (owner != null && collision.gameObject == owner) return;

        if (collision.CompareTag("Wall"))
        {
            ReturnToPool();
            return;
        }

        if (collision.CompareTag("Player"))
        {
            if (collision.TryGetComponent(out PlayerEvents playerEvents))
            {
                playerEvents.TriggerDamaged(damage, owner != null ? owner : gameObject);
            }

            ReturnToPool();
        }
    }

    private void ReturnToPool()
    {
        if (GameObjectPoolManager.Instance != null)
        {
            GameObjectPoolManager.Instance.Release(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }
}
