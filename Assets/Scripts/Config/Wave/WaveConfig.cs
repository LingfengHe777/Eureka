using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 波次结构配置：波次列表、间隔与无尽标志；难度倍率与无尽增长由 ModeConfig 提供。
/// </summary>
[CreateAssetMenu(fileName = "WaveConfig", menuName = "Eureka/Wave/WaveConfig")]
public class WaveConfig : ScriptableObject
{
    [Header("备注")]
    [Tooltip("编辑器中区分用的名称")]
    public string configName = "Wave配置";

    [Header("波次")]
    [Tooltip("按顺序排列的每一波配置")]
    public List<WaveData> waves = new();

    [Header("无尽")]
    [Tooltip("通关全部波次后是否可进无尽")]
    public bool enableEndlessMode = true;

    /// <summary>
    /// 普通敌人在持续生成时的加权随机条目。
    /// </summary>
    [System.Serializable]
    public class NormalEnemySpawnData
    {
        [Tooltip("从池中随机到的敌人配置")]
        public EnemyConfig enemyConfig;

        [Min(0f)]
        [Tooltip("抽取权重，越大越常见，0 为不参与")]
        public float weight = 1f;
    }

    /// <summary>
    /// 特殊敌人：固定数量与相对波次开始的延迟。
    /// </summary>
    [System.Serializable]
    public class SpecialEnemySpawnData
    {
        [Tooltip("特殊敌人配置")]
        public EnemyConfig enemyConfig;

        [Min(1)]
        [Tooltip("生成数量，每点最多一只")]
        public int spawnCount = 1;

        [Min(0f)]
        [Tooltip("相对本波开始的延迟，秒")]
        public float spawnDelay = 0f;
    }

    /// <summary>
    /// 单个波次的时长、生成与强度参数。
    /// </summary>
    [System.Serializable]
    public class WaveData
    {
        [Tooltip("本波显示名，可空")]
        public string waveName = "Wave";

        [Header("时间")]
        [Min(1f)]
        [Tooltip("本波持续秒数，期间持续刷怪")]
        public float waveDuration = 40f;

        [Min(0f)]
        [Tooltip("本波开始前等待，秒")]
        public float waveStartDelay = 0f;

        [Header("普通怪池")]
        [Tooltip("持续刷怪时按权重抽取的敌人")]
        public List<NormalEnemySpawnData> normalEnemySpawns = new List<NormalEnemySpawnData>();

        [Header("特殊怪")]
        [Tooltip("按时间与点位刷出的特殊敌人")]
        public List<SpecialEnemySpawnData> specialEnemySpawns = new List<SpecialEnemySpawnData>();

        [Header("刷怪点")]
        [Min(1)]
        [Tooltip("本波使用的地图生成点个数")]
        public int spawnPointCount = 8;

        [Header("刷怪节奏")]
        [Min(0.1f)]
        [Tooltip("基础生成间隔，秒；随强度曲线缩短")]
        public float baseSpawnInterval = 2f;

        [Min(0.05f)]
        [Tooltip("间隔下限，秒，避免过快")]
        public float minSpawnInterval = 0.35f;

        [Min(1)]
        [Tooltip("单次刷怪最少数量")]
        public int minSpawnCountPerTick = 1;

        [Min(1)]
        [Tooltip("单次刷怪最多数量")]
        public int maxSpawnCountPerTick = 3;

        [Min(1)]
        [Tooltip("每轮刷怪使用的锚点数量")]
        public int spawnAnchorsPerTick = 1;

        [Min(0f)]
        [Tooltip("同一锚点周围簇生散布半径")]
        public float burstRadius = 1.5f;

        [Min(1)]
        [Tooltip("场上同时存活敌人上限")]
        public int maxAliveEnemies = 80;

        [Header("强度曲线")]
        [Tooltip("横轴本波进度 0～1，纵轴强度 0～1，影响间隔与每批数量")]
        public AnimationCurve spawnIntensityCurve = AnimationCurve.Linear(0f, 0f, 1f, 1f);

        [Header("预警")]
        [Tooltip("本波是否在生成前显示地面预警")]
        public bool enableSpawnTelegraph = false;
    }

    /// <summary>
    /// 返回配置的波次数。
    /// </summary>
    public int GetTotalWaves() => waves.Count;

    /// <summary>
    /// 按从 0 开始的索引取波次数据；越界返回 null。
    /// </summary>
    public WaveData GetWaveData(int waveIndex)
    {
        if (waveIndex >= 0 && waveIndex < waves.Count)
            return waves[waveIndex];
        return null;
    }
}
