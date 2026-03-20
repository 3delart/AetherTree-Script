using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

// =============================================================
// ELEMENTALBARUI.CS — Jauge élémentaire
// Path : Assets/Scripts/UI/PanelBot/ElementalBarUI.cs
// AetherTree GDD v30 — §7
//
// Barre horizontale segmentée à proportions dynamiques.
// Chaque segment = un élément présent (affinité > 0).
// Couleurs depuis ElementType.GetColor() (data-driven — ElementData.cs).
// Proportions lissées visuellement (Lerp) pour éviter les sauts brusques.
//
// Structure Unity :
//   PlayerJaugePanel
//     ├── HorizontalLayoutGroup (Child Controls Size ✅, Child Force Expand ❌)
//     ├── Seg_Neutral   (Image + LayoutElement)
//     ├── Seg_Fire      (Image + LayoutElement)
//     ├── Seg_Water     (Image + LayoutElement)
//     ├── Seg_Earth     (Image + LayoutElement)
//     ├── Seg_Nature    (Image + LayoutElement)
//     ├── Seg_Lightning (Image + LayoutElement)
//     ├── Seg_Darkness  (Image + LayoutElement)
//     └── Seg_Light     (Image + LayoutElement)
//
// Poser ce script sur PlayerJaugePanel.
// Assigner les 8 segments dans l'Inspector.
//
// Rangs §7.4 affichés via tooltips / debug — pas de texte sur la barre
// (choix UX : la barre seule suffit).
//
// Repères seuils affichés en overlay sur le segment dominant :
//   Trait fin à 25% (seuil Mono / Rang 2)
//   Trait fin à 50% (seuil Rang 3)
//   Trait fin à 75% (seuil Rang 4)
//   Trait fin à 90% (seuil Rang 5)
// =============================================================

public class ElementalBarUI : MonoBehaviour
{
    // ── Segments Inspector ────────────────────────────────────
    [Header("Segments — assigner dans l'Inspector (8 éléments v30)")]
    public RectTransform seg_Neutral;
    public RectTransform seg_Fire;
    public RectTransform seg_Water;
    public RectTransform seg_Earth;
    public RectTransform seg_Nature;
    public RectTransform seg_Lightning;
    public RectTransform seg_Darkness;
    public RectTransform seg_Light;

    // ── Options ───────────────────────────────────────────────
    [Header("Options")]
    [Tooltip("Vitesse de lissage des transitions de taille (Lerp). 0 = instantané.")]
    [Range(0f, 20f)]
    public float smoothSpeed = 8f;

    [Tooltip("Seuil minimum d'affinité pour afficher un segment (évite les sliver d'1px)")]
    [Range(0f, 0.02f)]
    public float minVisibleAffinity = 0.005f;

    // ── Privé ─────────────────────────────────────────────────
    private ElementalSystem _elementalSystem;

    // Mapping élément → composants du segment
    private Dictionary<ElementType, RectTransform>  _segments;
    private Dictionary<ElementType, LayoutElement>  _layoutElements;
    private Dictionary<ElementType, Image>          _images;

    // Valeurs actuelles lissées (flexibleWidth en cours d'interpolation)
    private Dictionary<ElementType, float> _currentWidths;

    // Ordre d'affichage (de gauche à droite dans la barre)
    private static readonly ElementType[] DISPLAY_ORDER =
    {
        ElementType.Neutral,
        ElementType.Fire,
        ElementType.Water,
        ElementType.Earth,
        ElementType.Nature,
        ElementType.Lightning,
        ElementType.Darkness,
        ElementType.Light,
    };

    // =========================================================
    // INIT
    // =========================================================

    private void Awake()
    {
        _segments       = new Dictionary<ElementType, RectTransform>();
        _layoutElements = new Dictionary<ElementType, LayoutElement>();
        _images         = new Dictionary<ElementType, Image>();
        _currentWidths  = new Dictionary<ElementType, float>();

        // Mappe chaque élément vers son RectTransform Inspector
        RegisterSegment(ElementType.Neutral,   seg_Neutral);
        RegisterSegment(ElementType.Fire,      seg_Fire);
        RegisterSegment(ElementType.Water,     seg_Water);
        RegisterSegment(ElementType.Earth,     seg_Earth);
        RegisterSegment(ElementType.Nature,    seg_Nature);
        RegisterSegment(ElementType.Lightning, seg_Lightning);
        RegisterSegment(ElementType.Darkness,  seg_Darkness);
        RegisterSegment(ElementType.Light,     seg_Light);
    }

    private void RegisterSegment(ElementType type, RectTransform rt)
    {
        if (rt == null)
        {
            Debug.LogWarning($"[ElementalBarUI] Segment {type} non assigné dans l'Inspector.");
            return;
        }

        _segments[type] = rt;

        // LayoutElement — ajouté automatiquement si manquant
        LayoutElement le = rt.GetComponent<LayoutElement>();
        if (le == null)
        {
            le = rt.gameObject.AddComponent<LayoutElement>();
            Debug.LogWarning($"[ElementalBarUI] LayoutElement ajouté automatiquement sur {rt.name}.");
        }
        _layoutElements[type] = le;

        // Image — pour la couleur
        Image img = rt.GetComponent<Image>();
        if (img == null)
            img = rt.gameObject.AddComponent<Image>();
        _images[type] = img;

        // Couleur de base depuis ElementData (data-driven)
        img.color = type.GetColor();

        // Initialisation : Neutre à 1, reste à 0
        _currentWidths[type] = (type == ElementType.Neutral) ? 1f : 0f;
        le.flexibleWidth = _currentWidths[type];

        // Désactive les segments non-Neutre au départ
        rt.gameObject.SetActive(type == ElementType.Neutral);
    }

    private void Start()
    {
        // Cherche d'abord dans les parents (cas où la barre est enfant du Player)
        Player player = GetComponentInParent<Player>();
        if (player != null)
            _elementalSystem = player.GetComponent<ElementalSystem>();

        // Fallback : cherche dans la scène
        if (_elementalSystem == null)
            _elementalSystem = FindObjectOfType<ElementalSystem>();

        if (_elementalSystem == null)
            Debug.LogWarning("[ElementalBarUI] ElementalSystem introuvable !");

        // Refresh immédiat sans lerp pour l'état initial
        RefreshInstant();
    }

    // =========================================================
    // UPDATE
    // =========================================================

    private void Update()
    {
        RefreshSmooth();
    }

    // =========================================================
    // REFRESH LISSÉ (update normal)
    // =========================================================

    private void RefreshSmooth()
    {
        if (_elementalSystem == null) return;

        float totalWeight = _elementalSystem.GetTotalWeight();

        foreach (ElementType type in DISPLAY_ORDER)
        {
            if (!_segments.ContainsKey(type)) continue;

            float targetAffinity = (totalWeight <= 0f && type == ElementType.Neutral)
                ? 1f
                : _elementalSystem.GetAffinity(type);

            // Lerp vers la valeur cible
            float current = _currentWidths[type];
            float next    = smoothSpeed > 0f
                ? Mathf.Lerp(current, targetAffinity, Time.deltaTime * smoothSpeed)
                : targetAffinity;

            _currentWidths[type] = next;

            // Active/désactive selon seuil minimum
            bool visible = next >= minVisibleAffinity;
            _segments[type].gameObject.SetActive(visible);

            if (visible && _layoutElements.TryGetValue(type, out LayoutElement le))
                le.flexibleWidth = next;
        }
    }

    // =========================================================
    // REFRESH INSTANTANÉ (init / changement de scène)
    // =========================================================

    public void RefreshInstant()
    {
        if (_elementalSystem == null) return;

        float totalWeight = _elementalSystem.GetTotalWeight();

        foreach (ElementType type in DISPLAY_ORDER)
        {
            if (!_segments.ContainsKey(type)) continue;

            float affinity = (totalWeight <= 0f && type == ElementType.Neutral)
                ? 1f
                : _elementalSystem.GetAffinity(type);

            _currentWidths[type] = affinity;

            bool visible = affinity >= minVisibleAffinity;
            _segments[type].gameObject.SetActive(visible);

            if (visible && _layoutElements.TryGetValue(type, out LayoutElement le))
                le.flexibleWidth = affinity;
        }
    }

    // =========================================================
    // UTILITAIRES DEBUG
    // =========================================================

    /// <summary>
    /// Retourne un résumé lisible de l'état actuel — utile pour les tooltips.
    /// Ex: "Feu Rang 3 (52%) | Eau Rang 1 (18%)"
    /// </summary>
    public string GetDebugSummary()
    {
        if (_elementalSystem == null) return "ElementalSystem absent";

        List<ElementAffinityPair> actives = _elementalSystem.GetActiveAffinities();
        if (actives.Count == 0) return "Neutre Rang 5 (100%)";

        var parts = new System.Text.StringBuilder();
        foreach (ElementAffinityPair pair in actives)
        {
            if (parts.Length > 0) parts.Append(" | ");
            int rank = _elementalSystem.GetElementRank(pair.element);
            parts.Append($"{pair.element.GetLabel()} Rang {rank} ({pair.affinity * 100f:F0}%)");
        }

        TitleMode mode = _elementalSystem.GetTitleMode();
        parts.Append($"  [{mode}]");
        return parts.ToString();
    }
}