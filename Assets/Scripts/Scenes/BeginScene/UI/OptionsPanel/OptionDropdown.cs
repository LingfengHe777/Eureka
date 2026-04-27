using TMPro;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// TMP_Dropdown 封装：对外抛出选中索引，供视频设置等绑定。
/// </summary>
public class OptionDropdown : MonoBehaviour
{
    public TMP_Dropdown dropdown;

    //对外抛出index
    public UnityEvent<int> onValueChanged;

    /// <summary>
    /// 绑定 dropdown 与内部转发。
    /// </summary>
    void Awake()
    {
        if (dropdown == null)
            dropdown = GetComponent<TMP_Dropdown>();

        dropdown.onValueChanged.AddListener(OnDropdownChanged);
    }

    /// <summary>
    /// 移除监听避免泄漏。
    /// </summary>
    void OnDestroy()
    {
        dropdown.onValueChanged.RemoveListener(OnDropdownChanged);
    }

    /// <summary>
    /// 转发索引到 onValueChanged。
    /// </summary>
    void OnDropdownChanged(int index)
    {
        onValueChanged?.Invoke(index);
    }

    /// <summary>
    /// 无通知设置下拉值（用于从存档刷新 UI）。
    /// </summary>
    public void SetValueWithoutNotify(int index)
    {
        dropdown.SetValueWithoutNotify(index);
    }

    /// <summary>
    /// 当前选中索引。
    /// </summary>
    public int GetValue()
    {
        return dropdown.value;
    }
}
