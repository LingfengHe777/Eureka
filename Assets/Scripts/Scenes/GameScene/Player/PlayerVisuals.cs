using UnityEngine;

/// <summary>
/// 挂在 Player 根节点：监听 PlayerEvents，在视觉子物体上驱动 Animator 与 SpriteRenderer（与刚体根分离）。
/// 必须在 Inspector 指定 visualTransform，且该子物体上须同时有 SpriteRenderer、Animator。
/// </summary>
[RequireComponent(typeof(PlayerEvents))]
public class PlayerVisuals : MonoBehaviour
{
    [Header("视觉子物体")]
    [SerializeField]
    [Tooltip("带 SpriteRenderer 与 Animator 的子物体，勿为空")]
    //身体渲染与动画所在子物体
    private Transform visualTransform;

    //Animator
    private Animator animator;
    //SpriteRenderer
    private SpriteRenderer sr;
    //PlayerEvents
    private PlayerEvents playerEvents;

    public SpriteRenderer BodySpriteRenderer => sr;

    public Animator BodyAnimator => animator;

    private void Awake()
    {
        playerEvents = GetComponent<PlayerEvents>();

        if (visualTransform == null)
        {
            enabled = false;
            return;
        }

        animator = visualTransform.GetComponent<Animator>();
        sr = visualTransform.GetComponent<SpriteRenderer>();

        if (animator == null || sr == null)
        {
            enabled = false;
        }
    }

    /// <summary>
    /// 从位于玩家层级下的 Transform（如 gunPos）取得身体 SpriteRenderer（用于武器 Sorting 等）。
    /// </summary>
    public static bool TryGetBodySpriteRenderer(Transform fromMountPoint, out SpriteRenderer spriteRenderer)
    {
        spriteRenderer = null;
        if (fromMountPoint == null)
        {
            return false;
        }

        PlayerVisuals pv = fromMountPoint.root.GetComponent<PlayerVisuals>();
        if (pv == null || pv.BodySpriteRenderer == null)
        {
            return false;
        }

        spriteRenderer = pv.BodySpriteRenderer;
        return true;
    }

    private void OnEnable()
    {
        if (playerEvents == null || animator == null || sr == null)
        {
            return;
        }

        playerEvents.OnMove += HandleMove;
        playerEvents.OnStopMove += HandleStop;
    }

    private void OnDisable()
    {
        if (playerEvents == null)
        {
            return;
        }

        playerEvents.OnMove -= HandleMove;
        playerEvents.OnStopMove -= HandleStop;
    }

    private void HandleMove(Vector2 dir)
    {
        //无 AnimatorController 时 SetBool 会刷屏报错；移动逻辑不依赖此处
        if (animator.runtimeAnimatorController != null)
        {
            animator.SetBool("isRun", true);
        }

        if (dir.x > 0.01f)
        {
            sr.flipX = false;
        }
        else if (dir.x < -0.01f)
        {
            sr.flipX = true;
        }
    }

    private void HandleStop()
    {
        if (animator.runtimeAnimatorController != null)
        {
            animator.SetBool("isRun", false);
        }
    }
}
