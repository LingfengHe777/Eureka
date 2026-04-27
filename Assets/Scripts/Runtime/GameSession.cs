using UnityEngine;

/// <summary>
/// 游戏会话 ScriptableObject：记录玩家选择的武器、角色与难度。
/// </summary>
[CreateAssetMenu(fileName = "GameSession", menuName = "Eureka/Runtime/GameSession")]
public class GameSession : ScriptableObject
{
    [Header("开局选择")]
    [Tooltip("玩家选用的初始武器")]
    public WeaponConfig selectedWeapon;

    [Tooltip("玩家选用的角色")]
    public SpecialStatConfig selectedCharacter;

    [Tooltip("难度与模式，含波次与商店等配置")]
    public ModeConfig selectedMode;

    /// <summary>
    /// 清空会话中的选中项。
    /// </summary>
    public void Reset()
    {
        selectedWeapon = null;
        selectedCharacter = null;
        selectedMode = null;
    }
}
