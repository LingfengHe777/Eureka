using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 波次、刷怪调度、商店、升级/无尽
/// </summary>
public class WaveManager : MonoBehaviour
{
    // 运行时依赖缓存
    private GameContext gameContext;
    private GameMgr gameMgr;
    private SpawnManager spawnManager;
    private PlayerEvents playerEvents;
    private InventoryManager inventoryManager;
    private LevelSystem levelSystem;
    private PlayerHealth playerHealth;

    // 配置缓存
    private WaveConfig waveConfig;
    private ModeConfig modeConfig;

    // 波次状态
    private int currentWave = 0;
    private float currentWaveTime = 0f;
    private WaveConfig.WaveData currentWaveData;
    private int currentWaveSpawnedCount = 0;
    private float currentWaveElapsedTime = 0f;
    private bool isEndlessMode = false;
    private bool isWaitingForPlayerChoice = false;
    private bool isWavePaused = false;
    private bool isGamePlaying = false;
    private bool isSpawning = false;

    //每波结束自增；用于使子协程内预警、生成，在StopCoroutine后失效
    private int spawnSessionId;

    //管理的协程：初始化、等待游戏准备、刷怪、波间流程
    private Coroutine initializeRoutine;

    //等待游戏准备的协程（如果游戏开始时尚未准备好）
    private Coroutine waitGameplayReadyRoutine;

    //刷怪协程
    private Coroutine spawningRoutine;

    //波间流程协程（升级选择 → 商店/升级面板）
    private Coroutine intermissionRoutine;

    // 波内累计待处理的升级次数
    private int pendingLevelUpsThisWave;

    // 复用Buffer，减少运行时分配
    private readonly List<EnemyConfig> preloadEnemyConfigsBuffer = new();
    private readonly List<Vector3> spawnPointsBuffer = new(64);
    private readonly List<Vector3> anchorPoolBuffer = new(64);
    private readonly List<Vector3> selectedAnchorsBuffer = new(64);
    private readonly List<Vector3> availableSpecialSpawnPointsBuffer = new(64);

    // 对外事件
    public Action<int, bool> OnWaveStarted;
    public Action<int, bool> OnWaveEnded;
    public Action<float> OnTimerUpdated;
    public Action OnEndlessModeStarted;

    /// <summary>
    /// 组件初始化时绑定上下文。
    /// </summary>
    private void Awake()
    {
        BindGameContext();
    }

    /// <summary>
    /// 注册游戏状态相关事件。
    /// </summary>
    private void OnEnable()
    {
        GameMgr.OnGameStateChanged += HandleGameStateChanged;
        GameMgr.OnGameplayReady += HandleGameplayReady;
    }

    /// <summary>
    /// 反注册游戏状态相关事件。
    /// </summary>
    private void OnDisable()
    {
        GameMgr.OnGameStateChanged -= HandleGameStateChanged;
        GameMgr.OnGameplayReady -= HandleGameplayReady;
    }

    /// <summary>
    /// 推进波次倒计时并在到时后结束当前波次。
    /// </summary>
    private void Update()
    {
        if (!isGamePlaying || isWavePaused || isWaitingForPlayerChoice || currentWave == 0)
        {
            return;
        }

        if (currentWaveTime > 0f)
        {
            currentWaveTime -= Time.deltaTime;
            OnTimerUpdated?.Invoke(currentWaveTime);

            if (currentWaveTime <= 0f)
            {
                currentWaveTime = 0f;
                EndCurrentWave();
            }
        }
    }

    /// <summary>
    /// OnGameStateChanged事件响应
    /// </summary>
    private void HandleGameStateChanged(GameMgr.GameState newState)
    {
        ResolveDependencies();

        switch (newState)
        {
            case GameMgr.GameState.Playing:
                if (!isGamePlaying)
                {
                    //游戏开始时可能尚未准备好，等准备好后再初始化
                    StartInitializationWhenReady();
                }
                break;

            case GameMgr.GameState.Paused:
                break;

            case GameMgr.GameState.Victory:
                isGamePlaying = false;
                isSpawning = false;
                StopManagedCoroutines();
                UnsubscribePlayerEvents();
                DifficultySystem.Shutdown();
                break;

            case GameMgr.GameState.Defeat:
                isGamePlaying = false;
                isSpawning = false;
                StopManagedCoroutines();
                UnsubscribePlayerEvents();
                DifficultySystem.Shutdown();
                break;
        }
    }

    /// <summary>
    /// 加载 Wave和Mode配置 并创建 DifficultySystem
    /// </summary>
    private bool LoadConfigs()
    {
        GameSession session = GameSessionManager.Instance.GetSession();
        if (session == null)
        {
            Debug.LogError("[WaveManager] LoadConfigs failed: GameSession is null.");
            return false;
        }

        if (session.selectedMode == null)
        {
            Debug.LogError("[WaveManager] LoadConfigs failed: selectedMode is null.");
            return false;
        }

        modeConfig = session.selectedMode;
        waveConfig = modeConfig.waveConfig;

        if (waveConfig == null)
        {
            Debug.LogError($"[WaveManager] LoadConfigs failed: mode '{modeConfig.modeName}' has null waveConfig.");
            return false;
        }

        DifficultySystem.Create(modeConfig, waveConfig.GetTotalWaves());

        return true;
    }

    /// <summary>
    /// 预加载 -- 订阅玩家 -- 第一波
    /// </summary>
    private IEnumerator InitializeAndStart()
    {
        if (waveConfig == null || modeConfig == null || spawnManager == null)
        {
            initializeRoutine = null;
            yield break;
        }

        bool preloadSuccess = false;
        yield return StartCoroutine(PreloadAllEnemyPrefabs((success) => preloadSuccess = success));
        if (!preloadSuccess)
        {
            isGamePlaying = false;
            initializeRoutine = null;
            yield break;
        }

        yield return StartCoroutine(FindAndSubscribePlayerEvents());
        yield return null;
        initializeRoutine = null;
        StartNextWave();
    }

    /// <summary>
    /// 预加载本模式全部敌人Prefab
    /// </summary>
    private IEnumerator PreloadAllEnemyPrefabs(Action<bool> onComplete)
    {
        if (waveConfig == null || spawnManager == null)
        {
            onComplete?.Invoke(false);
            yield break;
        }

        preloadEnemyConfigsBuffer.Clear();
        foreach (var wave in waveConfig.waves)
        {
            foreach (var spawn in wave.normalEnemySpawns)
            {
                if (spawn.enemyConfig != null)
                    preloadEnemyConfigsBuffer.Add(spawn.enemyConfig);
            }
            foreach (var spawn in wave.specialEnemySpawns)
            {
                if (spawn.enemyConfig != null)
                    preloadEnemyConfigsBuffer.Add(spawn.enemyConfig);
            }
        }

        if (preloadEnemyConfigsBuffer.Count == 0)
        {
            onComplete?.Invoke(false);
            yield break;
        }

        bool loadingComplete = false;
        bool preloadSuccess = false;
        spawnManager.PreloadEnemyPrefabs(preloadEnemyConfigsBuffer, (success) =>
        {
            preloadSuccess = success;
            loadingComplete = true;
        });
        while (!loadingComplete)
        {
            yield return null;
        }

        onComplete?.Invoke(preloadSuccess);
    }

    /// <summary>
    /// 寻找PlayerEvents以订阅升级事件，若未找到则持续尝试
    /// </summary>
    /// <returns></returns>
    private IEnumerator FindAndSubscribePlayerEvents()
    {
        yield return new WaitForSeconds(0.5f);

        int attempts = 0;
        int maxAttempts = 100;
        PlayerEvents foundPlayerEvents = null;
        while (foundPlayerEvents == null && attempts < maxAttempts)
        {
            if (gameContext != null && gameContext.TryGetPlayerEvents(out foundPlayerEvents))
            {
                break;
            }
            if (foundPlayerEvents == null)
            {
                yield return new WaitForSeconds(0.1f);
                attempts++;
            }
        }

        if (foundPlayerEvents != null)
        {
            if (playerEvents != null && playerEvents != foundPlayerEvents)
            {
                playerEvents.OnLevelUp -= OnPlayerLevelUp;
            }

            playerEvents = foundPlayerEvents;
            playerEvents.OnLevelUp -= OnPlayerLevelUp;
            playerEvents.OnLevelUp += OnPlayerLevelUp;
        }
    }

    /// <summary>
    /// 推进波次或弹出终局选择
    /// </summary>
    public void StartNextWave()
    {
        if (!isGamePlaying || waveConfig == null) return;

        int totalWaves = waveConfig.GetTotalWaves();
        if (currentWave >= totalWaves && !isEndlessMode)
        {
            // 如果当前波次是最后一波，则进入终局选择
            EnterPostWavesChoice();
            return;
        }

        currentWave++;
        currentWaveSpawnedCount = 0;
        currentWaveElapsedTime = 0f;
        isWavePaused = false;
        pendingLevelUpsThisWave = 0;

        if (isEndlessMode || currentWave > totalWaves)
        {
            currentWaveData = waveConfig.GetWaveData(totalWaves - 1);
        }
        else
        {
            currentWaveData = waveConfig.GetWaveData(currentWave - 1);
        }

        if (currentWaveData == null)
        {
            return;
        }

        //开战前补清（主清理在 EndCurrentWave）
        ClearWaveSceneObjects();

        if (TryGetPlayer(out GameObject playerObj))
        {
            playerObj.SetActive(true);
            if (playerObj.TryGetComponent<WeaponManager>(out WeaponManager weaponManager))
                weaponManager.RebuildMountVisualsFromWeaponSlots();
        }

        currentWaveTime = currentWaveData.waveDuration;
        ShowGamePanelForCombat();

        if (DifficultySystem.Instance == null)
        {
            return;
        }
        DifficultySystem.Instance.UpdateWaveState(currentWave, isEndlessMode);

        OnWaveStarted?.Invoke(currentWave, isEndlessMode);
        StopAndClearCoroutine(ref spawningRoutine);
        spawningRoutine = StartCoroutine(StartWaveSpawning());
    }

    /// <summary>
    /// 停刷、清场、波间流程
    /// </summary>
    public void EndCurrentWave()
    {
        if (currentWave == 0 || !isGamePlaying) return;

        isSpawning = false;
        spawnSessionId++;
        StopAndClearCoroutine(ref spawningRoutine);

        ClearWaveSceneObjects();
        HideGamePanelForIntermission();

        if (TryGetPlayer(out GameObject playerObj))
        {
            if (playerObj.TryGetComponent<WeaponManager>(out WeaponManager weaponManager))
                weaponManager.ClearMountSlotsForIntermission();
            playerObj.transform.position = Vector3.zero;
            playerObj.SetActive(false);
        }

        OnWaveEnded?.Invoke(currentWave, isEndlessMode);

        if (ShouldEnterPostWavesChoice())
        {
            EnterPostWavesChoice();
            return;
        }

        isWavePaused = true;
        StopAndClearCoroutine(ref intermissionRoutine);
        intermissionRoutine = StartCoroutine(HandleIntermissionFlow());
    }

    /// <summary>
    /// 波间「继续」：恢复血量后下一波。
    /// </summary>
    public void ContinueToNextWave()
    {
        if (!isWavePaused)
        {
            return;
        }

        ResolveDependencies();
        RestorePlayerToFullHealth();
        isWavePaused = false;
        //同帧 Resume 后首帧 deltaTime 可能过大；延后一帧再开波，避免预警 WaitForSeconds 被吞
        StartCoroutine(ResumeAndStartNextWave());

        IEnumerator ResumeAndStartNextWave()
        {
            yield return null;
            StartNextWave();
        }
    }

    /// <summary>
    /// 打开波间商店（或升级）面板并确保游戏处于暂停状态。
    /// </summary>
    private void OpenShopOrUpgradePanel()
    {
        if (EnsureGameMgrResolved() && gameMgr.GetGameState() != GameMgr.GameState.Paused)
        {
            gameMgr.SetGameState(GameMgr.GameState.Paused);
        }

        UIManager ui = UIManager.Instance;
        if (ui == null) return;
        ui.ShowPanel<StorePanel>((panel) => { });
    }

    private IEnumerator HandleIntermissionFlow()
    {
        ResolveDependencies();
        UIManager ui = UIManager.Instance;
        if (ui == null)
        {
            intermissionRoutine = null;
            yield break;
        }

        if (EnsureGameMgrResolved())
        {
            gameMgr.SetGameState(GameMgr.GameState.Paused);
        }

        int upgradeCount = Mathf.Max(0, pendingLevelUpsThisWave);
        pendingLevelUpsThisWave = 0;
        yield return StartCoroutine(RunPendingUpgradeSelections(upgradeCount));

        yield return null;
        OpenShopOrUpgradePanel();
        intermissionRoutine = null;
    }

    private IEnumerator RunPendingUpgradeSelections(int upgradeCount)
    {
        if (upgradeCount <= 0)
        {
            yield break;
        }

        ResolveUpgradeDependencies();
        int playerLevel = levelSystem != null ? levelSystem.GetCurrentLevel() : 1;

        for (int i = 0; i < upgradeCount; i++)
        {
            bool completed = false;
            UIManager.Instance.ShowPanel<UpgradePanel>((panel) =>
            {
                if (panel == null)
                {
                    completed = true;
                    return;
                }

                panel.BeginSelection(playerLevel, inventoryManager, () => completed = true);
            });

            while (!completed)
            {
                yield return null;
            }
        }
    }

    private void ResolveUpgradeDependencies()
    {
        if (gameContext == null) return;

        if (inventoryManager == null)
        {
            gameContext.TryGetInventoryManager(out inventoryManager);
        }

        if (levelSystem == null)
        {
            gameContext.TryGetLevelSystem(out levelSystem);
        }

        if (inventoryManager != null && levelSystem != null)
        {
            return;
        }

        if (!TryGetPlayer(out GameObject playerObj))
        {
            return;
        }

        if (inventoryManager == null)
        {
            inventoryManager = playerObj.GetComponent<InventoryManager>();
        }

        if (levelSystem == null)
        {
            levelSystem = playerObj.GetComponent<LevelSystem>();
        }
    }

    public void ChooseVictory()
    {
        if (!isWaitingForPlayerChoice) return;

        isWaitingForPlayerChoice = false;
        if (!EnsureGameMgrResolved()) return;
        gameMgr.PrepareVictoryNormal();
        gameMgr.SetGameState(GameMgr.GameState.Victory);
    }

    /// <summary>
    /// 终局选择：进入无尽。
    /// </summary>
    public void ChooseEndlessMode()
    {
        if (!isWaitingForPlayerChoice) return;

        if (waveConfig == null || !waveConfig.enableEndlessMode)
        {
            return;
        }

        isWaitingForPlayerChoice = false;
        isEndlessMode = true;
        OnEndlessModeStarted?.Invoke();

        isWavePaused = true;
        StopAndClearCoroutine(ref intermissionRoutine);
        intermissionRoutine = StartCoroutine(HandleIntermissionFlow());
    }

    /// <summary>
    /// 本波持续刷怪。
    /// </summary>
    private IEnumerator StartWaveSpawning()
    {
        isSpawning = true;

        if (currentWaveData == null)
        {
            yield break;
        }
        if (currentWaveData.waveStartDelay > 0f)
        {
            yield return new WaitForSeconds(currentWaveData.waveStartDelay);
        }
        if (spawnManager != null)
        {
            spawnManager.SetSpawnPointsForWave(currentWaveData.spawnPointCount);
        }
        yield return StartCoroutine(SpawnSpecialEnemiesCoroutine());
        yield return StartCoroutine(SpawnNormalEnemiesContinuousCoroutine());
        float lastSpawnTime = 0f;
        currentWaveElapsedTime = 0f;
        while (isSpawning && isGamePlaying && !isWavePaused)
        {
            currentWaveElapsedTime += Time.deltaTime;

            float currentInterval = GetCurrentSpawnInterval();
            if (currentWaveElapsedTime - lastSpawnTime >= currentInterval)
            {
                yield return StartCoroutine(SpawnNormalEnemiesContinuousCoroutine());
                lastSpawnTime = currentWaveElapsedTime;
            }

            yield return null;
        }
        spawningRoutine = null;
    }

    /// <summary>
    /// 按 tick 生成普通敌（簇/权重）。
    /// </summary>
    private IEnumerator SpawnNormalEnemiesContinuousCoroutine()
    {
        if (spawnManager == null)
        {
            yield break;
        }

        spawnManager.GetSpawnPointsNonAlloc(spawnPointsBuffer);
        if (spawnPointsBuffer.Count == 0)
        {
            yield break;
        }

        if (currentWaveData == null)
        {
            yield break;
        }
        if (currentWaveData.normalEnemySpawns.Count == 0)
        {
            yield break;
        }

        if (DifficultySystem.Instance == null)
        {
            yield break;
        }

        int spawnCountOffset = DifficultySystem.Instance.GetSpawnCountOffset();
        float spawnMultiplier = DifficultySystem.Instance.GetSpawnCountMultiplier();
        int baseSpawnCount = GetCurrentTickSpawnCount();
        int spawnCount = Mathf.RoundToInt((baseSpawnCount + spawnCountOffset) * spawnMultiplier);
        spawnCount = Mathf.Max(0, spawnCount);

        int currentActiveEnemies = spawnManager.GetActiveEnemyCount();
        int allowByCap = Mathf.Max(0, currentWaveData.maxAliveEnemies - currentActiveEnemies);
        spawnCount = Mathf.Min(spawnCount, allowByCap);

        if (spawnCount <= 0)
        {
            yield break;
        }

        int anchorCount = Mathf.Clamp(currentWaveData.spawnAnchorsPerTick, 1, Mathf.Max(1, spawnPointsBuffer.Count));
        SelectAnchors(spawnPointsBuffer, anchorCount, selectedAnchorsBuffer);
        if (selectedAnchorsBuffer.Count == 0)
        {
            yield break;
        }

        int launchedCount = 0;
        int completedCount = 0;
        int successCount = 0;

        int remaining = spawnCount;
        for (int i = 0; i < selectedAnchorsBuffer.Count && remaining > 0; i++)
        {
            int anchorsLeft = selectedAnchorsBuffer.Count - i;
            int clusterCount = Mathf.CeilToInt((float)remaining / anchorsLeft);
            clusterCount = Mathf.Clamp(clusterCount, 1, remaining);

            Vector3 anchor = selectedAnchorsBuffer[i];
            for (int j = 0; j < clusterCount; j++)
            {
                EnemyConfig randomEnemy = GetRandomWeightedNormalEnemy();
                if (randomEnemy == null)
                {
                    completedCount++;
                    remaining--;
                    continue;
                }

                Vector3 spawnPos = GetBurstSpawnPosition(anchor, j, clusterCount, currentWaveData.burstRadius);

                launchedCount++;
                remaining--;
                StartCoroutine(SpawnNormalEnemyAtPosition(randomEnemy, spawnPos, () =>
                {
                    completedCount++;
                    successCount++;
                    currentWaveSpawnedCount++;
                },
                () =>
                {
                    completedCount++;
                }));
            }
        }

        while (completedCount < launchedCount)
        {
            yield return null;
        }
    }

    /// <summary>
    /// 特殊敌：延迟与预警。
    /// </summary>
    private IEnumerator SpawnSpecialEnemiesCoroutine()
    {
        if (spawnManager == null) yield break;

        spawnManager.GetSpawnPointsNonAlloc(spawnPointsBuffer);
        if (spawnPointsBuffer.Count == 0) yield break;

        foreach (var specialSpawn in currentWaveData.specialEnemySpawns)
        {
            if (specialSpawn.enemyConfig == null)
                continue;

            if (specialSpawn.spawnDelay > 0f)
            {
                yield return new WaitForSeconds(specialSpawn.spawnDelay);
            }

            availableSpecialSpawnPointsBuffer.Clear();
            availableSpecialSpawnPointsBuffer.AddRange(spawnPointsBuffer);
            int spawnCount = Mathf.Min(specialSpawn.spawnCount, availableSpecialSpawnPointsBuffer.Count);

            for (int i = 0; i < spawnCount; i++)
            {
                if (availableSpecialSpawnPointsBuffer.Count == 0) break;

                int randomIndex = UnityEngine.Random.Range(0, availableSpecialSpawnPointsBuffer.Count);
                Vector3 spawnPos = availableSpecialSpawnPointsBuffer[randomIndex];
                availableSpecialSpawnPointsBuffer.RemoveAt(randomIndex);

                int sid = spawnSessionId;
                GameObject enemy = null;
                yield return StartCoroutine(spawnManager.SpawnEnemyWithTelegraph(
                    specialSpawn.enemyConfig,
                    spawnPos,
                    currentWaveData.enableSpawnTelegraph,
                    modeConfig.spawnTelegraphDuration,
                    (spawned) => enemy = spawned,
                    true,
                    () => spawnSessionId == sid
                ));
                if (enemy != null)
                {
                    currentWaveSpawnedCount++;
                }

                yield return new WaitForSeconds(0.5f);
            }
        }
    }

    /// <summary>
    /// 根据当前波内已用时间计算进度（0~1）。
    /// </summary>
    private float GetWaveProgress()
    {
        if (currentWaveData == null || currentWaveData.waveDuration <= 0f)
        {
            return 0f;
        }

        return Mathf.Clamp01(currentWaveElapsedTime / currentWaveData.waveDuration);
    }

    /// <summary>
    /// 根据曲线计算当前刷怪强度（0~1）。
    /// </summary>
    private float GetCurrentSpawnIntensity()
    {
        if (currentWaveData == null || currentWaveData.spawnIntensityCurve == null)
        {
            return 0f;
        }

        float t = GetWaveProgress();
        return Mathf.Clamp01(currentWaveData.spawnIntensityCurve.Evaluate(t));
    }

    /// <summary>
    /// 依据强度在最大/最小间隔间插值得到当前刷怪间隔。
    /// </summary>
    private float GetCurrentSpawnInterval()
    {
        if (currentWaveData == null)
        {
            return 1f;
        }

        float intensity = GetCurrentSpawnIntensity();
        float baseInterval = Mathf.Max(0.05f, currentWaveData.baseSpawnInterval);
        float minInterval = Mathf.Clamp(currentWaveData.minSpawnInterval, 0.05f, baseInterval);
        return Mathf.Lerp(baseInterval, minInterval, intensity);
    }

    /// <summary>
    /// 依据强度在每tick最小/最大数量间插值得到当前刷怪数。
    /// </summary>
    private int GetCurrentTickSpawnCount()
    {
        if (currentWaveData == null)
        {
            return 1;
        }

        int minCount = Mathf.Max(1, currentWaveData.minSpawnCountPerTick);
        int maxCount = Mathf.Max(minCount, currentWaveData.maxSpawnCountPerTick);
        float intensity = GetCurrentSpawnIntensity();
        return Mathf.RoundToInt(Mathf.Lerp(minCount, maxCount, intensity));
    }

    /// <summary>
    /// 从普通敌配置中按权重随机选择一个敌人配置。
    /// </summary>
    private EnemyConfig GetRandomWeightedNormalEnemy()
    {
        if (currentWaveData == null || currentWaveData.normalEnemySpawns == null || currentWaveData.normalEnemySpawns.Count == 0)
        {
            return null;
        }

        float totalWeight = 0f;
        for (int i = 0; i < currentWaveData.normalEnemySpawns.Count; i++)
        {
            WaveConfig.NormalEnemySpawnData data = currentWaveData.normalEnemySpawns[i];
            if (data == null || data.enemyConfig == null) continue;
            totalWeight += Mathf.Max(0f, data.weight);
        }

        if (totalWeight <= 0f)
        {
            return null;
        }

        float roll = UnityEngine.Random.Range(0f, totalWeight);
        float cumulative = 0f;
        for (int i = 0; i < currentWaveData.normalEnemySpawns.Count; i++)
        {
            WaveConfig.NormalEnemySpawnData data = currentWaveData.normalEnemySpawns[i];
            if (data == null || data.enemyConfig == null) continue;

            cumulative += Mathf.Max(0f, data.weight);
            if (roll <= cumulative)
            {
                return data.enemyConfig;
            }
        }

        return null;
    }

    /// <summary>
    /// 从候选出生点中随机选择指定数量的锚点。
    /// </summary>
    private void SelectAnchors(List<Vector3> spawnPoints, int count, List<Vector3> result)
    {
        if (spawnPoints == null || result == null)
        {
            return;
        }

        anchorPoolBuffer.Clear();
        anchorPoolBuffer.AddRange(spawnPoints);
        result.Clear();

        for (int i = 0; i < count && anchorPoolBuffer.Count > 0; i++)
        {
            int index = UnityEngine.Random.Range(0, anchorPoolBuffer.Count);
            result.Add(anchorPoolBuffer[index]);
            anchorPoolBuffer.RemoveAt(index);
        }
    }

    /// <summary>
    /// 计算簇式刷怪中单个单位的实际出生位置。
    /// </summary>
    private Vector3 GetBurstSpawnPosition(Vector3 anchor, int index, int totalCount, float radius)
    {
        if (totalCount <= 1 || radius <= 0f)
        {
            return anchor;
        }

        float angleStep = 360f / totalCount;
        float angleDeg = angleStep * index + UnityEngine.Random.Range(-8f, 8f);
        float angleRad = angleDeg * Mathf.Deg2Rad;
        float dist = UnityEngine.Random.Range(radius * 0.45f, radius);
        Vector3 offset = new Vector3(Mathf.Cos(angleRad), Mathf.Sin(angleRad), 0f) * dist;
        return anchor + offset;
    }

    /// <summary>
    /// 在指定位置生成普通敌，并按结果回调成功/失败。
    /// </summary>
    private IEnumerator SpawnNormalEnemyAtPosition(EnemyConfig enemyConfig, Vector3 spawnPos, Action onSuccess, Action onFail)
    {
        int sid = spawnSessionId;
        GameObject enemy = null;
        yield return StartCoroutine(spawnManager.SpawnEnemyWithTelegraph(
            enemyConfig,
            spawnPos,
            currentWaveData.enableSpawnTelegraph,
            modeConfig.spawnTelegraphDuration,
            spawned => enemy = spawned,
            false,
            () => spawnSessionId == sid
        ));

        if (enemy != null)
        {
            onSuccess?.Invoke();
        }
        else
        {
            onFail?.Invoke();
        }
    }

    /// <summary>
    /// 记录波内升级次数，供波间统一处理升级选择。
    /// </summary>
    private void OnPlayerLevelUp(int newLevel)
    {
        if (!isGamePlaying || isWavePaused || currentWave <= 0) return;
        pendingLevelUpsThisWave++;
    }

    /// <summary>
    /// 停止本管理器维护的所有协程。
    /// </summary>
    private void StopManagedCoroutines()
    {
        spawnSessionId++;
        StopAndClearCoroutine(ref waitGameplayReadyRoutine);
        StopAndClearCoroutine(ref initializeRoutine);
        StopAndClearCoroutine(ref spawningRoutine);
        StopAndClearCoroutine(ref intermissionRoutine);
    }

    /// <summary>
    /// 取消玩家事件订阅并清理缓存引用。
    /// </summary>
    private void UnsubscribePlayerEvents()
    {
        if (playerEvents != null)
        {
            playerEvents.OnLevelUp -= OnPlayerLevelUp;
            playerEvents = null;
        }
    }

    /// <summary>
    /// 解析依赖关系
    /// </summary>
    private void ResolveDependencies()
    {
        if (gameContext == null)
        {
            gameContext = GameContext.Instance;
            if (gameContext == null)
            {
                return;
            }
        }

        if (gameMgr == null)
        {
            gameContext.TryGetGameMgr(out gameMgr);
        }

        if (spawnManager == null)
        {
            gameContext.TryGetSpawnManager(out spawnManager);
        }
        if (inventoryManager == null)
        {
            gameContext.TryGetInventoryManager(out inventoryManager);
        }
        if (levelSystem == null)
        {
            gameContext.TryGetLevelSystem(out levelSystem);
        }
        if (playerHealth == null)
        {
            gameContext.TryGetPlayerHealth(out playerHealth);
        }
    }

    /// <summary>
    /// 绑定GameContext，注册自己并尝试解析 GameMgr/SpawnManager
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

        gameContext.RegisterWaveManager(this);
        gameContext.TryGetGameMgr(out gameMgr);
        gameContext.TryGetSpawnManager(out spawnManager);
    }

    /// <summary>
    /// OnGameplayReady事件响应
    /// </summary>
    private void HandleGameplayReady()
    {
        if (gameMgr != null && gameMgr.GetGameState() == GameMgr.GameState.Playing && !isGamePlaying)
        {
            StartInitializationWhenReady();
        }
    }

    /// <summary>
    /// 确保可用的GameMgr引用。
    /// </summary>
    private bool EnsureGameMgrResolved()
    {
        if (gameMgr != null)
        {
            return true;
        }

        if (gameContext != null)
        {
            gameContext.TryGetGameMgr(out gameMgr);
        }

        return gameMgr != null;
    }

    /// <summary>
    /// 在游戏准备就绪后启动初始化流程。
    /// </summary>
    private void StartInitializationWhenReady()
    {
        if (waitGameplayReadyRoutine != null)
        {
            StopCoroutine(waitGameplayReadyRoutine);
            waitGameplayReadyRoutine = null;
        }

        if (GameMgr.IsGameplayReady)
        {
            StartInitializationNow();
            return;
        }
        waitGameplayReadyRoutine = StartCoroutine(WaitGameplayReadyAndInitialize());
    }

    /// <summary>
    /// 等待游戏准备就绪的协程，超时后放弃初始化
    /// </summary>
    /// <returns></returns>
    private IEnumerator WaitGameplayReadyAndInitialize()
    {
        float timeout = 0f;
        while (!GameMgr.IsGameplayReady && timeout < 10f)
        {
            timeout += Time.unscaledDeltaTime;
            yield return null;
        }

        waitGameplayReadyRoutine = null;
        if (!GameMgr.IsGameplayReady)
        {
            isGamePlaying = false;
            yield break;
        }

        StartInitializationNow();
    }

    /// <summary>
    /// 开始初始化：加载配置 → 预加载敌人 → 订阅玩家事件 → 开始第一波
    /// </summary>
    private void StartInitializationNow()
    {
        if (!LoadConfigs())
        {
            isGamePlaying = false;
            return;
        }

        isGamePlaying = true;
        if (initializeRoutine != null)
        {
            StopCoroutine(initializeRoutine);
        }
        initializeRoutine = StartCoroutine(InitializeAndStart());
    }

    /// <summary>
    /// 将玩家生命值恢复到满值。
    /// </summary>
    private void RestorePlayerToFullHealth()
    {
        if (playerHealth == null)
        {
            return;
        }

        playerHealth.FillToMaxHealth();
    }

    /// <summary>
    /// 进入波间阶段时隐藏战斗主界面。
    /// </summary>
    private void HideGamePanelForIntermission()
    {
        UIManager ui = UIManager.Instance;
        if (ui == null) return;
        if (ui.TryGetPanel<GamePanel>(out GamePanel gamePanel) && gamePanel != null && gamePanel.isShow)
        {
            gamePanel.HideMe(null);
        }
    }

    /// <summary>
    /// 进入战斗阶段时显示战斗主界面。
    /// </summary>
    private void ShowGamePanelForCombat()
    {
        UIManager ui = UIManager.Instance;
        if (ui == null) return;

        if (ui.TryGetPanel<GamePanel>(out GamePanel gamePanel) && gamePanel != null)
        {
            if (!gamePanel.isShow)
            {
                gamePanel.ShowMe();
            }
            return;
        }

        ui.ShowPanel<GamePanel>();
    }

    /// <summary>
    /// 清理波次场景对象与敌人活跃列表。
    /// </summary>
    private void ClearWaveSceneObjects()
    {
        ResolveDependencies();
        GameObjectPoolManager pool = GameObjectPoolManager.Instance;
        pool?.ReleaseAllActivePooledInstances();

        if (spawnManager != null)
        {
            spawnManager.ClearActiveEnemyListOnly();
        }
    }

    /// <summary>
    /// 判断当前波次结束后是否进入终局选择。
    /// </summary>
    private bool ShouldEnterPostWavesChoice()
    {
        if (isEndlessMode || waveConfig == null)
        {
            return false;
        }

        int totalWaves = waveConfig.GetTotalWaves();
        return totalWaves > 0 && currentWave >= totalWaves;
    }

    /// <summary>
    /// 进入终局选择流程并弹出选择面板。
    /// </summary>
    private void EnterPostWavesChoice()
    {
        isWaitingForPlayerChoice = true;
        isWavePaused = true;
        if (EnsureGameMgrResolved())
        {
            gameMgr.SetGameState(GameMgr.GameState.Paused);
        }

        if (UIManager.Instance != null)
        {
            UIManager.Instance.ShowPanel<PostWavesChoicePanel>(_ => { });
        }
    }

    /// <summary>
    /// 获取当前波次编号。
    /// </summary>
    public int GetCurrentWave() => currentWave;

    /// <summary>
    /// 获取当前波次剩余时间。
    /// </summary>
    public float GetCurrentWaveTime() => currentWaveTime;

    /// <summary>
    /// 是否处于无尽模式。
    /// </summary>
    public bool IsEndlessMode() => isEndlessMode;

    /// <summary>
    /// 是否正在等待终局选择。
    /// </summary>
    public bool IsWaitingForPlayerChoice() => isWaitingForPlayerChoice;

    /// <summary>
    /// 获取当前波次配置。
    /// </summary>
    public WaveConfig GetWaveConfig() => waveConfig;

    /// <summary>
    /// 获取当前模式配置。
    /// </summary>
    public ModeConfig GetModeConfig() => modeConfig;

    /// <summary>
    /// 获取总波次数量。
    /// </summary>
    public int GetTotalWaves()
    {
        return waveConfig != null ? waveConfig.GetTotalWaves() : 0;
    }

    /// <summary>
    /// 获取当前波次的显示索引，无尽模式或波次超出配置时持续显示最后一波的索引
    /// </summary>
    public int GetEndlessWaveDisplayIndex()
    {
        if (!isEndlessMode || waveConfig == null)
        {
            return 0;
        }

        return Mathf.Max(1, currentWave);
    }

    /// <summary>
    /// 暂停当前波次推进。
    /// </summary>
    public void PauseWave()
    {
        isWavePaused = true;
    }

    /// <summary>
    /// 恢复当前波次推进。
    /// </summary>
    public void ResumeWave()
    {
        isWavePaused = false;
    }

    /// <summary>
    /// 从上下文中获取有效玩家对象。
    /// </summary>
    private bool TryGetPlayer(out GameObject playerObj)
    {
        playerObj = null;
        return gameContext != null && gameContext.TryGetPlayer(out playerObj) && playerObj != null;
    }

    /// <summary>
    /// 停止指定协程并清空引用。
    /// </summary>
    private void StopAndClearCoroutine(ref Coroutine routine)
    {
        if (routine == null) return;
        StopCoroutine(routine);
        routine = null;
    }

    /// <summary>
    /// 销毁时统一清理协程与事件订阅。
    /// </summary>
    private void OnDestroy()
    {
        StopManagedCoroutines();
        UnsubscribePlayerEvents();
    }
}
