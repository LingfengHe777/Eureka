using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 挂在近战武器 prefab 上（常与 Animator 同物体）。攻击节奏仅由动画事件驱动：
/// WeaponConfig.MeleeEventAttackBegin / WeaponConfig.MeleeEventAttackEnd / WeaponConfig.MeleeEventAnimEnd。
/// </summary>
[DisallowMultipleComponent]
public class MeleeWeaponHitbox : MonoBehaviour
{
    private WeaponController _owner;
    private WeaponConfig _config;
    private MeleeData _meleeData;

    private readonly HashSet<int> _hitInstanceIds = new HashSet<int>();
    private bool _playedAttackSfxThisSwing;
    private bool _armed;
    private Collider2D[] _colliders;
    private bool _attackCycleActive;
    private readonly List<Collider2D> _overlapWorkList = new List<Collider2D>(24);

    private void Awake()
    {
        EnsureKinematicRigidbody();
        _colliders = GetComponentsInChildren<Collider2D>(true);
        EnableColliders(false);
    }

    private static void EnsureKinematicRigidbodyOn(GameObject go)
    {
        Rigidbody2D rb = go.GetComponent<Rigidbody2D>();
        if (rb == null)
            rb = go.AddComponent<Rigidbody2D>();
        rb.bodyType = RigidbodyType2D.Kinematic;
        rb.simulated = true;
        rb.useFullKinematicContacts = true;
    }

    private void EnsureKinematicRigidbody()
    {
        EnsureKinematicRigidbodyOn(gameObject);
    }

    /// <summary>
    /// 由 WeaponManager / WeaponController 在实例化后调用。
    /// </summary>
    public void Initialize(WeaponController owner, WeaponConfig config)
    {
        _owner = owner;
        _config = config;
    }

    /// <summary>
    /// 在触发 WeaponConfig.MeleeAttackTriggerParameterName 之前由 WeaponController 调用：备战本段攻击，等待 AttackBegin。
    /// </summary>
    public void PrepareMeleeAttack(MeleeData meleeData)
    {
        _attackCycleActive = true;
        _meleeData = meleeData;
        _hitInstanceIds.Clear();
        _playedAttackSfxThisSwing = false;
        _armed = false;
        EnableColliders(false);
    }

    /// <summary>
    /// 动画事件：开始攻击伤害判定。
    /// </summary>
    public void AttackBegin()
    {
        if (!_attackCycleActive || _meleeData == null)
            return;

        _owner?.RefreshMeleeSwingAimAtAttackBegin(this);

        _hitInstanceIds.Clear();
        _playedAttackSfxThisSwing = false;
        _armed = true;
        EnableColliders(true);
        FlushOverlapsAfterAttackBegin();
    }

    private void FlushOverlapsAfterAttackBegin()
    {
        ContactFilter2D filter = new ContactFilter2D();
        filter.useLayerMask = false;
        filter.useTriggers = true;

        for (int i = 0; i < _colliders.Length; i++)
        {
            Collider2D c = _colliders[i];
            if (c == null || !c.enabled) continue;
            _overlapWorkList.Clear();
            c.OverlapCollider(filter, _overlapWorkList);
            for (int j = 0; j < _overlapWorkList.Count; j++)
            {
                TryApplyHit(_overlapWorkList[j]);
            }
        }
    }

    /// <summary>
    /// 动画事件：攻击伤害判定结束（关闭碰撞体）。
    /// </summary>
    public void AttackEnd()
    {
        if (!_attackCycleActive)
            return;

        _armed = false;
        EnableColliders(false);
    }

    /// <summary>
    /// 动画事件：攻击动画结束，恢复 gunPos 自动瞄准。
    /// </summary>
    public void AnimEnd()
    {
        if (!_attackCycleActive)
            return;

        _armed = false;
        EnableColliders(false);

        _owner?.NotifyMeleeAimUnlockedFromAnimation(this);
        _attackCycleActive = false;
    }

    private void EnableColliders(bool on)
    {
        if (_colliders == null) return;
        for (int i = 0; i < _colliders.Length; i++)
        {
            if (_colliders[i] != null)
                _colliders[i].enabled = on;
        }
    }

    private void TryApplyHit(Collider2D other)
    {
        if (!_armed || _owner == null || _config == null || _meleeData == null) return;
        if (other == null) return;

        Enemy enemy = other.GetComponent<Enemy>() ?? other.GetComponentInParent<Enemy>();
        if (enemy == null) return;
        if (enemy.GetCurrentHealth() <= 0f) return;

        int id = enemy.GetInstanceID();
        if (_hitInstanceIds.Contains(id)) return;

        _hitInstanceIds.Add(id);

        if (!_playedAttackSfxThisSwing)
        {
            _playedAttackSfxThisSwing = true;
            _owner.TryPlayWeaponAttackSfxFromMeleeHit(this, _config);
        }

        Vector2 dir = (Vector2)enemy.transform.position - (Vector2)transform.position;
        if (dir.sqrMagnitude < 0.0001f)
            dir = (Vector2)transform.right;
        else
            dir.Normalize();

        _owner.ApplyMeleeHitFromHitbox(_config, enemy, dir, _meleeData);
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        TryApplyHit(other);
    }

    private void OnTriggerStay2D(Collider2D other)
    {
        TryApplyHit(other);
    }

    private void OnDisable()
    {
        _armed = false;
        EnableColliders(false);
        if (_attackCycleActive && _owner != null)
        {
            _owner.NotifyMeleeAimUnlockedFromAnimation(this);
            _attackCycleActive = false;
        }
    }
}
