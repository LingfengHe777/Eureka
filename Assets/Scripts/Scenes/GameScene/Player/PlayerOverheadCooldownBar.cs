using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 玩家头顶冷却条（世界空间）：
/// - 由技能脚本主动驱动 SetCooldown
/// - 冷却中显示，冷却完成时隐藏
/// </summary>
[DisallowMultipleComponent]
public class PlayerOverheadCooldownBar : MonoBehaviour
{
    [SerializeField] private GameObject cooldownBarRoot;
    [SerializeField] private Image cooldownFillImage;
    [SerializeField] private float readyEpsilon = 0.001f;

    /// <summary>
    /// 更新冷却显示（remaining/total 均为秒）。
    /// </summary>
    public void SetCooldown(float remaining, float total)
    {
        if (total <= 0f)
        {
            SetBarVisible(false);
            return;
        }

        float rem = Mathf.Max(0f, remaining);
        float progress = Mathf.Clamp01(1f - rem / total);

        if (cooldownFillImage != null)
        {
            cooldownFillImage.fillAmount = progress;
        }

        bool ready = rem <= readyEpsilon;
        SetBarVisible(!ready);
    }

    public void HideImmediately()
    {
        SetBarVisible(false);
        if (cooldownFillImage != null)
        {
            cooldownFillImage.fillAmount = 1f;
        }
    }

    private void SetBarVisible(bool visible)
    {
        if (cooldownBarRoot != null)
        {
            cooldownBarRoot.SetActive(visible);
            return;
        }

        if (cooldownFillImage != null)
        {
            cooldownFillImage.gameObject.SetActive(visible);
        }
    }
}

