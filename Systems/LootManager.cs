using System.Collections.Generic;
using UnityEngine;

// =============================================================
// LOOTMANAGER — Spawne les items au sol à la mort des mobs
// Path : Assets/Scripts/Systems/LootManager.cs
// AetherTree GDD v30 — Section 33.2
//
// S'abonne à GameEventBus.OnMobKilled.
// Pour chaque mob tué :
//   1. Roll la LootTable du mob
//   2. Spawne un WorldLootItem par item droppé autour de la position de mort
//
// Setup :
//   Poser sur _Managers.
//   Assigner lootItemPrefab (prefab avec WorldLootItem).
//   Régler spawnRadius, itemLifetime.
// =============================================================
public class LootManager : MonoBehaviour
{
    public static LootManager Instance { get; private set; }

    [Header("Prefab item au sol")]
    [Tooltip("Prefab avec composant WorldLootItem.")]
    public GameObject lootItemPrefab;

    [Tooltip("Prefab pile d'Aeris avec composant WorldAerisItem.")]
    public GameObject aerisItemPrefab;

    [Header("Paramètres spawn")]
    [Tooltip("Rayon de dispersion autour de la position de mort.")]
    public float spawnRadius  = 1.5f;

    [Tooltip("Durée de vie des items au sol en secondes.")]
    public float itemLifetime = 30f;

    [Tooltip("Délai avant apparition (laisse le mob mourir visuellement).")]
    public float spawnDelay   = 0.5f;

    // =========================================================
    // INIT
    // =========================================================

    private void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;
    }

    private void OnEnable()  => Resubscribe();
    private void OnDisable() => GameEventBus.OnMobKilled -= OnMobKilled;

    public void Resubscribe()
    {
        GameEventBus.OnMobKilled -= OnMobKilled;
        GameEventBus.OnMobKilled += OnMobKilled;
    }

    // =========================================================
    // ÉVÉNEMENT MOB TUÉ
    // =========================================================

    private void OnMobKilled(MobKilledEvent e)
    {
        // Pas de loot si aucun joueur n'a contribué (tué par un Garde PNJ seul)
        if (e.eligiblePlayers == null || e.eligiblePlayers.Count == 0) return;

        Debug.Log($"[LOOTMANAGER] OnMobKilled reçu — mob={e.mob?.mobName} lootTable={e.mob?.lootTable?.name ?? "NULL"}");

        if (e.mob?.lootTable == null)
        {
            Debug.LogWarning("[LOOTMANAGER] Pas de LootTable sur ce mob.");
            return;
        }

        LootRollResult roll = e.mob.lootTable.RollAll();
        Debug.Log($"[LOOTMANAGER] Roll — items={roll.items.Count}");

        if (roll.items.Count == 0 && roll.aeris == 0) return;

        StartCoroutine(SpawnLootDelayed(roll, e.deathPosition));
    }

    // =========================================================
    // SPAWN
    // =========================================================

    private System.Collections.IEnumerator SpawnLootDelayed(LootRollResult roll, Vector3 origin)
    {
        yield return new WaitForSeconds(spawnDelay);

        foreach (InventoryItem item in roll.items)
            SpawnItem(item, origin);

        // Spawn les Aeris au sol si > 0
        if (roll.aeris > 0)
            SpawnAeris(roll.aeris, origin);
    }

    private void SpawnItem(InventoryItem item, Vector3 origin)
    {
        if (item == null) return;

        // Position aléatoire dans le rayon
        Vector2 rand     = Random.insideUnitCircle * spawnRadius;
        Vector3 spawnPos = origin + new Vector3(rand.x, 0.3f, rand.y);

        // Colle au terrain
        if (Physics.Raycast(spawnPos + Vector3.up * 2f, Vector3.down, out RaycastHit hit, 10f))
            spawnPos = hit.point + Vector3.up * 0.3f;

        // Préfère le prefab du SO si disponible, sinon fallback sur lootItemPrefab
        GameObject sourcePrefab = GetItemPrefab(item) ?? lootItemPrefab;

        GameObject go;
        if (sourcePrefab != null)
        {
            go = Instantiate(sourcePrefab, spawnPos, Quaternion.identity);
        }
        else
        {
            // Fallback — sphère procédurale si aucun prefab assigné
            go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            go.transform.position   = spawnPos;
            go.transform.localScale = Vector3.one * 0.3f;
        }

        // Ajoute WorldLootItem si absent (cas du prefab SO sans le composant)
        WorldLootItem loot = go.GetComponent<WorldLootItem>();
        if (loot == null) loot = go.AddComponent<WorldLootItem>();

        // Ajoute un Collider si absent
        if (go.GetComponent<Collider>() == null)
            go.AddComponent<SphereCollider>();

        loot.Init(item, itemLifetime);
    }

    private void SpawnAeris(int amount, Vector3 origin)
    {
        Vector2 rand     = Random.insideUnitCircle * spawnRadius;
        Vector3 spawnPos = origin + new Vector3(rand.x, 0.3f, rand.y);

        if (Physics.Raycast(spawnPos + Vector3.up * 2f, Vector3.down, out RaycastHit hit, 10f))
            spawnPos = hit.point + Vector3.up * 0.3f;

        GameObject go;
        if (aerisItemPrefab != null)
        {
            go = Instantiate(aerisItemPrefab, spawnPos, Quaternion.identity);
        }
        else
        {
            // Fallback — sphère dorée
            go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            go.transform.position   = spawnPos;
            go.transform.localScale = Vector3.one * 0.2f;
            var mat = go.GetComponent<Renderer>()?.material;
            if (mat != null) mat.color = new Color(1f, 0.85f, 0.1f);
        }

        var aeris = go.GetComponent<WorldAerisItem>();
        if (aeris == null) aeris = go.AddComponent<WorldAerisItem>();
        if (go.GetComponent<Collider>() == null) go.AddComponent<SphereCollider>();
        aeris.Init(amount, itemLifetime * 2f); // les Aeris durent plus longtemps
    }

    /// <summary>Récupère le prefab 3D du SO de l'item si disponible.</summary>
    private GameObject GetItemPrefab(InventoryItem item)
    {
        if (item.WeaponInstance?.data?.weaponPrefab   != null) return item.WeaponInstance.data.weaponPrefab;
        if (item.ArmorInstance?.data?.armorPrefab     != null) return item.ArmorInstance.data.armorPrefab;
        if (item.HelmetInstance?.data?.helmetPrefab   != null) return item.HelmetInstance.data.helmetPrefab;
        if (item.GlovesInstance?.data?.glovesPrefab   != null) return item.GlovesInstance.data.glovesPrefab;
        if (item.BootsInstance?.data?.bootsPrefab     != null) return item.BootsInstance.data.bootsPrefab;
        if (item.ConsumableInstance?.data?.prefab     != null) return item.ConsumableInstance.data.prefab;
        if (item.ResourceInstance?.data?.prefab       != null) return item.ResourceInstance.data.prefab;
        return null;
    }
}
