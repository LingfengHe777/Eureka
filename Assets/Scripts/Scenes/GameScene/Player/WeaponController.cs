using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using UnityEngine.AddressableAssets;

/// <summary>
/// 武器控制器（MonoBehaviour）。
/// 根据 WeaponConfig 执行近战/远程攻击逻辑；与 StatHandler 联动计算伤害，与 PlayerEvents 联动触发事件。
/// </summary>
[RequireComponent(typeof(StatHandler), typeof(PlayerEvents), typeof(WeaponManager))]
[DefaultExecutionOrder(500)]
public class WeaponController : MonoBehaviour
{
    [Header("配置引用")]
    //当前武器（GameSession）
    private WeaponConfig weaponConfig;

    [Header("组件引用")]
    private StatHandler statHandler;
    private PlayerEvents playerEvents;

    [Header("攻击状态")]
    //各挂载点上次攻击时间
    private readonly Dictionary<int, float> weaponLastAttackTime = new Dictionary<int, float>();
    private readonly Dictionary<int, float> weaponLastSfxPlayTime = new Dictionary<int, float>();
    //近战挥砍：gunPos 旋转锁定；PositiveInfinity 表示等动画事件解锁
    private readonly Dictionary<int, float> mountAttackRotationLockUntilTime = new Dictionary<int, float>();
    //挥砍锁定期间每帧保持的世界旋转
    private readonly Dictionary<int, Quaternion> meleeSwingFrozenWorldRotation = new Dictionary<int, Quaternion>();
    private readonly Dictionary<WeaponConfig, List<WeaponModifier>> loadedModifiersByWeapon = new Dictionary<WeaponConfig, List<WeaponModifier>>();
    private readonly HashSet<WeaponConfig> loadingModifiers = new HashSet<WeaponConfig>();
    private readonly HashSet<WeaponModifier> loadedModifierAssets = new HashSet<WeaponModifier>();
    private bool initialModifierPreloadCompleted;

    [Header("近战攻击")]
    [SerializeField]
    //敌人 LayerMask（默认第6层）
    private LayerMask enemyLayerMask = 1 << 6;
    [Header("武器旋转")]
    [SerializeField]
    //是否自动瞄最近敌人
    private bool autoAimAtEnemy = true;

    [SerializeField]
    //自动瞄准搜索半径
    private float autoAimRange = 15f;

    [SerializeField]
    [Range(0f, 90f)]
    [Tooltip("允许攻击的最大朝向偏差，度")]
    //允许开火的最大朝向偏差（度）
    private float maxAttackAngleDeviation = 15f;

    [Header("武器挂载点")]
    //gunPos 数组（WeaponManager 提供）
    private Transform[] weaponMountPoints = new Transform[WeaponManager.MOUNT_POINT_COUNT];

    //WeaponManager（挂载点与武器槽来源）
    private WeaponManager weaponManager;

    //子弹实例父节点
    private static Transform bulletsParent;
    private const int EnemyOverlapInitialBufferSize = 256;
    private const int EnemyOverlapMaxBufferSize = 2048;
    private Collider2D[] enemyOverlapBuffer = new Collider2D[EnemyOverlapInitialBufferSize];

    private void Awake()
    {
        statHandler = GetComponent<StatHandler>();
        playerEvents = GetComponent<PlayerEvents>();
        weaponManager = GetComponent<WeaponManager>();

        EnsureBulletsParent();
    }

    /// <summary>
    /// 确保子弹父节点存在（用于在 Hierarchy 中归类所有子弹）。
    /// </summary>
    private void EnsureBulletsParent()
    {
        if (bulletsParent == null)
        {
            GameObject bulletsParentObj = new GameObject("Bullets");
            bulletsParent = bulletsParentObj.transform;
        }
    }

    private void Start()
    {
        InitializeMountPoints();

        ResetMountPointRotations();

        InitializeWeapon();

        StartCoroutine(PreloadInitialModifiersCoroutine());
    }

    private void ResetMountPointRotations()
    {
        for (int i = 0; i < weaponMountPoints.Length; i++)
        {
            if (weaponMountPoints[i] != null)
            {
                weaponMountPoints[i].rotation = Quaternion.identity;
            }
        }
    }

    /// <summary>
    /// 瞄准放在 LateUpdate：在玩家移动、物理、Animator 之后用最新坐标指向「当前」最近敌人，便于双方持续移动时仍跟踪。
    /// 近战挥砍期间 mountAttackRotationLockUntilTime 会禁止旋转，直至动画事件 AnimEnd。
    /// </summary>
    private void LateUpdate()
    {
        UpdateWeaponRotation();

        if (!initialModifierPreloadCompleted)
        {
            return;
        }

        var allWeapons = weaponManager.GetAllWeapons();
        foreach (var slot in allWeapons)
        {
            if (slot.weaponConfig != null && CanAttackWeapon(slot.weaponConfig, slot.mountPointIndex) && HasEnemyInRangeForWeapon(slot.weaponConfig, slot.mountPointIndex))
            {
                PerformAttackForWeapon(slot.weaponConfig, slot.mountPointIndex);
            }
        }
    }

    private void InitializeMountPoints()
    {
        weaponMountPoints = weaponManager.GetMountPoints();
    }

    /// <summary>
    /// 初始化武器（从 GameSession 读取）。
    /// </summary>
    private void InitializeWeapon()
    {
        GameSession session = GameSessionManager.Instance?.GetSession();
        if (session != null && session.selectedWeapon != null)
        {
            weaponConfig = session.selectedWeapon;
        }
    }

    /// <summary>
    /// 武器实际攻击间隔（秒）：当前等级 WeaponTierData.attackCooldown ÷ (1 + 攻击速度加成)。
    /// 攻击速度为百分比加成（如 0.5 表示 +50%），攻速越高间隔越短。
    /// </summary>
    private float GetWeaponCooldown(WeaponConfig config)
    {
        if (config == null) return 0f;

        float baseCooldown = config.GetCurrentAttackCooldown();
        if (statHandler == null)
            return Mathf.Max(baseCooldown, 0f);

        float attackSpeedBonus = statHandler.GetStat(StatType.AttackSpeed);
        return baseCooldown / (1f + attackSpeedBonus);
    }

    /// <summary>
    /// 是否允许发起一次攻击（含 SetTrigger / PrepareMeleeAttack）。
    /// 上一刀在 PerformAttackForWeapon 里已写入 weaponLastAttackTime，在 GetWeaponCooldown 秒内本方法为 false，等价于「冷却中不能攻击」。
    /// </summary>
    private bool CanAttackWeapon(WeaponConfig config, int mountPointIndex)
    {
        if (config == null) return false;
        if (mountPointIndex < 0 || mountPointIndex >= weaponMountPoints.Length) return false;

        if (!weaponLastAttackTime.ContainsKey(mountPointIndex))
        {
            weaponLastAttackTime[mountPointIndex] = 0f;
        }

        float cooldown = Mathf.Max(GetWeaponCooldown(config), 0.0001f);
        if (Time.time - weaponLastAttackTime[mountPointIndex] < cooldown) return false;

        if (config.type == WeaponType.Melee && IsMountAimRotationLocked(mountPointIndex))
        {
            return false;
        }

        if (!IsModifierReady(config))
        {
            EnsureWeaponModifiersLoaded(config);
            return false;
        }

        return true;
    }

    /// <summary>
    /// 近战挥砍期间为 true：此帧不会更新 gunPos 朝向，且不应再触发新的攻击动画。
    /// </summary>
    private bool IsMountAimRotationLocked(int mountPointIndex)
    {
        if (mountPointIndex < 0) return false;
        if (!mountAttackRotationLockUntilTime.TryGetValue(mountPointIndex, out float unlockTime)) return false;
        if (float.IsPositiveInfinity(unlockTime)) return true;
        return Time.time < unlockTime;
    }

    /// <summary>
    /// 检查指定武器攻击范围内是否存在敌人。
    /// </summary>
    private bool HasEnemyInRangeForWeapon(WeaponConfig config, int mountPointIndex)
    {
        if (config == null || mountPointIndex < 0 || mountPointIndex >= weaponMountPoints.Length) return false;
        if (weaponMountPoints[mountPointIndex] == null) return false;

        float actualRange = GetWeaponRange(config);

        Vector2 weaponPosition = weaponMountPoints[mountPointIndex].position;
        GameObject nearestEnemy = FindNearestAliveEnemy(weaponPosition, actualRange);

        if (nearestEnemy == null) return false;

        if (config.type == WeaponType.Melee)
        {
            return true;
        }

        Transform mountPoint = weaponMountPoints[mountPointIndex];
        Vector2 directionToEnemy = ((Vector2)nearestEnemy.transform.position - (Vector2)mountPoint.position).normalized;
        Vector2 weaponDirection = mountPoint.right;

        float angleDifference = Vector2.Angle(weaponDirection, directionToEnemy);

        return angleDifference <= maxAttackAngleDeviation;
    }

    /// <summary>
    /// 在指定范围内查找最近的存活敌人。
    /// </summary>
    private GameObject FindNearestAliveEnemy(Vector2 center, float range)
    {
        GameObject nearestEnemy = null;
        float nearestDistance = float.MaxValue;
        int count = OverlapEnemies(center, range);

        for (int i = 0; i < count; i++)
        {
            Collider2D enemyCollider = enemyOverlapBuffer[i];
            UpdateNearestEnemyCandidate(center, enemyCollider, ref nearestEnemy, ref nearestDistance);
        }

        return nearestEnemy;
    }

    private int OverlapEnemies(Vector2 center, float range)
    {
        int count = Physics2D.OverlapCircleNonAlloc(center, range, enemyOverlapBuffer, enemyLayerMask);
        while (count >= enemyOverlapBuffer.Length && enemyOverlapBuffer.Length < EnemyOverlapMaxBufferSize)
        {
            int newSize = Mathf.Min(enemyOverlapBuffer.Length * 2, EnemyOverlapMaxBufferSize);
            enemyOverlapBuffer = new Collider2D[newSize];
            count = Physics2D.OverlapCircleNonAlloc(center, range, enemyOverlapBuffer, enemyLayerMask);
        }

        return Mathf.Min(count, enemyOverlapBuffer.Length);
    }

    private static void UpdateNearestEnemyCandidate(
        Vector2 center,
        Collider2D enemyCollider,
        ref GameObject nearestEnemy,
        ref float nearestDistance)
    {
        if (!TryGetAliveEnemy(enemyCollider, out Enemy enemy))
        {
            return;
        }

        float distance = Vector2.Distance(center, (Vector2)enemy.transform.position);
        if (distance >= nearestDistance)
        {
            return;
        }

        nearestDistance = distance;
        nearestEnemy = enemy.gameObject;
    }

    private static bool TryGetAliveEnemy(Collider2D enemyCollider, out Enemy enemy)
    {
        enemy = null;
        if (enemyCollider == null)
        {
            return false;
        }

        enemy = enemyCollider.GetComponent<Enemy>() ?? enemyCollider.GetComponentInParent<Enemy>();
        return enemy != null && enemy.GetCurrentHealth() > 0;
    }

    private void UpdateWeaponRotation()
    {
        var allWeapons = weaponManager.GetAllWeapons();
        foreach (var slot in allWeapons)
        {
            if (slot.mountPointIndex >= 0 && slot.mountPointIndex < weaponMountPoints.Length)
            {
                Transform mountPoint = weaponMountPoints[slot.mountPointIndex];
                if (mountPoint != null)
                {
                    UpdateSingleWeaponRotation(mountPoint);
                }
            }
        }
    }

    /// <summary>
    /// 更新单个武器挂载点的旋转。
    /// 仅在存在敌人时更新朝向；瞬时对齐到目标方向（不做插值旋转），避免逐帧旋转与攻击判定不同步。
    /// </summary>
    private void UpdateSingleWeaponRotation(Transform mountPoint)
    {
        if (mountPoint == null) return;

        int mountIdx = GetMountPointIndex(mountPoint);
        if (mountIdx >= 0 && mountAttackRotationLockUntilTime.TryGetValue(mountIdx, out float unlockTime))
        {
            if (float.IsPositiveInfinity(unlockTime) || Time.time < unlockTime)
            {
                if (meleeSwingFrozenWorldRotation.TryGetValue(mountIdx, out Quaternion frozen))
                {
                    mountPoint.rotation = frozen;
                }

                return;
            }
        }

        Vector2 targetDirection = GetTargetDirectionForRotation(mountPoint);

        if (targetDirection.sqrMagnitude <= 0.01f)
        {
            return;
        }

        float targetAngle = Mathf.Atan2(targetDirection.y, targetDirection.x) * Mathf.Rad2Deg;

        Quaternion targetRotation = Quaternion.AngleAxis(targetAngle, Vector3.forward);
        mountPoint.rotation = targetRotation;
    }

    private static int GetMountPointIndex(Transform mountPoint, Transform[] mounts)
    {
        if (mountPoint == null || mounts == null) return -1;
        for (int i = 0; i < mounts.Length; i++)
        {
            if (mounts[i] == mountPoint) return i;
        }
        return -1;
    }

    private int GetMountPointIndex(Transform mountPoint) => GetMountPointIndex(mountPoint, weaponMountPoints);

    /// <summary>
    /// 获取用于旋转的目标方向（仅返回指向敌人，不返回移动方向）。
    /// </summary>
    private Vector2 GetTargetDirectionForRotation(Transform mountPoint)
    {
        if (mountPoint == null) return Vector2.zero;

        Vector2 weaponPosition = mountPoint.position;

        if (autoAimAtEnemy)
        {
            GameObject nearestEnemy = FindNearestEnemyForMountPoint(mountPoint);
            if (nearestEnemy != null)
            {
                Vector2 directionToEnemy = (nearestEnemy.transform.position - (Vector3)weaponPosition).normalized;
                return directionToEnemy;
            }
        }

        return Vector2.zero;
    }

    /// <summary>
    /// 该挂载点当前武器的攻击范围（用于与瞄准/出手判定一致）。
    /// </summary>
    private float GetWeaponRangeForMount(int mountPointIndex)
    {
        if (weaponManager.TryGetWeaponAtMountPoint(mountPointIndex, out WeaponConfig cfg) && cfg != null)
        {
            return GetWeaponRange(cfg);
        }

        return autoAimRange;
    }

    /// <summary>
    /// 为指定挂载点查找用于瞄准的最近敌人：优先与攻击范围内最近目标一致，避免 gunPos 指向远处敌人却打近处。
    /// </summary>
    private GameObject FindNearestEnemyForMountPoint(Transform mountPoint)
    {
        if (mountPoint == null) return null;

        int mountIdx = GetMountPointIndex(mountPoint);
        float weaponRange = GetWeaponRangeForMount(mountIdx);
        float extendedRange = Mathf.Max(autoAimRange, weaponRange);

        Vector2 weaponPosition = mountPoint.position;
        GameObject inAttackRange = FindNearestAliveEnemy(weaponPosition, weaponRange);
        if (inAttackRange != null)
        {
            return inAttackRange;
        }

        return FindNearestAliveEnemy(weaponPosition, extendedRange);
    }

    /// <summary>
    /// 获取武器实际攻击范围。
    /// 远程：基础范围 + Range 属性（数值相加）。
    /// 近战：仅武器基础范围，不受 Range 属性影响。
    /// </summary>
    private float GetWeaponRange(WeaponConfig config)
    {
        if (config == null || statHandler == null) return 0f;

        if (config.type == WeaponType.Melee)
        {
            return config.GetCurrentAttackRange();
        }

        if (config.type == WeaponType.Ranged)
        {
            float playerRange = statHandler.GetStat(StatType.Range);

            return config.GetCurrentAttackRange() + playerRange;
        }

        return config.GetCurrentAttackRange();
    }

    /// <summary>
    /// 执行指定武器的一次攻击。
    /// </summary>
    private void PerformAttackForWeapon(WeaponConfig config, int mountPointIndex)
    {
        if (config == null)
        {
            return;
        }

        weaponLastAttackTime[mountPointIndex] = Time.time;

        InvokeModifiersBeforeAttack(config, mountPointIndex);
        if (config.type == WeaponType.Melee)
        {
            PerformMeleeAttackForWeapon(config, mountPointIndex);
        }
        else if (config.type == WeaponType.Ranged)
        {
            PerformRangedAttackForWeapon(config, mountPointIndex);
        }

        if (config.type == WeaponType.Ranged)
            TryPlayWeaponAttackSfx(config, mountPointIndex);

        InvokeModifiersAfterAttack(config, mountPointIndex);
    }

    /// <summary>
    /// 近战：碰撞体首次碰到敌人时由 MeleeWeaponHitbox 调用；空击不播放。
    /// </summary>
    public void TryPlayWeaponAttackSfxFromMeleeHit(MeleeWeaponHitbox hitbox, WeaponConfig config)
    {
        int idx = FindMountIndexForMeleeHitbox(hitbox);
        if (idx < 0) return;
        TryPlayWeaponAttackSfx(config, idx);
    }

    private int FindMountIndexForMeleeHitbox(MeleeWeaponHitbox hitbox)
    {
        if (hitbox == null) return -1;
        Transform t = hitbox.transform;
        for (int i = 0; i < weaponMountPoints.Length; i++)
        {
            if (weaponMountPoints[i] == null) continue;
            if (t == weaponMountPoints[i] || t.IsChildOf(weaponMountPoints[i]))
                return i;
        }
        return -1;
    }

    /// <summary>
    /// 播放武器攻击音效（可选）。
    /// 按该武器冷却的一定比例做最小间隔，避免同帧重复触发。
    /// </summary>
    private void TryPlayWeaponAttackSfx(WeaponConfig config, int mountPointIndex)
    {
        if (config == null) return;
        if (string.IsNullOrEmpty(config.attackSfxKey)) return;
        if (mountPointIndex < 0 || mountPointIndex >= weaponMountPoints.Length) return;

        float minInterval = Mathf.Max(0.02f, Mathf.Max(GetWeaponCooldown(config), 0.0001f) * 0.5f);
        if (weaponLastSfxPlayTime.TryGetValue(mountPointIndex, out float lastTime))
        {
            if (Time.time - lastTime < minInterval)
            {
                return;
            }
        }

        weaponLastSfxPlayTime[mountPointIndex] = Time.time;
        GameDataMgr.Instance.PlaySound(config.attackSfxKey);
    }

    /// <summary>
    /// 执行指定武器的近战攻击：仅开挥砍窗口，命中由预制体上 Trigger + MeleeWeaponHitbox 结算。
    /// 仅在 CanAttackWeapon（距上次触发已满 GetWeaponCooldown）通过后调用；Animator SetTrigger、PrepareMeleeAttack 与伤害检测同源。
    /// </summary>
    private void PerformMeleeAttackForWeapon(WeaponConfig config, int mountPointIndex)
    {
        if (!config.TryGetCurrentMeleeData(out MeleeData meleeData))
        {
            return;
        }

        if (config.meleeWeaponPrefab == null)
        {
            return;
        }

        Transform mount = mountPointIndex >= 0 && mountPointIndex < weaponMountPoints.Length
            ? weaponMountPoints[mountPointIndex]
            : null;
        MeleeWeaponHitbox hb = mount != null ? mount.GetComponentInChildren<MeleeWeaponHitbox>(true) : null;
        if (hb != null)
        {
            hb.PrepareMeleeAttack(meleeData);
        }

        if (mountPointIndex >= 0 && mountPointIndex < weaponMountPoints.Length)
            config.TriggerMeleeAttackAnimation(weaponMountPoints[mountPointIndex]);

        if (mountPointIndex >= 0 && mountPointIndex < weaponMountPoints.Length && weaponMountPoints[mountPointIndex] != null)
        {
            Transform m = weaponMountPoints[mountPointIndex];
            meleeSwingFrozenWorldRotation[mountPointIndex] = m.rotation;
            mountAttackRotationLockUntilTime[mountPointIndex] = float.PositiveInfinity;
        }
    }

    /// <summary>
    /// 由 MeleeWeaponHitbox.AttackBegin 调用：伤害窗口开始时按当前最近敌人再对准一次并更新冻结旋转，
    /// 缓解前摇期间玩家位移或敌人走位与 SetTrigger 当帧瞄准不一致的问题（无法消除伤害帧内目标继续移动带来的空刀）。
    /// </summary>
    public void RefreshMeleeSwingAimAtAttackBegin(MeleeWeaponHitbox hitbox)
    {
        int idx = FindMountIndexForMeleeHitbox(hitbox);
        if (idx < 0) return;
        Transform mount = weaponMountPoints[idx];
        if (mount == null) return;

        Vector2 targetDirection = GetTargetDirectionForRotation(mount);
        if (targetDirection.sqrMagnitude <= 0.01f)
            return;

        float targetAngle = Mathf.Atan2(targetDirection.y, targetDirection.x) * Mathf.Rad2Deg;
        Quaternion targetRotation = Quaternion.AngleAxis(targetAngle, Vector3.forward);
        mount.rotation = targetRotation;
        meleeSwingFrozenWorldRotation[idx] = targetRotation;
    }

    /// <summary>
    /// 由 MeleeWeaponHitbox.AnimEnd（动画事件 WeaponConfig.MeleeEventAnimEnd）调用：攻击动画结束，恢复该挂载点 gunPos 自动瞄准。
    /// </summary>
    public void NotifyMeleeAimUnlockedFromAnimation(MeleeWeaponHitbox hitbox)
    {
        int idx = FindMountIndexForMeleeHitbox(hitbox);
        if (idx < 0) return;
        mountAttackRotationLockUntilTime.Remove(idx);
        meleeSwingFrozenWorldRotation.Remove(idx);
    }

    /// <summary>
    /// 由 MeleeWeaponHitbox 在 Trigger 中调用。
    /// </summary>
    public void ApplyMeleeHitFromHitbox(WeaponConfig config, Enemy enemy, Vector2 knockbackDirection, MeleeData meleeData)
    {
        if (config == null || enemy == null || meleeData == null || statHandler == null) return;

        float playerMelee = statHandler.GetStat(StatType.MeleeDamage);
        float playerRanged = statHandler.GetStat(StatType.RangedDamage);
        float playerElemental = statHandler.GetStat(StatType.ElementalDamage);
        float damageMultiplier = statHandler.GetStat(StatType.Damage);
        float damageBeforeCrit = config.GetTotalDamage(playerMelee, playerRanged, playerElemental) * (1f + damageMultiplier);

        ApplySingleMeleeHit(config, enemy, meleeData, damageBeforeCrit, knockbackDirection);
    }

    private void ApplySingleMeleeHit(
        WeaponConfig config,
        Enemy enemy,
        MeleeData meleeData,
        float damageBeforeCrit,
        Vector2 knockbackDirection)
    {
        float critChance = statHandler.GetStat(StatType.CritChance);
        float healthSteal = statHandler.GetStat(StatType.HealthSteal);

        bool isCrit = Random.Range(0f, 1f) < critChance;
        float finalDamage = damageBeforeCrit * (isCrit ? 2f : 1f);
        finalDamage = ApplyDamageModifiers(config, finalDamage, enemy);

        enemy.TakeDamage(finalDamage, gameObject);
        if (meleeData.knockbackDistance > 0f)
        {
            enemy.ApplyKnockback(knockbackDirection, meleeData.knockbackDistance, meleeData.knockbackDuration);
        }

        playerEvents.TriggerDealDamage(finalDamage, enemy.gameObject, isCrit);
        InvokeModifiersAfterHit(config, enemy, finalDamage);

        if (healthSteal > 0f && TryGetComponent<PlayerHealth>(out PlayerHealth playerHealthForSteal))
        {
            playerHealthForSteal.Heal(finalDamage * healthSteal);
        }
    }

    private void PerformRangedAttackForWeapon(WeaponConfig config, int mountPointIndex)
    {
        if (!config.TryGetCurrentRangedData(out RangedData rangedData))
        {
            return;
        }

        if (rangedData.bulletPrefab == null)
        {
            return;
        }

        Vector2 spawnPosition = transform.position;
        Vector2 direction = Vector2.right;

        if (mountPointIndex >= 0 && mountPointIndex < weaponMountPoints.Length && weaponMountPoints[mountPointIndex] != null)
        {
            spawnPosition = weaponMountPoints[mountPointIndex].position;
            direction = weaponMountPoints[mountPointIndex].right;
        }

        if (autoAimAtEnemy)
        {
            float actualRange = GetWeaponRange(config);
            GameObject nearestEnemy = FindNearestAliveEnemy(spawnPosition, actualRange);
            if (nearestEnemy != null)
            {
                Vector2 preciseDirection = ((Vector2)nearestEnemy.transform.position - spawnPosition);
                if (preciseDirection.sqrMagnitude > 0.0001f)
                {
                    direction = preciseDirection.normalized;
                }
            }
        }

        if (direction.sqrMagnitude <= 0.0001f)
        {
            direction = Vector2.right;
        }
        else
        {
            direction.Normalize();
        }

        float playerMelee = statHandler.GetStat(StatType.MeleeDamage);
        float playerRanged = statHandler.GetStat(StatType.RangedDamage);
        float playerElemental = statHandler.GetStat(StatType.ElementalDamage);
        float damageMultiplier = statHandler.GetStat(StatType.Damage);

        float totalDamage = config.GetTotalDamage(playerMelee, playerRanged, playerElemental);
        totalDamage *= (1f + damageMultiplier);
        totalDamage = ApplyDamageModifiers(config, totalDamage, null);

        Vector3 spawnPos3D = new Vector3(spawnPosition.x, spawnPosition.y, 0f);

        EnsureBulletsParent();

        GameObject bullet = GameObjectPoolManager.Instance.Spawn(rangedData.bulletPrefab, spawnPos3D, Quaternion.identity, bulletsParent);

        if (bullet.TryGetComponent<Bullet>(out Bullet bulletScript))
        {
            bulletScript.Initialize(direction, totalDamage, rangedData.bulletSpeed, rangedData.pierceCount, gameObject, statHandler);
        }
        else
        {
            GameObjectPoolManager.Instance.Release(bullet);
        }
    }

    private void EnsureWeaponModifiersLoaded(WeaponConfig config)
    {
        if (config == null) return;
        if (IsModifierReady(config)) return;
        if (loadingModifiers.Contains(config)) return;

        List<AssetReferenceT<WeaponModifier>> modifierRefs = config.GetCurrentModifierRefs();
        if (modifierRefs == null || modifierRefs.Count == 0)
        {
            loadedModifiersByWeapon[config] = new List<WeaponModifier>();
            return;
        }

        loadingModifiers.Add(config);
        List<WeaponModifier> loadedList = new List<WeaponModifier>();
        int remaining = 0;

        for (int i = 0; i < modifierRefs.Count; i++)
        {
            AssetReferenceT<WeaponModifier> modifierRef = modifierRefs[i];
            if (modifierRef == null || !modifierRef.RuntimeKeyIsValid())
            {
                continue;
            }

            remaining++;
            AddressablesMgr.Instance.LoadAsset<WeaponModifier>(modifierRef, (modifier) =>
            {
                if (modifier != null)
                {
                    loadedList.Add(modifier);
                    loadedModifierAssets.Add(modifier);
                }

                remaining--;
                if (remaining <= 0)
                {
                    loadedModifiersByWeapon[config] = loadedList;
                    loadingModifiers.Remove(config);
                }
            });
        }

        if (remaining == 0)
        {
            loadedModifiersByWeapon[config] = loadedList;
            loadingModifiers.Remove(config);
        }
    }

    private bool IsModifierReady(WeaponConfig config)
    {
        if (config == null) return false;
        return loadedModifiersByWeapon.ContainsKey(config) && !loadingModifiers.Contains(config);
    }

    private IEnumerator PreloadInitialModifiersCoroutine()
    {
        initialModifierPreloadCompleted = false;

        HashSet<WeaponConfig> uniqueWeapons = new HashSet<WeaponConfig>();
        List<WeaponManager.WeaponSlot> slots = weaponManager.GetAllWeapons();
        for (int i = 0; i < slots.Count; i++)
        {
            WeaponConfig cfg = slots[i]?.weaponConfig;
            if (cfg != null)
            {
                uniqueWeapons.Add(cfg);
            }
        }

        foreach (WeaponConfig cfg in uniqueWeapons)
        {
            EnsureWeaponModifiersLoaded(cfg);
        }

        bool hasPending = true;
        while (hasPending)
        {
            hasPending = false;
            foreach (WeaponConfig cfg in uniqueWeapons)
            {
                if (!IsModifierReady(cfg))
                {
                    hasPending = true;
                    break;
                }
            }

            if (hasPending)
            {
                yield return null;
            }
        }

        initialModifierPreloadCompleted = true;
    }

    private void InvokeModifiersBeforeAttack(WeaponConfig config, int mountPointIndex)
    {
        if (config == null) return;
        if (!loadedModifiersByWeapon.TryGetValue(config, out List<WeaponModifier> mods) || mods == null) return;
        for (int i = 0; i < mods.Count; i++)
        {
            WeaponModifier modifier = mods[i];
            if (modifier != null)
            {
                modifier.OnBeforeAttack(gameObject, config, mountPointIndex);
            }
        }
    }

    private void InvokeModifiersAfterAttack(WeaponConfig config, int mountPointIndex)
    {
        if (config == null) return;
        if (!loadedModifiersByWeapon.TryGetValue(config, out List<WeaponModifier> mods) || mods == null) return;
        for (int i = 0; i < mods.Count; i++)
        {
            WeaponModifier modifier = mods[i];
            if (modifier != null)
            {
                modifier.OnAfterAttack(gameObject, config, mountPointIndex);
            }
        }
    }

    private float ApplyDamageModifiers(WeaponConfig config, float inputDamage, Enemy target)
    {
        if (config == null) return inputDamage;
        if (!loadedModifiersByWeapon.TryGetValue(config, out List<WeaponModifier> mods) || mods == null) return inputDamage;

        float damage = inputDamage;
        for (int i = 0; i < mods.Count; i++)
        {
            WeaponModifier modifier = mods[i];
            if (modifier != null)
            {
                damage = modifier.ModifyDamage(damage, gameObject, config, target);
            }
        }
        return damage;
    }

    private void InvokeModifiersAfterHit(WeaponConfig config, Enemy target, float finalDamage)
    {
        if (config == null || target == null) return;
        if (!loadedModifiersByWeapon.TryGetValue(config, out List<WeaponModifier> mods) || mods == null) return;
        for (int i = 0; i < mods.Count; i++)
        {
            WeaponModifier modifier = mods[i];
            if (modifier != null)
            {
                modifier.OnAfterHit(gameObject, config, target, finalDamage);
            }
        }
    }

    /// <summary>
    /// 获取会话初始武器配置（与 WeaponManager 槽位可能不同步，仅作只读参考）。
    /// </summary>
    public WeaponConfig GetWeaponConfig() => weaponConfig;

    /// <summary>
    /// 获取第一槽武器的攻击冷却进度（0–1）。
    /// </summary>
    public float GetCooldownProgress()
    {
        List<WeaponManager.WeaponSlot> slots = weaponManager.GetAllWeapons();
        if (slots == null || slots.Count == 0) return 1f;

        WeaponConfig cfg = slots[0].weaponConfig;
        int mountIdx = slots[0].mountPointIndex;
        if (cfg == null) return 1f;

        if (!weaponLastAttackTime.ContainsKey(mountIdx))
        {
            return 1f;
        }

        float cooldown = Mathf.Max(GetWeaponCooldown(cfg), 0.0001f);
        float elapsed = Time.time - weaponLastAttackTime[mountIdx];
        return Mathf.Clamp01(elapsed / cooldown);
    }

    private void OnDestroy()
    {
        foreach (WeaponModifier modifier in loadedModifierAssets)
        {
            if (modifier != null)
            {
                AddressablesMgr.Instance.Release(modifier);
            }
        }
        loadedModifierAssets.Clear();
        loadedModifiersByWeapon.Clear();
        loadingModifiers.Clear();
    }

    /// <summary>
    /// 在 Scene 视图中绘制攻击范围（编辑器下调试用）。
    /// </summary>
    private void OnDrawGizmosSelected()
    {
        //编辑器下 Gizmo 可能早于 Awake 调用，需兜底获取引用
        if (weaponManager == null)
        {
            weaponManager = GetComponent<WeaponManager>();
        }
        if (weaponManager == null)
        {
            return;
        }

        List<WeaponManager.WeaponSlot> slots = weaponManager.GetAllWeapons();
        if (slots == null)
        {
            return;
        }

        for (int s = 0; s < slots.Count; s++)
        {
            WeaponConfig cfg = slots[s].weaponConfig;
            int i = slots[s].mountPointIndex;
            if (cfg == null || i < 0 || i >= weaponMountPoints.Length || weaponMountPoints[i] == null) continue;

            float actualRange = GetWeaponRange(cfg);
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(weaponMountPoints[i].position, actualRange);
        }
    }
}

