using System;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 双按钮提示面板：可配置文案与取消/确认回调。
/// </summary>
public class TipsPanel : BasePanel
{
    [Header("界面")]
    public Text textTips;
    public Button buttonCancel;
    public Button buttonConfirm;

    private Action cancelAction;
    private Action confirmAction;

    /// <summary>
    /// 绑定取消与确认按钮点击。
    /// </summary>
    public override void Init()
    {
        if (buttonCancel != null)
        {
            buttonCancel.onClick.RemoveAllListeners();
            buttonCancel.onClick.AddListener(OnCancelClicked);
        }

        if (buttonConfirm != null)
        {
            buttonConfirm.onClick.RemoveAllListeners();
            buttonConfirm.onClick.AddListener(OnConfirmClicked);
        }
    }

    /// <summary>
    /// 设置提示文本、取消按钮动作、确认按钮动作。
    /// </summary>
    public void Setup(string tips, Action onCancel, Action onConfirm)
    {
        if (textTips != null)
        {
            textTips.text = tips ?? string.Empty;
        }

        cancelAction = onCancel;
        confirmAction = onConfirm;
    }

    /// <summary>
    /// 取消按钮：执行 cancelAction。
    /// </summary>
    private void OnCancelClicked()
    {
        cancelAction?.Invoke();
    }

    /// <summary>
    /// 确认按钮：执行 confirmAction。
    /// </summary>
    private void OnConfirmClicked()
    {
        confirmAction?.Invoke();
    }
}
