using UnityEngine;

/// <summary>
/// 生成预警动效（单 Sprite 版本）
/// - Alpha 脉冲
/// - Scale 脉冲
/// - 可选旋转
/// - 结束前淡出
/// 供对象池复用：每次 OnEnable 重置初始状态，避免上一轮动画残留。
/// </summary>
[RequireComponent(typeof(SpriteRenderer))]
public class SpawnTelegraphPulse : MonoBehaviour
{
    [Tooltip("基础颜色，会叠加透明度动画")]
    public Color baseColor = Color.white;

    [Header("透明度脉冲")]
    [Range(0f, 1f)]
    public float minAlpha = 0.35f;
    [Range(0f, 1f)]
    public float maxAlpha = 1f;
    [Min(0f)]
    public float alphaPulseSpeed = 10f;

    [Header("缩放脉冲")]
    [Min(0.01f)]
    public float baseScale = 1f;
    [Min(0f)]
    public float scaleAmplitude = 0.15f;
    [Min(0f)]
    public float scalePulseSpeed = 8f;

    [Header("旋转")]
    public bool enableRotation = true;
    public float rotationSpeed = 120f;

    [Header("末尾淡出")]
    [Tooltip("结束前强制淡出的提前量，秒")]
    [Min(0f)]
    public float fadeOutTime = 0.12f;

    //渲染组件
    private SpriteRenderer spriteRenderer;
    //已存活时间
    private float aliveTime;
    //预警总时长
    private float telegraphDuration;
    //当前颜色缓存
    private Color cachedColor;

    private void Awake()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();
    }

    private void OnEnable()
    {
        if (spriteRenderer == null)
        {
            spriteRenderer = GetComponent<SpriteRenderer>();
        }

        aliveTime = 0f;
        transform.localScale = Vector3.one * baseScale;
        transform.rotation = Quaternion.identity;

        cachedColor = baseColor;
        cachedColor.a = maxAlpha;
        spriteRenderer.color = cachedColor;
    }

    /// <summary>
    /// 由外部在生成时注入统一预警时长。
    /// </summary>
    public void SetTelegraphDuration(float duration)
    {
        telegraphDuration = duration;
    }

    private void Update()
    {
        aliveTime += Time.deltaTime;
        float t = aliveTime;

        float alpha01 = 0.5f + 0.5f * Mathf.Sin(t * alphaPulseSpeed);
        float pulseAlpha = Mathf.Lerp(minAlpha, maxAlpha, alpha01);

        float finalAlpha = pulseAlpha;
        if (fadeOutTime > 0f && telegraphDuration > 0f)
        {
            float fadeStart = Mathf.Max(0f, telegraphDuration - fadeOutTime);
            if (t >= fadeStart)
            {
                float fadeT = Mathf.InverseLerp(fadeStart, telegraphDuration, t);
                finalAlpha = Mathf.Lerp(pulseAlpha, 0f, fadeT);
            }
        }

        cachedColor = baseColor;
        cachedColor.a = Mathf.Clamp01(finalAlpha);
        spriteRenderer.color = cachedColor;

        float scale01 = 0.5f + 0.5f * Mathf.Sin(t * scalePulseSpeed);
        float scale = baseScale + scaleAmplitude * (scale01 * 2f - 1f);
        transform.localScale = Vector3.one * Mathf.Max(0.01f, scale);

        if (enableRotation && rotationSpeed != 0f)
        {
            transform.Rotate(0f, 0f, rotationSpeed * Time.deltaTime);
        }
    }
}
