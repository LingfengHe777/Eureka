using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 飘字表现组件（仅负责动画与文本显示，不负责事件订阅）
/// </summary>
public class CombatPopupView : MonoBehaviour
{
    [Header("界面引用")]
    //飘字RectTransform
    [SerializeField]
    private RectTransform rectTransform;

    //飘字CanvasGroup
    [SerializeField]
    private CanvasGroup canvasGroup;

    //飘字文本
    [SerializeField]
    private Text contentText;

    [Header("动画参数")]
    //飘字持续时间
    [SerializeField]
    private float duration = 0.55f;

    //飘字上升距离
    [SerializeField]
    private float riseDistance = 60f;

    //飘字随机偏移范围
    [SerializeField]
    private Vector2 randomOffset = new Vector2(18f, 8f);

    //飘字缩放曲线
    [SerializeField]
    private AnimationCurve scaleCurve = new AnimationCurve(new Keyframe(0f, 0.65f),
                                                            new Keyframe(0.14f, 1.18f),
                                                            new Keyframe(1f, 1f));

    //飘字透明度曲线
    [SerializeField]
    private AnimationCurve alphaCurve = new AnimationCurve(new Keyframe(0f, 0f),
                                                            new Keyframe(0.08f, 1f),
                                                            new Keyframe(0.75f, 1f),
                                                            new Keyframe(1f, 0f));

    //飘字移动曲线
    [SerializeField]
    private AnimationCurve moveCurve = new AnimationCurve(new Keyframe(0f, 0f), new Keyframe(1f, 1f));

    //飘字播放协程
    private Coroutine playCoroutine;

    /// <summary>
    /// 播放飘字（text/color/anchoredStart/onFinished；visualScaleMultiplier 暴击可大于 1）。
    /// </summary>
    public void Play(string text, Color color, Vector2 anchoredStart, Action onFinished, float visualScaleMultiplier = 1f)
    {
        if (rectTransform == null) rectTransform = GetComponent<RectTransform>();
        if (canvasGroup == null) canvasGroup = GetComponent<CanvasGroup>();
        if (contentText == null) contentText = GetComponentInChildren<Text>();

        if (rectTransform == null || canvasGroup == null || contentText == null)
        {
            onFinished?.Invoke();
            return;
        }

        if (playCoroutine != null)
        {
            StopCoroutine(playCoroutine);
            playCoroutine = null;
        }

        contentText.text = text;
        contentText.color = color;

        float offsetX = UnityEngine.Random.Range(-randomOffset.x, randomOffset.x);
        float offsetY = UnityEngine.Random.Range(-randomOffset.y, randomOffset.y);
        Vector2 start = anchoredStart + new Vector2(offsetX, offsetY);
        Vector2 end = start + new Vector2(0f, riseDistance);

        playCoroutine = StartCoroutine(PlayRoutine(start, end, onFinished, Mathf.Max(0.01f, visualScaleMultiplier)));
    }

    /// <summary>
    /// 播放飘字协程（start/end/onFinished；返回协程）。
    /// </summary>
    private IEnumerator PlayRoutine(Vector2 start, Vector2 end, Action onFinished, float visualScaleMultiplier)
    {
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            rectTransform.anchoredPosition = Vector2.LerpUnclamped(start, end, moveCurve.Evaluate(t));
            float s = scaleCurve.Evaluate(t) * visualScaleMultiplier;
            rectTransform.localScale = new Vector3(s, s, 1f);
            canvasGroup.alpha = alphaCurve.Evaluate(t);

            yield return null;
        }
        playCoroutine = null;
        onFinished?.Invoke();
    }

    /// <summary>
    /// 当飘字对象被禁用时，停止播放飘字协程
    /// </summary>
    private void OnDisable()
    {
        if (playCoroutine != null)
        {
            StopCoroutine(playCoroutine);
            playCoroutine = null;
        }

        if (rectTransform != null)
        {
            rectTransform.localScale = Vector3.one;
            rectTransform.anchoredPosition = Vector2.zero;
        }

        if (canvasGroup != null)
        {
            canvasGroup.alpha = 1f;
        }
    }
}
