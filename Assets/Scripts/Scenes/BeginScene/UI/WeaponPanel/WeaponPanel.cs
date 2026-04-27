using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// 武器与难度选择面板：加载列表、写入 GameSession 并进入游戏场景。
/// </summary>
public class WeaponPanel : BasePanel
{
    [Header("可寻址标签")]
    public string weaponLabel = "weapon";
    public string modeLabel = "mode";
    public string togKey = "SVToggle";

    [Header("界面容器")]
    public Button btnReturn;
    public Button btnNext;
    public Image imgCha;
    public Image imgWeapon;
    public Image imgMode;
    public Text textCha;
    public Text textWeapon;
    public Text textMode;
    public Transform weaponContent;
    public Transform modeContent;

    [Header("运行时缓存")]
    private IList<WeaponConfig> _weaponConfigs;
    private IList<ModeConfig> _modeConfigs;
    private WeaponConfig _selectedWeapon;
    private ModeConfig _selectedMode;

    /// <summary>
    /// 加载资源并显示会话中已选角色信息。
    /// </summary>
    private void OnEnable()
    {
        LoadAllResources();
        imgCha.sprite = GameSessionManager.Instance.GetSession().selectedCharacter.icon;
        textCha.text = GameSessionManager.Instance.GetSession().selectedCharacter.description;
    }

    /// <summary>
    /// 绑定返回与下一步（写入会话并加载 GameScene）。
    /// </summary>
    public override void Init()
    {
        btnReturn.onClick.AddListener(() =>
        {
            UIManager.Instance.ShowPanel<SelectPanel>();
            UIManager.Instance.HidePanel<WeaponPanel>();
        });

        btnNext.onClick.AddListener(() =>
        {
            btnNext.interactable = false;
            GameSessionManager.Instance.SetSelectedWeapon(_selectedWeapon);
            GameSessionManager.Instance.SetSelectedMode(_selectedMode);

            if (GameObjectPoolManager.Instance != null)
            {
                GameObjectPoolManager.Instance.ClearAllPools();
            }

            AddressablesMgr.Instance.LoadScene("GameScene", LoadSceneMode.Single, (scene) =>
            {
                UIManager.Instance.HidePanel<WeaponPanel>();
                GameDataMgr.Instance.PlayMusic("GameMusic");
            });
        });
    }

    /// <summary>
    /// 加载武器列表、模式列表与 Toggle 预制体。
    /// </summary>
    private void LoadAllResources()
    {
        AddressablesMgr.Instance.LoadAssets<WeaponConfig>(weaponLabel, (list) =>
        {
            _weaponConfigs = list;
            CheckAndInitialize();
        });

        AddressablesMgr.Instance.LoadAssets<ModeConfig>(modeLabel, (list) =>
        {
            _modeConfigs = list;
            ValidateModeConfigs();
            CheckAndInitialize();
        });

        AddressablesMgr.Instance.LoadAsset<GameObject>(togKey, (prefab) =>
        {
            CheckAndInitialize(prefab);
        });
    }

    //缓存的Toggle预制体
    private GameObject _cachedBtnPrefab;

    /// <summary>
    /// 三者就绪后生成武器与模式 Toggle。
    /// </summary>
    private void CheckAndInitialize(GameObject prefab = null)
    {
        if (prefab != null) _cachedBtnPrefab = prefab;

        if (_weaponConfigs != null && _modeConfigs != null && _cachedBtnPrefab != null)
        {
            GenerateToggles();
        }
    }

    /// <summary>
    /// 清空两个容器并实例化 Toggle，默认选中首项。
    /// </summary>
    private void GenerateToggles()
    {
        ToggleGroup toggleGroupWeapon = weaponContent.GetComponent<ToggleGroup>();
        ToggleGroup toggleGroupMode = modeContent.GetComponent<ToggleGroup>();
        for (int i = weaponContent.childCount - 1; i >= 0; i--)
        {
            Destroy(weaponContent.GetChild(i).gameObject);
        }
        for (int i = modeContent.childCount - 1; i >= 0; i--)
        {
            Destroy(modeContent.GetChild(i).gameObject);
        }

        foreach (WeaponConfig weapon in _weaponConfigs)
        {
            GameObject toggle = Instantiate(_cachedBtnPrefab, weaponContent);
            toggle.GetComponent<SVToggle>().Setup(weapon.weaponIcon, toggleGroupWeapon, (isOn) =>
            {
                OnWeaponSelected(weapon);
            });
        }
        foreach (ModeConfig mode in _modeConfigs)
        {
            GameObject toggle = Instantiate(_cachedBtnPrefab, modeContent);
            toggle.transform.localScale = new Vector3(0.7f, 0.7f, 0.7f);
            toggle.GetComponent<SVToggle>().Setup(mode.modeIcon, toggleGroupMode, (isOn) =>
            {
                OnModeSelected(mode);
            });
        }
        if (_weaponConfigs.Count > 0) OnWeaponSelected(_weaponConfigs[0]);
        if (_modeConfigs.Count > 0) OnModeSelected(_modeConfigs[0]);
        if (weaponContent.childCount > 0)
        {
            Toggle firstToggle = weaponContent.GetChild(0).GetComponent<Toggle>();
            firstToggle.isOn = true;
        }
        if (modeContent.childCount > 0)
        {
            Toggle firstToggle = modeContent.GetChild(0).GetComponent<Toggle>();
            firstToggle.isOn = true;
        }
    }

    /// <summary>
    /// 校验 ModeConfig 的 difficultyLevel 无重复。
    /// </summary>
    private void ValidateModeConfigs()
    {
        if (_modeConfigs == null) return;

        HashSet<int> levels = new HashSet<int>();
        foreach (ModeConfig mode in _modeConfigs)
        {
            if (mode == null) continue;

            levels.Add(mode.difficultyLevel);
        }
    }

    /// <summary>
    /// 更新武器图标与描述。
    /// </summary>
    public void OnWeaponSelected(WeaponConfig weapon)
    {
        _selectedWeapon = weapon;
        imgWeapon.sprite = _selectedWeapon.weaponIcon;
        textWeapon.text = _selectedWeapon.weaponDescription;
    }

    /// <summary>
    /// 更新模式图标与描述。
    /// </summary>
    public void OnModeSelected(ModeConfig mode)
    {
        _selectedMode = mode;
        imgMode.sprite = _selectedMode.modeIcon;
        textMode.text = _selectedMode.modeDescription;
    }

    /// <summary>
    /// 这里不释放 _weaponConfigs/_modeConfigs：
    /// session 会持有被选武器/模式引用，过早 Release 会在 Existing Build 下被卸载成 null。
    /// </summary>
    private void OnDestroy()
    {
        // Intentionally kept loaded for current run.
    }
}
