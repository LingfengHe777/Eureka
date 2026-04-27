using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// 武器管理器：挂载点（gunPos1–6 环形 + gunPos7 单人）、武器槽与合成。
/// WeaponController 从本组件取挂载点。优先使用 Inspector 绑定，缺失时会按名称自动解析。
/// </summary>
public class WeaponManager : MonoBehaviour
{
    private const string MeleeVisualInstanceName = "MeleeWeaponVisualInstance";
    //最大可同时持有的武器数
    public const int MAX_WEAPON_COUNT = 6;

    //普通挂载点数量（gunPos1–gunPos6）
    public const int NORMAL_MOUNT_COUNT = 6;

    //特殊挂载点索引（gunPos7）
    public const int SOLO_MOUNT_INDEX = 6;

    //挂载点数组长度：6个普通+1个特殊
    public const int MOUNT_POINT_COUNT = 7;

    [Tooltip("武器显示尺寸，世界单位")]
    public float weaponDisplaySize = 0.8f;

    /// <summary>
    /// 单槽：配置与挂载点索引
    /// </summary>
    [System.Serializable]
    public class WeaponSlot
    {
        //武器配置
        public WeaponConfig weaponConfig;
        //挂载点索引
        public int mountPointIndex;

        /// <summary>
        /// 构造武器槽
        /// </summary>
        public WeaponSlot(WeaponConfig config, int mountIndex)
        {
            weaponConfig = config;
            mountPointIndex = mountIndex;
        }
    }

    //武器背包（唯一真值，顺序即背包顺序）
    private readonly List<WeaponConfig> weaponBackpack = new();
    //当前回合挂载映射（仅用于战斗与显示）
    private readonly List<WeaponSlot> weaponSlots = new();

    [Header("挂载点")]
    [SerializeField]
    [Tooltip("索引 0～5 为 gunPos1～6，索引 6 为 gunPos7")]
    private Transform[] mountPoints = new Transform[MOUNT_POINT_COUNT];

    //挂载点是否已解析
    private bool mountPointsInitialized = false;
    private bool mountPointResolveWarned;

    //添加武器（配置，挂载点索引）
    public System.Action<WeaponConfig, int> OnWeaponAdded;

    //移除武器（配置，挂载点索引）
    public System.Action<WeaponConfig, int> OnWeaponRemoved;

    //合成完成（旧武器1，旧武器2，合成结果）
    public System.Action<WeaponConfig, WeaponConfig, WeaponConfig> OnWeaponMerged;

    //数量变化（当前数，上限）
    public System.Action<int, int> OnWeaponCountChanged;

    private void Start()
    {
        InitializeMountPoints();
        InitializeFromSession();
    }

    /// <summary>
    /// 确保挂载点数组长度正确
    /// </summary>
    private void InitializeMountPoints()
    {
        if (mountPoints == null || mountPoints.Length != MOUNT_POINT_COUNT)
        {
            mountPoints = new Transform[MOUNT_POINT_COUNT];
        }
        TryAutoResolveMountPoints();
        mountPointsInitialized = true;
    }

    /// <summary>
    /// 返回全部挂载点数组
    /// </summary>
    public Transform[] GetMountPoints()
    {
        if (!mountPointsInitialized)
        {
            InitializeMountPoints();
        }
        return mountPoints;
    }

    /// <summary>
    /// 按索引取挂载点，越界返回null
    /// </summary>
    public Transform GetMountPoint(int index)
    {
        if (index < 0 || index >= MOUNT_POINT_COUNT) return null;
        if (!mountPointsInitialized) InitializeMountPoints();
        return mountPoints[index];
    }

    /// <summary>
    /// 从GameSession带入开局武器
    /// </summary>
    private void InitializeFromSession()
    {
        weaponBackpack.Clear();
        weaponSlots.Clear();
        GameSession session = GameSessionManager.Instance.GetSession();
        if (session != null && session.selectedWeapon != null)
        {
            TryAddWeapon(session.selectedWeapon);
        }
    }

    /// <summary>
    /// 添加武器：未满则直接加，满则尝试被动合成
    /// </summary>
    public bool TryAddWeapon(WeaponConfig weaponConfig)
    {
        if (weaponConfig == null)
        {
            return false;
        }
        //如果背包数量小于上限，则直接加入背包
        if (weaponBackpack.Count < MAX_WEAPON_COUNT)
        {
            return AddWeaponDirectly(weaponConfig);
        }
        //满背包时尝试与背包内某把同级同名武器合成
        int mergeableIndex = FindMergeableBackpackIndex(weaponConfig);
        if (mergeableIndex >= 0)
        {
            return PerformMergeWithIncoming(mergeableIndex, weaponConfig);
        }
        return false;
    }

    /// <summary>
    /// 分配到可用挂载点并刷新显示与事件
    /// </summary>
    private bool AddWeaponDirectly(WeaponConfig weaponConfig)
    {
        weaponBackpack.Add(weaponConfig);
        RebuildMountVisualsFromWeaponBackpack();

        int mappedMountIndex = ResolveMappedMountIndex(weaponBackpack.Count - 1, weaponBackpack.Count);
        OnWeaponAdded?.Invoke(weaponConfig, mappedMountIndex);
        OnWeaponCountChanged?.Invoke(weaponBackpack.Count, MAX_WEAPON_COUNT);
        return true;
    }

    private int FindMergeableBackpackIndex(WeaponConfig newWeapon)
    {
        for (int i = 0; i < weaponBackpack.Count; i++)
        {
            WeaponConfig candidate = weaponBackpack[i];
            if (candidate != null && CanMergeWeapons(candidate, newWeapon))
            {
                return i;
            }
        }

        return -1;
    }

    /// <summary>
    /// 新购入武器与背包内目标位合成，结果回写到该背包位。
    /// </summary>
    private bool PerformMergeWithIncoming(int existingBackpackIndex, WeaponConfig newWeapon)
    {
        if (existingBackpackIndex < 0 || existingBackpackIndex >= weaponBackpack.Count)
        {
            return false;
        }

        WeaponConfig existingWeapon = weaponBackpack[existingBackpackIndex];
        if (existingWeapon == null || existingWeapon.GetCurrentLevel() >= 4) return false;

        WeaponConfig mergedWeapon = CreateMergedWeapon(existingWeapon, newWeapon);
        if (mergedWeapon == null) return false;

        weaponBackpack[existingBackpackIndex] = mergedWeapon;
        RebuildMountVisualsFromWeaponBackpack();
        OnWeaponMerged?.Invoke(existingWeapon, newWeapon, mergedWeapon);
        return true;
    }

    /// <summary>
    /// 生成高一级武器配置；等级属性由WeaponConfig数据驱动
    /// </summary>
    private WeaponConfig CreateMergedWeapon(WeaponConfig weapon1, WeaponConfig weapon2)
    {
        //如果两把武器等级不同或id不同，则返回null
        if (weapon1.GetCurrentLevel() != weapon2.GetCurrentLevel() || weapon1.WeaponIdOrName != weapon2.WeaponIdOrName) return null;
        //计算新等级
        int newLevel = weapon1.GetCurrentLevel() + 1;
        //如果新等级大于4，则返回null
        if (newLevel > 4) return null;
        //创建合成结果
        return WeaponConfig.CreateRuntimeCopy(weapon1, newLevel);
    }

    /// <summary>
    /// 清除该挂载点下的武器显示，保留Transform供瞄准
    /// </summary>
    private void ClearWeaponVisual(int mountPointIndex)
    {
        EnsureMountPointsReady();
        if (mountPointIndex < 0 || mountPointIndex >= mountPoints.Length || mountPoints[mountPointIndex] == null) return;
        Transform mountPoint = mountPoints[mountPointIndex];
        CleanupMeleeVisualInstances(mountPoint);
        Transform visual = WeaponConfig.GetOrCreateWeaponVisualChild(mountPoint);
        if (visual != null)
        {
            for (int i = visual.childCount - 1; i >= 0; i--)
            {
                Transform child = visual.GetChild(i);
                if (child != null)
                {
                    Destroy(child.gameObject);
                }
            }

            SpriteRenderer visualSr = visual.GetComponent<SpriteRenderer>();
            if (visualSr != null)
            {
                visualSr.sprite = null;
                visualSr.color = Color.white;
                visualSr.enabled = true;
            }

            visual.localPosition = Vector3.zero;
            visual.localRotation = Quaternion.identity;
            visual.localScale = Vector3.one;
            visual.gameObject.SetActive(true);
        }

        WeaponConfig.ClearMeleeAnimatorFromMount(mountPoint);
    }

    /// <summary>
    /// 近战武器：实例化prefab；远程武器：WeaponVisual + Sprite并按weaponDisplaySize缩放。
    /// </summary>
    private void SetupWeaponVisual(WeaponConfig weaponConfig, int mountPointIndex)
    {
        EnsureMountPointsReady();
        if (mountPointIndex < 0 || mountPointIndex >= mountPoints.Length) return;
        Transform mountPoint = mountPoints[mountPointIndex];
        if (mountPoint == null) return;
        mountPoint.gameObject.SetActive(true);

        ClearWeaponVisual(mountPointIndex);

        Transform visual = WeaponConfig.GetOrCreateWeaponVisualChild(mountPoint);
        if (visual == null) return;
        visual.gameObject.SetActive(true);
        SpriteRenderer sr = visual.GetComponent<SpriteRenderer>();
        if (sr == null) sr = visual.gameObject.AddComponent<SpriteRenderer>();
        sr.enabled = true;

        //近战：恢复为挂在 mountPoint 下（兼容既有动画事件/命中盒链路），远程仍使用 WeaponVisual 节点显示贴图。
        if (weaponConfig.type == WeaponType.Melee && weaponConfig.meleeWeaponPrefab != null)
        {
            GameObject inst = Instantiate(weaponConfig.meleeWeaponPrefab, mountPoint);
            inst.name = MeleeVisualInstanceName;
            TryApplyDisplayScaleToMeleeInstance(inst.transform);

            WeaponController wc = GetComponent<WeaponController>();
            if (wc != null)
            {
                WeaponConfig.InitializeMeleeWeaponHitbox(inst, wc, weaponConfig);
            }

            // 近战视觉完全依赖 prefab；weaponSprite 仅作配置参考，不用于挂点显示。
            sr.sprite = null;
            visual.localScale = Vector3.one;
            sr.color = Color.white;
            weaponConfig.ApplyMeleeAnimatorToMount(mountPoint);
            return;
        }

        Sprite weaponSprite = weaponConfig.weaponSprite;
        if (weaponSprite != null)
        {
            sr.sprite = weaponSprite;

            if (sr.sprite != null)
            {
                float spriteWidth = sr.sprite.bounds.size.x;
                float spriteHeight = sr.sprite.bounds.size.y;
                float spriteMaxSize = Mathf.Max(spriteWidth, spriteHeight);

                if (spriteMaxSize > 0.001f)
                {
                    float scale = weaponDisplaySize / spriteMaxSize;
                    visual.localScale = new Vector3(scale, scale, 1f);
                }
            }
        }
        else
        {
            sr.sprite = null;
            visual.localScale = Vector3.one;
        }

        sr.color = Color.white;
        WeaponConfig.ApplyRangedWeaponSpriteSorting(sr, mountPoint);

        weaponConfig.ApplyMeleeAnimatorToMount(mountPoint);
    }

    /// <summary>
    /// 购买入口，语义同TryAddWeapon
    /// </summary>
    public bool TryPurchaseWeapon(WeaponConfig weaponConfig) => TryAddWeapon(weaponConfig);

    /// <summary>
    /// 是否还能买：未满或可合成
    /// </summary>
    public bool CanPurchaseWeapon(WeaponConfig weaponConfig)
    {
        if (weaponConfig == null) return false;
        if (weaponBackpack.Count < MAX_WEAPON_COUNT) return true;
        return FindMergeableBackpackIndex(weaponConfig) >= 0;
    }

    /// <summary>
    /// 当前持有武器数（背包）。
    /// </summary>
    public int GetCurrentWeaponCount() => weaponBackpack.Count;

    /// <summary>
    /// 最大武器数上限。
    /// </summary>
    public int GetMaxWeaponCount() => MAX_WEAPON_COUNT;

    /// <summary>
    /// 返回槽列表副本
    /// </summary>
    public List<WeaponSlot> GetAllWeapons() => new List<WeaponSlot>(weaponSlots);

    /// <summary>
    /// 背包顺序视图（用于商店背包显示，不依赖当前回合挂载映射）。
    /// </summary>
    public List<WeaponConfig> GetWeaponBackpack()
    {
        return new List<WeaponConfig>(weaponBackpack);
    }

    /// <summary>
    /// 清空所有挂载点显示（不改背包槽数据）。
    /// </summary>
    public void ClearAllWeaponMountVisuals()
    {
        for (int i = 0; i < MOUNT_POINT_COUNT; i++)
        {
            ClearWeaponVisual(i);
        }
    }

    /// <summary>
    /// 指定两把配置进行手动合成
    /// </summary>
    public bool TryManualMerge(WeaponConfig weapon1, WeaponConfig weapon2)
    {
        if (!CanMergeWeapons(weapon1, weapon2)) return false;

        int firstIndex = weaponBackpack.IndexOf(weapon1);
        int secondIndex = weaponBackpack.IndexOf(weapon2);
        if (firstIndex < 0 || secondIndex < 0 || firstIndex == secondIndex) return false;

        WeaponConfig merged = CreateMergedWeapon(weapon1, weapon2);
        if (merged == null) return false;

        int keepIndex = Mathf.Min(firstIndex, secondIndex);
        int removeIndex = Mathf.Max(firstIndex, secondIndex);
        weaponBackpack.RemoveAt(removeIndex);
        weaponBackpack[keepIndex] = merged;

        RebuildMountVisualsFromWeaponBackpack();
        OnWeaponMerged?.Invoke(weapon1, weapon2, merged);
        return true;
    }

    /// <summary>
    /// 按两个挂载点索引手动合成
    /// </summary>
    public bool TryManualMergeByMountIndex(int firstMountIndex, int secondMountIndex)
    {
        int firstBackpackIndex = ResolveBackpackIndexByMountIndex(firstMountIndex);
        int secondBackpackIndex = ResolveBackpackIndexByMountIndex(secondMountIndex);
        if (firstBackpackIndex < 0 || secondBackpackIndex < 0 || firstBackpackIndex == secondBackpackIndex) return false;

        WeaponConfig firstWeapon = weaponBackpack[firstBackpackIndex];
        WeaponConfig secondWeapon = weaponBackpack[secondBackpackIndex];
        if (!CanMergeWeapons(firstWeapon, secondWeapon)) return false;
        WeaponConfig merged = CreateMergedWeapon(firstWeapon, secondWeapon);
        if (merged == null) return false;

        int keepIndex = Mathf.Min(firstBackpackIndex, secondBackpackIndex);
        int removeIndex = Mathf.Max(firstBackpackIndex, secondBackpackIndex);
        WeaponConfig oldA = weaponBackpack[firstBackpackIndex];
        WeaponConfig oldB = weaponBackpack[secondBackpackIndex];

        weaponBackpack.RemoveAt(removeIndex);
        weaponBackpack[keepIndex] = merged;
        RebuildMountVisualsFromWeaponBackpack();
        OnWeaponMerged?.Invoke(oldA, oldB, merged);
        return true;
    }

    /// <summary>
    /// 按挂载点移除武器（出售）
    /// </summary>
    public bool TryRemoveWeaponAtMountPoint(int mountPointIndex, out WeaponConfig removedWeapon)
    {
        removedWeapon = null;
        int backpackIndex = ResolveBackpackIndexByMountIndex(mountPointIndex);
        return TryRemoveWeaponAtBackpackIndex(backpackIndex, out removedWeapon);
    }

    public bool TryRemoveWeaponAtBackpackIndex(int backpackIndex, out WeaponConfig removedWeapon)
    {
        removedWeapon = null;
        if (backpackIndex < 0 || backpackIndex >= weaponBackpack.Count) return false;

        removedWeapon = weaponBackpack[backpackIndex];
        weaponBackpack.RemoveAt(backpackIndex);
        RebuildMountVisualsFromWeaponBackpack();
        OnWeaponCountChanged?.Invoke(weaponBackpack.Count, MAX_WEAPON_COUNT);
        return removedWeapon != null;
    }

    /// <summary>
    /// 同名同类型同级且未满级时可合成
    /// </summary>
    public bool CanMergeWeapons(WeaponConfig weapon1, WeaponConfig weapon2)
    {
        if (weapon1 == null || weapon2 == null) return false;

        if (weapon1.WeaponIdOrName == weapon2.WeaponIdOrName && weapon1.type != weapon2.type)
        {
            return false;
        }

        return weapon1.GetCurrentLevel() == weapon2.GetCurrentLevel() &&
               weapon1.WeaponIdOrName == weapon2.WeaponIdOrName &&
               weapon1.type == weapon2.type &&
               weapon1.GetCurrentLevel() < 4;
    }

    /// <summary>
    /// 按挂载点取当前武器配置
    /// </summary>
    public bool TryGetWeaponAtMountPoint(int mountPointIndex, out WeaponConfig weapon)
    {
        weapon = null;
        WeaponSlot slot = weaponSlots.FirstOrDefault(s => s.mountPointIndex == mountPointIndex);
        if (slot == null || slot.weaponConfig == null) return false;
        weapon = slot.weaponConfig;
        return true;
    }

    /// <summary>
    /// 该槽是否存在另一把可与之合成的武器
    /// </summary>
    public bool CanQuickMergeAtMountPoint(int mountPointIndex)
    {
        int backpackIndex = ResolveBackpackIndexByMountIndex(mountPointIndex);
        return CanQuickMergeAtBackpackIndex(backpackIndex);
    }

    public bool CanQuickMergeAtBackpackIndex(int backpackIndex)
    {
        if (backpackIndex < 0 || backpackIndex >= weaponBackpack.Count) return false;
        WeaponConfig source = weaponBackpack[backpackIndex];
        if (source == null || source.GetCurrentLevel() >= 4) return false;

        for (int i = 0; i < weaponBackpack.Count; i++)
        {
            if (i == backpackIndex) continue;
            WeaponConfig candidate = weaponBackpack[i];
            if (candidate == null) continue;
            if (CanMergeWeapons(source, candidate)) return true;
        }
        return false;
    }

    /// <summary>
    /// 一键合成：与第一把可合成的其它槽合并
    /// </summary>
    public bool TryQuickMergeAtMountPoint(int mountPointIndex)
    {
        int backpackIndex = ResolveBackpackIndexByMountIndex(mountPointIndex);
        return TryQuickMergeAtBackpackIndex(backpackIndex);
    }

    public bool TryQuickMergeAtBackpackIndex(int backpackIndex)
    {
        if (backpackIndex < 0 || backpackIndex >= weaponBackpack.Count) return false;
        WeaponConfig source = weaponBackpack[backpackIndex];
        if (source == null || source.GetCurrentLevel() >= 4) return false;

        for (int i = 0; i < weaponBackpack.Count; i++)
        {
            if (i == backpackIndex) continue;
            WeaponConfig candidate = weaponBackpack[i];
            if (candidate == null) continue;
            if (!CanMergeWeapons(source, candidate)) continue;

            int keepIndex = Mathf.Min(backpackIndex, i);
            int removeIndex = Mathf.Max(backpackIndex, i);
            WeaponConfig oldA = weaponBackpack[backpackIndex];
            WeaponConfig oldB = weaponBackpack[i];
            WeaponConfig merged = CreateMergedWeapon(oldA, oldB);
            if (merged == null) return false;

            weaponBackpack.RemoveAt(removeIndex);
            weaponBackpack[keepIndex] = merged;
            RebuildMountVisualsFromWeaponBackpack();
            OnWeaponMerged?.Invoke(oldA, oldB, merged);
            return true;
        }
        return false;
    }

    /// <summary>
    /// 清空当前回合挂载映射（背包保留）。
    /// </summary>
    public void ClearMountSlotsForIntermission()
    {
        weaponSlots.Clear();
        ClearAllWeaponMountVisuals();
    }

    /// <summary>
    /// 按背包顺序重建挂载：
    /// - 1把：gunPos7
    /// - 多把：gunPos1..n
    /// </summary>
    public void RebuildMountVisualsFromWeaponBackpack()
    {
        EnsureMountPointsReady();
        weaponSlots.Clear();
        ClearAllWeaponMountVisuals();

        int total = Mathf.Min(weaponBackpack.Count, MAX_WEAPON_COUNT);
        if (total <= 0) return;

        if (total == 1)
        {
            WeaponConfig onlyWeapon = weaponBackpack[0];
            if (onlyWeapon != null)
            {
                weaponSlots.Add(new WeaponSlot(onlyWeapon, SOLO_MOUNT_INDEX));
                SetupWeaponVisual(onlyWeapon, SOLO_MOUNT_INDEX);
            }
            return;
        }

        for (int i = 0; i < total; i++)
        {
            WeaponConfig weapon = weaponBackpack[i];
            if (weapon == null) continue;
            int mountIndex = Mathf.Clamp(i, 0, NORMAL_MOUNT_COUNT - 1);
            weaponSlots.Add(new WeaponSlot(weapon, mountIndex));
            SetupWeaponVisual(weapon, mountIndex);
        }
    }

    /// <summary>
    /// 兼容旧调用：内部已切换为按背包重建。
    /// </summary>
    public void RebuildMountVisualsFromWeaponSlots() => RebuildMountVisualsFromWeaponBackpack();

    private static int ResolveMappedMountIndex(int backpackIndex, int totalCount)
    {
        if (backpackIndex < 0) return -1;
        if (totalCount <= 1) return SOLO_MOUNT_INDEX;
        return Mathf.Clamp(backpackIndex, 0, NORMAL_MOUNT_COUNT - 1);
    }

    private int ResolveBackpackIndexByMountIndex(int mountPointIndex)
    {
        WeaponSlot slot = weaponSlots.FirstOrDefault(s => s.mountPointIndex == mountPointIndex);
        if (slot == null || slot.weaponConfig == null)
        {
            return -1;
        }

        for (int i = 0; i < weaponBackpack.Count; i++)
        {
            if (weaponBackpack[i] == slot.weaponConfig)
            {
                return i;
            }
        }

        return -1;
    }

    private void EnsureMountPointsReady()
    {
        if (!mountPointsInitialized)
        {
            InitializeMountPoints();
            return;
        }

        TryAutoResolveMountPoints();
    }

    private void TryAutoResolveMountPoints()
    {
        if (mountPoints == null || mountPoints.Length != MOUNT_POINT_COUNT)
        {
            return;
        }

        for (int i = 0; i < MOUNT_POINT_COUNT; i++)
        {
            if (mountPoints[i] != null)
            {
                continue;
            }

            string mountName = i == SOLO_MOUNT_INDEX ? "gunPos7" : $"gunPos{i + 1}";
            Transform found = transform.Find(mountName);
            if (found == null)
            {
                found = FindDeepChildByName(transform, mountName);
            }
            mountPoints[i] = found;
        }

        if (mountPointResolveWarned)
        {
            return;
        }

        for (int i = 0; i < MOUNT_POINT_COUNT; i++)
        {
            if (mountPoints[i] == null)
            {
                mountPointResolveWarned = true;
                Debug.LogWarning("[WeaponManager] 挂载点缺失：请在预设体绑定 gunPos1~7。", this);
                break;
            }
        }
    }

    private static Transform FindDeepChildByName(Transform root, string childName)
    {
        if (root == null || string.IsNullOrEmpty(childName))
        {
            return null;
        }

        for (int i = 0; i < root.childCount; i++)
        {
            Transform child = root.GetChild(i);
            if (child.name == childName)
            {
                return child;
            }

            Transform nested = FindDeepChildByName(child, childName);
            if (nested != null)
            {
                return nested;
            }
        }

        return null;
    }

    private void TryApplyDisplayScaleToMeleeInstance(Transform meleeInstance)
    {
        if (meleeInstance == null)
        {
            return;
        }

        SpriteRenderer sr = meleeInstance.GetComponentInChildren<SpriteRenderer>(true);
        if (sr == null || sr.sprite == null)
        {
            return;
        }

        float spriteWidth = sr.sprite.bounds.size.x;
        float spriteHeight = sr.sprite.bounds.size.y;
        float spriteMaxSize = Mathf.Max(spriteWidth, spriteHeight);
        if (spriteMaxSize <= 0.001f)
        {
            return;
        }

        float scale = weaponDisplaySize / spriteMaxSize;
        meleeInstance.localScale = new Vector3(scale, scale, 1f);
    }

    private static void CleanupMeleeVisualInstances(Transform mountPoint)
    {
        if (mountPoint == null)
        {
            return;
        }

        for (int i = mountPoint.childCount - 1; i >= 0; i--)
        {
            Transform child = mountPoint.GetChild(i);
            if (child == null)
            {
                continue;
            }

            bool isNamedMeleeInstance = child.name == MeleeVisualInstanceName;
            bool hasMeleeHitbox = child.GetComponentInChildren<MeleeWeaponHitbox>(true) != null;
            if (!isNamedMeleeInstance && !hasMeleeHitbox)
            {
                continue;
            }

            Destroy(child.gameObject);
        }
    }
}
