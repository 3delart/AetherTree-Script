using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;

// =============================================================
// QUESTTRACKERUI — Tracker HUD (en jeu, affiché en permanence)
// Path : Assets/Scripts/UI/HUD/QuestTrackerUI.cs
// AetherTree GDD v31 — §25
//
// Affiche les quêtes actives avec leurs objectifs en cours.
// Se rafraîchit automatiquement via GameEventBus.OnQuestAction
// et GameEventBus.OnMobKilled (pour la progression des objectifs).
//
// Hiérarchie Unity attendue :
//   QuestTrackerPanel
//     └── TrackerContent  (VerticalLayoutGroup — les entrées s'empilent ici)
//
// Prefabs requis :
//   questBlockPrefab   — conteneur d'une quête (VerticalLayoutGroup)
//     ├── QuestName    (TMP)
//     └── [objectifs instanciés dans ce GO]
//   objectiveLinePrefab — une ligne d'objectif
//     ├── Bullet       (Image — rond coloré)
//     └── ObjectiveText (TMP)
//
// Le tracker se cache automatiquement s'il n'y a aucune quête active.
// Nb de quêtes affichées en même temps : maxDisplayed (défaut 3).
// =============================================================

public class QuestTrackerUI : MonoBehaviour
{
    public static QuestTrackerUI Instance { get; private set; }

    [Header("Contenu")]
    public Transform  trackerContent;
    public GameObject questBlockPrefab;
    public GameObject objectiveLinePrefab;

    [Header("Paramètres")]
    [Tooltip("Nombre max de quêtes affichées simultanément dans le tracker.")]
    public int maxDisplayed = 3;

    // Couleurs bullets
    private static readonly Color BULLET_DONE    = new Color(0.3f, 0.9f,  0.4f);
    private static readonly Color BULLET_PENDING = new Color(0.65f, 0.65f, 0.75f);

    private readonly List<GameObject> _blocks = new List<GameObject>();
    private bool _dirty = true;  // true = refresh nécessaire au prochain Update

    // =========================================================
    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    private void Start()
    {
        GameEventBus.OnQuestAction += OnQuestAction;
        GameEventBus.OnMobKilled   += OnMobKilled;
        Refresh();
    }

    private void OnDestroy()
    {
        GameEventBus.OnQuestAction -= OnQuestAction;
        GameEventBus.OnMobKilled   -= OnMobKilled;
    }

    // Marque dirty à chaque événement quête ou kill pour éviter
    // de rebuilder le tracker à chaque frame d'une salve de kills.
    private void OnQuestAction(QuestEvent e) => _dirty = true;
    private void OnMobKilled(MobKilledEvent e) => _dirty = true;

    private void Update()
    {
        if (!_dirty) return;
        _dirty = false;
        Refresh();
    }

    // =========================================================
    // REFRESH COMPLET
    // =========================================================

    public void Refresh()
    {
        // Vide les anciens blocs
        foreach (var go in _blocks)
            if (go != null) Destroy(go);
        _blocks.Clear();

        if (QuestSystem.Instance == null || trackerContent == null || questBlockPrefab == null)
        {
            gameObject.SetActive(false);
            return;
        }

        var activeQuests = QuestSystem.Instance.GetActiveQuests();

        // Cache le tracker s'il n'y a rien à afficher
        if (activeQuests == null || activeQuests.Count == 0)
        {
            gameObject.SetActive(false);
            return;
        }

        gameObject.SetActive(true);

        int displayed = 0;
        foreach (var quest in activeQuests)
        {
            if (displayed >= maxDisplayed) break;
            SpawnQuestBlock(quest);
            displayed++;
        }
    }

    // =========================================================
    // SPAWN UN BLOC DE QUÊTE
    // =========================================================

    private void SpawnQuestBlock(QuestData quest)
    {
        if (quest == null) return;

        var block = Instantiate(questBlockPrefab, trackerContent);
        _blocks.Add(block);

        // Nom de la quête
        var nameText = block.transform.Find("QuestName")?.GetComponent<TextMeshProUGUI>()
                    ?? block.GetComponentInChildren<TextMeshProUGUI>();
        if (nameText != null)
        {
            nameText.text  = quest.questName;
            nameText.color = RankColor(quest.questRank);
        }

        // Objectifs actifs uniquement
        if (objectiveLinePrefab == null || quest.objectives == null) return;

        var activeIndices = quest.GetActiveObjectiveIndices();

        // Si tous les objectifs sont actifs en même temps (parallèle),
        // on affiche aussi les objectifs déjà complétés pour garder le contexte
        bool showAll = !quest.objectivesInOrder;

        for (int i = 0; i < quest.objectives.Count; i++)
        {
            var obj = quest.objectives[i];
            if (obj == null) continue;

            // En mode séquentiel : on n'affiche que les actifs + les complétés récents
            if (quest.objectivesInOrder && !activeIndices.Contains(i) && !obj.IsComplete)
                continue;

            SpawnObjectiveLine(block.transform, obj);
        }
    }

    // =========================================================
    // SPAWN UNE LIGNE D'OBJECTIF
    // =========================================================

    private void SpawnObjectiveLine(Transform parent, QuestObjective obj)
    {
        var line = Instantiate(objectiveLinePrefab, parent);

        // Bullet coloré
        var bullet = line.transform.Find("Bullet")?.GetComponent<Image>();
        if (bullet != null)
            bullet.color = obj.IsComplete ? BULLET_DONE : BULLET_PENDING;

        // Texte principal
        var txt = line.transform.Find("ObjectiveText")?.GetComponent<TextMeshProUGUI>()
               ?? line.GetComponentInChildren<TextMeshProUGUI>();

        if (txt != null)
        {
            // Construit le texte : description + progression si > 1
            string label = obj.description;
            if (obj.requiredCount > 1)
                label += $"  {obj.ProgressLabel}";

            txt.text  = label;
            txt.color = obj.IsComplete
                ? new Color(0.55f, 0.85f, 0.55f)   // vert complété
                : new Color(0.85f, 0.85f, 0.90f);  // blanc légèrement bleuté
        }
    }

    // =========================================================
    // HELPERS
    // =========================================================

    private static Color RankColor(QuestRank rank) => rank switch
    {
        QuestRank.Main      => new Color(1.0f, 0.85f, 0.2f),   // or
        QuestRank.Secondary => new Color(0.85f, 0.90f, 1.0f),  // bleu clair
        QuestRank.Daily     => new Color(0.6f, 0.95f, 0.65f),  // vert
        QuestRank.Guild     => new Color(0.9f, 0.65f, 1.0f),   // violet
        QuestRank.Event     => new Color(1.0f, 0.65f, 0.35f),  // orange
        QuestRank.Hidden    => new Color(0.75f, 0.75f, 0.8f),  // gris
        _                   => Color.white
    };
}
