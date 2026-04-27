using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.AddressableAssets;

/// <summary>
/// 飘字管理器：监听战斗事件、负责飘字实例化、坐标转换与回收
/// </summary>
public class CombatPopupManager : MonoBehaviour
{
    [Header("可寻址")]
    //飘字预制体引用
    [HideInInspector]
    [SerializeField] private AssetReferenceGameObject popupPrefabReference;

    [Header("显示参数")]
    [SerializeField]
    private Color damageColor = Color.white;

    [SerializeField]
    private Color critDamageColor = new Color(1f, 0.85f, 0.2f, 1f);

    [SerializeField]
    private Color dodgeColor = new Color(0.35f, 1f, 1f, 1f);

    [SerializeField]
    [Tooltip("暴击飘字相对普通伤害的缩放")]
    private float critDamageScaleMultiplier = 1.35f;

    //飘字伤害偏移（相对于目标位置）
    [SerializeField]
    private float damageYOffset = 0.75f;

    //飘字闪避偏移（相对于玩家位置）
    [SerializeField]
    private float dodgeYOffset = 1.1f;

    //飘字Canvas排序顺序
    [SerializeField]
    private int canvasSortOrder = 300;

    [Header("伤害飘字合并")]
    [SerializeField]
    [Tooltip("同一目标合并伤害的统计窗口，单位秒")]
    private float damageMergeWindow = 0.08f;
    [SerializeField]
    [Tooltip("同一目标分层条数，减轻重叠")]
    private int damageLaneCount = 3;
    [SerializeField]
    [Tooltip("分层纵向步长，世界单位")]
    private float damageLaneVerticalStep = 0.18f;
    [SerializeField]
    [Tooltip("分层横向步长，世界单位")]
    private float damageLaneHorizontalStep = 0.12f;

    //玩家事件引用
    private PlayerEvents playerEvents;

    //玩家位置引用
    private Transform playerTransform;

    //飘字预制体引用
    private GameObject popupPrefab;

    //飘字Canvas
    private Canvas popupCanvas;

    //飘字根RectTransform
    private RectTransform popupRootRect;

    //是否已初始化
    private bool isInitialized;

    private readonly Dictionary<int, PendingDamagePopup> pendingDamageByTarget = new();
    private readonly Dictionary<int, int> laneCursorByTarget = new();
    private readonly List<int> flushKeys = new();

    private class PendingDamagePopup
    {
        public Transform targetTransform;
        public Vector3 fallbackPosition;
        public float totalDamage;
        public float flushTime;
        public int laneIndex;
        public bool anyCrit;
    }

    /// <summary>
    /// 初始化飘字管理器（eventsSource 玩家事件；player 位置；prefabReference 预制体）。
    /// </summary>
    public void Initialize(PlayerEvents eventsSource, Transform player, AssetReferenceGameObject prefabReference)
    {
        if (isInitialized)
        {
            UnbindEvents();
        }

        playerEvents = eventsSource;
        playerTransform = player;
        popupPrefabReference = prefabReference;

        EnsurePopupCanvas();
        LoadPopupPrefab();
        BindEvents();
        isInitialized = true;
    }

    private void Update()
    {
        FlushMergedDamagePopups();
    }

    /// <summary>
    /// 绑定事件
    /// </summary>
    private void BindEvents()
    {
        if (playerEvents == null)
        {
            return;
        }

        playerEvents.OnDealDamage += HandleDealDamage;
        playerEvents.OnDodgeSuccess += HandleDodgeSuccess;
    }

    /// <summary>
    /// 解除事件绑定
    /// </summary>
    private void UnbindEvents()
    {
        if (playerEvents == null) return;
        playerEvents.OnDealDamage -= HandleDealDamage;
        playerEvents.OnDodgeSuccess -= HandleDodgeSuccess;
    }

    /// <summary>
    /// 确保飘字Canvas存在
    /// </summary>
    private void EnsurePopupCanvas()
    {
        if (popupCanvas != null && popupRootRect != null) return;

        GameObject canvasObj = new GameObject("CombatPopupCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        canvasObj.transform.SetParent(transform, false);

        popupCanvas = canvasObj.GetComponent<Canvas>();

        popupCanvas.renderMode = RenderMode.ScreenSpaceOverlay;

        popupCanvas.sortingOrder = canvasSortOrder;

        CanvasScaler scaler = canvasObj.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;

        popupRootRect = canvasObj.GetComponent<RectTransform>();
        popupRootRect.anchorMin = Vector2.zero;
        popupRootRect.anchorMax = Vector2.one;
        popupRootRect.offsetMin = Vector2.zero;
        popupRootRect.offsetMax = Vector2.zero;
    }

    /// <summary>
    /// 加载飘字预制体
    /// </summary>
    private void LoadPopupPrefab()
    {
        if (popupPrefabReference == null || !popupPrefabReference.RuntimeKeyIsValid())
        {
            return;
        }

        AddressablesMgr.Instance.LoadAsset<GameObject>(popupPrefabReference, prefab =>
        {
            popupPrefab = prefab;
        });
    }

    /// <summary>
    /// 处理伤害事件（damage/target/isCrit）。
    /// </summary>
    private void HandleDealDamage(float damage, GameObject target, bool isCrit)
    {
        if (target == null) return;

        int targetId = target.GetInstanceID();
        float now = Time.unscaledTime;

        if (!pendingDamageByTarget.TryGetValue(targetId, out PendingDamagePopup pending))
        {
            pending = new PendingDamagePopup
            {
                targetTransform = target.transform,
                fallbackPosition = target.transform.position,
                totalDamage = 0f,
                flushTime = now + Mathf.Max(0.01f, damageMergeWindow),
                laneIndex = GetNextLaneIndex(targetId),
                anyCrit = false
            };
            pendingDamageByTarget[targetId] = pending;
        }

        pending.targetTransform = target.transform;
        pending.fallbackPosition = target.transform.position;
        pending.totalDamage += Mathf.Max(0f, damage);
        pending.flushTime = now + Mathf.Max(0.01f, damageMergeWindow);
        pending.anyCrit = pending.anyCrit || isCrit;
    }

    /// <summary>
    /// 处理闪避事件
    /// </summary>
    private void HandleDodgeSuccess()
    {
        if (playerTransform == null) return;
        Vector3 worldPos = playerTransform.position + Vector3.up * dodgeYOffset;
        ShowPopup("DODGE", dodgeColor, worldPos);
    }

    /// <summary>
    /// 显示飘字（text/color/worldPosition；visualScaleMultiplier 暴击可大于 1）。
    /// </summary>
    private void ShowPopup(string text, Color color, Vector3 worldPosition, float visualScaleMultiplier = 1f)
    {
        if (popupPrefab == null || popupRootRect == null) return;

        Vector2 anchoredPos = WorldToCanvasAnchoredPosition(worldPosition);
        GameObject popupObj = GameObjectPoolManager.Instance.Spawn(popupPrefab, Vector3.zero, Quaternion.identity, popupRootRect);
        if (popupObj == null) return;

        CombatPopupView view = popupObj.GetComponent<CombatPopupView>();
        if (view == null)
        {
            GameObjectPoolManager.Instance.Release(popupObj);
            return;
        }

        view.Play(text, color, anchoredPos, () =>
        {
            GameObjectPoolManager.Instance.Release(popupObj);
        }, visualScaleMultiplier);
    }

    /// <summary>
    /// 合并窗口到期后刷新伤害飘字
    /// </summary>
    private void FlushMergedDamagePopups()
    {
        if (pendingDamageByTarget.Count == 0) return;

        float now = Time.unscaledTime;
        flushKeys.Clear();
        CollectFlushKeys(now);

        for (int i = 0; i < flushKeys.Count; i++)
        {
            FlushSinglePendingPopup(flushKeys[i]);
        }
    }

    private void CollectFlushKeys(float now)
    {
        foreach (KeyValuePair<int, PendingDamagePopup> kv in pendingDamageByTarget)
        {
            PendingDamagePopup pending = kv.Value;
            if (pending == null || now >= pending.flushTime)
            {
                flushKeys.Add(kv.Key);
            }
        }
    }

    private void FlushSinglePendingPopup(int key)
    {
        if (!pendingDamageByTarget.TryGetValue(key, out PendingDamagePopup pending) || pending == null)
        {
            pendingDamageByTarget.Remove(key);
            return;
        }

        Vector3 basePos = pending.targetTransform != null ? pending.targetTransform.position : pending.fallbackPosition;
        Vector3 laneOffset = GetLaneWorldOffset(pending.laneIndex);
        Vector3 worldPos = basePos + Vector3.up * damageYOffset + laneOffset;
        Color popupColor = pending.anyCrit ? critDamageColor : damageColor;
        float scale = pending.anyCrit ? Mathf.Max(0.01f, critDamageScaleMultiplier) : 1f;
        ShowPopup(Mathf.RoundToInt(pending.totalDamage).ToString(), popupColor, worldPos, scale);
        pendingDamageByTarget.Remove(key);
    }

    /// <summary>
    /// 取下一分层索引（同一目标轮换）
    /// </summary>
    private int GetNextLaneIndex(int targetId)
    {
        int lanes = Mathf.Max(1, damageLaneCount);
        if (!laneCursorByTarget.TryGetValue(targetId, out int cursor))
        {
            cursor = 0;
        }

        int lane = cursor % lanes;
        laneCursorByTarget[targetId] = (cursor + 1) % lanes;
        return lane;
    }

    /// <summary>
    /// 分层横向/纵向世界偏移
    /// </summary>
    private Vector3 GetLaneWorldOffset(int laneIndex)
    {
        if (damageLaneCount <= 1)
        {
            return Vector3.zero;
        }

        int pattern = laneIndex % 3;
        float xSign = pattern == 0 ? 0f : (pattern == 1 ? -1f : 1f);
        float y = Mathf.Max(0f, laneIndex) * damageLaneVerticalStep;
        return new Vector3(xSign * damageLaneHorizontalStep, y, 0f);
    }

    /// <summary>
    /// 世界坐标转 Canvas 锚点（返回本地锚点）。
    /// </summary>
    private Vector2 WorldToCanvasAnchoredPosition(Vector3 worldPosition)
    {
        Camera cam = Camera.main;
        Vector2 screenPos = RectTransformUtility.WorldToScreenPoint(cam, worldPosition);
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            popupRootRect,
            screenPos,
            null,
            out Vector2 localPoint);

        return localPoint;
    }

    /// <summary>
    /// 当飘字管理器销毁时，解除事件绑定并释放飘字预制体
    /// </summary>
    private void OnDestroy()
    {
        UnbindEvents();
        pendingDamageByTarget.Clear();
        laneCursorByTarget.Clear();
        flushKeys.Clear();
        if (popupPrefab != null)
        {
            AddressablesMgr.Instance.Release(popupPrefab);
            popupPrefab = null;
        }
    }
}
