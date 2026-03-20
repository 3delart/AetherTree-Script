using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;

// =============================================================
// TARGETPANEL.CS — Panel secondaire — Affichage de la cible
// AetherTree GDD v21
// =============================================================
// SETUP HIERARCHY :
//   TargetPanel
//     ├── TargetNamePanel
//     │     ├── TargetName       (TMP)
//     │     ├── TargetLevel      (TMP)
//     │     └── TargetIcon       (Image)
//     ├── TargetHPBar            (Slider)
//     ├── TargetMPBar            (Slider)
//     └── StatusEffectMobs       (Transform — icônes buffs/debuffs)
//
// CLIC DROIT → ouvre DetailWindow via DetailWindowManager
// Appelé par TargetingSystem : SetTarget(entity) / ClearTarget()
// =============================================================

public class TargetPanel : MonoBehaviour, IPointerClickHandler
{
    public static TargetPanel Instance { get; private set; }

    // ── Références UI ─────────────────────────────────────────
    [Header("Identité")]
    public Image           targetIcon;
    public TextMeshProUGUI targetNameText;
    public TextMeshProUGUI targetLevelText;

    [Header("Barres")]
    public Slider          hpBar;
    public Slider          mpBar;

    [Header("Effets (container HorizontalLayoutGroup)")]
    public Transform       statusEffectContainer;
    [Tooltip("Prefab icône effet — même prefab que PlayerInfosPanel")]
    public GameObject      effectIconPrefab;

    // ── Runtime ──────────────────────────────────────────────
    private Entity _currentTarget;
    private Dictionary<string, StatusEffectIcon> _activeIcons = new Dictionary<string, StatusEffectIcon>();

    // ─────────────────────────────────────────────────────────
    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        gameObject.SetActive(false);
    }

    private void Update()
    {
        if (_currentTarget == null || _currentTarget.isDead)
        {
            ClearTarget();
            return;
        }
        RefreshBars();
        RefreshEffects();
    }

    // =========================================================
    // API PUBLIQUE — appelée par TargetingSystem
    // =========================================================

    public void SetTarget(Entity target)
    {
        // Clear les icônes de l'ancienne cible
        foreach (var kvp in _activeIcons) Destroy(kvp.Value.gameObject);
        _activeIcons.Clear();

        _currentTarget = target;

        if (target == null)
        {
            gameObject.SetActive(false);
            return;
        }

        gameObject.SetActive(true);
        RefreshIdentity();
        RefreshBars();
        RefreshEffects();
    }

    public void ClearTarget()
    {
        _currentTarget = null;
        foreach (var kvp in _activeIcons) Destroy(kvp.Value.gameObject);
        _activeIcons.Clear();
        gameObject.SetActive(false);
    }

    // Alias — compatibilité TargetingSystem
    public void Show(Entity target) => SetTarget(target);
    public void Hide()              => ClearTarget();

    // =========================================================
    // REFRESH
    // =========================================================

    private void RefreshIdentity()
    {
        if (_currentTarget == null) return;

        // Nom
        if (targetNameText != null)
            targetNameText.text = _currentTarget.entityName;

        // Niveau + Icône selon le type d'entité
        Mob mob = _currentTarget as Mob;
        Player player = _currentTarget as Player;

        if (targetLevelText != null)
        {
            if (mob    != null) targetLevelText.text = $"Niv. {mob.mobLevel}";
            else if (player != null) targetLevelText.text = $"Niv. {player.level}";
            else targetLevelText.text = "";
        }

        if (targetIcon != null)
        {
            targetIcon.sprite = null;
            targetIcon.color  = new Color(0.3f, 0.3f, 0.3f, 1f);

            if (mob != null && mob.data?.portrait != null)
            {
                targetIcon.sprite = mob.data.portrait;
                targetIcon.color  = Color.white;
            }
            else if (player != null && player.characterData?.portrait != null)
            {
                targetIcon.sprite = player.characterData.portrait;
                targetIcon.color  = Color.white;
            }
            // ── AJOUTER CES LIGNES ──
            else
            {
                PNJ pnj = _currentTarget as PNJ;
                if (pnj != null && pnj.data?.portrait != null)
                {
                    targetIcon.sprite = pnj.data.portrait;
                    targetIcon.color  = Color.white;
                }
            }
        }
    }

    private void RefreshBars()
    {
        if (_currentTarget == null) return;

        if (hpBar != null)
        {
            hpBar.maxValue = _currentTarget.MaxHP;
            hpBar.value    = _currentTarget.CurrentHP;
        }

        if (mpBar != null)
        {
            mpBar.maxValue = _currentTarget.MaxMana;
            mpBar.value    = _currentTarget.CurrentMana;
        }
    }

    // =========================================================
    // STATUS EFFECTS
    // =========================================================

    private void RefreshEffects()
    {
        if (statusEffectContainer == null || effectIconPrefab == null) return;
        if (_currentTarget?.statusEffects == null) return;

        var effects = _currentTarget.statusEffects.GetActiveEffectsForUI();

        // Ajouter les nouveaux effets
        foreach (var entry in effects)
        {
            if (!_activeIcons.ContainsKey(entry.key))
            {
                var go = Instantiate(effectIconPrefab, statusEffectContainer);
                go.name = entry.key;
                var icon = go.GetComponent<StatusEffectIcon>() ?? go.AddComponent<StatusEffectIcon>();
                icon.Setup(entry, entry.isDebuff
                    ? new UnityEngine.Color(0.8f, 0.1f, 0.1f)
                    : new UnityEngine.Color(0.1f, 0.8f, 0.2f), 28f);
                _activeIcons[entry.key] = icon;
            }
            else
            {
                _activeIcons[entry.key].UpdateDisplay(entry.remainingTime, entry.totalDuration);
            }
        }

        // Supprimer les effets expirés
        var toRemove = new List<string>();
        foreach (var kvp in _activeIcons)
            if (!effects.Exists(e => e.key == kvp.Key)) toRemove.Add(kvp.Key);

        foreach (var key in toRemove)
        {
            Destroy(_activeIcons[key].gameObject);
            _activeIcons.Remove(key);
        }
    }

    // =========================================================
    // CLIC DROIT — ouvre DetailWindow
    // =========================================================

    public void OnPointerClick(PointerEventData eventData)
    {
        if (eventData.button != PointerEventData.InputButton.Right) return;
        if (_currentTarget == null) return;

        // TODO : DetailWindowManager.Instance.OpenTargetDetail(_currentTarget);
        Debug.Log($"[TargetPanel] Clic droit → DetailWindow pour {_currentTarget.entityName} (non implémenté)");
    }
}