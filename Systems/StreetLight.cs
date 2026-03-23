using UnityEngine;

// =============================================================
// STREETLIGHT.CS — Lampadaire jour/nuit
// Path : Assets/Scripts/World/StreetLight.cs
// AetherTree GDD v30 — §20
//
// Setup prefab lampadaire :
//   1. Ajouter ce script sur le GO racine
//   2. Assigner pointLight (Light enfant — Point Light)
//   3. Assigner (optionnel) emissiveRenderer + emissiveMaterial
//      pour faire briller le matériau (émissivité)
//
// Le lampadaire s'allume automatiquement la nuit
// via les events DayNightCycle.OnNightStart / OnDayStart.
// =============================================================

public class StreetLight : MonoBehaviour
{
    [Header("Lumière")]
    [Tooltip("Point Light enfant du lampadaire.")]
    public Light pointLight;

    [Tooltip("Intensité la nuit.")]
    public float nightIntensity = 2f;

    [Tooltip("Couleur de la lumière la nuit (jaune chaud).")]
    public Color lightColor = new Color(1f, 0.85f, 0.5f);

    [Header("Émissivité (optionnel)")]
    [Tooltip("Renderer de la partie lumineuse (ampoule, globe...).")]
    public Renderer emissiveRenderer;

    [Tooltip("Index du matériau émissif sur le Renderer.")]
    public int materialIndex = 0;

    [Tooltip("Couleur émissive la nuit.")]
    public Color emissiveColor = new Color(1f, 0.85f, 0.4f);

    // ── Runtime ───────────────────────────────────────────────
    private Material _emissiveMat;

    // =========================================================
    private void Awake()
    {
        // Clone le matériau pour ne pas modifier l'asset
        if (emissiveRenderer != null)
        {
            var mats = emissiveRenderer.materials;
            if (materialIndex < mats.Length)
            {
                _emissiveMat = new Material(mats[materialIndex]);
                mats[materialIndex] = _emissiveMat;
                emissiveRenderer.materials = mats;
            }
        }
    }

    private void Start()
    {
        // État initial selon l'heure courante
        bool isNight = DayNightCycle.Instance?.IsNight ?? false;
        SetState(isNight);
    }

    private void OnEnable()
    {
        DayNightCycle.OnNightStart += TurnOn;
        DayNightCycle.OnDayStart   += TurnOff;
    }

    private void OnDisable()
    {
        DayNightCycle.OnNightStart -= TurnOn;
        DayNightCycle.OnDayStart   -= TurnOff;
    }

    // =========================================================
    private void TurnOn()  => SetState(true);
    private void TurnOff() => SetState(false);

    private void SetState(bool on)
    {
        // Lumière
        if (pointLight != null)
        {
            pointLight.enabled   = on;
            pointLight.intensity = on ? nightIntensity : 0f;
            pointLight.color     = lightColor;
        }

        // Émissivité
        if (_emissiveMat != null)
        {
            if (on)
            {
                _emissiveMat.EnableKeyword("_EMISSION");
                _emissiveMat.SetColor("_EmissionColor", emissiveColor * nightIntensity);
            }
            else
            {
                _emissiveMat.DisableKeyword("_EMISSION");
                _emissiveMat.SetColor("_EmissionColor", Color.black);
            }
        }
    }
}
