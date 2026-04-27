using UnityEngine;

/// <summary>
/// 主相机：SmoothDamp 跟随目标 XY，锁定 Z；可叠加屏幕抖动。
/// </summary>
public class MainCamera : MonoBehaviour
{
    private Transform target;
    public float smoothTime = 0.15f;

    [Header("受伤镜头抖动")]
    [SerializeField] private float shakeStrengthAtFullHpHit = 0.2f;

    private Vector3 velocity = Vector3.zero;

    private Vector3 followedPosition;

    private Vector3 shakeOffset;

    private PlayerEvents boundPlayerEvents;

    /// <summary>
    /// 设置跟随目标并订阅受伤抖动。
    /// </summary>
    public void SetCameraTarget(Transform newTarget)
    {
        UnbindPlayerDamageEvent();

        target = newTarget;
        followedPosition = transform.position;

        if (newTarget == null) return;

        PlayerEvents pe = newTarget.GetComponent<PlayerEvents>();
        if (pe != null)
        {
            pe.OnDamageTaken += OnPlayerDamageTakenForShake;
            boundPlayerEvents = pe;
        }
    }

    /// <summary>
    /// 解除受伤订阅。
    /// </summary>
    private void UnbindPlayerDamageEvent()
    {
        if (boundPlayerEvents != null)
        {
            boundPlayerEvents.OnDamageTaken -= OnPlayerDamageTakenForShake;
            boundPlayerEvents = null;
        }
    }

    private void OnPlayerDamageTakenForShake(float actualDamage, GameObject source)
    {
        if (actualDamage <= 0f || shakeStrengthAtFullHpHit <= 0f) return;

        float maxHp = 1f;
        if (target != null && target.TryGetComponent(out PlayerHealth ph))
        {
            maxHp = Mathf.Max(1f, ph.GetMaxHealth());
        }

        float ratio = Mathf.Clamp01(actualDamage / maxHp);
        float shake = shakeStrengthAtFullHpHit * Mathf.Lerp(0.35f, 1f, ratio);
        AddScreenShakeImpulse(shake);
    }

    private void Awake()
    {
        followedPosition = transform.position;
    }

    private void OnDestroy()
    {
        UnbindPlayerDamageEvent();
    }

    /// <summary>
    /// 叠加世界 XY 抖动。
    /// </summary>
    public void AddScreenShakeImpulse(float strength)
    {
        if (strength <= 0f) return;
        Vector2 r = Random.insideUnitCircle * strength;
        shakeOffset += new Vector3(r.x, r.y, 0f);
    }

    private void DecayShake()
    {
        if (shakeOffset.sqrMagnitude < 1e-6f)
        {
            shakeOffset = Vector3.zero;
            return;
        }

        shakeOffset = Vector3.Lerp(shakeOffset, Vector3.zero, Time.deltaTime * 14f);
    }

    /// <summary>
    /// LateUpdate：平滑跟随后叠加衰减中的 shakeOffset。
    /// </summary>
    void LateUpdate()
    {
        if (target == null) return;

        Vector3 targetPos = new Vector3(
            target.position.x,
            target.position.y,
            transform.position.z
        );

        followedPosition = Vector3.SmoothDamp(
            followedPosition,
            targetPos,
            ref velocity,
            smoothTime
        );

        DecayShake();
        transform.position = followedPosition + shakeOffset;
    }
}
