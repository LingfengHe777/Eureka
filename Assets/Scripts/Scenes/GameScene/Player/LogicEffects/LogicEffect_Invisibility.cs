using UnityEngine;

/// <summary>
/// 隐身逻辑效果：
/// - 生效期间玩家免疫伤害
/// - 生效期间下调玩家可见 Sprite 的 Alpha
/// - 到时自动恢复
/// </summary>
public class LogicEffect_Invisibility : MonoBehaviour, IEffectComponent
{
    private float cooldownSeconds;
    private float durationSeconds;
    private float invisibleAlpha;
    private KeyCode activationKey;

    private bool isInitialized;
    private bool isActive;
    private float nextAvailableTime;
    private float invisibleEndTime;
    private PlayerHealth playerHealth;
    private PlayerOverheadCooldownBar overheadCooldownBar;
    private SpriteRenderer[] spriteRenderers;
    private float[] originalAlphas;

    public void ApplyConfig(LogicEffect_InvisibilitySO config)
    {
        if (config == null)
        {
            Debug.LogError("[LogicEffect_Invisibility] 配置为空。", this);
            enabled = false;
            return;
        }

        durationSeconds = Mathf.Max(0.01f, config.durationSeconds);
        cooldownSeconds = Mathf.Max(0f, config.cooldownSeconds);
        invisibleAlpha = Mathf.Clamp01(config.invisibleAlpha);
        activationKey = config.activationKey;
        playerHealth = GetComponent<PlayerHealth>();
        overheadCooldownBar = GetComponentInChildren<PlayerOverheadCooldownBar>(true);
        spriteRenderers = GetComponentsInChildren<SpriteRenderer>(true);
        originalAlphas = new float[spriteRenderers.Length];
        isInitialized = true;
        EndInvisibility();
        if (overheadCooldownBar != null)
        {
            overheadCooldownBar.HideImmediately();
        }
    }

    public void CleanupEffect()
    {
        EndInvisibility();
        isInitialized = false;
        nextAvailableTime = 0f;
        if (overheadCooldownBar != null)
        {
            overheadCooldownBar.HideImmediately();
        }
    }

    private void Update()
    {
        if (!enabled || !isInitialized)
        {
            return;
        }

        UpdateCooldownUI();

        if (isActive && Time.time >= invisibleEndTime)
        {
            EndInvisibility();
            return;
        }

        if (isActive)
        {
            return;
        }

        if (Time.time < nextAvailableTime)
        {
            return;
        }

        if (!Input.GetKeyDown(activationKey))
        {
            return;
        }

        StartInvisibility();
    }

    private void StartInvisibility()
    {
        isActive = true;
        nextAvailableTime = Time.time + cooldownSeconds;
        invisibleEndTime = Time.time + durationSeconds;

        if (playerHealth != null)
        {
            playerHealth.AddInvulnerability();
        }

        for (int i = 0; i < spriteRenderers.Length; i++)
        {
            SpriteRenderer sr = spriteRenderers[i];
            if (sr == null) continue;

            Color c = sr.color;
            originalAlphas[i] = c.a;
            c.a = invisibleAlpha;
            sr.color = c;
        }
    }

    private void EndInvisibility()
    {
        if (!isActive) return;

        isActive = false;
        if (playerHealth != null)
        {
            playerHealth.RemoveInvulnerability();
        }

        for (int i = 0; i < spriteRenderers.Length; i++)
        {
            SpriteRenderer sr = spriteRenderers[i];
            if (sr == null) continue;

            Color c = sr.color;
            c.a = originalAlphas[i];
            sr.color = c;
        }
    }

    private void UpdateCooldownUI()
    {
        if (overheadCooldownBar == null)
        {
            return;
        }

        overheadCooldownBar.SetCooldown(GetCooldownRemaining(), cooldownSeconds);
    }

    private float GetCooldownRemaining()
    {
        return Mathf.Max(0f, nextAvailableTime - Time.time);
    }
}

