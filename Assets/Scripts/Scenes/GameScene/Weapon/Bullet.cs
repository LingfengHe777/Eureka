using UnityEngine;
using System.Collections;

/// <summary>
/// 子弹脚本
/// 处理子弹的移动、碰撞检测、伤害计算、穿透等
/// </summary>
[RequireComponent(typeof(Rigidbody2D))]
public class Bullet : MonoBehaviour
{
    [Header("子弹属性")]
    //伤害
    private float damage;

    //速度
    private float speed;

    //可穿透敌人数
    private int pierceCount;

    //当前已穿透数
    private int currentPierceCount = 0;

    //飞行方向
    private Vector2 direction;

    //所有者（通常为玩家）
    private GameObject owner;

    //所有者 StatHandler（暴击等）
    private StatHandler ownerStatHandler;

    //存活时间
    private float lifetime = 5f;

    //已命中敌人（防重复伤害）
    private System.Collections.Generic.HashSet<GameObject> hitEnemies = new System.Collections.Generic.HashSet<GameObject>();

    [Header("组件引用")]
    private Rigidbody2D rb;
    private Coroutine lifetimeCoroutine;

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
    }

    /// <summary>
    /// 初始化子弹方向、伤害、穿透与所有者。
    /// </summary>
    public void Initialize(Vector2 dir, float dmg, float spd, int pierce, GameObject own, StatHandler statHandler)
    {
        hitEnemies.Clear();
        currentPierceCount = 0;
        direction = dir.normalized;
        damage = dmg;
        speed = spd;
        pierceCount = pierce;
        owner = own;
        ownerStatHandler = statHandler;

        if (rb != null)
        {
            rb.velocity = direction * speed;
        }

        float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
        transform.rotation = Quaternion.AngleAxis(angle, Vector3.forward);

        Vector3 pos = transform.position;
        if (Mathf.Abs(pos.z) > 0.01f)
        {
            transform.position = new Vector3(pos.x, pos.y, 0f);
        }

        RestartLifetimeCountdown();
    }

    private void OnTriggerEnter2D(Collider2D collision)
    {
        if (collision.gameObject == owner) return;

        if (collision.TryGetComponent<Enemy>(out Enemy enemy))
        {
            if (hitEnemies.Contains(enemy.gameObject)) return;

            float critChance = ownerStatHandler != null ? ownerStatHandler.GetStat(StatType.CritChance) : 0f;
            bool isCrit = Random.Range(0f, 1f) < critChance;
            float finalDamage = damage * (isCrit ? 2f : 1f);

            enemy.TakeDamage(finalDamage, owner);

            if (owner != null && owner.TryGetComponent<PlayerEvents>(out PlayerEvents playerEvents))
            {
                playerEvents.TriggerDealDamage(finalDamage, enemy.gameObject, isCrit);
            }

            if (ownerStatHandler != null && owner != null)
            {
                float healthSteal = ownerStatHandler.GetStat(StatType.HealthSteal);
                if (healthSteal > 0f)
                {
                    float healAmount = finalDamage * healthSteal;
                    if (owner.TryGetComponent<PlayerHealth>(out PlayerHealth playerHealth))
                    {
                        playerHealth.Heal(healAmount);
                    }
                }
            }

            hitEnemies.Add(enemy.gameObject);
            currentPierceCount++;

            if (currentPierceCount > pierceCount)
            {
                ReturnToPool();
            }
        }
    }

    /// <summary>
    /// 设置存活时间并重启倒计时。
    /// </summary>
    public void SetLifetime(float time)
    {
        lifetime = time;
        RestartLifetimeCountdown();
    }

    private void OnDisable()
    {
        if (rb != null)
        {
            rb.velocity = Vector2.zero;
            rb.angularVelocity = 0f;
        }
        if (lifetimeCoroutine != null)
        {
            StopCoroutine(lifetimeCoroutine);
            lifetimeCoroutine = null;
        }
        hitEnemies.Clear();
        currentPierceCount = 0;
    }

    private void RestartLifetimeCountdown()
    {
        if (lifetimeCoroutine != null)
        {
            StopCoroutine(lifetimeCoroutine);
        }
        lifetimeCoroutine = StartCoroutine(LifetimeCoroutine());
    }

    private IEnumerator LifetimeCoroutine()
    {
        yield return new WaitForSeconds(lifetime);
        ReturnToPool();
    }

    private void ReturnToPool()
    {
        if (lifetimeCoroutine != null)
        {
            StopCoroutine(lifetimeCoroutine);
            lifetimeCoroutine = null;
        }
        GameObjectPoolManager.Instance.Release(gameObject);
    }
}
