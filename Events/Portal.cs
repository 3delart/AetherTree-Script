using UnityEngine;
using System.Collections;

// =============================================================
// PORTAL.CS — Portail bidirectionnel entre maps
// Path : Assets/Scripts/World/Portal.cs
// AetherTree GDD v30 — Section 17
//
// Chaque portail a un ID unique. Le portail de destination
// sert de point de spawn — le joueur apparaît dessus.
//
// Setup Unity (exemple Map_01 ↔ Map_02) :
//   Portail A (dans Map_01) :
//     - portalID       = "Portal_A"
//     - targetMap      = "Map_02"
//     - targetPortalID = "Portal_B"
//   Portail B (dans Map_02) :
//     - portalID       = "Portal_B"
//     - targetMap      = "Map_01"
//     - targetPortalID = "Portal_A"
//
//   Collider Trigger sur le GO.
// =============================================================
public class Portal : MonoBehaviour
{
    [Header("Identité")]
    [Tooltip("ID unique de CE portail")]
    public string portalID = "Portal_A";

    [Header("Destination")]
    [Tooltip("Scène cible (doit être dans Build Settings)")]
    public string targetMap = "Map_02";

    [Tooltip("ID du portail dans la scène cible — le joueur spawne dessus")]
    public string targetPortalID = "Portal_B";

    [Tooltip("Nom affiché au joueur")]
    public string destinationLabel = "";

    [Header("Paramètres")]
    [Tooltip("Cooldown après spawn pour ne pas re-traverser immédiatement (secondes)")]
    public float spawnCooldown = 3f;

    [Tooltip("Délai avant téléportation (secondes)")]
    public float teleportDelay = 0.5f;

    private static float _cooldownTimer = 0f;
    private bool _teleporting  = false;

    private void Start()
    {
        // Si le joueur arrive via ce portail, le repositionne ici
        if (PortalTransferData.TargetPortalID == portalID)
        {
            RepositionPlayer();
            _cooldownTimer = spawnCooldown;
            PortalTransferData.Clear();
        }
    }

    private void Update()
    {
        if (_cooldownTimer > 0f) _cooldownTimer -= Time.deltaTime;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.GetComponent<Player>() == null) return;
        if (_cooldownTimer > 0f) return;
        if (_teleporting) return;
        StartCoroutine(TeleportRoutine());
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.GetComponent<Player>() == null) return;
        _teleporting = false;
    }

    private IEnumerator TeleportRoutine()
    {
        _teleporting = true;
        Debug.Log($"[PORTAL] {portalID} → {targetMap} ({targetPortalID})");
        yield return new WaitForSeconds(teleportDelay);
        PortalTransferData.TargetPortalID = targetPortalID;
        SceneLoader.Instance?.LoadMap(targetMap);
    }

    private void RepositionPlayer()
    {
        var player = FindObjectOfType<Player>();
        if (player == null) return;
        Vector3 spawnPos = transform.position + transform.forward * 1.5f;
        var agent = player.GetComponent<UnityEngine.AI.NavMeshAgent>();
        if (agent != null) agent.Warp(spawnPos);
        else player.transform.position = spawnPos;
        player.transform.rotation = transform.rotation;
        Debug.Log($"[PORTAL] Joueur spawné sur {portalID}");
    }

#if UNITY_EDITOR
    private void OnDrawGizmos()
    {
        Gizmos.color = new Color(0.3f, 0.6f, 1f, 0.3f);
        Gizmos.DrawCube(transform.position, new Vector3(2f, 3f, 0.5f));
        Gizmos.color = new Color(0.3f, 0.6f, 1f, 0.9f);
        Gizmos.DrawWireCube(transform.position, new Vector3(2f, 3f, 0.5f));
        UnityEditor.Handles.color = Color.cyan;
        UnityEditor.Handles.Label(transform.position + Vector3.up * 2f,
            $"{portalID} → {targetMap}/{targetPortalID}");
    }
#endif
}

// =============================================================
// PORTALTRANSFERDATA — données persistantes entre scènes
// =============================================================
public static class PortalTransferData
{
    public static string TargetPortalID { get; set; } = "";
    public static void Clear() => TargetPortalID = "";
}
