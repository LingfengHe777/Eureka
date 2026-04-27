using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 预加载、实例化、活跃列表与生成点；表现由 Prefab 决定。
/// </summary>
public class SpawnManager : MonoBehaviour
{
    private GameContext gameContext;

    [Header("地图配置")]
    [Tooltip("地图大小，须手动配置且非零")]
    public Vector2 mapSize = Vector2.zero;

    [Tooltip("地图中心，为零则用场景中心")]
    public Vector2 mapCenter = Vector2.zero;

    [Min(2f)]
    [Tooltip("生成点距离地图边界的距离")]
    public float spawnPointBorderDistance = 5f;
    private bool isMapBoundsReady;

    private readonly List<Vector3> currentSpawnPoints = new();
    private readonly List<Vector3> spawnPointsSnapshot = new();

    private Dictionary<string, GameObject> prefabCache = new();
    private Dictionary<string, GameObject> telegraphPrefabCache = new();

    private GameObject player;

    private PlayerEvents playerEvents;

    private bool isSubscribedToPlayerEvents = false;
    private Coroutine findPlayerCoroutine;

    private readonly List<GameObject> activeEnemies = new();
    private readonly List<GameObject> activeEnemiesSnapshot = new();

    public Action<GameObject> OnEnemySpawned;

    public Action<GameObject> OnEnemyDied;

    private void Awake()
    {
        BindGameContext();
        InitializeMapBounds();
    }

    private void Start()
    {
        BindGameContext();
    }

    private void OnEnable()
    {
        BindGameContext();
        GameMgr.OnGameStateChanged += HandleGameStateChanged;
    }

    private void OnDisable()
    {
        GameMgr.OnGameStateChanged -= HandleGameStateChanged;
        if (findPlayerCoroutine != null)
        {
            StopCoroutine(findPlayerCoroutine);
            findPlayerCoroutine = null;
        }
        UnsubscribeFromPlayerEvents();
    }

    private void OnDestroy()
    {
        UnsubscribeFromPlayerEvents();
    }

    /// <summary>
    /// Playing 时订阅击杀；Victory/Defeat 时解除。
    /// </summary>
    private void HandleGameStateChanged(GameMgr.GameState newState)
    {
        if (newState == GameMgr.GameState.Playing && !isSubscribedToPlayerEvents)
        {
            RestartFindPlayerCoroutine();
        }
        else if (newState == GameMgr.GameState.Defeat || newState == GameMgr.GameState.Victory)
        {
            StopFindPlayerCoroutine();
            UnsubscribeFromPlayerEvents();
            player = null;
        }
    }

    /// <summary>
    /// 轮询 Context 直至拿到玩家并订阅击杀。
    /// </summary>
    private IEnumerator FindAndSubscribePlayer()
    {
        BindGameContext();

        yield return new WaitForSeconds(0.5f);

        int attempts = 0;
        while (player == null && attempts < 100)
        {
            if (TryResolvePlayerFromContext())
            {
                break;
            }

            yield return new WaitForSeconds(0.1f);
            attempts++;
        }

        if (player == null)
        {
            findPlayerCoroutine = null;
            yield break;
        }

        playerEvents = player.GetComponent<PlayerEvents>();
        if (playerEvents != null)
        {
            playerEvents.OnEnemyKilled -= HandleEnemyDeath;
            playerEvents.OnEnemyKilled += HandleEnemyDeath;
            isSubscribedToPlayerEvents = true;
        }

        findPlayerCoroutine = null;
    }

    private void RestartFindPlayerCoroutine()
    {
        StopFindPlayerCoroutine();
        findPlayerCoroutine = StartCoroutine(FindAndSubscribePlayer());
    }

    private void StopFindPlayerCoroutine()
    {
        if (findPlayerCoroutine == null)
        {
            return;
        }

        StopCoroutine(findPlayerCoroutine);
        findPlayerCoroutine = null;
    }

    private bool TryResolvePlayerFromContext()
    {
        if (gameContext == null)
        {
            return false;
        }

        if (!gameContext.TryGetPlayer(out GameObject foundPlayer))
        {
            return false;
        }

        player = foundPlayer;
        return player != null;
    }

    /// <summary>
    /// 单种敌人 Prefab 预加载。
    /// </summary>
    public void PreloadEnemyPrefab(EnemyConfig config, Action<bool> onComplete = null)
    {
        if (config == null || string.IsNullOrEmpty(config.prefabKey))
        {
            onComplete?.Invoke(false);
            return;
        }

        if (prefabCache.ContainsKey(config.prefabKey))
        {
            onComplete?.Invoke(true);
            return;
        }

        AddressablesMgr.Instance.LoadAsset<GameObject>(config.prefabKey, (prefab) =>
        {
            if (prefab != null)
            {
                prefabCache[config.prefabKey] = prefab;
                onComplete?.Invoke(true);
            }
            else
            {
                onComplete?.Invoke(false);
            }
        });
    }

    /// <summary>
    /// 批量预加载（去重 key）。
    /// </summary>
    public void PreloadEnemyPrefabs(List<EnemyConfig> configs, Action<bool> onAllComplete = null)
    {
        if (configs == null || configs.Count == 0)
        {
            onAllComplete?.Invoke(false);
            return;
        }

        HashSet<string> uniqueKeys = new();
        List<EnemyConfig> uniqueConfigs = new();
        foreach (EnemyConfig config in configs)
        {
            if (config != null && !string.IsNullOrEmpty(config.prefabKey) && uniqueKeys.Add(config.prefabKey))
            {
                uniqueConfigs.Add(config);
            }
        }

        if (uniqueConfigs.Count == 0)
        {
            onAllComplete?.Invoke(false);
            return;
        }

        int loadedCount = 0;
        int totalCount = uniqueConfigs.Count;
        bool hasFailure = false;

        foreach (EnemyConfig config in uniqueConfigs)
        {
            PreloadEnemyPrefab(config, (success) =>
            {
                if (!success)
                {
                    hasFailure = true;
                }
                loadedCount++;
                if (loadedCount >= totalCount)
                {
                    onAllComplete?.Invoke(!hasFailure);
                }
            });
        }
    }

    /// <summary>
    /// prefabKey 是否已在缓存。
    /// </summary>
    public bool IsPrefabCached(EnemyConfig config)
    {
        return config != null && !string.IsNullOrEmpty(config.prefabKey) && prefabCache.ContainsKey(config.prefabKey);
    }

    /// <summary>
    /// 池化生成并 InitializeForSpawn。
    /// </summary>
    public GameObject SpawnEnemy(EnemyConfig enemyConfig, Vector3? spawnPosition = null, bool applyRandomOffset = true)
    {
        if (enemyConfig == null)
        {
            return null;
        }
        if (string.IsNullOrEmpty(enemyConfig.prefabKey))
        {
            return null;
        }
        bool hasPrefab = prefabCache.TryGetValue(enemyConfig.prefabKey, out GameObject prefab);
        if (!hasPrefab || prefab == null)
        {
            return null;
        }

        Vector3 spawnPos = spawnPosition ?? GetRandomSpawnPosition();
        if (applyRandomOffset && spawnPosition.HasValue)
        {
            float randomOffset = UnityEngine.Random.Range(-0.5f, 0.5f);
            spawnPos += new Vector3(randomOffset, randomOffset, 0f);
        }
        GameObject enemy = GameObjectPoolManager.Instance.Spawn(prefab, spawnPos, Quaternion.identity);
        if (enemy != null)
        {
            Enemy enemyComponent = enemy.GetComponent<Enemy>();
            if (enemyComponent == null)
            {
                Destroy(enemy);
                return null;
            }

            enemyComponent.InitializeForSpawn(enemyConfig);
            activeEnemies.Add(enemy);
            OnEnemySpawned?.Invoke(enemy);
        }
        return enemy;
    }

    /// <summary>
    /// 可选预警后再 SpawnEnemy。shouldContinueSpawning 为 false 时波次已结束等，中止并回收预警；预警等待用 scaled Time.deltaTime 累计。
    /// </summary>
    public IEnumerator SpawnEnemyWithTelegraph(
        EnemyConfig enemyConfig,
        Vector3 spawnPosition,
        bool enableTelegraph,
        float telegraphDelay,
        Action<GameObject> onSpawned,
        bool applyRandomOffset = true,
        Func<bool> shouldContinueSpawning = null)
    {
        GameObject telegraphObj = null;
        Vector3 finalSpawnPosition = spawnPosition;

        if (applyRandomOffset)
        {
            float randomOffset = UnityEngine.Random.Range(-0.5f, 0.5f);
            finalSpawnPosition += new Vector3(randomOffset, randomOffset, 0f);
        }

        if (enableTelegraph)
        {
            if (telegraphDelay <= 0f)
            {
                onSpawned?.Invoke(null);
                yield break;
            }

            if (enemyConfig == null || string.IsNullOrEmpty(enemyConfig.spawnTelegraphPrefabKey))
            {
                onSpawned?.Invoke(null);
                yield break;
            }

            GameObject telegraphPrefab = null;
            string telegraphKey = enemyConfig.spawnTelegraphPrefabKey;
            yield return StartCoroutine(LoadTelegraphPrefabIfNeeded(telegraphKey));
            if (shouldContinueSpawning != null && !shouldContinueSpawning())
            {
                onSpawned?.Invoke(null);
                yield break;
            }

            telegraphPrefabCache.TryGetValue(telegraphKey, out telegraphPrefab);
            if (telegraphPrefab == null)
            {
                onSpawned?.Invoke(null);
                yield break;
            }

            telegraphObj = GameObjectPoolManager.Instance.Spawn(telegraphPrefab, finalSpawnPosition, Quaternion.identity);
            if (telegraphObj != null)
            {
                SpawnTelegraphPulse pulse = telegraphObj.GetComponent<SpawnTelegraphPulse>();
                if (pulse != null)
                {
                    pulse.SetTelegraphDuration(telegraphDelay);
                }
            }

            //与 WaitForSeconds(telegraphDelay) 同为 scaled 时间
            float telegraphElapsed = 0f;
            while (telegraphElapsed < telegraphDelay)
            {
                if (shouldContinueSpawning != null && !shouldContinueSpawning())
                {
                    if (telegraphObj != null)
                    {
                        GameObjectPoolManager.Instance.Release(telegraphObj);
                        telegraphObj = null;
                    }

                    onSpawned?.Invoke(null);
                    yield break;
                }

                telegraphElapsed += Time.deltaTime;
                yield return null;
            }
        }

        if (telegraphObj != null)
        {
            GameObjectPoolManager.Instance.Release(telegraphObj);
        }

        if (shouldContinueSpawning != null && !shouldContinueSpawning())
        {
            onSpawned?.Invoke(null);
            yield break;
        }

        GameObject enemy = SpawnEnemy(enemyConfig, finalSpawnPosition, false);
        onSpawned?.Invoke(enemy);
    }

    /// <summary>
    /// mapSize 为零则不可生成。
    /// </summary>
    private void InitializeMapBounds()
    {
        if (mapSize == Vector2.zero)
        {
            isMapBoundsReady = false;
            return;
        }

        isMapBoundsReady = true;
    }

    /// <summary>
    /// 可生成区内随机点。
    /// </summary>
    public List<Vector3> GenerateSpawnPoints(int spawnPointCount)
    {
        List<Vector3> points = new();

        if (!isMapBoundsReady)
        {
            InitializeMapBounds();
            if (!isMapBoundsReady)
            {
                return points;
            }
        }

        Vector2 spawnArea = mapSize - 2f * spawnPointBorderDistance * Vector2.one;
        if (spawnArea.x <= 0f || spawnArea.y <= 0f)
        {
            return points;
        }
        Vector2 halfArea = spawnArea * 0.5f;

        Vector2 minPos = mapCenter - halfArea;
        Vector2 maxPos = mapCenter + halfArea;

        for (int i = 0; i < spawnPointCount; i++)
        {
            Vector3 randomPoint = new(
                UnityEngine.Random.Range(minPos.x, maxPos.x),
                UnityEngine.Random.Range(minPos.y, maxPos.y),
                0f
            );
            points.Add(randomPoint);
        }

        return points;
    }

    /// <summary>
    /// 生成并写入当前波生成点。
    /// </summary>
    public void SetSpawnPointsForWave(int spawnPointCount)
    {
        if (spawnPointCount <= 0)
        {
            currentSpawnPoints.Clear();
            return;
        }

        List<Vector3> generatedPoints = GenerateSpawnPoints(spawnPointCount);
        currentSpawnPoints.Clear();
        currentSpawnPoints.AddRange(generatedPoints);
        if (currentSpawnPoints.Count == 0)
        {
            return;
        }
    }

    /// <summary>
    /// 优先当前波点列表，否则地图内随机或原点。
    /// </summary>
    public Vector3 GetRandomSpawnPosition()
    {
        if (currentSpawnPoints.Count > 0)
        {
            return currentSpawnPoints[UnityEngine.Random.Range(0, currentSpawnPoints.Count)];
        }

        if (!isMapBoundsReady)
        {
            InitializeMapBounds();
        }

        if (isMapBoundsReady && mapSize != Vector2.zero)
        {
            Vector2 halfSize = mapSize * 0.5f;
            return new Vector3(
                UnityEngine.Random.Range(mapCenter.x - halfSize.x, mapCenter.x + halfSize.x),
                UnityEngine.Random.Range(mapCenter.y - halfSize.y, mapCenter.y + halfSize.y),
                0f
            );
        }

        return Vector3.zero;
    }

    /// <summary>
    /// 生成点副本。
    /// </summary>
    public List<Vector3> GetSpawnPoints()
    {
        spawnPointsSnapshot.Clear();
        spawnPointsSnapshot.AddRange(currentSpawnPoints);
        return spawnPointsSnapshot;
    }

    /// <summary>
    /// 非分配方式写入生成点。
    /// </summary>
    public void GetSpawnPointsNonAlloc(List<Vector3> output)
    {
        if (output == null)
        {
            return;
        }

        output.Clear();
        output.AddRange(currentSpawnPoints);
    }

    /// <summary>
    /// 从活跃列表移除并 OnEnemyDied。
    /// </summary>
    public void HandleEnemyDeath(GameObject enemy)
    {
        if (enemy == null) return;

        if (activeEnemies.Contains(enemy))
        {
            activeEnemies.Remove(enemy);
            OnEnemyDied?.Invoke(enemy);
        }
    }

    /// <summary>
    /// 活跃数（先 RemoveAll null）。
    /// </summary>
    public int GetActiveEnemyCount()
    {
        activeEnemies.RemoveAll(e => e == null);
        return activeEnemies.Count;
    }

    /// <summary>
    /// 活跃列表副本。
    /// </summary>
    public List<GameObject> GetActiveEnemies()
    {
        activeEnemies.RemoveAll(e => e == null);
        activeEnemiesSnapshot.Clear();
        activeEnemiesSnapshot.AddRange(activeEnemies);
        return activeEnemiesSnapshot;
    }

    /// <summary>
    /// 仅清列表；实例由池统一回收。
    /// </summary>
    public void ClearActiveEnemyListOnly()
    {
        activeEnemies.Clear();
    }

    /// <summary>
    /// 解除击杀订阅。
    /// </summary>
    private void UnsubscribeFromPlayerEvents()
    {
        if (playerEvents != null)
        {
            playerEvents.OnEnemyKilled -= HandleEnemyDeath;
            playerEvents = null;
        }
        isSubscribedToPlayerEvents = false;
    }

    /// <summary>
    /// 预警 Prefab 异步加载并缓存。
    /// </summary>
    private IEnumerator LoadTelegraphPrefabIfNeeded(string telegraphKey)
    {
        if (string.IsNullOrEmpty(telegraphKey)) yield break;
        if (telegraphPrefabCache.ContainsKey(telegraphKey)) yield break;

        bool done = false;
        GameObject loaded = null;
        AddressablesMgr.Instance.LoadAsset<GameObject>(telegraphKey, (prefab) =>
        {
            loaded = prefab;
            done = true;
        });

        float timeout = 0f;
        while (!done && timeout < 5f)
        {
            timeout += Time.unscaledDeltaTime;
            yield return null;
        }

        if (loaded != null && !telegraphPrefabCache.ContainsKey(telegraphKey))
        {
            telegraphPrefabCache.Add(telegraphKey, loaded);
        }
    }

    /// <summary>
    /// 向 GameContext 注册。
    /// </summary>
    private void BindGameContext()
    {
        if (gameContext == null)
        {
            gameContext = GameContext.Instance;
        }

        if (gameContext == null)
        {
            return;
        }

        gameContext.RegisterSpawnManager(this);
    }
}
