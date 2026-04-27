using UnityEngine;

/// <summary>
/// 金币收集器：检测拾取范围内金币并吸引。
/// </summary>
[RequireComponent(typeof(StatHandler))]
public class CoinCollector : MonoBehaviour
{
    [Header("组件引用")]
    //StatHandler
    private StatHandler statHandler;

    [Header("检测设置")]
    [SerializeField]
    //金币检测层级（默认第7层）
    private LayerMask coinLayerMask = 1 << 7;

    [SerializeField]
    //检测间隔秒
    private float detectionInterval = 0.1f;

    //上次检测时间
    private float lastDetectionTime = 0f;

    //OverlapCircleNonAlloc 缓冲区
    private const int CoinOverlapInitialBufferSize = 256;
    private const int CoinOverlapMaxBufferSize = 2048;
    private Collider2D[] coinOverlapBuffer = new Collider2D[CoinOverlapInitialBufferSize];

    private void Awake()
    {
        statHandler = GetComponent<StatHandler>();
    }

    private void Update()
    {
        if (Time.time - lastDetectionTime >= detectionInterval)
        {
            DetectCoins();
            lastDetectionTime = Time.time;
        }
    }

    /// <summary>
    /// 圆形检测范围内金币并启动吸引。
    /// </summary>
    private void DetectCoins()
    {
        if (statHandler == null)
        {
            return;
        }

        float pickRange = statHandler.GetStat(StatType.PickRange);

        if (pickRange <= 0f)
        {
            return;
        }

        int count = OverlapCoins(pickRange);
        for (int i = 0; i < count; i++)
        {
            Collider2D coinCollider = coinOverlapBuffer[i];
            if (coinCollider != null && coinCollider.TryGetComponent<Coin>(out Coin coin))
            {
                coin.StartAttraction();
            }
        }
    }

    private int OverlapCoins(float pickRange)
    {
        int count = Physics2D.OverlapCircleNonAlloc(transform.position, pickRange, coinOverlapBuffer, coinLayerMask);
        while (count >= coinOverlapBuffer.Length && coinOverlapBuffer.Length < CoinOverlapMaxBufferSize)
        {
            int newSize = Mathf.Min(coinOverlapBuffer.Length * 2, CoinOverlapMaxBufferSize);
            coinOverlapBuffer = new Collider2D[newSize];
            count = Physics2D.OverlapCircleNonAlloc(transform.position, pickRange, coinOverlapBuffer, coinLayerMask);
        }

        return Mathf.Min(count, coinOverlapBuffer.Length);
    }

    /// <summary>
    /// 绘制拾取范围（编辑器调试）。
    /// </summary>
    private void OnDrawGizmosSelected()
    {
        if (statHandler != null)
        {
            float pickRange = statHandler.GetStat(StatType.PickRange);
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(transform.position, pickRange);
        }
    }
}
