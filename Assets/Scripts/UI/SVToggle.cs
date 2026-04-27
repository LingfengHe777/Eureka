using System;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 通用 Toggle 包装：切换背景色并通过布尔回调把 isOn 交给外部。
/// </summary>
public class SVToggle : MonoBehaviour
{
    public Image image;
    public Image imageBK;
    public Color selectedColor = Color.green;
    public Color normalColor = Color.white;

    private Toggle _toggle;

    /// <summary>
    /// 绑定 Sprite、ToggleGroup 与值变化回调。
    /// </summary>
    public void Setup(Sprite sprite, ToggleGroup group, Action<bool> onValueChanged)
    {
        image.sprite = sprite;

        _toggle = GetComponent<Toggle>();
        _toggle.group = group;

        _toggle.onValueChanged.RemoveAllListeners();
        _toggle.onValueChanged.AddListener((isOn) =>
        {
            imageBK.color = isOn ? selectedColor : normalColor;

            onValueChanged?.Invoke(isOn);
        });
    }
}
