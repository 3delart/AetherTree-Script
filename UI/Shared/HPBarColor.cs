using UnityEngine;
using UnityEngine.UI;

// =============================================================
// HPBARCOLOR.CS — Couleur dynamique d'une barre (HP ou MP)
// AetherTree GDD v21
// =============================================================
// SETUP :
//   Ajouter sur le même GameObject que le Slider (ou un enfant)
//   Assigner le Slider cible
//   Fonctionne pour HP (vert→jaune→rouge) et MP (bleu, fixe)
// =============================================================

public class HPBarColor : MonoBehaviour
{
    [Header("Slider cible")]
    public Slider slider;

    [Header("Couleurs HP (dégradé selon %)")]
    public Color highColor   = new Color(0.18f, 0.80f, 0.24f); // vert
    public Color mediumColor = new Color(0.95f, 0.77f, 0.06f); // jaune
    public Color lowColor    = new Color(0.85f, 0.15f, 0.10f); // rouge

    [Header("Seuils (0-1)")]
    [Range(0f, 1f)] public float lowThreshold    = 0.25f;
    [Range(0f, 1f)] public float mediumThreshold = 0.50f;

    [Header("Mode fixe (ex: barre MP — désactive le dégradé)")]
    public bool  useFixedColor = false;
    public Color fixedColor    = new Color(0.15f, 0.45f, 0.90f); // bleu MP

    // ── Runtime ──────────────────────────────────────────────
    private Image _fill;

    // ─────────────────────────────────────────────────────────
    private void Start()
    {
        if (slider != null)
            _fill = slider.fillRect?.GetComponent<Image>();


        // Couleur fixe immédiate (ex: barre MP)
        if (useFixedColor && _fill != null)
            _fill.color = fixedColor;
    }

    private void Update()
    {
        if (_fill == null || slider == null || useFixedColor) return;

        float ratio = slider.maxValue > 0f ? slider.value / slider.maxValue : 0f;
        _fill.color = EvaluateColor(ratio);
    }

    // ─────────────────────────────────────────────────────────
    private Color EvaluateColor(float ratio)
    {
        if (ratio <= lowThreshold)
        {
            // rouge pur sous le seuil bas
            return lowColor;
        }
        else if (ratio <= mediumThreshold)
        {
            // rouge → jaune
            float t = Mathf.InverseLerp(lowThreshold, mediumThreshold, ratio);
            return Color.Lerp(lowColor, mediumColor, t);
        }
        else
        {
            // jaune → vert
            float t = Mathf.InverseLerp(mediumThreshold, 1f, ratio);
            return Color.Lerp(mediumColor, highColor, t);
        }
    }

    // ─────────────────────────────────────────────────────────
    /// <summary>Force une couleur fixe dynamiquement (ex: shield actif).</summary>
    public void SetOverrideColor(Color color)
    {
        useFixedColor = true;
        fixedColor    = color;
        if (_fill != null) _fill.color = color;
    }

    /// <summary>Repasse en mode dégradé dynamique.</summary>
    public void ClearOverride()
    {
        useFixedColor = false;
    }
}
