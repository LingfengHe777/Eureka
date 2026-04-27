using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// SVButton 点击后弹出的操作面板。
/// 脚本只处理出售/合成按钮和出售价格文本。
/// </summary>
public class SVButtonActionPopupView : MonoBehaviour
{
    [SerializeField] private Button sellButton;
    [SerializeField] private Button mergeButton;
    [SerializeField] private Text sellPriceText;

    /// <summary>
    /// 配置出售/合成按钮与售价显示
    /// </summary>
    public void Setup(
        bool canSell,
        int sellPrice,
        System.Action onSell,
        bool canMerge,
        System.Action onMerge,
        System.Action onClose)
    {
        if (sellButton != null)
        {
            sellButton.gameObject.SetActive(canSell);
            sellButton.onClick.RemoveAllListeners();
            if (canSell && onSell != null)
            {
                sellButton.onClick.AddListener(() => onSell.Invoke());
            }
        }

        if (mergeButton != null)
        {
            mergeButton.gameObject.SetActive(canMerge);
            mergeButton.onClick.RemoveAllListeners();
            if (canMerge && onMerge != null)
            {
                mergeButton.onClick.AddListener(() => onMerge.Invoke());
            }
        }

        if (sellPriceText != null)
        {
            sellPriceText.gameObject.SetActive(canSell);
            if (canSell)
            {
                sellPriceText.text = $"Price：{sellPrice}";
            }
        }
    }
}
