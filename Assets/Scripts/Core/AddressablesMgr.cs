using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.ResourceManagement.ResourceProviders;
using UnityEngine.SceneManagement;

/// <summary>
/// Addressables：句柄缓存、LIFO 释放、场景加载与卸载。
/// </summary>
public class AddressablesMgr
{
    private static readonly Lazy<AddressablesMgr> _instance =
        new Lazy<AddressablesMgr>(() => new AddressablesMgr());

    /// <summary>
    /// 单例。
    /// </summary>
    public static AddressablesMgr Instance => _instance.Value;

    //多次 Load 同一资源时句柄入栈，Release 按栈顶释放
    private Dictionary<object, List<AsyncOperationHandle>> _handleCache;

    /// <summary>
    /// 单例。
    /// </summary>
    private AddressablesMgr()
    {
        _handleCache = new Dictionary<object, List<AsyncOperationHandle>>();
    }

    /// <summary>
    /// 缓存句柄供 Release 按序释放。
    /// </summary>
    private void CacheHandle(object key, AsyncOperationHandle handle)
    {
        if (key == null)
        {
            return;
        }

        if (!_handleCache.TryGetValue(key, out List<AsyncOperationHandle> list))
        {
            list = new List<AsyncOperationHandle>(1);
            _handleCache[key] = list;
        }

        list.Add(handle);
    }

    #region 单资源

    /// <summary>
    /// 按地址异步加载，成功则回调并缓存句柄。
    /// </summary>
    public void LoadAsset<T>(string address, Action<T> callback)
    {
        if (string.IsNullOrEmpty(address))
        {
            Debug.LogError($"[AddressablesMgr] LoadAsset<{typeof(T).Name}> failed: address is null or empty.");
            callback?.Invoke(default);
            return;
        }

        Addressables.LoadAssetAsync<T>(address).Completed += (handle) =>
        {
            if (handle.Status == AsyncOperationStatus.Succeeded)
            {
                T result = handle.Result;
                if (result != null)
                {
                    CacheHandle(result, handle);
                }
                callback?.Invoke(result);
            }
            else
            {
                Debug.LogError($"[AddressablesMgr] LoadAsset<{typeof(T).Name}> failed. Address: {address}, Exception: {handle.OperationException}");
                Addressables.Release(handle);
                callback?.Invoke(default);
            }
        };
    }

    /// <summary>
    /// 通过 AssetReference 加载（RuntimeKey 为地址）。
    /// </summary>
    public void LoadAsset<T>(AssetReference assetRef, Action<T> callback) where T : UnityEngine.Object
    {
        if (assetRef == null || !assetRef.RuntimeKeyIsValid())
        {
            Debug.LogError($"[AddressablesMgr] LoadAsset<{typeof(T).Name}> failed: invalid AssetReference runtime key.");
            callback?.Invoke(default);
            return;
        }

        LoadAsset<T>(assetRef.RuntimeKey.ToString(), callback);
    }

    #endregion

    #region 批量

    /// <summary>
    /// 按 Label 批量加载同类型资源。
    /// </summary>
    public void LoadAssets<T>(string label, Action<IList<T>> callback)
    {
        if (string.IsNullOrEmpty(label))
        {
            Debug.LogError($"[AddressablesMgr] LoadAssets<{typeof(T).Name}> failed: label is null or empty.");
            callback?.Invoke(null);
            return;
        }

        Addressables.LoadResourceLocationsAsync(label, typeof(T)).Completed += (locHandle) =>
        {
            if (locHandle.Status == AsyncOperationStatus.Succeeded && locHandle.Result.Count > 0)
            {
                var loadHandle = Addressables.LoadAssetsAsync<T>(locHandle.Result, null);
                loadHandle.Completed += (handle) =>
                {
                    if (handle.Status == AsyncOperationStatus.Succeeded)
                    {
                        IList<T> resultList = handle.Result;
                        CacheHandle(resultList, handle);
                        callback?.Invoke(resultList);
                    }
                    else
                    {
                        Debug.LogError($"[AddressablesMgr] LoadAssets<{typeof(T).Name}> failed while loading locations. Label: {label}, Exception: {handle.OperationException}");
                        Addressables.Release(handle);
                        callback?.Invoke(null);
                    }
                };
            }
            else
            {
                string exceptionText = locHandle.OperationException != null ? locHandle.OperationException.ToString() : "None";
                Debug.LogError($"[AddressablesMgr] LoadAssets<{typeof(T).Name}> failed to resolve locations. Label: {label}, Count: {(locHandle.Result == null ? -1 : locHandle.Result.Count)}, Exception: {exceptionText}");
                Addressables.Release(locHandle);
                callback?.Invoke(null);
            }
        };
    }

    #endregion

    #region 释放

    /// <summary>
    /// 释放该对象最近一次加载句柄（须为回调返回的同一引用）；assetOrList 为加载回调返回的原始对象。
    /// </summary>
    public void Release(object assetOrList)
    {
        if (assetOrList == null) return;

        if (!_handleCache.TryGetValue(assetOrList, out List<AsyncOperationHandle> list) || list == null || list.Count == 0)
        {
            return;
        }

        int last = list.Count - 1;
        AsyncOperationHandle handle = list[last];
        list.RemoveAt(last);
        if (list.Count == 0)
        {
            _handleCache.Remove(assetOrList);
        }

        if (handle.IsValid())
        {
            Addressables.Release(handle);
        }
    }

    /// <summary>
    /// 同 Release(object)。
    /// </summary>
    public void Release<T>(IList<T> assets) => Release((object)assets);

    #endregion

    #region 场景

    /// <summary>
    /// 异步加载 Addressable 场景（address 场景地址；loadMode Single/Additive；callback 完成回调）。
    /// </summary>
    public void LoadScene(string address, LoadSceneMode loadMode = LoadSceneMode.Single, Action<SceneInstance> callback = null)
    {
        AsyncOperationHandle<SceneInstance> handle = Addressables.LoadSceneAsync(address, loadMode);
        handle.Completed += (op) =>
        {
            if (op.Status == AsyncOperationStatus.Succeeded)
            {
                CacheHandle(op.Result, op);
                callback?.Invoke(op.Result);
            }
            else
            {
                Addressables.Release(handle);
            }
        };
    }

    #endregion
}
