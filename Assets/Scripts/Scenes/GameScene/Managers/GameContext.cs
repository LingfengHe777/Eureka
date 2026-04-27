using UnityEngine;

/// <summary>
/// 局内依赖收口：显式注册，减少散落查找
/// </summary>
public class GameContext : MonoBehaviour
{
    //单例模式
    private static GameContext instance;
    public static GameContext Instance => instance;
    public static bool HasInstance => instance != null;

    private GameMgr gameMgr;
    private WaveManager waveManager;
    private SpawnManager spawnManager;
    private GameObject player;
    private PlayerEvents playerEvents;
    private CoinManager coinManager;
    private InventoryManager inventoryManager;
    private WeaponManager weaponManager;
    private StatHandler statHandler;
    private ExperienceManager experienceManager;
    private LevelSystem levelSystem;
    private PlayerHealth playerHealth;

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;
    }

    public void RegisterGameMgr(GameMgr mgr)
    {
        gameMgr = mgr;
    }

    public void RegisterWaveManager(WaveManager mgr)
    {
        waveManager = mgr;
    }

    public void RegisterSpawnManager(SpawnManager mgr)
    {
        spawnManager = mgr;
    }

    public void RegisterPlayer(GameObject playerObject)
    {
        player = playerObject;
        if (player == null)
        {
            playerEvents = null;
            coinManager = null;
            inventoryManager = null;
            weaponManager = null;
            statHandler = null;
            experienceManager = null;
            levelSystem = null;
            playerHealth = null;
            return;
        }

        playerEvents = player.GetComponent<PlayerEvents>();
        coinManager = player.GetComponent<CoinManager>();
        inventoryManager = player.GetComponent<InventoryManager>();
        weaponManager = player.GetComponent<WeaponManager>();
        statHandler = player.GetComponent<StatHandler>();
        experienceManager = player.GetComponent<ExperienceManager>();
        levelSystem = player.GetComponent<LevelSystem>();
        playerHealth = player.GetComponent<PlayerHealth>();
    }

    public bool TryGetGameMgr(out GameMgr value)
    {
        value = gameMgr;
        return value != null;
    }

    public bool TryGetWaveManager(out WaveManager value)
    {
        value = waveManager;
        return value != null;
    }

    public bool TryGetSpawnManager(out SpawnManager value)
    {
        value = spawnManager;
        return value != null;
    }

    public bool TryGetPlayer(out GameObject value)
    {
        value = player;
        return value != null;
    }

    public bool TryGetPlayerEvents(out PlayerEvents value)
    {
        if (playerEvents == null && TryGetPlayer(out GameObject playerObj) && playerObj != null)
        {
            playerEvents = playerObj.GetComponent<PlayerEvents>();
        }

        value = playerEvents;
        return value != null;
    }

    public bool TryGetCoinManager(out CoinManager value)
    {
        if (coinManager == null && TryGetPlayer(out GameObject playerObj) && playerObj != null)
        {
            coinManager = playerObj.GetComponent<CoinManager>();
        }

        value = coinManager;
        return value != null;
    }

    public bool TryGetExperienceManager(out ExperienceManager value)
    {
        if (experienceManager == null && TryGetPlayer(out GameObject playerObj) && playerObj != null)
        {
            experienceManager = playerObj.GetComponent<ExperienceManager>();
        }

        value = experienceManager;
        return value != null;
    }

    public bool TryGetInventoryManager(out InventoryManager value)
    {
        if (inventoryManager == null && TryGetPlayer(out GameObject playerObj) && playerObj != null)
        {
            inventoryManager = playerObj.GetComponent<InventoryManager>();
        }

        value = inventoryManager;
        return value != null;
    }

    public bool TryGetWeaponManager(out WeaponManager value)
    {
        if (weaponManager == null && TryGetPlayer(out GameObject playerObj) && playerObj != null)
        {
            weaponManager = playerObj.GetComponent<WeaponManager>();
        }

        value = weaponManager;
        return value != null;
    }

    public bool TryGetStatHandler(out StatHandler value)
    {
        if (statHandler == null && TryGetPlayer(out GameObject playerObj) && playerObj != null)
        {
            statHandler = playerObj.GetComponent<StatHandler>();
        }

        value = statHandler;
        return value != null;
    }

    public bool TryGetLevelSystem(out LevelSystem value)
    {
        if (levelSystem == null && TryGetPlayer(out GameObject playerObj) && playerObj != null)
        {
            levelSystem = playerObj.GetComponent<LevelSystem>();
        }

        value = levelSystem;
        return value != null;
    }

    public bool TryGetPlayerHealth(out PlayerHealth value)
    {
        if (playerHealth == null && TryGetPlayer(out GameObject playerObj) && playerObj != null)
        {
            playerHealth = playerObj.GetComponent<PlayerHealth>();
        }

        value = playerHealth;
        return value != null;
    }

    /// <summary>
    /// 检查 Wave/Spawn/GameMgr 是否已注册
    /// </summary>
    public bool ValidateRequiredServices(out string errorMessage)
    {
        string missing = string.Empty;
        if (gameMgr == null) missing += "GameMgr, ";
        if (waveManager == null) missing += "WaveManager, ";
        if (spawnManager == null) missing += "SpawnManager, ";

        if (string.IsNullOrEmpty(missing))
        {
            errorMessage = null;
            return true;
        }

        missing = missing.Substring(0, missing.Length - 2);
        errorMessage = $"GameContext: 缺少关键注册 [{missing}]。请检查场景对象挂载与GameContext注册流程";
        return false;
    }

    private void OnDestroy()
    {
        if (instance == this)
        {
            instance = null;
        }
    }
}
