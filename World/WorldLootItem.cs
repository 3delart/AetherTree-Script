using UnityEngine;
using UnityEngine.EventSystems;

// =============================================================
// WORLDLOOTITEM — Item au sol cliquable
// Path : Assets/Scripts/World/WorldLootItem.cs
// AetherTree GDD v30 — Section 33.2
//
// Spawné par LootManager à la mort d'un mob.
// Clic gauche → ramasse l'item dans InventorySystem.
// Disparaît après lifetime secondes.
//
// Setup prefab :
//   - Mesh/Sprite visible
//   - Collider (trigger) pour le clic
//   - Ce script sur le GO racine
//   - (optionnel) Canvas enfant avec nameplate
// =============================================================
public class WorldLootItem : MonoBehaviour
{
    [Tooltip("Durée de vie en secondes avant disparition automatique.")]
    public float lifetime = 30f;

    [Tooltip("Clignote pendant les X dernières secondes avant disparition.")]
    public float blinkTime = 5f;

    // Item encapsulé
    private InventoryItem _item;
    private float         _timer;
    private Renderer[]    _renderers;
    private bool          _isBlinking;

    // =========================================================
    // INIT
    // =========================================================

    private void Awake()
    {
        _renderers = GetComponentsInChildren<Renderer>();
    }

    public void Init(InventoryItem item, float customLifetime = -1f)
    {
        _item      = item;
        _timer     = customLifetime > 0f ? customLifetime : lifetime;
        if (_renderers == null || _renderers.Length == 0)
            _renderers = GetComponentsInChildren<Renderer>();

        // Nomme le GO pour debug
        if (item != null)
            gameObject.name = $"Loot_{item.Name}";
    }

    // =========================================================
    // UPDATE — durée de vie + clignotement
    // =========================================================

    private void Update()
    {
        _timer -= Time.deltaTime;

        // Clignotement avant disparition
        if (_timer <= blinkTime && !_isBlinking)
            _isBlinking = true;

        if (_isBlinking && _renderers != null)
        {
            float alpha = Mathf.PingPong(Time.time * 4f, 1f);
            foreach (var r in _renderers)
                SetAlpha(r, alpha);
        }

        if (_timer <= 0f)
            Destroy(gameObject);
    }

    // =========================================================
    // CLIC — ramassage
    // =========================================================

    private void OnMouseDown()
    {
        if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
            return;
        PickUp();
    }

    // Fallback — appelable depuis un raycast externe (PlayerController)
    public void TryPickUp() => PickUp();

    [Tooltip("Distance max de ramassage en unités world.")]
    public float pickupRange = 3f;

    private void PickUp()
    {
        if (_item == null) return;

        // Vérifie la distance
        var player = UnityEngine.Object.FindObjectOfType<Player>();
        if (player != null)
        {
            float dist = Vector3.Distance(player.transform.position, transform.position);
            if (dist > pickupRange)
            {
                // Auto-approche — le joueur se déplace vers l'item
                LootApproach.Instance?.ApproachLoot(this);
                return;
            }
        }

        var inventory = InventorySystem.Instance;
        if (inventory == null) { Debug.LogWarning("[LOOT] InventorySystem introuvable !"); return; }

        if (inventory.IsFull)
        {
            FloatingText.Spawn("Inventaire plein !", transform.position, Color.red, 1.5f);
            return;
        }

        bool added = inventory.AddItem(_item);
        if (added)
        {
            FloatingText.Spawn($"+{_item.Name}", transform.position, Color.yellow, 1.5f);
            Destroy(gameObject);
        }
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

    // Accesseur pour UI nameplate
    public string ItemName => _item?.Name ?? "???";
    public Sprite ItemIcon => _item?.Icon;
}
