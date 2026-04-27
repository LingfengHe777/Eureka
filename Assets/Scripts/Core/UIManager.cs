using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// UI：Canvas 懒加载、面板显示/隐藏、局间清理。
/// </summary>
public class UIManager
{
    private static readonly UIManager instance = new UIManager();
    public static UIManager Instance => instance;

    private readonly Dictionary<string, BasePanel> panelDic = new Dictionary<string, BasePanel>();

    private Transform canvasTrans;
    private bool isCanvasReady = false;

    private readonly List<Action> pendingShowRequests = new List<Action>();

    /// <summary>
    /// 异步加载 Canvas 并执行队列中的 Show。
    /// </summary>
    private UIManager()
    {
        AddressablesMgr.Instance.LoadAsset<GameObject>("Canvas", (obj) =>
        {
            GameObject canvas = GameObject.Instantiate(obj);
            canvasTrans = canvas.transform;
            GameObject.DontDestroyOnLoad(canvas);
            isCanvasReady = true;

            foreach (var action in pendingShowRequests)
            {
                action.Invoke();
            }
            pendingShowRequests.Clear();
        });
    }

    #region 显示

    /// <summary>
    /// 显示指定类型面板（已存在则仅 Show）。
    /// </summary>
    public void ShowPanel<T>(Action<T> onComplete = null) where T : BasePanel
    {
        Action showAction = () =>
        {
            string panelName = typeof(T).Name;

            if (panelDic.ContainsKey(panelName))
            {
                T existingPanel = panelDic[panelName] as T;
                if (existingPanel != null)
                {
                    existingPanel.ShowMe();
                    onComplete?.Invoke(existingPanel);
                    return;
                }
                else
                {
                    panelDic.Remove(panelName);
                }
            }

            AddressablesMgr.Instance.LoadAsset<GameObject>(panelName, (obj) =>
            {
                GameObject panelObj = GameObject.Instantiate(obj, canvasTrans, false);
                T panel = panelObj.GetComponent<T>();
                panelDic.Add(panelName, panel);

                panel.ShowMe();

                onComplete?.Invoke(panel);
            });
        };

        if (isCanvasReady)
        {
            showAction();
        }
        else
        {
            pendingShowRequests.Add(showAction);
        }
    }

    #endregion

    #region 隐藏

    /// <summary>
    /// 销毁并移除面板实例。
    /// </summary>
    public void HidePanel<T>(bool isFade = true) where T : BasePanel
    {
        string panelName = typeof(T).Name;
        if (!panelDic.ContainsKey(panelName))
            return;

        BasePanel panel = panelDic[panelName];

        if (isFade)
        {
            panel.HideMe(() =>
            {
                GameObject.Destroy(panel.gameObject);
                panelDic.Remove(panelName);
            });
        }
        else
        {
            GameObject.Destroy(panel.gameObject);
            panelDic.Remove(panelName);
        }
    }

    #endregion

    /// <summary>
    /// 重载/离场景前：恢复 timeScale 并移除局内 UI，避免悬空引用。
    /// </summary>
    public void TeardownPersistentGameplayPanels()
    {
        Time.timeScale = 1f;
        HidePanel<StorePanel>(false);
        HidePanel<UpgradePanel>(false);
        HidePanel<PostWavesChoicePanel>(false);
        HidePanel<GameEndPanel>(false);
        HidePanel<GamePanel>(false);
    }

    #region 查询

    /// <summary>
    /// 按类型取当前面板。
    /// </summary>
    public bool TryGetPanel<T>(out T panel) where T : BasePanel
    {
        string panelName = typeof(T).Name;
        if (panelDic.TryGetValue(panelName, out BasePanel p))
        {
            panel = p as T;
            return true;
        }

        panel = null;
        return false;
    }

    #endregion
}
