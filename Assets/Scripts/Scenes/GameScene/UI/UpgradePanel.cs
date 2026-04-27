using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 升级面板：
/// 每次显示随机提供 3 个道具，玩家可选择 1 个或跳过。
/// </summary>
public class UpgradePanel : BasePanel
{
    [Header("可寻址键")]
    [Tooltip("升级池道具配置的可寻址标签")]
    public string itemLabel = "ItemConfig";

    [Header("选项")]
    public UpgradeOptionView option1;
    public UpgradeOptionView option2;
    public UpgradeOptionView option3;

    [Header("跳过按钮")]
    public Button buttonSkip;

    //道具池（Addressables）
    private readonly List<ItemConfig> itemPool = new();
    private bool itemPoolLoaded;
    private bool itemPoolLoadRequested;
    private bool isInitialized;
    private Coroutine prepareOptionsCoroutine;
    private Action onSelectionCompleted;
    private InventoryManager currentInventoryManager;
    //当前三条选项
    private readonly List<ItemConfig> currentOptions = new(3);

    /// <summary>
    /// 绑定跳过与选项按钮并加载道具池
    /// </summary>
    public override void Init()
    {
        if (isInitialized) return;
        isInitialized = true;

        if (buttonSkip != null)
        {
            buttonSkip.onClick.RemoveAllListeners();
            buttonSkip.onClick.AddListener(OnSkipClicked);
        }

        SetupOptionButton(option1, 0);
        SetupOptionButton(option2, 1);
        SetupOptionButton(option3, 2);

        LoadItemPool();
    }

    /// <summary>
    /// 由 WaveManager 调用，开始一次升级选择。
    /// </summary>
    public void BeginSelection(int playerLevel, InventoryManager inventoryManager, Action onCompleted)
    {
        if (!isInitialized)
        {
            Init();
        }

        currentInventoryManager = inventoryManager;
        onSelectionCompleted = onCompleted;


        if (prepareOptionsCoroutine != null)
        {
            StopCoroutine(prepareOptionsCoroutine);
        }
        prepareOptionsCoroutine = StartCoroutine(PrepareOptionsCoroutine(playerLevel));
    }

    /// <summary>
    /// 绑定单个选项按钮
    /// </summary>
    private void SetupOptionButton(UpgradeOptionView optionView, int index)
    {
        if (optionView == null || optionView.button == null) return;

        optionView.button.onClick.RemoveAllListeners();
        optionView.button.onClick.AddListener(() => OnOptionClicked(index));
    }

    /// <summary>
    /// 从 Addressables 加载全部 ItemConfig 到内存池
    /// </summary>
    private void LoadItemPool()
    {
        if (itemPoolLoadRequested) return;
        itemPoolLoadRequested = true;

        AddressablesMgr.Instance.LoadAssets<ItemConfig>(itemLabel, (items) =>
        {
            itemPoolLoaded = true;
            itemPool.Clear();
            if (items == null) return;

            for (int i = 0; i < items.Count; i++)
            {
                ItemConfig item = items[i];
                if (item != null)
                {
                    itemPool.Add(item);
                }
            }
        });
    }

    /// <summary>
    /// 等待道具池就绪后生成三条选项并刷新 UI
    /// </summary>
    private System.Collections.IEnumerator PrepareOptionsCoroutine(int playerLevel)
    {
        if (!itemPoolLoaded)
        {
            LoadItemPool();
            float timeout = 0f;
            while (!itemPoolLoaded && timeout < 5f)
            {
                timeout += Time.unscaledDeltaTime;
                yield return null;
            }
        }

        GenerateOptions(playerLevel);
        RefreshOptionsUI();
        prepareOptionsCoroutine = null;
    }

    /// <summary>
    /// 按等级偏置随机抽三条不重复道具
    /// </summary>
    private void GenerateOptions(int playerLevel)
    {
        currentOptions.Clear();
        if (!itemPoolLoaded || itemPool.Count == 0)
        {
            return;
        }

        HashSet<ItemConfig> used = new();
        for (int i = 0; i < 3; i++)
        {
            ItemConfig picked = PickItemByLevelBias(playerLevel, used);
            if (picked == null) break;

            currentOptions.Add(picked);
            used.Add(picked);
        }
    }

    /// <summary>
    /// 按权重随机选取一个未使用道具
    /// </summary>
    private ItemConfig PickItemByLevelBias(int playerLevel, HashSet<ItemConfig> used)
    {
        List<ItemConfig> candidates = new();
        float totalWeight = 0f;

        for (int i = 0; i < itemPool.Count; i++)
        {
            ItemConfig item = itemPool[i];
            if (item == null) continue;
            if (used != null && used.Contains(item)) continue;

            float weight = GetItemWeightByPlayerLevel(item, playerLevel);
            if (weight <= 0f) continue;

            candidates.Add(item);
            totalWeight += weight;
        }

        if (candidates.Count == 0 || totalWeight <= 0f)
        {
            return null;
        }

        float roll = UnityEngine.Random.Range(0f, totalWeight);
        float accum = 0f;
        for (int i = 0; i < candidates.Count; i++)
        {
            ItemConfig item = candidates[i];
            accum += GetItemWeightByPlayerLevel(item, playerLevel);
            if (roll <= accum)
            {
                return item;
            }
        }

        return candidates[candidates.Count - 1];
    }

    /// <summary>
    /// 等级越高，高等级道具权重越高（仅影响抽取概率）
    /// </summary>
    private float GetItemWeightByPlayerLevel(ItemConfig item, int playerLevel)
    {
        int itemLevel = Mathf.Clamp(item.itemLevel, 1, 4);
        int tier = Mathf.Clamp(1 + (Mathf.Max(1, playerLevel) - 1) / 5, 1, 4);

        float[,] weights =
        {
            { 70f, 24f, 6f, 1f },
            { 45f, 32f, 16f, 7f },
            { 28f, 32f, 25f, 15f },
            { 15f, 26f, 32f, 27f }
        };

        return weights[tier - 1, itemLevel - 1];
    }

    /// <summary>
    /// 刷新三个选项槽显示
    /// </summary>
    private void RefreshOptionsUI()
    {
        RefreshOptionUI(option1, 0);
        RefreshOptionUI(option2, 1);
        RefreshOptionUI(option3, 2);
    }

    /// <summary>
    /// 刷新单个选项槽
    /// </summary>
    private void RefreshOptionUI(UpgradeOptionView view, int index)
    {
        if (view == null) return;

        bool hasOption = index < currentOptions.Count && currentOptions[index] != null;
        if (view.root != null)
        {
            view.root.SetActive(hasOption);
        }
        if (!hasOption) return;

        ItemConfig item = currentOptions[index];

        if (view.icon != null) view.icon.sprite = item.itemIcon;
        if (view.textName != null) view.textName.text = item.itemName;
        if (view.textDesc != null) view.textDesc.text = item.itemDescription;
        if (view.textLevel != null)
        {
            view.textLevel.text = "Lv" + item.itemLevel.ToString();
            view.textLevel.color = item.GetThemeColor();
        }

        if (view.button != null)
        {
            view.button.interactable = true;
        }
    }

    /// <summary>
    /// 选择道具并关闭
    /// </summary>
    private void OnOptionClicked(int index)
    {
        if (index < 0 || index >= currentOptions.Count) return;
        ItemConfig selected = currentOptions[index];
        if (selected == null) return;

        if (currentInventoryManager != null)
        {
            currentInventoryManager.AddItem(selected);
        }

        CompleteAndHide();
    }

    /// <summary>
    /// 跳过并关闭
    /// </summary>
    private void OnSkipClicked()
    {
        CompleteAndHide();
    }

    /// <summary>
    /// 隐藏面板并回调 WaveManager
    /// </summary>
    private void CompleteAndHide()
    {
        Action callback = onSelectionCompleted;
        onSelectionCompleted = null;
        currentInventoryManager = null;

        HideMe(() =>
        {
            callback?.Invoke();
        });
    }

    /// <summary>
    /// 停止协程并移除监听
    /// </summary>
    private void OnDestroy()
    {
        if (prepareOptionsCoroutine != null)
        {
            StopCoroutine(prepareOptionsCoroutine);
            prepareOptionsCoroutine = null;
        }

        if (buttonSkip != null)
        {
            buttonSkip.onClick.RemoveAllListeners();
        }
        CleanupOption(option1);
        CleanupOption(option2);
        CleanupOption(option3);
    }

    /// <summary>
    /// 移除选项按钮监听
    /// </summary>
    private void CleanupOption(UpgradeOptionView optionView)
    {
        if (optionView != null && optionView.button != null)
        {
            optionView.button.onClick.RemoveAllListeners();
        }
    }
}

/// <summary>
/// 升级面板单条选项的序列化引用
/// </summary>
[Serializable]
public class UpgradeOptionView
{
    public GameObject root;
    public Button button;
    public Image icon;
    public Text textName;
    public Text textDesc;
    public Text textLevel;
}
