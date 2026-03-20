using UnityEngine;
using System.Collections.Generic;

// =============================================================
// QUESTDATA — ScriptableObject de définition de quête
// Path : Assets/Scripts/Data/Quests/QuestData.cs
// AetherTree GDD v31 — §25
//
// Setup Inspector :
//   questID            = identifiant unique (ex: "quest_braven_01")
//   objectivesInOrder  = true  → séquentiels (groupID pour parallèle dans le groupe)
//                        false → tous parallèles
//
// Exemple "Tuer 10 poulets ET 5 loups PUIS reparler au PNJ" :
//   objectivesInOrder = true
//   Objectif 0 : Kill "Poulet"  groupID="grp1"
//   Objectif 1 : Kill "Loup"    groupID="grp1"   ← même groupe → parallèle
//   Objectif 2 : TalkTo "PNJ"   groupID="grp2"   ← groupe suivant → débloqué après grp1
// =============================================================

[CreateAssetMenu(fileName = "Quest_", menuName = "AetherTree/Quests/QuestData")]
public class QuestData : ScriptableObject
{
    [Header("Identité")]
    [Tooltip("ID unique — ex: quest_braven_01. Doit être unique dans le projet.")]
    public string    questID   = "";
    public string    questName = "Quest";
    public QuestRank questRank = QuestRank.Secondary;
    [TextArea(2, 5)]
    public string    description = "";

    [Header("Prérequis")]
    public int       minLevel          = 1;
    [Tooltip("Quête à compléter avant celle-ci (chaîne de quêtes)")]
    public QuestData prerequisiteQuest = null;

    [Header("Objectifs")]
    [Tooltip("True = séquentiels (avec groupID pour le mix)\nFalse = tous actifs simultanément")]
    public bool objectivesInOrder = false;
    public List<QuestObjective> objectives = new List<QuestObjective>();

    [Header("Récompenses")]
    public int xpReward    = 100;
    public int aerisReward = 50;

    // ── Helpers ───────────────────────────────────────────────

    public bool AllObjectivesComplete()
    {
        if (objectives == null || objectives.Count == 0) return true;
        foreach (var o in objectives)
            if (!o.IsComplete) return false;
        return true;
    }

    /// <summary>
    /// Indices des objectifs actuellement actifs (non complétés et débloqués).
    /// Parallèle : tous. Séquentiel : groupe courant seulement.
    /// </summary>
    public List<int> GetActiveObjectiveIndices()
    {
        var result = new List<int>();
        if (objectives == null) return result;

        if (!objectivesInOrder)
        {
            for (int i = 0; i < objectives.Count; i++)
                if (!objectives[i].IsComplete) result.Add(i);
            return result;
        }

        // Mode séquentiel — trouve le groupe courant
        for (int i = 0; i < objectives.Count; i++)
        {
            if (objectives[i].IsComplete) continue;

            string activeGroup = objectives[i].groupID;

            // Ajoute tous les non-complétés du même groupe
            for (int j = i; j < objectives.Count; j++)
            {
                if (objectives[j].groupID == activeGroup && !objectives[j].IsComplete)
                    result.Add(j);
                else if (objectives[j].groupID != activeGroup)
                    break;
            }
            return result;
        }
        return result;
    }

    public void ResetProgress()
    {
        if (objectives == null) return;
        foreach (var o in objectives) o.currentCount = 0;
    }
}

// =============================================================
public enum QuestRank { Main, Secondary, Daily, Guild, Event, Hidden }

// =============================================================
[System.Serializable]
public class QuestObjective
{
    [Tooltip("Description affichée. Ex: Tuer 10 poulets")]
    public string description = "";

    [Tooltip("Objectifs avec le même groupID sont actifs simultanément en mode séquentiel.\nLaisser vide = objectif isolé.")]
    public string groupID = "";

    public QuestObjectiveType type = QuestObjectiveType.Kill;

    [Tooltip("Kill / Boss — glisser le MobData")]
    public MobData targetMob;

    [Tooltip("TalkTo — glisser le PNJData")]
    public PNJData targetPNJ;

    [Tooltip("Deliver / Gather / Craft — glisser le SO item (ResourceData, ConsumableData, WeaponData...)")]
    public ScriptableObject targetItem;

    [Tooltip("Explore — ID de zone (string)")]
    public string targetZoneID = "";

    public int requiredCount = 1;
    public int currentCount  = 0;

    public bool   IsComplete    => currentCount >= requiredCount;
    public string ProgressLabel => $"{currentCount}/{requiredCount}";

    // Nom de la cible — utilisé par QuestSystem pour les comparaisons
    public string TargetName => type switch
    {
        QuestObjectiveType.Kill    => targetMob  != null ? targetMob.mobName  : "",
        QuestObjectiveType.Boss    => targetMob  != null ? targetMob.mobName  : "",
        QuestObjectiveType.TalkTo  => targetPNJ  != null ? targetPNJ.pnjName  : "",
        QuestObjectiveType.Deliver => targetItem != null ? targetItem.name    : "",
        QuestObjectiveType.Gather  => targetItem != null ? targetItem.name    : "",
        QuestObjectiveType.Craft   => targetItem != null ? targetItem.name    : "",
        QuestObjectiveType.Explore => targetZoneID,
        _                          => ""
    };

    public bool Increment(int amount = 1)
    {
        if (IsComplete) return false;
        currentCount = Mathf.Min(currentCount + amount, requiredCount);
        return IsComplete;
    }
}

// =============================================================
public enum QuestObjectiveType
{
    Kill,
    TalkTo,
    Deliver,
    Gather,
    Explore,
    Craft,
    Boss,
}