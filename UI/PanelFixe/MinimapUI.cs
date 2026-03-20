using UnityEngine;
using UnityEngine.UI;

// =============================================================
// MINIMAPUI.CS — UI de la minimap
// AetherTree GDD v21
//
// SETUP HIERARCHY :
//   MinimapPanel
//     ├── MapNamePanel
//     │     └── (TMP nom de zone — branché plus tard)
//     ├── Map                  ← RawImage — assigner ici
//     │     └── PlayerIcon     ← Image (flèche) — assigner ici
//     └── MapButton            ← Button — assigner ici
//           └── Text (TMP)     "MAP"
//
// SETUP INSPECTOR :
//   mapDisplay       → RawImage "Map"
//   mapRenderTexture → la RenderTexture créée (256×256)
//   playerIcon       → Image "PlayerIcon" (sprite flèche)
//   mapButton        → Button "MapButton"
//   playerTransform  → laisser vide (auto-détecté)
// =============================================================
public class MinimapUI : MonoBehaviour
{
    public static MinimapUI Instance { get; private set; }

    [Header("Affichage")]
    [Tooltip("RawImage 'Map' dans la hiérarchie MinimapPanel")]
    public RawImage  mapDisplay;
    [Tooltip("RenderTexture assignée à la MinimapCamera ET ici")]
    public RenderTexture mapRenderTexture;

    [Header("Icône joueur")]
    [Tooltip("Image flèche au centre de la minimap — tourne selon l'orientation du joueur")]
    public Image     playerIcon;

    [Header("Bouton MAP")]
    [Tooltip("Bouton qui ouvre le panel grande carte")]
    public Button    mapButton;

    // ── Runtime ──────────────────────────────────────────────
    private Transform _playerTransform;

    // ─────────────────────────────────────────────────────────
    private void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;
    }

    private void Start()
    {
        // Brancher la RenderTexture sur le RawImage
        if (mapDisplay != null && mapRenderTexture != null)
            mapDisplay.texture = mapRenderTexture;
        else
            Debug.LogWarning("[MinimapUI] mapDisplay ou mapRenderTexture non assigné !");

        // Bouton MAP → ouvre le panel grande carte
        if (mapButton != null)
            mapButton.onClick.AddListener(() => UIManager.Instance?.TogglePanel("Map"));

        // Récupérer le joueur
        Player p = FindObjectOfType<Player>();
        if (p != null) _playerTransform = p.transform;
        else Debug.LogWarning("[MinimapUI] Player introuvable.");
    }

    private void LateUpdate()
    {
        RotatePlayerIcon();
    }

    // =========================================================
    // ICÔNE JOUEUR — tourne selon l'orientation du joueur
    // =========================================================
    private void RotatePlayerIcon()
    {
        if (playerIcon == null || _playerTransform == null) return;

        // Angle Y du joueur → rotation Z de l'icône UI (vue du dessus)
        float angle = _playerTransform.eulerAngles.y;
        playerIcon.rectTransform.localRotation = Quaternion.Euler(0f, 0f, -angle);
    }

    // =========================================================
    // API PUBLIQUE
    // =========================================================

    /// <summary>Met à jour le nom de zone affiché (à brancher sur ZoneManager plus tard).</summary>
    public void SetZoneName(string zoneName)
    {
        // TODO : brancher MapNamePanel TMP quand ZoneManager sera implémenté
        Debug.Log($"[MinimapUI] Zone : {zoneName}");
    }
}
