using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;

// =============================================================
// PROGRESSBARUI — Barre de progression World Space
// Path : Assets/Scripts/UI/ProgressBarUI.cs
// AetherTree GDD v3.1
//
// Prefab attendu (Canvas World Space, Scale 0.01) :
//   ProgressBarPrefab  ← Canvas racine (World Space, Scale 0.01)
//     ├── BG           ← Image fond
//     ├── Fill         ← Image (Filled, Horizontal, Left) — nom exact "Fill"
//     └── Label        ← TextMeshPro — nom exact "Label"
//
// IMPORTANT : le prefab doit être dans Assets/Prefabs — PAS dans
// la hiérarchie de la scène. Il est instancié dynamiquement.
// =============================================================

public class ProgressBarUI : MonoBehaviour
{
    public static ProgressBarUI Instance { get; private set; }

    [Header("Prefab World Space")]
    [Tooltip("Canvas World Space (Scale 0.01). Racine = Canvas, enfants = BG + Fill + Label.")]
    public GameObject progressBarPrefab;

    [Header("Position")]
    [Tooltip("Hauteur au-dessus du followTarget en unités world.")]
    public float heightOffset = 2.5f;

    [Header("Couleurs par type")]
    public Color colorDefault = new Color(0.3f, 0.75f, 0.3f);
    public Color colorHarvest = new Color(0.8f, 0.65f, 0.2f);
    public Color colorCraft   = new Color(0.3f, 0.5f,  0.9f);
    public Color colorCast    = new Color(0.7f, 0.3f,  0.9f);

    public enum BarType { Default, Harvest, Craft, Cast }

    // ── Runtime ──────────────────────────────────────────────
    private GameObject      _instance;
    private Image           _fill;
    private TextMeshProUGUI _label;
    private Coroutine       _activeCoroutine;
    private bool            _isRunning;
    private System.Action   _onComplete;
    private System.Action   _onCancel;
    private Transform       _followTarget;

    // =========================================================
    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    private void Update()
    {
        if (_instance == null || _followTarget == null) return;

        // Suit la cible
        _instance.transform.position = _followTarget.position + Vector3.up * heightOffset;

        // Toujours face à la caméra
        if (Camera.main != null)
            _instance.transform.forward = Camera.main.transform.forward;
    }

    // =========================================================
    // API
    // =========================================================

    public void StartProgress(string label, float duration,
        System.Action onComplete,
        System.Action onCancel  = null,
        BarType       type      = BarType.Default,
        Transform     followTarget = null)
    {
        // Annule la barre précédente
        if (_activeCoroutine != null)
        {
            StopCoroutine(_activeCoroutine);
            _onCancel?.Invoke();
            DestroyBar();
        }

        _onComplete   = onComplete;
        _onCancel     = onCancel;
        _followTarget = followTarget;

        if (progressBarPrefab == null)
        {
            Debug.LogWarning("[ProgressBarUI] progressBarPrefab non assigné — fallback sans visuel.");
            _activeCoroutine = StartCoroutine(RunProgressNoVisual(duration));
            return;
        }

        // Instancie en dehors de tout Canvas — dans la scène monde
        Vector3 spawnPos = followTarget != null
            ? followTarget.position + Vector3.up * heightOffset
            : Vector3.zero;

        _instance = Instantiate(progressBarPrefab, spawnPos, Quaternion.identity);

        // Cherche Fill et Label par nom pour éviter de prendre BG
        _fill  = FindChildByName(_instance, "Fill")?.GetComponent<Image>();
        _label = FindChildByName(_instance, "Label")?.GetComponent<TextMeshProUGUI>();

        if (_fill  != null) { _fill.fillAmount = 0f; _fill.color = GetColor(type); }
        if (_label != null) _label.text = label;

        if (_fill == null)
            Debug.LogWarning("[ProgressBarUI] Enfant 'Fill' introuvable dans le prefab !");

        _isRunning       = true;
        _activeCoroutine = StartCoroutine(RunProgress(duration));
    }

    public void Cancel()
    {
        if (!_isRunning) return;
        if (_activeCoroutine != null) StopCoroutine(_activeCoroutine);
        _isRunning = false;
        DestroyBar();
        _onCancel?.Invoke();
        _onCancel = _onComplete = null;
    }

    public bool IsRunning => _isRunning;

    // =========================================================
    // COROUTINES
    // =========================================================

    private IEnumerator RunProgress(float duration)
    {
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            if (_fill != null) _fill.fillAmount = Mathf.Clamp01(elapsed / duration);
            yield return null;
        }

        if (_fill != null) _fill.fillAmount = 1f;
        yield return null;

        _isRunning       = false;
        _activeCoroutine = null;
        DestroyBar();
        _onComplete?.Invoke();
        _onComplete = _onCancel = null;
    }

    private IEnumerator RunProgressNoVisual(float duration)
    {
        _isRunning = true;
        yield return new WaitForSeconds(duration);
        _isRunning       = false;
        _activeCoroutine = null;
        _onComplete?.Invoke();
        _onComplete = _onCancel = null;
    }

    // =========================================================
    // HELPERS
    // =========================================================

    private void DestroyBar()
    {
        if (_instance != null) { Destroy(_instance); _instance = null; }
        _fill  = null;
        _label = null;
    }

    /// <summary>Cherche récursivement un enfant par nom exact.</summary>
    private GameObject FindChildByName(GameObject parent, string childName)
    {
        foreach (Transform t in parent.GetComponentsInChildren<Transform>(true))
            if (t.name == childName) return t.gameObject;
        return null;
    }

    private Color GetColor(BarType type) => type switch
    {
        BarType.Harvest => colorHarvest,
        BarType.Craft   => colorCraft,
        BarType.Cast    => colorCast,
        _               => colorDefault,
    };
}
