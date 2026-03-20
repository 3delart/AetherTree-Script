using UnityEngine;
using UnityEngine.EventSystems;
using TMPro;

// =============================================================
// WORLDAERISITEM — Pile d'Aeris au sol cliquable
// Path : Assets/Scripts/World/WorldAerisItem.cs
// AetherTree GDD v30 — Section 9.2
//
// Spawné par LootManager quand un mob drope des Aeris.
// Clic → ajoute les Aeris à AerisSystem + FloatingText.
// Disparaît après lifetime secondes.
//
// Setup prefab :
//   - Mesh/Sprite de pièce(s)
//   - Collider (non-trigger)
//   - Ce script sur le GO racine
//   - (optionnel) Canvas enfant avec TextMeshPro pour afficher le montant
// =============================================================
public class WorldAerisItem : MonoBehaviour
{
    [Tooltip("Durée de vie en secondes.")]
    public float lifetime = 60f;

    [Tooltip("Clignote pendant les X dernières secondes.")]
    public float blinkTime = 10f;

    [Tooltip("Texte 3D affichant le montant (optionnel).")]
    public TextMeshPro amountText;

    private int        _amount;
    private float      _timer;
    private Renderer[] _renderers;
    private bool       _isBlinking;

    // =========================================================
    // INIT
    // =========================================================

    private void Awake()
    {
        _renderers = GetComponentsInChildren<Renderer>();
    }

    public void Init(int amount, float customLifetime = -1f)
    {
        _amount = amount;
        _timer  = customLifetime > 0f ? customLifetime : lifetime;

        if (_renderers == null || _renderers.Length == 0)
            _renderers = GetComponentsInChildren<Renderer>();

        gameObject.name = $"Aeris_{amount}";

        if (amountText != null)
            amountText.text = $"{amount} ¤";
    }

    // =========================================================
    // UPDATE + CLIC
    // =========================================================

    private void OnMouseDown()
    {
        if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
            return;
        PickUp();
    }

    // Fallback raycast — clic détecté via Update si OnMouseDown ne fonctionne pas
    private void Update()
    {
        _timer -= Time.deltaTime;

        if (_timer <= blinkTime && !_isBlinking) _isBlinking = true;

        if (_isBlinking && _renderers != null)
        {
            float alpha = Mathf.PingPong(Time.time * 4f, 1f);
            foreach (var r in _renderers) SetAlpha(r, alpha);
        }

        if (_timer <= 0f) { Destroy(gameObject); return; }

        // Raycast manuel au clic gauche
        if (Input.GetMouseButtonDown(0))
        {
            if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject()) return;
            if (Camera.main == null) return;
            Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
            if (Physics.Raycast(ray, out RaycastHit hit, 100f))
                if (hit.collider != null && hit.collider.gameObject == gameObject)
                    PickUp();
        }
    }

    public void TryPickUp() => PickUp();

    [Tooltip("Distance max de ramassage en unités world.")]
    public float pickupRange = 3f;

    private void PickUp()
    {
        if (_amount <= 0) { Debug.Log("[AERIS] _amount <= 0"); return; }

        // Vérifie la distance
        var player = UnityEngine.Object.FindObjectOfType<Player>();
        if (player != null)
        {
            float dist = Vector3.Distance(player.transform.position, transform.position);
            if (dist > pickupRange)
            {
                // Auto-approche — le joueur se déplace vers les Aeris
                LootApproach.Instance?.ApproachAeris(this);
                return;
            }
        }

        if (AerisSystem.Instance == null)
        {
            Debug.LogWarning("[AERIS] AerisSystem.Instance est NULL — poser AerisSystem sur _Managers !");
            return;
        }

        AerisSystem.Instance.Add(_amount);
        FloatingText.Spawn($"+{_amount} ¤", transform.position, new Color(1f, 0.85f, 0.2f), 1.5f);
        Destroy(gameObject);
    }

    // =========================================================
    // UTILITAIRE
    // =========================================================

    private void SetAlpha(Renderer r, float alpha)
    {
        if (r == null) return;
        foreach (var mat in r.materials)
        {
            Color c = mat.color;
            c.a = alpha;
            mat.color = c;
        }
    }
}
