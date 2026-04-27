using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 选角面板：加载角色列表、生成 Toggle、展示属性与描述。
/// </summary>
public class SelectPanel : BasePanel
{
    [Header("可寻址键")]
    public string globalConfigKey = "GlobalStatConfig";
    public string characterLabel = "SpecialConfig";
    public string toggleCharacterKey = "SVToggle";

    [Header("界面容器")]
    public Transform content;
    public List<Text> textStats;
    public Text textDescription;
    public Button btnReturn;
    public Button btnNext;

    private GlobalStatConfig _globalConfig;
    private IList<SpecialStatConfig> _specialConfigs;
    //当前被选中的角色
    private SpecialStatConfig _selectedCharacter;

    /// <summary>
    /// 触发异步加载资源。
    /// </summary>
    private void OnEnable()
    {
        LoadAllResources();
    }

    /// <summary>
    /// 绑定返回与下一步（写入 GameSession 并打开武器面板）。
    /// </summary>
    public override void Init()
    {
        btnReturn.onClick.AddListener(() =>
        {

            UIManager.Instance.ShowPanel<BeginPanel>();
            UIManager.Instance.HidePanel<SelectPanel>();
        });
        btnNext.onClick.AddListener(() =>
        {
            GameSessionManager.Instance.SetSelectedCharacter(_selectedCharacter);
            UIManager.Instance.ShowPanel<WeaponPanel>();
            UIManager.Instance.HidePanel<SelectPanel>();
        });
    }

    /// <summary>
    /// 并行加载全局配置、角色列表与 Toggle 预制体。
    /// </summary>
    private void LoadAllResources()
    {
        AddressablesMgr.Instance.LoadAsset<GlobalStatConfig>(globalConfigKey, (config) =>
        {
            _globalConfig = config;
            CheckAndInitialize();
        });

        AddressablesMgr.Instance.LoadAssets<SpecialStatConfig>(characterLabel, (list) =>
        {
            _specialConfigs = list;
            CheckAndInitialize();
        });

        AddressablesMgr.Instance.LoadAsset<GameObject>(toggleCharacterKey, (prefab) =>
        {
            CheckAndInitialize(prefab);
        });
    }

    //缓存的Toggle预制体
    private GameObject _cachedBtnPrefab;

    /// <summary>
    /// 三者就绪后生成 Toggle 列表。
    /// </summary>
    private void CheckAndInitialize(GameObject prefab = null)
    {
        if (prefab != null) _cachedBtnPrefab = prefab;

        if (_globalConfig != null && _specialConfigs != null && _cachedBtnPrefab != null)
        {
            GenerateToggles();
        }
    }

    /// <summary>
    /// 清空容器并实例化角色 Toggle，默认选中第一项。
    /// </summary>
    private void GenerateToggles()
    {
        ToggleGroup toggleGroup = content.GetComponent<ToggleGroup>();
        for (int i = content.childCount - 1; i >= 0; i--)
        {
            Destroy(content.GetChild(i).gameObject);
        }

        foreach (SpecialStatConfig character in _specialConfigs)
        {
            GameObject toggle = Instantiate(_cachedBtnPrefab, content);
            toggle.GetComponent<SVToggle>().Setup(character.icon, toggleGroup, (isOn) =>
            {
                OnCharacterSelected(character);
            });
        }
        if (_specialConfigs.Count > 0) OnCharacterSelected(_specialConfigs[0]);
        if (content.childCount > 0)
        {
            Toggle firstToggle = content.GetChild(0).GetComponent<Toggle>();
            firstToggle.isOn = true;
        }
    }

    /// <summary>
    /// 更新描述与属性列表。
    /// </summary>
    public void OnCharacterSelected(SpecialStatConfig character)
    {
        _selectedCharacter = character;
        textDescription.text = _selectedCharacter.description;
        RefreshStatus(_selectedCharacter);
    }

    /// <summary>
    /// 将全局默认与角色加成合并后刷新各 Stat 文本。
    /// </summary>
    private void RefreshStatus(SpecialStatConfig character)
    {
        for (int i = 0; i < textStats.Count; i++)
        {
            StatType type = (StatType)i;
            float baseVal = _globalConfig.GetDefaultValue(type);
            float bonus = character.initialStats.Where(m => m.type == type).Sum(m => m.value);

            float final = baseVal + bonus;
            textStats[i].text = FormatText(type, final, bonus);
        }
    }

    /// <summary>
    /// 有加成时用颜色标注括号内差值。
    /// </summary>
    private string FormatText(StatType statType, float final, float bonus)
    {
        string color = bonus > 0 ? "#007801" : (bonus < 0 ? "#FF4444" : "#FFFFFF");
        string sign = bonus > 0 ? "+" : "";
        string finalText = FormatValue(statType, final);
        string bonusText = FormatValue(statType, bonus);

        if (bonus == 0)
        {
            return finalText;
        }
        return $"{finalText} <color={color}>({sign}{bonusText})</color>";
    }

    /// <summary>
    /// 将属性按浮点格式显示（最多两位小数）。
    /// </summary>
    private string FormatValue(StatType statType, float value)
    {
        return value.ToString("0.##");
    }

    /// <summary>
    /// 这里不释放 _specialConfigs：
    /// session 会持有被选角色引用，过早 Release 会在 Existing Build 下被卸载成 null。
    /// </summary>
    private void OnDestroy()
    {
        AddressablesMgr.Instance.Release(_globalConfig);
    }
}
