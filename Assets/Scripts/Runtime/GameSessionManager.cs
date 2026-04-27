using System.Threading.Tasks;
using UnityEngine;

/// <summary>
/// 游戏会话管理器（单例）：加载并持有 GameSession，供场景间读写。
/// </summary>
public class GameSessionManager : MonoBehaviour
{
    //单例模式
    private static GameSessionManager instance;
    public static GameSessionManager Instance
    {
        get
        {
            if (instance == null)
            {
                GameObject go = new("GameSessionManager");
                instance = go.AddComponent<GameSessionManager>();
                DontDestroyOnLoad(go);
            }
            return instance;
        }
    }

    //当前的游戏会话配置
    [HideInInspector]
    public GameSession currentSession;

    //游戏会话配置类的Addressable查询键
    [Header("可寻址键")]
    public string sessionKey = "session";

    //异步任务源（等待会话加载完成）
    private TaskCompletionSource<GameSession> _sessionTcs = new();

    private void Awake()
    {
        if (instance == null)
        {
            instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else if (instance != this)
        {
            Destroy(gameObject);
            return;
        }

        LoadSession();
    }

    /// <summary>
    /// 通过 Addressables 加载 GameSession 并写入 currentSession。
    /// </summary>
    private void LoadSession()
    {
        AddressablesMgr.Instance.LoadAsset<GameSession>(sessionKey, (session) =>
        {
            currentSession = session;
            if (currentSession == null)
            {
                _sessionTcs.TrySetResult(null);
            }
            else
            {
                _sessionTcs.TrySetResult(currentSession);
            }
        });
    }

    /// <summary>
    /// 异步等待会话就绪后返回（加载失败可能为 null）。
    /// </summary>
    public async Task<GameSession> GetSessionAsync()
    {
        return await _sessionTcs.Task;
    }

    /// <summary>
    /// 返回当前已加载的会话引用。
    /// </summary>
    public GameSession GetSession() => currentSession;

    /// <summary>
    /// 调用当前会话的 Reset。
    /// </summary>
    public void ResetSession()
    {
        if (currentSession != null) currentSession.Reset();
    }

    /// <summary>
    /// 设置会话中选中的角色。
    /// </summary>
    public void SetSelectedCharacter(SpecialStatConfig character)
    {
        if (currentSession != null) currentSession.selectedCharacter = character;
    }

    /// <summary>
    /// 设置会话中选中的武器。
    /// </summary>
    public void SetSelectedWeapon(WeaponConfig weapon)
    {
        if (currentSession != null) currentSession.selectedWeapon = weapon;
    }

    /// <summary>
    /// 设置会话中选中的难度模式。
    /// </summary>
    public void SetSelectedMode(ModeConfig mode)
    {
        if (currentSession != null) currentSession.selectedMode = mode;
    }
}
