using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 通用对象池
/// </summary>
public class GameObjectPoolManager : MonoBehaviour
{
    private static GameObjectPoolManager instance;
    public static GameObjectPoolManager Instance
    {
        get
        {
            if (instance == null)
            {
                GameObject go = new GameObject("GameObjectPoolManager");
                instance = go.AddComponent<GameObjectPoolManager>();
                DontDestroyOnLoad(go);
            }
            return instance;
        }
    }

    private readonly Dictionary<GameObject, Queue<GameObject>> poolByPrefab = new();
    private readonly Dictionary<GameObject, GameObject> prefabByInstance = new();
    private Transform poolRoot;

    [SerializeField]
    [Tooltip("每种预制体最多缓存数量，超出即销毁")]
    private int maxCachedPerPrefab = 128;

    [SerializeField]
    [Tooltip("全局最大缓存数量，超出即销毁")]
    private int maxCachedTotal = 2048;

    private int cachedCount;
    private int createdCount;
    private int destroyedCount;

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;
        DontDestroyOnLoad(gameObject);
        EnsurePoolRoot();
    }

    /// <summary>
    /// 取池或 Instantiate，激活并置于场景。
    /// </summary>
    public GameObject Spawn(GameObject prefab, Vector3 position, Quaternion rotation, Transform parent = null)
    {
        if (prefab == null) return null;
        EnsurePoolRoot();

        if (!poolByPrefab.TryGetValue(prefab, out Queue<GameObject> queue))
        {
            queue = new Queue<GameObject>();
            poolByPrefab[prefab] = queue;
        }

        GameObject instanceObj = null;
        while (queue.Count > 0 && instanceObj == null)
        {
            instanceObj = queue.Dequeue();
            if (cachedCount > 0)
            {
                cachedCount--;
            }
        }

        if (instanceObj == null)
        {
            instanceObj = Instantiate(prefab, position, rotation, parent);
            prefabByInstance[instanceObj] = prefab;
            createdCount++;
        }
        else
        {
            Transform tr = instanceObj.transform;
            tr.SetParent(parent, false);
            tr.SetPositionAndRotation(position, rotation);
            instanceObj.SetActive(true);
        }

        return instanceObj;
    }

    /// <summary>
    /// 归还或超限时销毁；可延迟。
    /// </summary>
    public void Release(GameObject instanceObj, float delay = 0f)
    {
        if (instanceObj == null) return;

        if (delay > 0f)
        {
            StartCoroutine(ReleaseDelayed(instanceObj, delay));
            return;
        }

        ReleaseImmediate(instanceObj);
    }

    /// <summary>
    /// 延迟后 ReleaseImmediate。
    /// </summary>
    private IEnumerator ReleaseDelayed(GameObject instanceObj, float delay)
    {
        yield return new WaitForSeconds(delay);
        ReleaseImmediate(instanceObj);
    }

    /// <summary>
    /// 入池或 Destroy。
    /// </summary>
    private void ReleaseImmediate(GameObject instanceObj)
    {
        if (instanceObj == null) return;
        EnsurePoolRoot();

        if (!prefabByInstance.TryGetValue(instanceObj, out GameObject prefab) || prefab == null)
        {
            Destroy(instanceObj);
            destroyedCount++;
            return;
        }

        if (!poolByPrefab.TryGetValue(prefab, out Queue<GameObject> queue))
        {
            queue = new Queue<GameObject>();
            poolByPrefab[prefab] = queue;
        }

        bool exceedPerPrefabLimit = maxCachedPerPrefab > 0 && queue.Count >= maxCachedPerPrefab;
        bool exceedTotalLimit = maxCachedTotal > 0 && cachedCount >= maxCachedTotal;
        if (exceedPerPrefabLimit || exceedTotalLimit)
        {
            prefabByInstance.Remove(instanceObj);
            Destroy(instanceObj);
            destroyedCount++;
            return;
        }

        instanceObj.SetActive(false);
        instanceObj.transform.SetParent(poolRoot, false);
        queue.Enqueue(instanceObj);
        cachedCount++;
    }

    /// <summary>
    /// 全部已激活借出体归还（波次结束等）；池中未激活项跳过。
    /// </summary>
    public void ReleaseAllActivePooledInstances()
    {
        EnsurePoolRoot();
        List<GameObject> keys = new List<GameObject>(prefabByInstance.Keys);
        for (int i = 0; i < keys.Count; i++)
        {
            GameObject obj = keys[i];
            if (obj == null)
            {
                continue;
            }

            //已回池 inactive，仅处理场景内借出体
            if (!obj.activeSelf)
            {
                continue;
            }

            ReleaseImmediate(obj);
        }
    }

    /// <summary>
    /// 懒创建 PooledObjects 根。
    /// </summary>
    private void EnsurePoolRoot()
    {
        if (poolRoot != null) return;

        GameObject root = new GameObject("PooledObjects");
        root.transform.SetParent(transform, false);
        poolRoot = root.transform;
    }

    /// <summary>
    /// 销毁追踪到的全部实例并清空；换场景/新局前防 DDOL 残留。
    /// </summary>
    public void ClearAllPools()
    {
        StopAllCoroutines();

        var instances = new List<GameObject>(prefabByInstance.Keys);
        for (int i = 0; i < instances.Count; i++)
        {
            GameObject obj = instances[i];
            if (obj != null)
            {
                Destroy(obj);
                destroyedCount++;
            }
        }

        prefabByInstance.Clear();
        poolByPrefab.Clear();
        cachedCount = 0;
    }
}
