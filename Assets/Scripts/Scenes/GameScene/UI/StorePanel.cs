using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 商店面板
/// 在波次之间显示，允许玩家购买道具和武器
/// </summary>
public class StorePanel : BasePanel
{
    /// <summary>
    /// UIManager可能在Start/Init之前同步调用ShowMe；须在此发起SVButton异步加载，否则首次打开会误报未加载。
    /// </summary>
    protected override void Awake()
    {
        base.Awake();
        LoadSVButtonPrefab();
    }

    [Header("可寻址键")]
    [Tooltip("道具配置的可寻址标签")]
    public string itemLabel = "ItemConfig";

    [Tooltip("武器配置的可寻址标签")]
    public string weaponLabel = "WeaponConfig";

    [Tooltip("商店列表条目预制体的可寻址键")]
    public string svButtonKey = "SVButton";


    [Header("标题显示")]
    [Tooltip("标题文本，含商店名与当前波次")]
    public Text textTitle;

    [Tooltip("金币数量文本")]
    public Text textCoinCount;


    [Header("刷新按钮")]
    [Tooltip("刷新商店栏位")]
    public Button buttonRefresh;

    [Tooltip("刷新按钮内当前刷新价格")]
    public Text textRefreshPrice;


    [Header("商品槽位")]
    [Tooltip("商品槽位 1")]
    public ProductSlot slot1;

    [Tooltip("商品槽位 2")]
    public ProductSlot slot2;

    [Tooltip("商品槽位 3")]
    public ProductSlot slot3;

    [Tooltip("商品槽位 4")]
    public ProductSlot slot4;


    [Header("属性显示")]
    [Tooltip("各属性一行文本，共 14 项")]
    public List<Text> textStats;


    [Header("道具背包")]
    [Tooltip("道具列表 Content")]
    public Transform itemContent;


    [Header("武器背包")]
    [Tooltip("武器列表 Content")]
    public Transform weaponContent;

    [Tooltip("武器背包标题，显示容量与当前数量")]
    public Text textWeaponTitle;


    [Header("继续按钮")]
    [Tooltip("继续游戏")]
    public Button buttonNext;


    //所有可购买的道具池
    private List<ItemConfig> itemPool = new();

    //所有可购买的武器池
    private List<WeaponConfig> weaponPool = new();

    //保存原始列表引用（用于释放Addressables handle）
    private IList<ItemConfig> itemPoolOriginal;
    private IList<WeaponConfig> weaponPoolOriginal;

    private const int SLOT_COUNT = 4;

    //当前显示的商品（可能是道具或武器）
    private List<ScriptableObject> currentProducts = new(SLOT_COUNT);

    //槽位锁定状态（按槽位索引，而非按商品）
    private readonly bool[] slotLockedStates = new bool[SLOT_COUNT];

    //组件引用
    private CoinManager coinManager;
    private InventoryManager inventoryManager;
    private WeaponManager weaponManager;
    private StatHandler statHandler;
    private WaveManager waveManager;
    private GameMgr gameMgr;
    private PlayerEvents playerEvents;

    //ScrollView（Addressables）
    private GameObject svButtonPrefab;
    private bool svButtonLoadRequested;

    //刷新状态
    private int refreshCountThisStore;

    //协程跟踪（用于清理）
    private Coroutine findComponentsCoroutine;
    private Coroutine waitAndRefreshCoroutine;
    private bool dependenciesReady;

    //商品池加载状态（避免“某一类为空时永不刷新”）
    private bool itemPoolLoaded;
    private bool weaponPoolLoaded;

    //缓存以减少分配
    private static StatType[] cachedStatTypes;
    private const string GREEN_COLOR_TAG = "<color=#007801>";
    private const string RED_COLOR_TAG = "<color=#FF4444>";
    private const string COLOR_END_TAG = "</color>";

    //缓存字符串以减少分配（用于数字显示）
    private string cachedCoinText = "";
    private string cachedRefreshPriceText = "";
    private int cachedCoinValue = -1;
    private int cachedRefreshPriceValue = -1;

    /// <summary>
    /// 初始化
    /// </summary>
    public override void Init()
    {
        InitializeProductSlots();

        buttonRefresh?.onClick.AddListener(OnRefreshClicked);
        buttonNext?.onClick.AddListener(OnNextClicked);

        LoadProductPools();

        LoadSVButtonPrefab();

        StopAndClearCoroutine(ref findComponentsCoroutine);
        findComponentsCoroutine = StartCoroutine(FindComponents());
    }

    /// <summary>
    /// 从Addressables预加载SVButton预设体
    /// </summary>
    private void LoadSVButtonPrefab()
    {
        if (svButtonPrefab != null || svButtonLoadRequested) return;

        if (string.IsNullOrEmpty(svButtonKey))
        {
            return;
        }

        svButtonLoadRequested = true;
        AddressablesMgr.Instance.LoadAsset<GameObject>(svButtonKey, (prefab) =>
        {
            svButtonPrefab = prefab;
            if (svButtonPrefab == null)
            {
                return;
            }

            if (isShow)
            {
                UpdateInventoryUI();
            }
        });
    }

    /// <summary>
    /// 初始化商品槽
    /// </summary>
    private void InitializeProductSlots()
    {
        if (slot1 != null) slot1.Initialize(0, this);
        if (slot2 != null) slot2.Initialize(1, this);
        if (slot3 != null) slot3.Initialize(2, this);
        if (slot4 != null) slot4.Initialize(3, this);
    }

    /// <summary>
    /// 等待玩家出现后绑定金币与组件引用
    /// </summary>
    private IEnumerator FindComponents()
    {
        dependenciesReady = false;
        GameContext gameContext = GameContext.Instance;

        GameObject player = null;
        int attempts = 0;
        WaitForSeconds waitInterval = new(0.1f);

        while (player == null && attempts < 100)
        {
            if (GameContext.HasInstance && GameContext.Instance.TryGetPlayer(out player))
            {
                break;
            }

            yield return waitInterval;
            attempts++;
        }

        if (player != null)
        {
            if (gameContext != null)
            {
                gameContext.TryGetCoinManager(out coinManager);
                gameContext.TryGetInventoryManager(out inventoryManager);
                gameContext.TryGetWeaponManager(out weaponManager);
                gameContext.TryGetStatHandler(out statHandler);
                gameContext.TryGetPlayerEvents(out playerEvents);
            }

            coinManager ??= player.GetComponent<CoinManager>();
            inventoryManager ??= player.GetComponent<InventoryManager>();
            weaponManager ??= player.GetComponent<WeaponManager>();
            statHandler ??= player.GetComponent<StatHandler>();
            playerEvents ??= player.GetComponent<PlayerEvents>();

            if (playerEvents != null)
            {
                playerEvents.OnCoinsChanged -= OnCoinsChanged;
                playerEvents.OnCoinsChanged += OnCoinsChanged;
            }
        }

        if (gameContext != null)
        {
            gameContext.TryGetWaveManager(out waveManager);
            gameContext.TryGetGameMgr(out gameMgr);
        }

        dependenciesReady = coinManager != null && statHandler != null && waveManager != null;
        if (isShow)
        {
            RefreshAllUI();
        }
    }

    /// <summary>
    /// 商品池加载
    /// </summary>
    private void LoadProductPools()
    {
        itemPoolLoaded = false;
        weaponPoolLoaded = false;

        AddressablesMgr.Instance.LoadAssets<ItemConfig>(itemLabel, (items) =>
        {
            itemPoolLoaded = true;
            if (items == null)
            {
                itemPool.Clear();
                CheckAndRefreshProducts();
                return;
            }

            itemPoolOriginal = items;

            itemPool.Clear();
            foreach (ItemConfig item in items)
            {
                if (item != null && item.shopPrice > 0)
                {
                    itemPool.Add(item);
                }
            }

            CheckAndRefreshProducts();
        });

        AddressablesMgr.Instance.LoadAssets<WeaponConfig>(weaponLabel, (weapons) =>
        {
            weaponPoolLoaded = true;
            if (weapons == null)
            {
                weaponPool.Clear();
                CheckAndRefreshProducts();
                return;
            }

            weaponPoolOriginal = weapons;

            weaponPool.Clear();
            foreach (WeaponConfig weapon in weapons)
            {
                if (weapon == null || weapon.tierDataList == null || weapon.tierDataList.Count == 0)
                {
                    continue;
                }

                for (int i = 0; i < weapon.tierDataList.Count; i++)
                {
                    WeaponTierData tier = weapon.tierDataList[i];
                    if (tier == null) continue;

                    WeaponConfig levelVariant = WeaponConfig.CreateRuntimeCopy(weapon, tier.level);
                    if (levelVariant == null) continue;
                    if (levelVariant.GetCurrentBuyBasePrice() <= 0) continue;
                    weaponPool.Add(levelVariant);
                }
            }

            CheckAndRefreshProducts();
        });
    }

    /// <summary>
    /// 两池均加载完成后刷新货架
    /// </summary>
    private void CheckAndRefreshProducts()
    {
        if (!isShow) return;

        if (!itemPoolLoaded || !weaponPoolLoaded) return;

        if (itemPool.Count > 0 || weaponPool.Count > 0)
        {
            RefreshProducts();
        }
    }

    /// <summary>
    /// 刷新商品列表（锁定基于槽位）
    /// 刷新时将锁定槽位前置，未锁定槽位重新抽取
    /// </summary>
    public void RefreshProducts()
    {
        if (itemPool.Count == 0 && weaponPool.Count == 0)
        {
            return;
        }

        List<ScriptableObject> lockedProductsInOrder = GetLockedProductsInDisplayOrder();
        List<ScriptableObject> finalProducts = new(SLOT_COUNT);
        bool[] finalLockStates = new bool[SLOT_COUNT];

        foreach (ScriptableObject lockedProduct in lockedProductsInOrder)
        {
            if (lockedProduct == null || finalProducts.Count >= SLOT_COUNT)
            {
                continue;
            }

            int targetIndex = finalProducts.Count;
            finalProducts.Add(lockedProduct);
            finalLockStates[targetIndex] = true;
        }

        while (finalProducts.Count < SLOT_COUNT)
        {
            ScriptableObject newProduct = GenerateRandomProduct();
            if (newProduct == null)
            {
                return;
            }

            finalProducts.Add(newProduct);
        }

        currentProducts.Clear();
        for (int i = 0; i < SLOT_COUNT; i++)
        {
            ProductSlot slot = GetSlot(i);
            slotLockedStates[i] = finalLockStates[i];
            if (slot == null) continue;

            ScriptableObject product = finalProducts[i];
            slot.SetProduct(product, coinManager);
            if (product != null)
            {
                currentProducts.Add(product);
            }
        }

        UpdateProductSlotsUI();

        for (int i = 0; i < SLOT_COUNT; i++)
        {
            ProductSlot slot = GetSlot(i);
            if (slot != null)
            {
                slot.UpdateLockUI();
            }
        }
    }

    /// <summary>
    /// 生成随机商品（商店槽位允许重复）
    /// 业务脚本禁止出现权重公式：抽取逻辑交由 ValueStrategy（SO）完成
    /// </summary>
    private ScriptableObject GenerateRandomProduct()
    {
        if (!TryGetModeConfig(out ModeConfig modeConfig)) return null;

        int wave = GetCurrentWaveSafe();

        if (modeConfig.shopProductRollStrategy == null)
        {
            return null;
        }

        var context = new ShopProductRollContext(itemPool, weaponPool, null, wave, modeConfig);
        return modeConfig.shopProductRollStrategy.RollProduct(context);
    }

    /// <summary>
    /// 获取当前显示顺序中的锁定商品（仅保留有商品的锁定槽位）
    /// </summary>
    private List<ScriptableObject> GetLockedProductsInDisplayOrder()
    {
        List<ScriptableObject> lockedProducts = new();
        for (int i = 0; i < SLOT_COUNT; i++)
        {
            if (!slotLockedStates[i]) continue;

            ProductSlot slot = GetSlot(i);
            if (slot == null || slot.CurrentProduct == null)
            {
                slotLockedStates[i] = false;
                continue;
            }

            lockedProducts.Add(slot.CurrentProduct);
        }
        return lockedProducts;
    }

    /// <summary>
    /// 刷新所有UI
    /// </summary>
    public void RefreshAllUI()
    {
        UpdateCoinUI();
        UpdateStatsUI();
        UpdateInventoryUI();
        UpdateTitleUI();
        UpdateRefreshButtonUI();
    }

    /// <summary>
    /// 金币变化事件回调
    /// </summary>
    private void OnCoinsChanged(int currentCoins)
    {
        UpdateCoinUI();
    }

    /// <summary>
    /// 更新金币显示
    /// </summary>
    private void UpdateCoinUI()
    {
        if (coinManager != null && textCoinCount != null)
        {
            int currentCoins = coinManager.GetCurrentCoins();
            if (currentCoins != cachedCoinValue)
            {
                cachedCoinValue = currentCoins;
                cachedCoinText = currentCoins.ToString();
                textCoinCount.text = cachedCoinText;
            }
        }

        UpdateProductSlotsUI();
    }

    /// <summary>
    /// 更新属性显示
    /// </summary>
    private void UpdateStatsUI()
    {
        if (statHandler == null || textStats == null || textStats.Count == 0) return;

        if (cachedStatTypes == null)
        {
            cachedStatTypes = (StatType[])System.Enum.GetValues(typeof(StatType));
        }
        StatType[] statTypes = cachedStatTypes;
        int count = Mathf.Min(textStats.Count, statTypes.Length);

        for (int i = 0; i < count; i++)
        {
            if (textStats[i] == null) continue;

            StatType statType = statTypes[i];
            float currentValue = statHandler.GetStat(statType);
            float baseValue = statHandler.GetBaseStat(statType);

            string valueText = FormatStatValue(statType, currentValue);

            if (currentValue > baseValue)
            {
                textStats[i].text = GREEN_COLOR_TAG + valueText + COLOR_END_TAG;
            }
            else if (currentValue < baseValue)
            {
                textStats[i].text = RED_COLOR_TAG + valueText + COLOR_END_TAG;
            }
            else
            {
                textStats[i].text = valueText;
            }
        }
    }

    /// <summary>
    /// 格式化属性值显示（保留浮点，最多两位小数）。
    /// </summary>
    private string FormatStatValue(StatType statType, float value)
    {
        float displayValue = StatValueConverter.ToDisplayValue(statType, value);
        return displayValue.ToString("0.##");
    }

    /// <summary>
    /// 更新背包显示
    /// </summary>
    private void UpdateInventoryUI()
    {
        UpdateItemInventory();
        UpdateWeaponInventory();
    }

    /// <summary>
    /// 更新道具背包
    /// </summary>
    private void UpdateItemInventory()
    {
        if (itemContent == null || inventoryManager == null) return;

        for (int i = itemContent.childCount - 1; i >= 0; i--)
        {
            Destroy(itemContent.GetChild(i).gameObject);
        }

        List<ItemConfig> items = inventoryManager.GetInventory();
        Dictionary<ItemConfig, int> itemCounts = new();

        foreach (ItemConfig item in items)
        {
            if (itemCounts.ContainsKey(item))
            {
                itemCounts[item]++;
            }
            else
            {
                itemCounts[item] = 1;
            }
        }

        foreach (var kvp in itemCounts)
        {
            CreateItemCard(kvp.Key, kvp.Value);
        }
    }

    /// <summary>
    /// 创建道具卡片
    /// </summary>
    private void CreateItemCard(ItemConfig item, int count)
    {
        GameObject entry = CreateSVButtonEntry(itemContent, item != null ? item.itemIcon : null, count, true);
        if (entry == null || item == null) return;

        SVButtonView view = entry.GetComponent<SVButtonView>();
        if (view != null)
        {
            view.SetLevelBackground(true, item.GetThemeColor());
        }
    }

    /// <summary>
    /// 更新武器背包
    /// </summary>
    private void UpdateWeaponInventory()
    {
        if (weaponManager == null)
        {
            UpdateWeaponTitleUI(0, WeaponManager.MAX_WEAPON_COUNT);
            return;
        }

        int currentWeaponCount = weaponManager.GetCurrentWeaponCount();
        int maxWeaponCount = weaponManager.GetMaxWeaponCount();
        UpdateWeaponTitleUI(currentWeaponCount, maxWeaponCount);

        if (weaponContent == null) return;

        for (int i = weaponContent.childCount - 1; i >= 0; i--)
        {
            Destroy(weaponContent.GetChild(i).gameObject);
        }

        List<WeaponConfig> weapons = weaponManager.GetWeaponBackpack();
        for (int i = 0; i < weapons.Count; i++)
        {
            CreateWeaponCard(weapons[i], i);
        }
    }

    /// <summary>
    /// 更新武器背包标题（Weapon（x/y））
    /// </summary>
    private void UpdateWeaponTitleUI(int currentCount, int maxCount)
    {
        if (textWeaponTitle == null) return;
        textWeaponTitle.text = "Weapon(" + currentCount.ToString() + "/" + maxCount.ToString() + ")";
    }

    /// <summary>
    /// 创建武器卡片
    /// </summary>
    private void CreateWeaponCard(WeaponConfig weapon, int backpackIndex)
    {
        if (weapon == null) return;
        GameObject entry = CreateSVButtonEntry(weaponContent, weapon.weaponIcon, 1, false);
        if (entry == null) return;

        SVButtonView view = entry.GetComponent<SVButtonView>();
        if (view != null)
        {
            int sellPrice = GetWeaponSellPrice(weapon);
            view.SetSellAction(() => OnWeaponSellClicked(backpackIndex), sellPrice);
            if (weapon.GetCurrentLevel() >= 4 || !weaponManager.CanQuickMergeAtBackpackIndex(backpackIndex))
            {
                view.SetMergeAction(null);
            }
            else
            {
                view.SetMergeAction(() => OnWeaponMergeClicked(backpackIndex));
            }
            view.SetLevelBackground(true, weapon.GetThemeColor());
        }
    }

    /// <summary>
    /// 出售指定挂载点武器
    /// </summary>
    private void OnWeaponSellClicked(int backpackIndex)
    {
        if (weaponManager == null || coinManager == null) return;

        List<WeaponConfig> weapons = weaponManager.GetWeaponBackpack();
        if (backpackIndex < 0 || backpackIndex >= weapons.Count)
        {
            return;
        }

        WeaponConfig weaponToSell = weapons[backpackIndex];
        if (weaponToSell == null)
        {
            return;
        }

        int sellPrice = GetWeaponSellPrice(weaponToSell);
        if (sellPrice <= 0)
        {
            return;
        }

        if (!weaponManager.TryRemoveWeaponAtBackpackIndex(backpackIndex, out WeaponConfig removedWeapon) || removedWeapon == null)
        {
            return;
        }
        coinManager.AddCoins(sellPrice);

        UpdateCoinUI();
        UpdateInventoryUI();
        UpdateRefreshButtonUI();
    }

    /// <summary>
    /// 指定挂载点一键合成
    /// </summary>
    private void OnWeaponMergeClicked(int backpackIndex)
    {
        if (weaponManager == null) return;
        bool merged = weaponManager.TryQuickMergeAtBackpackIndex(backpackIndex);
        if (merged)
        {
            UpdateInventoryUI();
            UpdateProductSlotsUI();
        }
        else
        {
            UpdateWeaponInventory();
        }
    }

    /// <summary>
    /// 实例化 SVButton 并初始化通用显示
    /// </summary>
    private GameObject CreateSVButtonEntry(Transform parent, Sprite sprite, int stackCount, bool enableCountDisplay)
    {
        if (parent == null) return null;

        if (svButtonPrefab == null)
        {
            return null;
        }

        GameObject entry = Instantiate(svButtonPrefab, parent);

        var view = entry.GetComponent<SVButtonView>();
        if (view != null)
        {
            view.SetIcon(sprite);
            view.SetStackCount(stackCount, enableCountDisplay);
            view.SetSellAction(null, 0);
            view.SetMergeAction(null);
            view.SetLevelBackground(false, Color.clear);
        }

        return entry;
    }

    /// <summary>
    /// 更新商品槽位UI
    /// </summary>
    private void UpdateProductSlotsUI()
    {
        for (int i = 0; i < SLOT_COUNT; i++)
        {
            ProductSlot slot = GetSlot(i);
            if (slot == null || slot.IsUpdatingLockUI) continue;
            slot.UpdateUI(coinManager);
        }
    }

    /// <summary>
    /// 更新标题
    /// </summary>
    private void UpdateTitleUI()
    {
        if (textTitle == null || waveManager == null) return;

        if (waveManager.IsEndlessMode())
        {
            textTitle.text = "EndLess Store";
            return;
        }

        int wave = GetCurrentWaveSafe();
        textTitle.text = "Store (Round " + wave.ToString() + ")";
    }

    /// <summary>
    /// 更新刷新按钮UI
    /// </summary>
    private void UpdateRefreshButtonUI()
    {
        int currentPrice = GetCurrentRefreshPrice();
        if (currentPrice < 1)
        {
            if (textRefreshPrice != null) textRefreshPrice.text = "ERR";
            if (buttonRefresh != null) buttonRefresh.interactable = false;
            return;
        }

        if (textRefreshPrice != null && currentPrice != cachedRefreshPriceValue)
        {
            cachedRefreshPriceValue = currentPrice;
            cachedRefreshPriceText = "-" + currentPrice.ToString();
            textRefreshPrice.text = cachedRefreshPriceText;
        }

        if (buttonRefresh != null && coinManager != null)
        {
            buttonRefresh.interactable = coinManager.HasEnoughCoins(currentPrice);
        }
    }

    /// <summary>
    /// 刷新按钮点击
    /// </summary>
    private void OnRefreshClicked()
    {
        if (coinManager == null) return;

        int price = GetCurrentRefreshPrice();
        if (price < 1 || !coinManager.SpendCoins(price)) return;

        refreshCountThisStore++;
        RefreshProducts();
        UpdateCoinUI();
        UpdateRefreshButtonUI();
    }

    /// <summary>
    /// 继续按钮点击
    /// </summary>
    private void OnNextClicked()
    {
        EnsureWaveManagerReference();
        EnsureGameMgrReference();

        if (waveManager == null || gameMgr == null)
        {
            return;
        }

        HideMe(() =>
        {
            gameMgr.SetGameState(GameMgr.GameState.Playing);
            waveManager.ContinueToNextWave();
        });
    }

    /// <summary>
    /// 购买商品（由 ProductSlot 调用）
    /// 业务脚本禁止出现购买限制公式：购买规则交由 ValueStrategy（SO）完成
    /// </summary>
    public void PurchaseProduct(ScriptableObject product, int slotIndex)
    {
        if (product == null || coinManager == null) return;

        if (!TryGetModeConfig(out ModeConfig modeConfig) || modeConfig.shopPurchaseRuleStrategy == null) return;

        if (!TryGetProductBasePrice(product, out int basePrice)) return;

        int price = GetProductPrice(basePrice);
        if (price < 1) return;

        int wave = GetCurrentWaveSafe();
        ShopPurchaseContext context = new ShopPurchaseContext(product, price, coinManager, inventoryManager, weaponManager, wave, modeConfig);
        ShopPurchaseDecision decision = modeConfig.shopPurchaseRuleStrategy.EvaluatePurchase(context);
        if (!decision.canPurchase) return;

        bool success = TryPurchaseByType(product, decision.priceToPay);
        if (!success) return;

        HandlePurchaseSuccess(product, slotIndex);
    }

    /// <summary>
    /// 从商品对象中提取基础价格（道具商店价 / 武器当前等级基础价）。
    /// </summary>
    private bool TryGetProductBasePrice(ScriptableObject product, out int basePrice)
    {
        if (product is ItemConfig item)
        {
            basePrice = item.shopPrice;
            return true;
        }

        if (product is WeaponConfig weapon)
        {
            basePrice = weapon.GetCurrentBuyBasePrice();
            return true;
        }

        basePrice = 0;
        return false;
    }

    /// <summary>
    /// 按商品类型执行购买；失败时负责回滚已扣金币。
    /// </summary>
    private bool TryPurchaseByType(ScriptableObject product, int finalPrice)
    {
        if (!TrySpendCoinsForPurchase(finalPrice)) return false;

        if (product is ItemConfig itemToPurchase)
        {
            return TryPurchaseItem(itemToPurchase, finalPrice);
        }

        if (product is WeaponConfig weaponToPurchase)
        {
            return TryPurchaseWeapon(weaponToPurchase, finalPrice);
        }

        coinManager.AddCoins(finalPrice);
        return false;
    }

    /// <summary>
    /// 扣除购买所需金币。
    /// </summary>
    private bool TrySpendCoinsForPurchase(int finalPrice)
    {
        if (finalPrice < 1 || !coinManager.HasEnoughCoins(finalPrice))
        {
            return false;
        }

        return coinManager.SpendCoins(finalPrice);
    }

    /// <summary>
    /// 尝试购买道具，失败时退回已扣金币。
    /// </summary>
    private bool TryPurchaseItem(ItemConfig itemToPurchase, int finalPrice)
    {
        if (inventoryManager == null || itemToPurchase == null)
        {
            coinManager.AddCoins(finalPrice);
            return false;
        }

        if (inventoryManager.AddItem(itemToPurchase))
        {
            return true;
        }

        coinManager.AddCoins(finalPrice);
        return false;
    }

    /// <summary>
    /// 尝试购买武器，失败时退回已扣金币。
    /// </summary>
    private bool TryPurchaseWeapon(WeaponConfig weaponToPurchase, int finalPrice)
    {
        if (weaponManager == null || weaponToPurchase == null || !weaponManager.CanPurchaseWeapon(weaponToPurchase))
        {
            coinManager.AddCoins(finalPrice);
            return false;
        }

        if (weaponManager.TryAddWeapon(weaponToPurchase))
        {
            return true;
        }

        coinManager.AddCoins(finalPrice);
        return false;
    }

    /// <summary>
    /// 购买成功后刷新槽位与所有相关UI。
    /// </summary>
    private void HandlePurchaseSuccess(ScriptableObject product, int slotIndex)
    {
        ProductSlot slot = GetSlot(slotIndex);
        SetSlotLockState(slotIndex, false);
        if (slot != null)
        {
            slot.SetProduct(null, coinManager);
            currentProducts.Remove(product);
        }

        UpdateCoinUI();
        UpdateStatsUI();
        UpdateInventoryUI();
        UpdateProductSlotsUI();
        UpdateRefreshButtonUI();
    }

    /// <summary>
    /// 按商店价格策略计算武器出售价格
    /// </summary>
    public int GetWeaponSellPrice(WeaponConfig weapon)
    {
        if (weapon == null) return -1;

        int baseSellPrice = weapon.GetCurrentSellBasePrice();
        if (baseSellPrice <= 0) return -1;

        int finalPrice = GetProductPrice(baseSellPrice);
        if (finalPrice <= 0) return -1;

        return finalPrice;
    }

    /// <summary>
    /// 获取购买决策（供 ProductSlot 调用，用于更新按钮状态）
    /// 业务脚本禁止出现购买限制公式：购买规则交由 ValueStrategy（SO）完成
    /// </summary>
    public ShopPurchaseDecision GetPurchaseDecision(ScriptableObject product, int price, CoinManager coinManager)
    {
        if (product == null || coinManager == null)
        {
            return new ShopPurchaseDecision(false, price, ShopPurchaseFailReason.ProductNull);
        }
        if (price < 1)
        {
            return ShopPurchaseDecision.Deny(price, ShopPurchaseFailReason.InvalidProductPrice);
        }

        if (!TryGetModeConfig(out ModeConfig modeConfig))
        {
            return ShopPurchaseDecision.Deny(price, ShopPurchaseFailReason.ModeConfigMissing);
        }

        int wave = GetCurrentWaveSafe();

        if (modeConfig.shopPurchaseRuleStrategy == null)
        {
            return ShopPurchaseDecision.Deny(price, ShopPurchaseFailReason.PurchaseRuleStrategyMissing);
        }

        var context = new ShopPurchaseContext(product, price, coinManager, inventoryManager, weaponManager, wave, modeConfig);
        return modeConfig.shopPurchaseRuleStrategy.EvaluatePurchase(context);
    }

    /// <summary>
    /// 设置槽位锁定状态（由 ProductSlot 调用）
    /// </summary>
    public void SetSlotLockState(int slotIndex, bool isLocked)
    {
        if (slotIndex < 0 || slotIndex >= SLOT_COUNT) return;

        ProductSlot slot = GetSlot(slotIndex);
        slotLockedStates[slotIndex] = slot != null && slot.CurrentProduct != null && isLocked;

        slot?.UpdateLockUI();
    }

    /// <summary>
    /// 获取槽位锁定状态（按槽位索引）
    /// </summary>
    public bool IsSlotLocked(int slotIndex)
    {
        return slotIndex >= 0 && slotIndex < SLOT_COUNT && slotLockedStates[slotIndex];
    }

    /// <summary>
    /// 获取指定索引的槽位
    /// </summary>
    private ProductSlot GetSlot(int index)
    {
        switch (index)
        {
            case 0: return slot1;
            case 1: return slot2;
            case 2: return slot3;
            case 3: return slot4;
            default: return null;
        }
    }

    /// <summary>
    /// 显示商店面板并重绑依赖、刷新货架
    /// </summary>
    public override void ShowMe()
    {
        base.ShowMe();
        RestartSceneDependenciesForShow();
        refreshCountThisStore = 0;

        StopAndClearCoroutine(ref waitAndRefreshCoroutine);
        waitAndRefreshCoroutine = StartCoroutine(WaitAndRefresh());

        UpdateCoinUI();
    }

    /// <summary>
    /// 每次显示前重新绑定场景内玩家与 Wave/GameMgr，避免 DontDestroy 面板跨局复用时引用已销毁对象。
    /// </summary>
    private void RestartSceneDependenciesForShow()
    {
        if (playerEvents != null)
        {
            playerEvents.OnCoinsChanged -= OnCoinsChanged;
        }

        coinManager = null;
        inventoryManager = null;
        weaponManager = null;
        statHandler = null;
        playerEvents = null;
        waveManager = null;
        gameMgr = null;
        dependenciesReady = false;

        StopAndClearCoroutine(ref findComponentsCoroutine);

        findComponentsCoroutine = StartCoroutine(FindComponents());
    }

    /// <summary>
    /// 取当前波次（至少为 1）
    /// </summary>
    private int GetCurrentWaveSafe()
    {
        EnsureWaveManagerReference();
        if (waveManager == null) return 1;
        return Mathf.Max(1, waveManager.GetCurrentWave());
    }

    /// <summary>
    /// 懒加载 WaveManager 引用
    /// </summary>
    private void EnsureWaveManagerReference()
    {
        if (waveManager != null) return;

        if (GameContext.HasInstance)
        {
            GameContext.Instance.TryGetWaveManager(out waveManager);
        }
    }

    /// <summary>
    /// 懒加载 GameMgr 引用
    /// </summary>
    private void EnsureGameMgrReference()
    {
        if (gameMgr != null) return;

        if (GameContext.HasInstance)
        {
            GameContext.Instance.TryGetGameMgr(out gameMgr);
        }
    }

    /// <summary>
    /// 计算商品的动态价格，考虑 Mode 与 Wave（basePrice 来自 ItemConfig/WeaponConfig.shopPrice；返回实际价）。
    /// </summary>
    public int GetProductPrice(int basePrice)
    {
        if (!TryGetModeConfig(out ModeConfig modeConfig)) return -1;

        int wave = GetCurrentWaveSafe();

        if (modeConfig.shopProductPriceStrategy == null)
        {
            return -1;
        }

        return modeConfig.shopProductPriceStrategy.MakeValue(basePrice, wave, modeConfig);
    }

    /// <summary>
    /// 获取当前刷新价格：刷新次数越多越贵，波次越靠后每次刷新的涨幅越快
    /// </summary>
    public int GetCurrentRefreshPrice()
    {
        if (!TryGetModeConfig(out ModeConfig modeConfig)) return -1;

        int wave = GetCurrentWaveSafe();

        if (modeConfig.shopRefreshPriceStrategy == null)
        {
            return -1;
        }

        return modeConfig.shopRefreshPriceStrategy.MakeValue(wave, refreshCountThisStore, modeConfig);
    }

    /// <summary>
    /// 获取当前有效的模式配置。
    /// </summary>
    private bool TryGetModeConfig(out ModeConfig modeConfig)
    {
        EnsureWaveManagerReference();
        modeConfig = waveManager != null ? waveManager.GetModeConfig() : null;
        return modeConfig != null;
    }

    /// <summary>
    /// 显示后等待依赖就绪再全量刷新
    /// </summary>
    private IEnumerator WaitAndRefresh()
    {
        float timeout = 0f;
        while (!dependenciesReady && timeout < 2f)
        {
            EnsureWaveManagerReference();
            dependenciesReady = coinManager != null && statHandler != null && waveManager != null;
            timeout += Time.unscaledDeltaTime;
            yield return null;
        }
        if (!dependenciesReady)
        {
            waitAndRefreshCoroutine = null;
            yield break;
        }

        RefreshAllUI();

        if ((itemPool?.Count ?? 0) > 0 || (weaponPool?.Count ?? 0) > 0)
        {
            RefreshProducts();
        }

        waitAndRefreshCoroutine = null;
    }

    /// <summary>
    /// 解除订阅、释放 Addressables 与列表引用
    /// </summary>
    private void OnDestroy()
    {
        if (playerEvents != null)
        {
            playerEvents.OnCoinsChanged -= OnCoinsChanged;
        }

        buttonRefresh?.onClick.RemoveAllListeners();
        buttonNext?.onClick.RemoveAllListeners();

        if (slot1 != null) slot1.Cleanup();
        if (slot2 != null) slot2.Cleanup();
        if (slot3 != null) slot3.Cleanup();
        if (slot4 != null) slot4.Cleanup();

        StopAndClearCoroutine(ref findComponentsCoroutine);
        StopAndClearCoroutine(ref waitAndRefreshCoroutine);
        if (itemPoolOriginal != null)
        {
            AddressablesMgr.Instance?.Release(itemPoolOriginal);
            itemPoolOriginal = null;
        }
        if (weaponPoolOriginal != null)
        {
            AddressablesMgr.Instance?.Release(weaponPoolOriginal);
            weaponPoolOriginal = null;
        }
        if (svButtonPrefab != null)
        {
            AddressablesMgr.Instance?.Release(svButtonPrefab);
            svButtonPrefab = null;
        }

        if (itemPool != null)
        {
            itemPool.Clear();
            itemPool = null;
        }
        if (weaponPool != null)
        {
            weaponPool.Clear();
            weaponPool = null;
        }
        if (currentProducts != null)
        {
            currentProducts.Clear();
            currentProducts = null;
        }
    }

    private void StopAndClearCoroutine(ref Coroutine routine)
    {
        if (routine == null) return;
        StopCoroutine(routine);
        routine = null;
    }
}

/// <summary>
/// 商品槽位组件
/// 每个商品槽位的UI和逻辑
/// </summary>
[System.Serializable]
public class ProductSlot
{
    [Tooltip("槽位根物体，控制整槽显隐")]
    public GameObject slotRoot;

    [Header("界面引用")]
    [Tooltip("商品图标")]
    public Image imageIcon;

    [Tooltip("图标底层背景")]
    public Image imageIconBK;

    [Tooltip("品阶背景色图")]
    public Image imageBK;

    [Tooltip("商品名称")]
    public Text textName;

    [Tooltip("商品标签")]
    public Text textTag;

    [Tooltip("商品描述")]
    public Text textDescription;

    [Tooltip("购买")]
    public Button buttonPurchase;

    [Tooltip("锁定开关")]
    public Toggle toggleLock;

    [Tooltip("价格")]
    public Text textPrice;

    [Header("颜色设置")]
    [Tooltip("金币不足时的价格颜色")]
    public Color unaffordablePriceColor = Color.red;

    [Tooltip("锁定态按钮颜色")]
    //锁定Toggle选中色（紫色）
    public Color lockedButtonColor = new(0.5f, 0f, 0.5f, 1f);

    // 槽位索引与面板引用
    private int slotIndex;
    private StorePanel storePanel;

    // 当前槽位商品（可能是道具或武器）
    [HideInInspector]
    public ScriptableObject currentProduct;
    public ScriptableObject CurrentProduct => currentProduct;

    // 缓存 Image 组件引用，避免每次调用 GetComponentInChildren
    private Image cachedColorImage;

    // 防止 Toggle 回调与代码同步互相触发
    private bool isUpdatingLockUI = false;

    /// <summary>
    /// 获取是否正在更新锁定UI（用于外部检查，避免在用户操作时干扰）
    /// </summary>
    public bool IsUpdatingLockUI => isUpdatingLockUI;

    /// <summary>
    /// 初始化槽位
    /// </summary>
    public void Initialize(int index, StorePanel panel)
    {
        slotIndex = index;
        storePanel = panel;

        if (toggleLock != null)
        {
            toggleLock.transition = Selectable.Transition.None;
            cachedColorImage = toggleLock.targetGraphic as Image ?? toggleLock.GetComponentInChildren<Image>();
        }

        if (buttonPurchase != null)
        {
            buttonPurchase.onClick.RemoveAllListeners();
            buttonPurchase.onClick.AddListener(OnPurchaseClicked);
        }

        if (toggleLock != null)
        {
            toggleLock.onValueChanged.RemoveAllListeners();
            toggleLock.onValueChanged.AddListener(OnLockToggleChanged);
        }
    }

    /// <summary>
    /// 设置商品
    /// </summary>
    public void SetProduct(ScriptableObject product, CoinManager coinManager)
    {
        currentProduct = product;
        UpdateUI(coinManager);
    }

    /// <summary>
    /// 更新UI
    /// </summary>
    public void UpdateUI(CoinManager coinManager)
    {
        if (currentProduct == null)
        {
            SetSlotVisible(false);
            if (imageIconBK != null)
            {
                imageIconBK.gameObject.SetActive(false);
            }
            if (imageBK != null)
            {
                imageBK.gameObject.SetActive(false);
            }
            UpdateLockUI();
            return;
        }

        SetSlotVisible(true);

        int basePrice = 0;
        string name = "";
        string description = "";
        Sprite icon = null;
        string tag = "";
        Color levelColor = Color.clear;
        bool hasLevelColor = false;

        if (currentProduct is ItemConfig item)
        {
            basePrice = item.shopPrice;
            name = item.itemName;
            description = item.itemDescription;
            icon = item.itemIcon;
            tag = "Lv" + item.itemLevel.ToString();
            levelColor = item.GetThemeColor();
            hasLevelColor = true;
        }
        else if (currentProduct is WeaponConfig weapon)
        {
            basePrice = weapon.GetCurrentBuyBasePrice();
            name = weapon.weaponName;
            description = weapon.weaponDescription;
            icon = weapon.weaponIcon;
            tag = "Lv" + weapon.GetCurrentLevel().ToString();
            levelColor = weapon.GetThemeColor();
            hasLevelColor = true;
        }

        int price = storePanel != null ? storePanel.GetProductPrice(basePrice) : basePrice;
        if (price < 1)
        {
            if (textPrice != null)
            {
                textPrice.text = "ERR";
                textPrice.color = unaffordablePriceColor;
            }
            if (buttonPurchase != null)
            {
                buttonPurchase.interactable = false;
            }
            return;
        }

        if (imageIcon != null) imageIcon.sprite = icon;
        if (imageIconBK != null)
        {
            imageIconBK.gameObject.SetActive(true);
        }
        if (imageBK != null)
        {
            imageBK.gameObject.SetActive(hasLevelColor);
            if (hasLevelColor)
            {
                imageBK.color = levelColor;
            }
        }
        if (textName != null) textName.text = name;
        if (textTag != null) textTag.text = tag;
        if (textDescription != null) textDescription.text = description;

        ShopPurchaseDecision decision = storePanel != null ? storePanel.GetPurchaseDecision(currentProduct, price, coinManager) : new ShopPurchaseDecision(false, price, ShopPurchaseFailReason.ProductNull);

        if (textPrice != null)
        {
            int displayPrice = decision.priceToPay;
            string priceText = displayPrice.ToString();
            if (textPrice.text != priceText)
            {
                textPrice.text = priceText;
            }
            textPrice.color = decision.canPurchase ? Color.white : unaffordablePriceColor;
        }

        if (buttonPurchase != null)
        {
            buttonPurchase.interactable = decision.canPurchase;
        }

        if (toggleLock != null && currentProduct != null && !isUpdatingLockUI)
        {
            bool isLocked = storePanel != null && storePanel.IsSlotLocked(slotIndex);
            if (toggleLock.isOn == isLocked && cachedColorImage != null)
            {
                cachedColorImage.color = isLocked ? lockedButtonColor : Color.black;
            }
        }
    }

    /// <summary>
    /// 更新锁定切换按钮UI
    /// 监听器一直挂载，使用标志位防止循环调用，确保点击准确快速
    /// </summary>
    public void UpdateLockUI()
    {
        if (toggleLock == null || isUpdatingLockUI) return;

        isUpdatingLockUI = true;

        try
        {
            if (currentProduct == null)
            {
                toggleLock.interactable = false;
                toggleLock.isOn = false;
                if (cachedColorImage != null)
                {
                    cachedColorImage.color = Color.black;
                }
                return;
            }

            toggleLock.interactable = true;

            bool isLocked = storePanel != null && storePanel.IsSlotLocked(slotIndex);

            if (toggleLock.isOn != isLocked)
            {
                toggleLock.isOn = isLocked;
            }

            if (cachedColorImage != null)
            {
                cachedColorImage.color = isLocked ? lockedButtonColor : Color.black;
            }
        }
        finally
        {
            isUpdatingLockUI = false;
        }
    }

    /// <summary>
    /// 购买按钮点击
    /// </summary>
    private void OnPurchaseClicked()
    {
        if (currentProduct == null || storePanel == null) return;
        storePanel.PurchaseProduct(currentProduct, slotIndex);
    }

    /// <summary>
    /// 锁定切换按钮值改变（用户点击时立即响应）
    /// 监听器一直挂载，使用标志位区分用户点击和代码更新
    /// </summary>
    private void OnLockToggleChanged(bool isOn)
    {
        if (isUpdatingLockUI) return;

        if (currentProduct == null || storePanel == null) return;

        if (cachedColorImage != null)
        {
            cachedColorImage.color = isOn ? lockedButtonColor : Color.black;
        }

        storePanel.SetSlotLockState(slotIndex, isOn);
    }

    /// <summary>
    /// 显示或隐藏槽位根节点
    /// </summary>
    private void SetSlotVisible(bool visible)
    {
        if (slotRoot == null || slotRoot.activeSelf == visible) return;
        slotRoot.SetActive(visible);
    }

    /// <summary>
    /// 清理事件监听器（在 StorePanel 销毁时调用）
    /// </summary>
    public void Cleanup()
    {
        buttonPurchase?.onClick.RemoveAllListeners();
        toggleLock?.onValueChanged.RemoveAllListeners();
    }
}