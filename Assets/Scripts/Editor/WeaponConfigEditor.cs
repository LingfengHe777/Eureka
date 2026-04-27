#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// WeaponConfig 检视器：遍历绘制各字段（近战预制体仅近战类型可见），并输出校验提示。
/// </summary>
[CustomEditor(typeof(WeaponConfig))]
public class WeaponConfigEditor : Editor
{
    /// <summary>
    /// 绘制检视器 GUI。
    /// </summary>
    public override void OnInspectorGUI()
    {
        serializedObject.Update();
        DrawWeaponConfigBody();
        serializedObject.ApplyModifiedProperties();

        WeaponConfig config = target as WeaponConfig;
        if (config == null)
        {
            return;
        }

        DrawValidationHints(config);
    }

    /// <summary>
    /// 按字段遍历绘制；近战预制体仅在 type 为近战时显示，避免远程配置项里出现近战 Prefab。
    /// </summary>
    private void DrawWeaponConfigBody()
    {
        SerializedProperty iterator = serializedObject.GetIterator();
        bool enterChildren = true;
        while (iterator.NextVisible(enterChildren))
        {
            enterChildren = false;
            if (iterator.name == "m_Script")
            {
                continue;
            }

            if (iterator.name == "meleeWeaponPrefab" && !IsMeleeWeaponType())
            {
                continue;
            }

            EditorGUILayout.PropertyField(iterator, true);
        }
    }

    private bool IsMeleeWeaponType()
    {
        SerializedProperty typeProp = serializedObject.FindProperty("type");
        if (typeProp == null)
        {
            return true;
        }

        return typeProp.enumValueIndex == (int)WeaponType.Melee;
    }

    /// <summary>
    /// 根据配置缺失项与 tierDataList 合法性输出 HelpBox。
    /// </summary>
    private static void DrawValidationHints(WeaponConfig config)
    {
        if (config.weaponIcon == null)
        {
            EditorGUILayout.HelpBox("未配置武器图标，商店与面板需要。", MessageType.Warning);
        }

        if (config.type == WeaponType.Ranged && config.weaponSprite == null)
        {
            EditorGUILayout.HelpBox("远程武器需配置枪口显示贴图；近战外观在近战预制体上配置。", MessageType.Warning);
        }

        if (config.type == WeaponType.Ranged && config.meleeWeaponPrefab != null)
        {
            EditorGUILayout.HelpBox("远程武器请清空近战预制体引用。", MessageType.Warning);
            if (GUILayout.Button("清空近战预制体引用"))
            {
                Undo.RecordObject(config, "Clear melee prefab");
                config.meleeWeaponPrefab = null;
                EditorUtility.SetDirty(config);
            }
        }

        if (config.tierDataList == null || config.tierDataList.Count == 0)
        {
            EditorGUILayout.HelpBox("未配置等级数据列表。", MessageType.Error);
            return;
        }

        HashSet<int> levels = new HashSet<int>();
        bool hasDuplicate = false;
        bool hasCurrentLevel = false;

        for (int i = 0; i < config.tierDataList.Count; i++)
        {
            WeaponTierData tier = config.tierDataList[i];
            if (tier == null)
            {
                continue;
            }

            int level = Mathf.Clamp(tier.level, 1, 4);
            if (!levels.Add(level))
            {
                hasDuplicate = true;
            }

            if (level == config.GetCurrentLevel())
            {
                hasCurrentLevel = true;
            }
        }

        if (hasDuplicate)
        {
            EditorGUILayout.HelpBox("等级数据中存在重复品阶，每项品阶须唯一。", MessageType.Error);
        }

        if (!hasCurrentLevel)
        {
            EditorGUILayout.HelpBox($"当前 weaponLevel={config.GetCurrentLevel()} 在等级数据中不存在。", MessageType.Error);
        }
    }
}

/// <summary>
/// WeaponTierData 自定义属性绘制：按武器类型折叠近战/远程子块。
/// </summary>
[CustomPropertyDrawer(typeof(WeaponTierData))]
public class WeaponTierDataDrawer : PropertyDrawer
{
    private const float Spacing = 2f;

    /// <summary>
    /// 计算属性块总高度。
    /// </summary>
    public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
    {
        float height = 0f;
        WeaponType weaponType = GetWeaponType(property);

        height += EditorGUI.GetPropertyHeight(property.FindPropertyRelative("level"), true) + Spacing;
        height += EditorGUI.GetPropertyHeight(property.FindPropertyRelative("baseDamage"), true) + Spacing;
        height += EditorGUI.GetPropertyHeight(property.FindPropertyRelative("attackCooldown"), true) + Spacing;
        height += EditorGUI.GetPropertyHeight(property.FindPropertyRelative("attackRange"), true) + Spacing;
        height += EditorGUI.GetPropertyHeight(property.FindPropertyRelative("buyPrice"), true) + Spacing;
        height += EditorGUI.GetPropertyHeight(property.FindPropertyRelative("sellPrice"), true) + Spacing;
        height += EditorGUI.GetPropertyHeight(property.FindPropertyRelative("isElemental"), true) + Spacing;
        height += EditorGUI.GetPropertyHeight(property.FindPropertyRelative("elementalBaseDamage"), true) + Spacing;

        if (weaponType == WeaponType.Melee)
        {
            height += EditorGUI.GetPropertyHeight(property.FindPropertyRelative("meleeData"), true) + Spacing;
        }
        else
        {
            height += EditorGUI.GetPropertyHeight(property.FindPropertyRelative("rangedData"), true) + Spacing;
        }

        height += EditorGUI.GetPropertyHeight(property.FindPropertyRelative("modifiers"), true);
        return height;
    }

    /// <summary>
    /// 绘制 SerializedProperty 各字段。
    /// </summary>
    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        EditorGUI.BeginProperty(position, label, property);

        Rect rect = new Rect(position.x, position.y, position.width, 0f);
        WeaponType weaponType = GetWeaponType(property);

        DrawField(ref rect, property.FindPropertyRelative("level"), "品阶");
        DrawField(ref rect, property.FindPropertyRelative("baseDamage"), "基础伤害");
        DrawField(ref rect, property.FindPropertyRelative("attackCooldown"), "攻击间隔");
        DrawField(ref rect, property.FindPropertyRelative("attackRange"), "攻击距离");
        DrawField(ref rect, property.FindPropertyRelative("buyPrice"), "购入价");
        DrawField(ref rect, property.FindPropertyRelative("sellPrice"), "出售价");
        DrawField(ref rect, property.FindPropertyRelative("isElemental"), "元素伤害");
        DrawField(ref rect, property.FindPropertyRelative("elementalBaseDamage"), "元素基础值");

        if (weaponType == WeaponType.Melee)
        {
            DrawField(ref rect, property.FindPropertyRelative("meleeData"), "近战数据");
        }
        else
        {
            DrawField(ref rect, property.FindPropertyRelative("rangedData"), "远程数据");
        }

        DrawField(ref rect, property.FindPropertyRelative("modifiers"), "修正器列表");

        EditorGUI.EndProperty();
    }

    /// <summary>
    /// 从 SerializedObject 读取武器类型。
    /// </summary>
    private static WeaponType GetWeaponType(SerializedProperty property)
    {
        SerializedProperty typeProp = property.serializedObject.FindProperty("type");
        if (typeProp == null)
        {
            return WeaponType.Melee;
        }

        int typeValue = Mathf.Clamp(typeProp.enumValueIndex, 0, 1);
        return (WeaponType)typeValue;
    }

    /// <summary>
    /// 在 rect 上绘制单行 PropertyField 并下移游标。
    /// </summary>
    private static void DrawField(ref Rect rect, SerializedProperty prop, string label)
    {
        if (prop == null)
        {
            return;
        }

        float height = EditorGUI.GetPropertyHeight(prop, true);
        rect.height = height;
        EditorGUI.PropertyField(rect, prop, new GUIContent(label), true);
        rect.y += height + Spacing;
    }
}
#endif