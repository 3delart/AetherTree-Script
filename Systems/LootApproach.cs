using UnityEngine;
using UnityEngine.AI;

// =============================================================
// LOOTAPPROACH — Auto-approche vers les items au sol
// Path : Assets/Scripts/Systems/LootApproach.cs
// AetherTree GDD v30
//
// Quand le joueur clique sur un WorldLootItem ou WorldAerisItem
// trop loin, il se déplace automatiquement vers l'item et
// ramasse dès qu'il est à portée.
//
// Setup : poser sur le GameObject Player (avec NavMeshAgent).
// =============================================================
public class LootApproach : MonoBehaviour
{
    public static LootApproach Instance { get; private set; }

    [Tooltip("Fréquence de vérification de la portée pendant l'approche (secondes).")]
    public float checkInterval = 0.1f;

    private NavMeshAgent    _agent;
    private WorldLootItem   _targetLoot;
    private WorldAerisItem  _targetAeris;
    private float           _checkTimer;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(this); return; }
        Instance = this;
        _agent   = GetComponent<NavMeshAgent>();
    }

    private void Update()
    {
        if (_targetLoot == null && _targetAeris == null) return;

        // Si le joueur clique pour se déplacer ailleurs, annule l'approche
        if (GameControls.MoveHeld) { Cancel(); return; }

        _checkTimer -= Time.deltaTime;
        if (_checkTimer > 0f) return;
        _checkTimer = checkInterval;

        if (_targetLoot != null)
        {
            if (_targetLoot.gameObject == null) { Cancel(); return; }

            float dist = Vector3.Distance(transform.position, _targetLoot.transform.position);
            if (dist <= _targetLoot.pickupRange)
            {
                _agent.ResetPath();
                _targetLoot.TryPickUp();
                _targetLoot = null;
            }
        }
        else if (_targetAeris != null)
        {
            if (_targetAeris.gameObject == null) { Cancel(); return; }

            float dist = Vector3.Distance(transform.position, _targetAeris.transform.position);
            if (dist <= _targetAeris.pickupRange)
            {
                _agent.ResetPath();
                _targetAeris.TryPickUp();
                _targetAeris = null;
            }
        }
    }

    // =========================================================
    // API
    // =========================================================

    public void ApproachLoot(WorldLootItem loot)
    {
        if (loot == null) return;
        _targetLoot  = loot;
        _targetAeris = null;
        _checkTimer  = 0f;
        _agent.SetDestination(loot.transform.position);
    }

    public void ApproachAeris(WorldAerisItem aeris)
    {
        if (aeris == null) return;
        _targetAeris = aeris;
        _targetLoot  = null;
        _checkTimer  = 0f;
        _agent.SetDestination(aeris.transform.position);
    }

    public void Cancel()
    {
        _targetLoot  = null;
        _targetAeris = null;
    }
}
