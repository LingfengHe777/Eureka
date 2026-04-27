using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// UI 面板基类：CanvasGroup 淡入淡出与显隐状态。
/// </summary>
public abstract class BasePanel : MonoBehaviour
{
    //面板的淡入淡出
    private CanvasGroup canvasGroup;
    //淡入淡出的速度
    private float alphaSpeed = 10;
    //面板是否需要显示
    public bool isShow = false;
    //隐藏面板后的委托函数
    private UnityAction hideCallBack = null;

    /// <summary>
    /// 获取或添加 CanvasGroup，初始为隐藏。
    /// </summary>
    protected virtual void Awake()
    {
        canvasGroup = this.GetComponent<CanvasGroup>();
        if (canvasGroup == null)
        {
            canvasGroup = this.gameObject.AddComponent<CanvasGroup>();
        }
        canvasGroup.alpha = 0;
        canvasGroup.interactable = false;
        canvasGroup.blocksRaycasts = false;
    }

    /// <summary>
    /// 调用子类实现的 Init。
    /// </summary>
    protected virtual void Start()
    {
        Init();
    }

    /// <summary>
    /// 子类在此绑定控件与事件。
    /// </summary>
    public abstract void Init();

    /// <summary>
    /// 显示：重置透明度并置顶。
    /// </summary>
    public virtual void ShowMe()
    {
        canvasGroup.alpha = 0;
        canvasGroup.interactable = true;
        canvasGroup.blocksRaycasts = true;
        transform.SetAsLastSibling();
        isShow = true;
    }

    /// <summary>
    /// 隐藏：开始淡出并在结束时调用回调。
    /// </summary>
    public virtual void HideMe(UnityAction callBack)
    {
        canvasGroup.alpha = 1;
        canvasGroup.interactable = false;
        canvasGroup.blocksRaycasts = false;
        isShow = false;
        hideCallBack = callBack;
    }

    /// <summary>
    /// 每帧插值透明度直至完全显示或隐藏。
    /// </summary>
    protected virtual void Update()
    {
        if (isShow && canvasGroup.alpha != 1)
        {
            canvasGroup.alpha += alphaSpeed * Time.unscaledDeltaTime;
            if (canvasGroup.alpha >= 1)
            {
                canvasGroup.alpha = 1;
            }
        }
        if (!isShow && canvasGroup.alpha != 0)
        {
            canvasGroup.alpha -= alphaSpeed * Time.unscaledDeltaTime;
            if (canvasGroup.alpha <= 0)
            {
                canvasGroup.alpha = 0;
                hideCallBack?.Invoke();
            }
        }
    }
}
