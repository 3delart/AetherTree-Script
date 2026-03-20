using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.AI.Navigation;

// =============================================================
// SPAWNMANAGER.CS — Zones de spawn | respawn | boss
// AetherTree GDD v18 — Section World
//
// Placer sur un GameObject dans chaque Map (pas dans _Persistent).
// Configurer les SpawnZones dans l'Inspector.
// Chaque MobData doit avoir son prefab assigné.
// =============================================================

public enum SpawnType
{
    ClassicMob,
    MapBoss,
    DungeonMob,
    DungeonBoss
}

public class SpawnManager : MonoBehaviour
{
    public static SpawnManager Instance { get; private set; }

    // =========================================================
    // STRUCTURES
    // =========================================================

    [System.Serializable]
    public class MobSpawnEntry
    {
        public MobData mobData;
        public float   weight = 1f;
    }

    [System.Serializable]
    public class SpawnZone
    {
        [Header("Zone")]
        public string    zoneName  = "Zone";
        public SpawnType spawnType = SpawnType.ClassicMob;
        public Vector3   center;
        public Vector3   size      = new Vector3(20f, 0f, 20f);

        [Header("Mobs")]
        public List<MobSpawnEntry> mobEntries;
        public int minLevel = 1;
        public int maxLevel = 5;

        [Header("Quantité & Timers")]
        public int   mobCount          = 5;
        public float respawnDelay      = 30f;
        public float bossRespawnDelay  = 7200f;

        [Header("Runtime — ne pas modifier")]
        [HideInInspector] public List<GameObject> aliveMobs     = new List<GameObject>();
        [HideInInspector] public int              pendingRespawns = 0;
    }

    // =========================================================
    // INSPECTOR
    // =========================================================

    [Header("Zones de spawn")]
    public List<SpawnZone> zones = new List<SpawnZone>();

    // =========================================================
    // INIT
    // =========================================================

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    private void OnEnable()
    {
        SceneLoader.OnMapLoaded += OnMapReady;
    }

    private void OnDisable()
    {
        SceneLoader.OnMapLoaded -= OnMapReady;
    }

    private bool _initialSpawnDone = false;

    private void Start()
    {
        // SceneLoader charge Map_01 en additif PUIS Map_01 s'initialise.
        // À ce stade OnMapLoaded est déjà passé — on spawne directement.
        // Si pas de SceneLoader (test direct depuis Map_01) → même chose.
        StartCoroutine(SpawnNextFrame());
    }

    private void OnMapReady(string mapName)
    {
        // Ignoré — le spawn est géré dans Start() qui s'exécute après le chargement
    }

    private IEnumerator SpawnNextFrame()
    {
        yield return null;

        NavMeshSurface surface = FindObjectOfType<NavMeshSurface>();
        if (surface != null)
            surface.BuildNavMesh();

        yield return null;
        yield return null;

        foreach (SpawnZone zone in zones)
            SpawnAllMobsInZone(zone);

        _initialSpawnDone = true;
    }

    // =========================================================
    // UPDATE — vérifie les mobs manquants et lance les respawns
    // =========================================================

    private void Update()
    {
        if (!_initialSpawnDone) return;

        foreach (SpawnZone zone in zones)
        {
            if (zone.spawnType == SpawnType.DungeonMob ||
                zone.spawnType == SpawnType.DungeonBoss) continue;

            zone.aliveMobs.RemoveAll(m => m == null);

            int maxCount = zone.spawnType == SpawnType.MapBoss ? 1 : zone.mobCount;
            int missing  = maxCount - zone.aliveMobs.Count - zone.pendingRespawns;

            for (int i = 0; i < missing; i++)
            {
                zone.pendingRespawns++;
                float delay = zone.spawnType == SpawnType.MapBoss
                    ? zone.bossRespawnDelay
                    : zone.respawnDelay;
                StartCoroutine(RespawnAfterDelay(zone, delay));
            }
        }
    }

    // =========================================================
    // SPAWN
    // =========================================================

    private void SpawnAllMobsInZone(SpawnZone zone)
    {
        if (zone.spawnType == SpawnType.DungeonMob ||
            zone.spawnType == SpawnType.DungeonBoss) return;

        int count = zone.spawnType == SpawnType.MapBoss ? 1 : zone.mobCount;
        for (int i = 0; i < count; i++)
            SpawnOneMob(zone);
    }

    private void SpawnOneMob(SpawnZone zone)
    {
        MobData chosenData = RollMobData(zone);
        if (chosenData == null) { Debug.LogWarning($"[SPAWN] {zone.zoneName} — RollMobData retourne null"); return; }

        if (chosenData.prefab == null)
        {
            Debug.LogWarning($"[SpawnManager] {chosenData.mobName} n'a pas de prefab assigné !");
            return;
        }

        int     level    = Random.Range(zone.minLevel, zone.maxLevel + 1);
        Vector3 spawnPos = GetRandomPositionInZone(zone);

        GameObject mobObj = Instantiate(chosenData.prefab, spawnPos, Quaternion.identity);

        Mob mob = mobObj.GetComponent<Mob>();
        if (mob != null)
        {
            mob.data     = chosenData;
            mob.mobLevel = level;

            // Callback mort → retire de la liste aliveMobs
            var capturedObj  = mobObj;
            var capturedZone = zone;
            mob.OnDeath(() =>
            {
                capturedZone.aliveMobs.Remove(capturedObj);
            });
        }

        zone.aliveMobs.Add(mobObj);
    }

    // =========================================================
    // RESPAWN
    // =========================================================

    private IEnumerator RespawnAfterDelay(SpawnZone zone, float delay)
    {
        yield return new WaitForSeconds(delay);
        zone.pendingRespawns--;
        SpawnOneMob(zone);
    }

    // =========================================================
    // UTILITAIRES
    // =========================================================

    private MobData RollMobData(SpawnZone zone)
    {
        if (zone.mobEntries == null || zone.mobEntries.Count == 0) return null;

        float totalWeight = 0f;
        foreach (var entry in zone.mobEntries) totalWeight += entry.weight;

        float roll       = Random.Range(0f, totalWeight);
        float cumulative = 0f;

        foreach (var entry in zone.mobEntries)
        {
            cumulative += entry.weight;
            if (roll <= cumulative) return entry.mobData;
        }

        return zone.mobEntries[0].mobData;
    }

    private Vector3 GetRandomPositionInZone(SpawnZone zone)
    {
        Vector3 randomPos = zone.center + new Vector3(
            Random.Range(-zone.size.x / 2f, zone.size.x / 2f),
            0f,
            Random.Range(-zone.size.z / 2f, zone.size.z / 2f));

        UnityEngine.AI.NavMeshHit hit;
        if (UnityEngine.AI.NavMesh.SamplePosition(randomPos, out hit, 5f, UnityEngine.AI.NavMesh.AllAreas))
            return hit.position;

        return zone.center;
    }

    // =========================================================
    // GIZMOS — visualisation des zones dans l'éditeur
    // =========================================================

    private void OnDrawGizmos()
    {
        if (zones == null) return;
        foreach (SpawnZone zone in zones)
        {
            Color c = zone.spawnType == SpawnType.MapBoss
                ? new Color(1f, 0f, 0f, 0.2f)
                : new Color(0f, 1f, 0.5f, 0.2f);

            Gizmos.color = c;
            Gizmos.DrawCube(zone.center, zone.size);
            Gizmos.color = new Color(c.r, c.g, c.b, 0.8f);
            Gizmos.DrawWireCube(zone.center, zone.size);

            #if UNITY_EDITOR
            UnityEditor.Handles.color = Color.white;
            UnityEditor.Handles.Label(zone.center + Vector3.up * 2f,
                $"{zone.zoneName}\n{zone.aliveMobs.Count}/{zone.mobCount} mobs");
            #endif
        }
    }
}