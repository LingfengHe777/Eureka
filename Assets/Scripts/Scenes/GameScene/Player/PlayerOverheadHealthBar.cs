using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 玩家头顶世界空间血条：与 HUD（GamePanel）同源，监听 PlayerEvents.OnHealthChanged。
/// 满血时隐藏整根血条，受伤后显示；回满或死亡后隐藏。
/// 挂在玩家预制体下血条根节点（或任意子物体），在 Inspector 绑定血条根物体与填充 Image。
/// </summary>
[DisallowMultipleComponent]
public class PlayerOverheadHealthBar : MonoBehaviour
{
    [Header("引用")]
    [SerializeField]
    [Tooltip("头顶血条根节点，控制显隐，满血或死亡时隐藏")]
    //血条根物体显隐
    private GameObject healthBarRoot;

    [SerializeField]
    [Tooltip("当前血量填充图，按 Image 填充量驱动")]
    //Fill Amount 填充图
    private Image imageCurrentHealth;

    //父级事件与生命
    private PlayerEvents _playerEvents;
    private PlayerHealth _playerHealth;

    private void Awake()
    {
        ResolvePlayerRefs();
    }

    private void OnEnable()
    {
        ResolvePlayerRefs();
        Subscribe();
        RefreshFromSources();
    }

    private void OnDisable()
    {
        Unsubscribe();
    }

    private void ResolvePlayerRefs()
    {
        _playerEvents = GetComponentInParent<PlayerEvents>();
        _playerHealth = GetComponentInParent<PlayerHealth>();
    }

    private void Subscribe()
    {
        if (_playerEvents == null)
        {
            return;
        }

        _playerEvents.OnHealthChanged -= HandleHealthChanged;
        _playerEvents.OnHealthChanged += HandleHealthChanged;

        _playerEvents.OnHealthFull -= HandleHealthFull;
        _playerEvents.OnHealthFull += HandleHealthFull;

        _playerEvents.OnDeath -= HandleDeath;
        _playerEvents.OnDeath += HandleDeath;
    }

    private void Unsubscribe()
    {
        if (_playerEvents == null)
        {
            return;
        }

        _playerEvents.OnHealthChanged -= HandleHealthChanged;
        _playerEvents.OnHealthFull -= HandleHealthFull;
        _playerEvents.OnDeath -= HandleDeath;
    }

    private void HandleHealthChanged(int currentHealth, int maxHealth)
    {
        ApplyHealthVisual(currentHealth, maxHealth);
    }

    private void HandleHealthFull()
    {
        RefreshFromSources();
    }

    private void HandleDeath()
    {
        SetBarVisible(false);
    }

    /// <summary>
    /// 从 PlayerHealth 读当前值（用于首帧或与事件顺序不确定时对齐 GamePanel）。
    /// </summary>
    private void RefreshFromSources()
    {
        if (_playerHealth != null)
        {
            ApplyHealthVisual(_playerHealth.GetCurrentHealth(), _playerHealth.GetMaxHealth());
            return;
        }

        if (_playerEvents == null)
        {
            SetBarVisible(false);
        }
    }

    private void ApplyHealthVisual(int currentHealth, int maxHealth)
    {
        if (imageCurrentHealth != null)
        {
            if (maxHealth > 0)
            {
                imageCurrentHealth.fillAmount = Mathf.Clamp01((float)currentHealth / maxHealth);
            }
            else
            {
                imageCurrentHealth.fillAmount = 0f;
            }
        }

        bool isFull = maxHealth > 0 && currentHealth >= maxHealth;
        bool isDead = currentHealth <= 0;

        if (isDead || isFull)
        {
            SetBarVisible(false);
        }
        else
        {
            SetBarVisible(true);
        }
    }

    private void SetBarVisible(bool visible)
    {
        if (healthBarRoot != null)
        {
            healthBarRoot.SetActive(visible);
            return;
        }

        if (imageCurrentHealth != null)
        {
            imageCurrentHealth.gameObject.SetActive(visible);
        }
    }
}
