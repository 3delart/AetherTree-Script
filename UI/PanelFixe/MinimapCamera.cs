using UnityEngine;

// =============================================================
// MINIMAPCAMERA.CS — Caméra orthographique minimap
// AetherTree GDD v21
//
// SETUP :
//   1. Créer un GameObject "MinimapCamera" enfant de la scène
//   2. Ajouter une Camera sur ce GameObject
//   3. Régler la Camera :
//        Projection    → Orthographic
//        Culling Mask  → tout sauf "UI"
//        Clear Flags   → Solid Color  (fond noir ou couleur neutre)
//        Depth         → -1 (ou tout chiffre < caméra principale)
//   4. Créer une RenderTexture (ex: 256×256, R8G8B8A8)
//        Assets → Create → Render Texture
//        L'assigner dans : Camera.targetTexture ET MinimapUI.mapRenderTexture
//   5. Ajouter ce script sur le même GameObject que la Camera
//   6. Assigner playerTransform dans l'Inspector
//
// LAYER MINIMAP (optionnel mais recommandé) :
//   Créer un layer "MinimapOnly" — objets visibles uniquement sur la minimap
//   (ex: icônes marqueurs, zones de spawn). Ajouter ce layer au Culling Mask.
// =============================================================
public class MinimapCamera : MonoBehaviour
{
    public static MinimapCamera Instance { get; private set; }

    [Header("Cible")]
    [Tooltip("Transform du joueur — la caméra le suit en permanence")]
    public Transform playerTransform;

    [Header("Vue")]
    [Tooltip("Taille orthographique — plus la valeur est grande, plus la zone visible est grande")]
    public float orthographicSize = 20f;

    [Header("Hauteur")]
    [Tooltip("Hauteur de la caméra au-dessus du joueur")]
    public float height = 50f;

    private Camera _cam;

    // ─────────────────────────────────────────────────────────
    private void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;

        _cam = GetComponent<Camera>();
        if (_cam == null)
        {
            Debug.LogError("[MinimapCamera] Aucune Camera trouvée sur ce GameObject !");
            return;
        }

        _cam.orthographic     = true;
        _cam.orthographicSize = orthographicSize;
    }

    private void Start()
    {
        // Cherche le joueur automatiquement si non assigné dans l'Inspector
        if (playerTransform == null)
        {
            Player p = FindObjectOfType<Player>();
            if (p != null) playerTransform = p.transform;
            else Debug.LogWarning("[MinimapCamera] Player introuvable — assigner playerTransform manuellement.");
        }
    }

    private void LateUpdate()
    {
        if (playerTransform == null) return;

        // Suit le joueur en X/Z, hauteur fixe
        transform.position = new Vector3(
            playerTransform.position.x,
            playerTransform.position.y + height,
            playerTransform.position.z
        );

        // Vue du dessus — rotation fixe
        transform.rotation = Quaternion.Euler(90f, 0f, 0f);
    }

    // ─────────────────────────────────────────────────────────
    /// <summary>Change le zoom de la minimap (ex: appui sur +/-).</summary>
    public void SetZoom(float size)
    {
        orthographicSize = Mathf.Clamp(size, 5f, 80f);
        if (_cam != null) _cam.orthographicSize = orthographicSize;
    }

    public float GetZoom() => orthographicSize;
}
