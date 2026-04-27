using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// 在指定EventTrigger事件上播放GameDataMgr音效
/// </summary>
public class UIAudioEvent : MonoBehaviour
{
    /// <summary>
    /// 音效资源名
    /// </summary>
    public enum AudioType
    {
        ButtonHover,
        ButtonClick,
        Coin
    }

    [Header("音效")]
    public AudioType sound = AudioType.ButtonHover;

    [Header("触发类型")]
    public EventTriggerType triggerType = EventTriggerType.PointerEnter;

    /// <summary>
    /// 确保存在EventTrigger并注册播放音效回调
    /// </summary>
    void Awake()
    {
        EventTrigger trigger = gameObject.GetComponent<EventTrigger>();
        if (trigger == null)
            trigger = gameObject.AddComponent<EventTrigger>();

        EventTrigger.Entry entry = new EventTrigger.Entry();
        entry.eventID = triggerType;
        entry.callback.AddListener((data) =>
        {
            GameDataMgr.Instance.PlayAudio(GameDataMgr.AudioType.Sound, sound.ToString());
        });
        trigger.triggers.Add(entry);
    }
}
