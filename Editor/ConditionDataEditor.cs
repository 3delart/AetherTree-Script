#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

// =============================================================
// CONDITIONDATAEDITOR.CS — Assets/Editor/ConditionDataEditor.cs
// Custom Editor complet pour ConditionData SO.
// Remplace le PropertyDrawer — EditorGUILayout gère la hauteur.
// =============================================================

[CustomEditor(typeof(ConditionData))]
public class ConditionDataEditor : Editor
{
    private ConditionData _data;
    private List<bool> _foldouts = new List<bool>();

    private void OnEnable()
    {
        _data = (ConditionData)target;
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        // ── Identifiant ───────────────────────────────────────
        EditorGUILayout.Space(4);
        SectionLabel("IDENTIFIANT UNIQUE");
        EditorGUILayout.PropertyField(serializedObject.FindProperty("conditionID"), new GUIContent("Condition ID"));

        // ── Conditions ────────────────────────────────────────
        EditorGUILayout.Space(8);
        SectionLabel("SOUS-CONDITIONS  (toutes doivent être remplies)");

        var condProp = serializedObject.FindProperty("conditions");

        // Sync foldouts
        while (_foldouts.Count < condProp.arraySize) _foldouts.Add(true);

        for (int i = 0; i < condProp.arraySize; i++)
        {
            var entry = condProp.GetArrayElementAtIndex(i);
            var typeProp = entry.FindPropertyRelative("type");
            var type = (ConditionType)typeProp.enumValueIndex;

            EditorGUILayout.BeginVertical(GetBoxStyle(type));
            EditorGUILayout.BeginHorizontal();

            _foldouts[i] = EditorGUILayout.Foldout(_foldouts[i],
                $"  [{i}]  {TypeLabel(type)}  —  x{entry.FindPropertyRelative("countRequired").intValue}",
                true, GetFoldoutStyle(type));

            if (GUILayout.Button("✕", GUILayout.Width(22), GUILayout.Height(18)))
            {
                condProp.DeleteArrayElementAtIndex(i);
                _foldouts.RemoveAt(i);
                break;
            }
            EditorGUILayout.EndHorizontal();

            if (_foldouts[i])
            {
                EditorGUI.indentLevel++;

                // Type
                SubLabel("TYPE");
                EditorGUILayout.PropertyField(typeProp, new GUIContent("Type"));

                // Filtre commun
                SubLabel("FILTRE COMMUN");
                EditorGUILayout.PropertyField(entry.FindPropertyRelative("common_weapon"),           new GUIContent("Arme (Any = toutes)"));
                EditorGUILayout.PropertyField(entry.FindPropertyRelative("common_locationID"),       new GUIContent("Location (vide = partout)"));
                EditorGUILayout.PropertyField(entry.FindPropertyRelative("common_playerLevelMin"),   new GUIContent("Niveau joueur min (0 = any)"));
                EditorGUILayout.PropertyField(entry.FindPropertyRelative("common_mustBeSolo"),       new GUIContent("Solo uniquement"));
                EditorGUILayout.PropertyField(entry.FindPropertyRelative("common_mustBeInGroup"),    new GUIContent("En groupe uniquement"));

                // Compteur — masqué pour les types où ça n'a pas de sens
                bool showCounter = type != ConditionType.Affinity
                                && type != ConditionType.Zone
                                && type != ConditionType.Time;
                if (showCounter)
                {
                    SubLabel("COMPTEUR");
                    EditorGUILayout.PropertyField(entry.FindPropertyRelative("countRequired"), new GUIContent("Nombre requis"));
                    EditorGUILayout.PropertyField(entry.FindPropertyRelative("mustBeLast"),    new GUIContent("Doit être le dernier"));
                }

                // Champs spécifiques
                TypeLabel_Colored(type);
                DrawTypeFields(entry, type);

                EditorGUI.indentLevel--;
            }

            EditorGUILayout.EndVertical();
            EditorGUILayout.Space(4);
        }

        EditorGUILayout.BeginHorizontal();
        GUILayout.FlexibleSpace();
        if (GUILayout.Button("＋  Ajouter une condition", GUILayout.Width(200), GUILayout.Height(24)))
        {
            condProp.InsertArrayElementAtIndex(condProp.arraySize);
            _foldouts.Add(true);
        }
        GUILayout.FlexibleSpace();
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.PropertyField(serializedObject.FindProperty("sequence_mustBeOrdered"),
            new GUIContent("Conditions dans l'ordre"));

        // ── Récompenses ───────────────────────────────────────
        EditorGUILayout.Space(12);
        SectionLabel("RÉCOMPENSES");
        EditorGUILayout.PropertyField(serializedObject.FindProperty("rewards"), new GUIContent("Rewards"), true);
        EditorGUILayout.PropertyField(serializedObject.FindProperty("rewardDescription"), new GUIContent("Description globale"));

        // ── Affichage ─────────────────────────────────────────
        EditorGUILayout.Space(12);
        SectionLabel("AFFICHAGE");
        EditorGUILayout.PropertyField(serializedObject.FindProperty("isHidden"),    new GUIContent("Caché"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("displayName"), new GUIContent("Nom affiché"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("description"), new GUIContent("Description"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("icon"),        new GUIContent("Icône"));

        serializedObject.ApplyModifiedProperties();
    }

    // =========================================================
    // CHAMPS PAR TYPE
    // =========================================================
    private void DrawTypeFields(SerializedProperty e, ConditionType type)
    {
        switch (type)
        {
            case ConditionType.Kill:
                Prop(e, "kill_specificMob",   "Mob (null = n'importe lequel)");
                Prop(e, "kill_mobElement",    "Elément mob (Neutral = any)");
                Prop(e, "kill_withSkill",     "Skill (null = n'importe lequel)");
                Prop(e, "kill_withElement",   "Elément skill (Neutral = any)");
                Prop(e, "kill_mustBeBoss",    "Boss uniquement");
                Prop(e, "kill_isPlayer",      "Cible = joueur");
                Prop(e, "kill_mustBeStealth", "En furtivité");
                Prop(e, "kill_mustBeUnarmed", "Sans arme");
                Prop(e, "kill_lowHP",         "HP joueur < 20%");
                Prop(e, "kill_atNight",       "La nuit");
                Prop(e, "kill_inZone",        "Zone ID (vide = any)");
                break;
            case ConditionType.Affinity:
                Prop(e, "affinity_element",        "Elément");
                Prop(e, "affinity_minAffinity",    "Affinité minimum");
                Prop(e, "affinity_rankMin",        "Rang minimum");
                Prop(e, "affinity_mustBeDominant", "Doit être dominant");
                Prop(e, "affinity_multiElement",   "Multi-éléments");
                break;
            case ConditionType.SkillCast:
                Prop(e, "skillcast_specificSkill", "Skill (null = n'importe lequel)");
                Prop(e, "skillcast_element",       "Elément (Neutral = any)");
                Prop(e, "skillcast_mustBeCombo",   "Combo uniquement");
                Prop(e, "skillcast_inZone",        "Zone ID (vide = any)");
                break;
            case ConditionType.Damage:
                Prop(e, "damage_minAmount",  "Dégâts minimum");
                Prop(e, "damage_element",    "Elément (Neutral = any)");
                Prop(e, "damage_isReceived", "Dégâts reçus (vs infligés)");
                Prop(e, "damage_inOneHit",   "En un seul coup");
                break;
            case ConditionType.Zone:
                Prop(e, "zone_zoneID",       "Zone ID (vide = any)");
                Prop(e, "zone_minDuration",  "Durée min (sec)");
                Prop(e, "zone_mustBeAFK",    "En AFK");
                Prop(e, "zone_atNight",      "La nuit");
                Prop(e, "zone_isDungeon",    "En donjon");
                Prop(e, "zone_dungeonSolo",  "Donjon solo");
                Prop(e, "zone_dungeonNoHit", "Sans prendre de coup");
                Prop(e, "zone_speedRunMax",  "Speed run max (sec, 0=off)");
                break;
            case ConditionType.Item:
                Prop(e, "item_itemID",  "Item ID (vide = any)");
                Prop(e, "item_action",  "Action");
                Prop(e, "item_minAeris", "Aeris minimum");
                break;
            case ConditionType.Social:
                Prop(e, "social_action",        "Action sociale");
                Prop(e, "social_mustBeInGroup", "En groupe");
                break;
            case ConditionType.Activity:
                Prop(e, "activity_actionType", "Action (nage, pêche, marche, récolte... vide = any)");
                Prop(e, "activity_levelMin",   "Niveau minimum (0 = any)");
                break;
            case ConditionType.Time:
                Prop(e, "time_action",     "Action");
                Prop(e, "time_minMinutes", "Minutes minimum");
                Prop(e, "time_minDays",    "Jours minimum");
                Prop(e, "time_isNight",    "La nuit");
                break;
        }
    }

    // =========================================================
    // HELPERS UI
    // =========================================================
    private void Prop(SerializedProperty parent, string field, string label)
    {
        var p = parent.FindPropertyRelative(field);
        if (p != null) EditorGUILayout.PropertyField(p, new GUIContent(label));
    }

    private void SectionLabel(string text)
    {
        var style = new GUIStyle(EditorStyles.boldLabel);
        style.normal.textColor = new Color(0.9f, 0.8f, 0.5f);
        style.fontSize = 11;
        EditorGUILayout.LabelField(text, style);
        Rect r = GUILayoutUtility.GetLastRect();
        r.y += r.height - 1;
        r.height = 1;
        EditorGUI.DrawRect(r, new Color(0.9f, 0.8f, 0.5f, 0.4f));
        EditorGUILayout.Space(2);
    }

    private void SubLabel(string text)
    {
        EditorGUILayout.Space(4);
        var style = new GUIStyle(EditorStyles.miniLabel);
        style.normal.textColor = new Color(0.6f, 0.6f, 0.6f);
        EditorGUILayout.LabelField(text, style);
    }

    private void TypeLabel_Colored(ConditionType type)
    {
        EditorGUILayout.Space(4);
        var style = new GUIStyle(EditorStyles.boldLabel);
        style.normal.textColor = AccentColor(type);
        EditorGUILayout.LabelField(TypeLabel(type), style);
    }

    private GUIStyle GetBoxStyle(ConditionType type)
    {
        var style = new GUIStyle(GUI.skin.box);
        // Pas de fond custom possible facilement — on utilise le box standard
        return style;
    }

    private GUIStyle GetFoldoutStyle(ConditionType type)
    {
        var style = new GUIStyle(EditorStyles.foldout);
        style.normal.textColor    = AccentColor(type);
        style.onNormal.textColor  = AccentColor(type);
        style.focused.textColor   = AccentColor(type);
        style.onFocused.textColor = AccentColor(type);
        return style;
    }

    private Color AccentColor(ConditionType t)
    {
        switch (t)
        {
            case ConditionType.Kill:      return new Color(1.0f, 0.45f, 0.45f);
            case ConditionType.Affinity:  return new Color(0.45f, 0.75f, 1.0f);
            case ConditionType.SkillCast: return new Color(0.75f, 0.55f, 1.0f);
            case ConditionType.Damage:    return new Color(1.0f, 0.65f, 0.3f);
            case ConditionType.Zone:      return new Color(0.45f, 1.0f, 0.55f);
            case ConditionType.Item:      return new Color(1.0f, 0.9f, 0.35f);
            case ConditionType.Social:    return new Color(0.85f, 0.85f, 0.85f);
            case ConditionType.Activity:  return new Color(0.95f, 0.75f, 0.45f);
            case ConditionType.Time:      return new Color(0.45f, 0.95f, 0.95f);
            
            default:                      return Color.white;
        }
    }

    private string TypeLabel(ConditionType t)
    {
        switch (t)
        {
            case ConditionType.Kill:      return "⚔  KILL";
            case ConditionType.Affinity:  return "✦  AFFINITÉ";
            case ConditionType.SkillCast: return "✦  SKILL CAST";
            case ConditionType.Damage:    return "💥  DÉGÂTS";
            case ConditionType.Zone:      return "📍  ZONE";
            case ConditionType.Item:      return "🎒  ITEM";
            case ConditionType.Social:    return "👥  SOCIAL";
            case ConditionType.Activity:  return "⚡  ACTIVITÉ";
            case ConditionType.Time:      return "⏱  TEMPS";
            
            default:                      return "—";
        }
    }
}
#endif
