using UnityEngine;

/// <summary>
/// 金币脚本
/// 处理金币的拾取逻辑，响应玩家的拾取范围
/// </summary>
[RequireComponent(typeof(Collider2D))]
public class Coin : MonoBehaviour
{
    //场景上下文
    private GameContext gameContext;
    [Header("金币属性")]
    //金币价值
    [SerializeField]
    private float coinValue = 1f;

    //是否已被拾取
    private bool isPickedUp = false;

    //拾取飞向玩家的速度
    [SerializeField]
    private float pickupSpeed = 10f;

    //玩家
    private GameObject player;

    //是否正在被吸引
    private bool isAttracted = false;

    [Header("组件引用")]
    private Collider2D coinCollider;
    private CoinManager coinManager;

    private void Awake()
    {
        gameContext = GameContext.Instance;
        coinCollider = GetComponent<Collider2D>();
        if (coinCollider != null)
        {
            coinCollider.isTrigger = true;
        }
    }

    private void Start()
    {
        ResolvePlayerAndCoinManager();
    }

    private void OnEnable()
    {
        isPickedUp = false;
        isAttracted = false;
        ResolvePlayerAndCoinManager();
    }

    private void Update()
    {
        if (isAttracted && player != null)
        {
            Vector2 direction = (player.transform.position - transform.position).normalized;
            transform.position += (Vector3)(direction * pickupSpeed * Time.deltaTime);
        }
    }

    /// <summary>
    /// 设置金币价值。
    /// </summary>
    public void SetCoinValue(float value)
    {
        coinValue = value;
    }

    /// <summary>
    /// 获取金币价值。
    /// </summary>
    public float GetCoinValue() => coinValue;

    /// <summary>
    /// 开始被吸引向玩家。
    /// </summary>
    public void StartAttraction()
    {
        if (!isPickedUp)
        {
            isAttracted = true;
        }
    }

    /// <summary>
    /// 拾取并回收到对象池。
    /// </summary>
    public void PickUp()
    {
        if (isPickedUp) return;

        isPickedUp = true;

        NotifyCoinCollected();

        GameObjectPoolManager.Instance.Release(gameObject);
    }

    /// <summary>
    /// 通知 CoinManager 增加金币。
    /// </summary>
    private void NotifyCoinCollected()
    {
        ResolvePlayerAndCoinManager();
        if (coinManager != null)
        {
            coinManager.AddCoins(coinValue);
        }
    }

    private void OnTriggerEnter2D(Collider2D collision)
    {
        if (collision.CompareTag("Player"))
        {
            PickUp();
        }
    }

    /// <summary>
    /// 从 GameContext 或玩家解析 CoinManager。
    /// </summary>
    private void ResolvePlayerAndCoinManager()
    {
        if (gameContext == null)
        {
            gameContext = GameContext.Instance;
        }

        if (gameContext != null)
        {
            if (player == null)
            {
                gameContext.TryGetPlayer(out player);
            }
            if (coinManager == null)
            {
                gameContext.TryGetCoinManager(out coinManager);
            }
        }

        if (coinManager == null && player != null)
        {
            player.TryGetComponent(out coinManager);
        }
    }
}
