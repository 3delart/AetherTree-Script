using UnityEngine;
using UnityEngine.AI;

// =============================================================
// ResourceApproach — Auto-approche vers les ResourceNodes
// Path : Assets/Scripts/Systems/ResourceApproach.cs
// AetherTree GDD v3.1
//
// Même pattern que LootApproach.
// Setup : poser sur le GameObject Player.
// =============================================================

public class ResourceApproach : MonoBehaviour
{
    public static ResourceApproach Instance { get; private set; }

    public float checkInterval = 0.1f;

    private NavMeshAgent _agent;
    private Player       _player;
    private ResourceNode _targetNode;
    private float        _checkTimer;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(this); return; }
        Instance = this;
        _agent   = GetComponent<NavMeshAgent>();
        _player  = GetComponent<Player>();
    }

    private void Update()
    {
        if (_targetNode == null) return;

        // Déplacement manuel → annule
        if (GameControls.MoveHeld) { Cancel(); return; }

        _checkTimer -= Time.deltaTime;
        if (_checkTimer > 0f) return;
        _checkTimer = checkInterval;

        // Node disparu ou épuisé
        if (!_targetNode.gameObject.activeSelf) { Cancel(); return; }

        float dist  = Vector3.Distance(transform.position, _targetNode.transform.position);
        float range = _targetNode.data?.interactionRadius ?? 2.5f;

        if (dist <= range)
        {
            _agent?.ResetPath();
            _targetNode.BeginCollect(_player);
            _targetNode = null;
        }
        else
        {
            _agent?.SetDestination(_targetNode.transform.position);
        }
    }

    public void ApproachNode(ResourceNode node, Player player)
    {
        if (node == null || player == null) return;
        _targetNode = node;
        _player     = player;
        _checkTimer = 0f;
        _agent?.SetDestination(node.transform.position);
    }

    public void Cancel()
    {
        _targetNode = null;
        ProgressBarUI.Instance?.Cancel();
    }
}
