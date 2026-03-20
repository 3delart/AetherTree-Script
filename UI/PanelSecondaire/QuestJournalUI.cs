using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;
using System.Collections.Generic;
using System.Text;

// =============================================================
// QUESTJOURNALUI — Journal des quêtes (touche J)
// Path : Assets/Scripts/UI/Panels/QuestJournalUI.cs
// AetherTree GDD v31 — §25
//
// Affiche uniquement les quêtes EN COURS (actives).
// Clic sur l'entrée → affiche le détail dans PanelRight.
// ButtonSuivis → réservé au tracker (implémentation future).
// Le panel bloque les clics monde via GraphicRaycaster.
//
// ── Prefab QuestPanel (entrée de liste) ───────────────────────
//   QuestPanel  (racine — Button)
//     ├── RankColor     (Image)  ← bande colorée selon rang
//     ├── QuestName     (TMP)    ← nom de la quête
//     ├── QuestRank     (TMP)    ← label du rang
//     └── ButtonSuivis  (Button) ← tracker (futur)
//
// ── Prefab QuestPanelInfo (détail) ───────────────────────────
//   QuestPanelInfo  (racine)
//     ├── QuestEntete
//     │     ├── QuestName         (TMP)
//     │     ├── QuestRank         (TMP)
//     │     └── QuestDescription  (TMP)
//     ├── QuestObjective
//     │     └── QuestObjectifs    (TMP)
//     └── QuestRewards
//           ├── XPRewards         (TMP)
//           ├── AerisRewards      (TMP)
//           └── ItemsRewards      (TMP)
// =============================================================

public class QuestJournalUI : MonoBehaviour
{
    public static QuestJournalUI Instance { get; private set; }

    // ── Panel principal ───────────────────────────────────────
    [Header("Panel")]
    public GameObject panel;
    public Button     closeButton;

    // ── Liste (PanelLeft) ─────────────────────────────────────
    [Header("Liste des quêtes — PanelLeft")]
    [Tooltip("Transform Content du ScrollRect dans PanelLeft")]
    public Transform  questListContent;
    [Tooltip("Prefab QuestPanel — une entrée de la liste")]
    public GameObject questEntryPrefab;

    // ── Détail (PanelRight) ───────────────────────────────────
    [Header("Détail — PanelRight")]
    [Tooltip("PanelRight — parent dans lequel QuestPanelInfo est instancié")]
    public Transform  detailParent;
    [Tooltip("Prefab QuestPanelInfo — le panneau détail complet")]
    public GameObject questDetailPrefab;

    // ── État interne ──────────────────────────────────────────
    private bool      _isOpen        = false;
    private QuestData _selectedQuest = null;

    private GameObject      _currentDetailInstance = null;
    private TextMeshProUGUI _detailName;
    private TextMeshProUGUI _detailRank;
    private TextMeshProUGUI _detailDescription;
    private TextMeshProUGUI _objectifsText;
    private TextMeshProUGUI _xpRewardsText;
    private TextMeshProUGUI _aerisRewardsText;
    private TextMeshProUGUI _itemsRewardsText;

    private readonly List<GameObject> _entryObjects = new List<GameObject>();

    // =========================================================
    // INIT
    // =========================================================

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        if (panel != null) panel.SetActive(false);
    }

    private void Start()
    {
        closeButton?.onClick.AddListener(Close);

        // Bloque les clics monde quand le panel est ouvert.
        // Le panel doit avoir un Image (raycast target = true) pour absorber les clics.
        // On l'ajoute automatiquement si manquant.
        EnsureRaycastBlocker();

        GameEventBus.OnQuestAction += OnQuestAction;
        GameEventBus.OnMobKilled   += OnMobKilled;
    }

    private void OnDestroy()
    {
        GameEventBus.OnQuestAction -= OnQuestAction;
        GameEventBus.OnMobKilled   -= OnMobKilled;
    }

    // ── Ajoute un Image transparent sur le panel pour bloquer les clics ──
    private void EnsureRaycastBlocker()
    {
        if (panel == null) return;

        var img = panel.GetComponent<Image>();
        if (img == null)
        {
            img = panel.AddComponent<Image>();
            img.color = new Color(0f, 0f, 0f, 0f); // transparent
        }
        img.raycastTarget = true;
    }

    // =========================================================
    // EVENTS
    // =========================================================

    private void OnQuestAction(QuestEvent e)
    {
        if (!_isOpen) return;
        RefreshList();
        // Rafraîchit le détail si c'est la quête sélectionnée
        if (_selectedQuest != null && e.quest == _selectedQuest)
            ShowDetail(_selectedQuest);
    }

    private void OnMobKilled(MobKilledEvent e)
    {
        if (!_isOpen || _selectedQuest == null) return;
        RefreshObjectifsText(_selectedQuest);
    }

    // =========================================================
    // OPEN / CLOSE / TOGGLE
    // =========================================================

    public void Open()
    {
        _isOpen = true;
        if (panel != null) panel.SetActive(true);

        RefreshList();

        if (_selectedQuest != null &&
            QuestSystem.Instance?.GetQuestState(_selectedQuest) == QuestState.Active)
            ShowDetail(_selectedQuest);
        else
            ClearDetail();
    }

    public void Close()
    {
        _isOpen = false;
        if (panel != null) panel.SetActive(false);
    }

    public void Toggle()
    {
        if (_isOpen) Close(); else Open();
    }

    // =========================================================
    // LISTE
    // =========================================================

    private void RefreshList()
    {
        foreach (var go in _entryObjects)
            if (go != null) Destroy(go);
        _entryObjects.Clear();

        if (QuestSystem.Instance == null || questListContent == null || questEntryPrefab == null)
            return;

        // Affiche actives + complétées (à rendre) — pas les TurnedIn
        var quests = new List<QuestData>();
        quests.AddRange(QuestSystem.Instance.GetActiveQuests());
        quests.AddRange(QuestSystem.Instance.GetCompletedQuests());

        if (quests == null || quests.Count == 0)
        {
            SpawnEmptyMessage("Aucune quête en cours.");
            return;
        }

        foreach (var quest in quests)
            SpawnQuestEntry(quest);
    }

    private void SpawnEmptyMessage(string msg)
    {
        var go        = new GameObject("EmptyMsg", typeof(RectTransform), typeof(TextMeshProUGUI));
        go.transform.SetParent(questListContent, false);
        var tmp       = go.GetComponent<TextMeshProUGUI>();
        tmp.text      = msg;
        tmp.fontSize  = 35;
        tmp.color     = new Color(0.5f, 0.5f, 0.6f);
        tmp.alignment = TextAlignmentOptions.Center;
        _entryObjects.Add(go);
    }

    private void SpawnQuestEntry(QuestData quest)
    {
        if (quest == null || questEntryPrefab == null) return;

        var go = Instantiate(questEntryPrefab, questListContent);
        _entryObjects.Add(go);

        // ── RankColor ─────────────────────────────────────────
        var rankColorImg = go.transform.Find("RankColor")?.GetComponent<Image>();
        if (rankColorImg != null)
            rankColorImg.color = RankColor(quest.questRank, 1f);

        // ── QuestName ─────────────────────────────────────────
        var nameText = go.transform.Find("QuestName")?.GetComponent<TextMeshProUGUI>();
        if (nameText != null)
            nameText.text = quest.questName;

        // ── QuestRank ─────────────────────────────────────────
        var rankText = go.transform.Find("QuestRank")?.GetComponent<TextMeshProUGUI>();
        if (rankText != null)
        {
            rankText.text  = RankLabel(quest.questRank);
            rankText.color = RankColor(quest.questRank, 0.85f);
        }

        // ── Surlignage si sélectionnée ────────────────────────
        var rootImg = go.GetComponent<Image>();
        if (rootImg != null)
            rootImg.color = (_selectedQuest == quest)
                ? new Color(0.30f, 0.20f, 0.55f, 1f)
                : new Color(0.15f, 0.15f, 0.22f, 1f);

        var captured = quest;

        // ── ButtonSuivis → tracker uniquement ────────────────
        // Configuré EN PREMIER pour que son EventTrigger soit
        // en place avant qu'on branche le rootBtn.
        var suiviBtn = go.transform.Find("ButtonSuivis")?.GetComponent<Button>();
        if (suiviBtn != null)
        {
            suiviBtn.onClick.RemoveAllListeners();
            suiviBtn.onClick.AddListener(() => OnSuiviClicked(captured));

            // Bloque la remontée du PointerClick vers le rootBtn parent
            var trigger = suiviBtn.gameObject.GetComponent<EventTrigger>()
                    ?? suiviBtn.gameObject.AddComponent<EventTrigger>();
            trigger.triggers.Clear();
            var entry = new EventTrigger.Entry { eventID = EventTriggerType.PointerClick };
            entry.callback.AddListener((_) => { }); // absorbe le clic
            trigger.triggers.Add(entry);
        }

        // ── rootBtn → affiche le détail (tout le prefab sauf S) ──
        var rootBtn = go.GetComponent<Button>();
        if (rootBtn != null)
        {
            rootBtn.onClick.RemoveAllListeners();
            rootBtn.onClick.AddListener(() => OnEntryClicked(captured));
        }

        // ── Indicateur complétée ──────────────────────────────
        bool isComplete = QuestSystem.Instance?.GetQuestState(quest) == QuestState.Completed;
        if (nameText != null && isComplete)
            nameText.text = $"✓ {quest.questName}";
    }



    private void OnEntryClicked(QuestData quest)
    {
        _selectedQuest = quest;
        ShowDetail(quest);
        RefreshList(); // met à jour le surlignage
    }

    // ── Futur : ajouter/retirer du QuestTrackerUI ─────────────
    private void OnSuiviClicked(QuestData quest)
    {
        // TODO : QuestTrackerUI.Instance?.ToggleTracked(quest);
        Debug.Log($"[QUEST] Suivi toggle : {quest.questName}");
    }

    // =========================================================
    // DÉTAIL
    // =========================================================

    private void ShowDetail(QuestData quest)
    {
        if (quest == null) { ClearDetail(); return; }

        if (_currentDetailInstance == null)
            InstantiateDetailPrefab();

        if (_currentDetailInstance == null) return;

        if (_detailName != null)
        {
            _detailName.text  = quest.questName;
            _detailName.color = RankColor(quest.questRank, 1f);
        }

        if (_detailRank != null)
        {
            _detailRank.text  = RankLabel(quest.questRank);
            _detailRank.color = RankColor(quest.questRank, 0.85f);
        }

        if (_detailDescription != null)
            _detailDescription.text = !string.IsNullOrEmpty(quest.description)
                ? quest.description
                : "Aucune description.";

        RefreshObjectifsText(quest);

        if (_xpRewardsText != null)
            _xpRewardsText.text = quest.xpReward > 0 ? $"+{quest.xpReward} XP" : "—";

        if (_aerisRewardsText != null)
            _aerisRewardsText.text = quest.aerisReward > 0 ? $"+{quest.aerisReward} ¤" : "—";

        if (_itemsRewardsText != null)
            _itemsRewardsText.text = "—";
    }

    private void InstantiateDetailPrefab()
    {
        if (questDetailPrefab == null || detailParent == null) return;

        if (_currentDetailInstance != null)
            Destroy(_currentDetailInstance);

        _currentDetailInstance = Instantiate(questDetailPrefab, detailParent);

        var root = _currentDetailInstance.transform;

        var entete         = root.Find("QuestEntete");
        _detailName        = entete?.Find("QuestName")       ?.GetComponent<TextMeshProUGUI>();
        _detailRank        = entete?.Find("QuestRank")       ?.GetComponent<TextMeshProUGUI>();
        _detailDescription = entete?.Find("QuestDescription")?.GetComponent<TextMeshProUGUI>();

        _objectifsText = root.Find("QuestObjective")?.Find("QuestObjectifs")
                             ?.GetComponent<TextMeshProUGUI>();

        var rewards       = root.Find("QuestRewards");
        _xpRewardsText    = rewards?.Find("XPRewards")   ?.GetComponent<TextMeshProUGUI>();
        _aerisRewardsText = rewards?.Find("AerisRewards") ?.GetComponent<TextMeshProUGUI>();
        _itemsRewardsText = rewards?.Find("ItemsRewards") ?.GetComponent<TextMeshProUGUI>();

        if (_detailName        == null) Debug.LogWarning("[QuestJournalUI] QuestEntete/QuestName introuvable.");
        if (_detailRank        == null) Debug.LogWarning("[QuestJournalUI] QuestEntete/QuestRank introuvable.");
        if (_detailDescription == null) Debug.LogWarning("[QuestJournalUI] QuestEntete/QuestDescription introuvable.");
        if (_objectifsText     == null) Debug.LogWarning("[QuestJournalUI] QuestObjective/QuestObjectifs introuvable.");
        if (_xpRewardsText     == null) Debug.LogWarning("[QuestJournalUI] QuestRewards/XPRewards introuvable.");
        if (_aerisRewardsText  == null) Debug.LogWarning("[QuestJournalUI] QuestRewards/AerisRewards introuvable.");
    }

    // =========================================================
    // OBJECTIFS — tout dans un seul TMP
    // =========================================================

    private void RefreshObjectifsText(QuestData quest)
    {
        if (_objectifsText == null || quest?.objectives == null) return;

        var sb = new StringBuilder();

        foreach (var obj in quest.objectives)
        {
            string icon  = obj.IsComplete ? "<color=#4DE670>✓</color>" : "•";
            string color = obj.IsComplete ? "#99E699" : "#DADBEB";
            string line  = $"{icon} <color={color}>{obj.description}</color>";

            if (obj.requiredCount > 1)
            {
                string progressColor = obj.IsComplete ? "#4DE670" : "#AAAACC";
                line += $"  <color={progressColor}>{obj.ProgressLabel}</color>";
            }

            sb.AppendLine(line);
        }

        _objectifsText.text = sb.ToString().TrimEnd();
    }

    // =========================================================
    // CLEAR
    // =========================================================

    private void ClearDetail()
    {
        _selectedQuest = null;

        if (_currentDetailInstance != null)
        {
            Destroy(_currentDetailInstance);
            _currentDetailInstance = null;
        }

        _detailName = _detailRank = _detailDescription = _objectifsText = null;
        _xpRewardsText = _aerisRewardsText = _itemsRewardsText = null;
    }

    // =========================================================
    // HELPERS
    // =========================================================

    private static string RankLabel(QuestRank rank) => rank switch
    {
        QuestRank.Main      => "Principale",
        QuestRank.Secondary => "Secondaire",
        QuestRank.Daily     => "Quotidienne",
        QuestRank.Guild     => "Guilde",
        QuestRank.Event     => "Événement",
        QuestRank.Hidden    => "Cachée",
        _                   => ""
    };

    private static Color RankColor(QuestRank rank, float alpha) => rank switch
    {
        QuestRank.Main      => new Color(1.0f, 0.85f, 0.2f,  alpha),
        QuestRank.Secondary => new Color(0.7f, 0.85f, 1.0f,  alpha),
        QuestRank.Daily     => new Color(0.6f, 0.95f, 0.65f, alpha),
        QuestRank.Guild     => new Color(0.9f, 0.65f, 1.0f,  alpha),
        QuestRank.Event     => new Color(1.0f, 0.65f, 0.35f, alpha),
        QuestRank.Hidden    => new Color(0.7f, 0.7f,  0.75f, alpha),
        _                   => new Color(1f,   1f,    1f,    alpha)
    };
}
