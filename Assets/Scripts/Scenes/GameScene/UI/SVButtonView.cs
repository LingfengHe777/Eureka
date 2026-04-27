using UnityEngine;
using UnityEngine.UI;
using UnityEngine.AddressableAssets;
using UnityEngine.Serialization;

/// <summary>
/// ScrollView 条目按钮（SVButton）的挂载脚本。
/// StorePanel 只负责实例化并调用SetIcon，具体结构由SVButton预制体自行维护。
/// </summary>
public class SVButtonView : MonoBehaviour
{
    //当前打开弹窗的条目（全局唯一）
    private static SVButtonView activePopupOwner;
    //根节点 Button
    private Button selfButton;
    [Tooltip("道具或武器图标")]
    [SerializeField] private Image iconImage;
    [Tooltip("等级底色，用于武器品阶色")]
    [SerializeField] private Image levelBackgroundImage;
    [Tooltip("右下角数量，道具堆叠用，武器可隐藏")]
    [SerializeField] private Text stackCountText;
    [Tooltip("操作弹窗预制体可寻址引用")]
    [SerializeField] private AssetReference actionPopupPrefab;
    [Tooltip("弹窗父节点，可空则挂在根下")]
    [FormerlySerializedAs("popupParentOverride")]
    [SerializeField] private Transform popPos;
    //未找到 stackCountText 时只警告一次
    private bool warnedMissingStackCountText;
    //出售回调
    private System.Action sellAction;
    //合成回调
    private System.Action mergeAction;
    //出售价格
    private int sellPrice;
    private bool sellEnabled;
    private bool mergeEnabled;

    //弹窗实例
    private GameObject popupInstance;
    private SVButtonActionPopupView popupView;
    private bool popupLoadingRequested;
    private int popupOpenedFrame = -1;

    private void Awake()
    {
        BindSelfButton();

        if (iconImage == null)
        {
            Image[] images = GetComponentsInChildren<Image>(true);
            if (images != null)
            {
                foreach (Image img in images)
                {
                    if (img != null && img.gameObject != gameObject)
                    {
                        iconImage = img;
                        break;
                    }
                }
            }
        }

        if (stackCountText == null)
        {
            Text[] texts = GetComponentsInChildren<Text>(true);
            if (texts != null)
            {
                foreach (Text txt in texts)
                {
                    if (txt != null && txt.gameObject != gameObject)
                    {
                        stackCountText = txt;
                        break;
                    }
                }
            }
        }
    }

    /// <summary>
    /// 获取根节点 Button 并绑定打开弹窗
    /// </summary>
    private void BindSelfButton()
    {
        selfButton = GetComponent<Button>();
        if (selfButton == null)
        {
            return;
        }

        selfButton.onClick.RemoveListener(OnClickOpenActionPopup);
        selfButton.onClick.AddListener(OnClickOpenActionPopup);
    }

    /// <summary>
    /// 由SVButton的Button.onClick直接绑定调用
    /// </summary>
    public void OnClickOpenActionPopup()
    {
        if (!sellEnabled && !mergeEnabled)
        {
            return;
        }

        if (popupInstance != null)
        {
            popupInstance.SetActive(!popupInstance.activeSelf);
            if (popupInstance.activeSelf)
            {
                RegisterAsActivePopupOwner();
                SyncPopupPositionToPopPos();
                popupOpenedFrame = Time.frameCount;
                BringPopupToFront();
            }
            else if (activePopupOwner == this)
            {
                activePopupOwner = null;
            }
            return;
        }

        if (popupLoadingRequested) return;
        if (actionPopupPrefab == null || !actionPopupPrefab.RuntimeKeyIsValid())
        {
            return;
        }
        if (popPos == null)
        {
            return;
        }

        popupLoadingRequested = true;
        AddressablesMgr.Instance.LoadAsset<GameObject>(actionPopupPrefab, (prefab) =>
        {
            popupLoadingRequested = false;
            if (prefab == null)
            {
                return;
            }

            Transform parent = ResolvePopupParent();
            if (parent == null)
            {
                return;
            }
            popupInstance = Instantiate(prefab, parent, false);
            popupView = popupInstance.GetComponent<SVButtonActionPopupView>();
            if (popupView == null)
            {
                Destroy(popupInstance);
                popupInstance = null;
                return;
            }

            popupInstance.SetActive(false);
            RefreshPopupActions();
            popupInstance.SetActive(true);
            RegisterAsActivePopupOwner();
            SyncPopupPositionToPopPos();
            popupOpenedFrame = Time.frameCount;
            BringPopupToFront();
        });
    }

    /// <summary>
    /// 跟随锚点并处理点击外部关闭
    /// </summary>
    private void Update()
    {
        if (activePopupOwner != this) return;
        SyncPopupPositionToPopPos();
        HandleOutsideClickToClose();
    }

    /// <summary>
    /// 点击空白处关闭弹窗
    /// </summary>
    private void HandleOutsideClickToClose()
    {
        if (popupInstance == null || !popupInstance.activeSelf) return;
        if (Time.frameCount == popupOpenedFrame) return;
        if (!Input.GetMouseButtonDown(0)) return;

        if (IsPointerInsidePopup(Input.mousePosition) || IsPointerInsideTrigger(Input.mousePosition))
        {
            return;
        }

        HandleClosePopup();
    }

    /// <summary>
    /// 指针是否在弹窗 Rect 内
    /// </summary>
    private bool IsPointerInsidePopup(Vector2 screenPoint)
    {
        RectTransform popupRect = popupInstance != null ? popupInstance.transform as RectTransform : null;
        if (popupRect == null) return false;
        return RectTransformUtility.RectangleContainsScreenPoint(popupRect, screenPoint, GetEventCamera(popupRect));
    }

    /// <summary>
    /// 指针是否在本按钮区域内
    /// </summary>
    private bool IsPointerInsideTrigger(Vector2 screenPoint)
    {
        if (selfButton == null) return false;
        RectTransform triggerRect = selfButton.transform as RectTransform;
        if (triggerRect == null) return false;
        return RectTransformUtility.RectangleContainsScreenPoint(triggerRect, screenPoint, GetEventCamera(triggerRect));
    }

    /// <summary>
    /// Overlay 返回 null，Camera 模式返回 Canvas 相机
    /// </summary>
    private Camera GetEventCamera(RectTransform rectTransform)
    {
        Canvas canvas = rectTransform != null ? rectTransform.GetComponentInParent<Canvas>() : null;
        if (canvas == null || canvas.renderMode == RenderMode.ScreenSpaceOverlay)
        {
            return null;
        }
        return canvas.worldCamera;
    }

    /// <summary>
    /// 弹窗父节点为根 Canvas
    /// </summary>
    private Transform ResolvePopupParent()
    {
        Canvas canvas = GetComponentInParent<Canvas>();
        if (canvas == null || canvas.rootCanvas == null)
        {
            return null;
        }
        return canvas.rootCanvas.transform;
    }

    /// <summary>
    /// 将弹窗置于同级最前
    /// </summary>
    private void BringPopupToFront()
    {
        if (popupInstance == null) return;
        popupInstance.transform.SetAsLastSibling();
    }

    /// <summary>
    /// 登记为当前唯一打开弹窗的条目
    /// </summary>
    private void RegisterAsActivePopupOwner()
    {
        if (activePopupOwner != null && activePopupOwner != this)
        {
            activePopupOwner.HandleClosePopup();
        }
        activePopupOwner = this;
    }

    /// <summary>
    /// 将弹窗对齐到 popPos 屏幕位置
    /// </summary>
    private void SyncPopupPositionToPopPos()
    {
        if (popupInstance == null || !popupInstance.activeSelf) return;
        if (popPos == null) return;

        RectTransform popupRect = popupInstance.transform as RectTransform;
        RectTransform parentRect = popupRect != null ? popupRect.parent as RectTransform : null;
        RectTransform anchorRect = popPos as RectTransform;
        if (popupRect == null || parentRect == null || anchorRect == null)
        {
            popupInstance.transform.position = popPos.position;
            return;
        }

        Camera anchorCamera = GetEventCamera(anchorRect);
        Vector2 screenPoint = RectTransformUtility.WorldToScreenPoint(anchorCamera, anchorRect.position);
        Camera parentCamera = GetEventCamera(parentRect);
        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(parentRect, screenPoint, parentCamera, out Vector2 localPoint))
        {
            popupRect.anchoredPosition = localPoint;
        }
    }

    public void SetIcon(Sprite sprite)
    {
        if (iconImage == null)
        {
            return;
        }

        iconImage.sprite = sprite;
        iconImage.enabled = sprite != null;
    }

    /// <summary>
    /// 设置堆叠数量显示：
    /// - enableCountDisplay=false 时始终隐藏（武器栏用）
    /// - enableCountDisplay=true 且 count>1 时显示数量（道具栏用）
    /// </summary>
    public void SetStackCount(int count, bool enableCountDisplay)
    {
        if (stackCountText == null)
        {
            if (enableCountDisplay && count > 1 && !warnedMissingStackCountText)
            {
                warnedMissingStackCountText = true;
            }
            return;
        }

        bool shouldShow = enableCountDisplay && count > 1;
        if (stackCountText.gameObject.activeSelf != shouldShow)
        {
            stackCountText.gameObject.SetActive(shouldShow);
        }
        if (shouldShow)
        {
            stackCountText.text = count.ToString();
        }
    }

    public void SetSellAction(System.Action onSell, int price)
    {
        sellAction = onSell;
        sellPrice = Mathf.Max(0, price);
        sellEnabled = sellAction != null;
        RefreshPopupActions();
    }

    public void SetMergeAction(System.Action onMerge)
    {
        mergeAction = onMerge;
        mergeEnabled = mergeAction != null;
        RefreshPopupActions();
    }

    public void SetLevelBackground(bool enabled, Color color)
    {
        if (levelBackgroundImage == null) return;
        levelBackgroundImage.gameObject.SetActive(enabled);
        if (enabled)
        {
            levelBackgroundImage.color = color;
        }
    }

    /// <summary>
    /// 将当前出售/合成状态同步到已加载弹窗
    /// </summary>
    private void RefreshPopupActions()
    {
        if (popupView == null) return;

        popupView.Setup(
            sellEnabled,
            sellPrice,
            sellEnabled ? HandleSellClick : (System.Action)null,
            mergeEnabled,
            mergeEnabled ? HandleMergeClick : (System.Action)null,
            HandleClosePopup);
    }

    /// <summary>
    /// 出售按钮：执行回调并关闭
    /// </summary>
    private void HandleSellClick()
    {
        sellAction?.Invoke();
        HandleClosePopup();
    }

    /// <summary>
    /// 合成按钮：执行回调并关闭
    /// </summary>
    private void HandleMergeClick()
    {
        mergeAction?.Invoke();
        HandleClosePopup();
    }

    /// <summary>
    /// 隐藏弹窗并清除全局 owner
    /// </summary>
    private void HandleClosePopup()
    {
        if (popupInstance != null)
        {
            popupInstance.SetActive(false);
        }
        if (activePopupOwner == this)
        {
            activePopupOwner = null;
        }
    }

    /// <summary>
    /// 禁用时关闭弹窗
    /// </summary>
    private void OnDisable()
    {
        HandleClosePopup();
    }

    /// <summary>
    /// 销毁时清理静态引用与实例
    /// </summary>
    private void OnDestroy()
    {
        if (activePopupOwner == this)
        {
            activePopupOwner = null;
        }
        if (popupInstance != null)
        {
            Destroy(popupInstance);
            popupInstance = null;
            popupView = null;
        }
    }
}