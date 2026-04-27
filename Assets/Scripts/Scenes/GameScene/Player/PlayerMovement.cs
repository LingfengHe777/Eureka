using UnityEngine;

/// <summary>
/// 玩家移动：读取轴输入、Rigidbody2D 速度，并通过 PlayerEvents 广播移动/停止。
/// </summary>
[RequireComponent(typeof(Rigidbody2D), typeof(StatHandler), typeof(PlayerEvents))]
public class PlayerMovement : MonoBehaviour
{
    //刚体
    private Rigidbody2D rb;
    //属性
    private StatHandler statHandler;
    //事件
    private PlayerEvents playerEvents;

    [Header("移动状态")]
    //输入方向
    private Vector2 inputDir;
    //是否在移动
    private bool isMoving;

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        statHandler = GetComponent<StatHandler>();
        playerEvents = GetComponent<PlayerEvents>();
    }

    private void Update()
    {
        float x = Input.GetAxisRaw("Horizontal");
        float y = Input.GetAxisRaw("Vertical");
        inputDir = new Vector2(x, y).normalized;

        HandleMovementEvents();
    }

    private void FixedUpdate()
    {
        float currentSpeed = statHandler.GetStat(StatType.MoveSpeed);
        rb.velocity = inputDir * currentSpeed;
    }

    /// <summary>
    /// 仅在状态变化或持续移动时广播移动/停止事件。
    /// </summary>
    private void HandleMovementEvents()
    {
        if (inputDir.sqrMagnitude > 0.01f)
        {
            if (!isMoving)
            {
                isMoving = true;
            }

            playerEvents.TriggerMove(inputDir);
        }
        else if (isMoving)
        {
            isMoving = false;
            playerEvents.TriggerStopMove();
        }
    }

    /// <summary>
    /// 当前输入方向（归一化或零）。
    /// </summary>
    public Vector2 GetInputDir() => inputDir;
}
