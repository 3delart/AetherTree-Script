using UnityEngine;
using System.Collections;
using System.Collections.Generic;

// =============================================================
// SAVESYSTEM — Sauvegarde complète du joueur
// Path : Assets/Scripts/Progression/Save/SaveSystem.cs
// AetherTree GDD v30 — Section 38.4
//
// ⚠ Temporaire : PlayerPrefs + JSON (remplacé par serveur Phase 8)
//
// Sauvegarde auto :
//   - Changement de map (appelé par SceneLoader)
//   - OnApplicationQuit
//   - OnDisable (éditeur Unity — bouton Stop)
//
// Chargement :
//   - LoadAfterInit() appelé via Invoke — attend que Player.Start()
//     soit terminé avant d'appliquer la sauvegarde
//
// Setup :
//   - Placer sur le même GameObject que tes autres managers
//     (_Persistent / _Managers) dans la scène persistante
// =============================================================

public class SaveSystem : MonoBehaviour
{
    public static SaveSystem Instance { get; private set; }

    private const string SAVE_KEY   = "AetherTree_CharacterProgress_1";
    private const float  LOAD_DELAY = 0.1f;

    // Position à restaurer après un changement de map au chargement
    private Vector3 _pendingPosition;
    private Player  _pendingPlayer;
    private bool    _hasPendingPosition;

    // =========================================================
    // SINGLETON + DONTDESTROYONLOAD
    // =========================================================

    private void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    // =========================================================
    // CHARGEMENT AU DÉMARRAGE
    // =========================================================

    private void Start()
    {
        if (HasSave())
            Invoke(nameof(LoadAfterInit), LOAD_DELAY);
    }

    /// <summary>
    /// Appelé via Invoke après LOAD_DELAY secondes.
    /// Garantit que Player.Start() est terminé avant d'appliquer la save.
    /// </summary>
    private void LoadAfterInit()
    {
        var player = FindObjectOfType<Player>();
        if (player != null)
            Load(player);
        else
            Debug.LogWarning("[SAVE] LoadAfterInit : aucun Player trouvé dans la scène.");
    }

    // =========================================================
    // SAUVEGARDE AUTO
    // =========================================================

    private void OnApplicationQuit()
    {
        var player = FindObjectOfType<Player>();
        if (player != null) Save(player);
    }

#if UNITY_EDITOR
    // Dans l'éditeur, OnApplicationQuit n'est pas toujours déclenché
    // quand on clique le bouton Stop. OnDisable prend le relais.
    private void OnDisable()
    {
        var player = FindObjectOfType<Player>();
        if (player != null) Save(player);
    }
#endif

    // =========================================================
    // SAVE PUBLIQUE
    // =========================================================

    public void Save(Player player)
    {
        if (player == null) return;

        var    progress = CollectProgress(player);
        string json     = JsonUtility.ToJson(progress, true);
        PlayerPrefs.SetString(SAVE_KEY, json);
        PlayerPrefs.Save();

        Debug.Log($"[SAVE] ✅ Sauvegardé — Niv.{progress.level} | XP:{progress.xpCombat} | {progress.items.Count} items | Map:{progress.lastMap} | Aeris:{progress.aeris}");
    }

    // =========================================================
    // LOAD PUBLIQUE
    // =========================================================

    public void Load(Player player)
    {
        if (player == null || !HasSave()) return;

        string json     = PlayerPrefs.GetString(SAVE_KEY);
        var    progress = JsonUtility.FromJson<CharacterProgress>(json);

        if (progress == null)
        {
            Debug.LogWarning("[LOAD] ⚠ JSON invalide — sauvegarde ignorée.");
            return;
        }

        ApplyProgress(player, progress);
        Debug.Log($"[LOAD] ✅ Chargé — Niv.{progress.level} | XP:{progress.xpCombat} | {progress.items.Count} items | Map:{progress.lastMap} | Aeris:{progress.aeris}");
    }

    public void DeleteSave()
    {
        PlayerPrefs.DeleteKey(SAVE_KEY);
        Debug.Log("[SAVE] 🗑 Sauvegarde supprimée.");
    }

    public bool HasSave() => PlayerPrefs.HasKey(SAVE_KEY);

    // =========================================================
    // COLLECTE — Player → CharacterProgress
    // =========================================================

    private CharacterProgress CollectProgress(Player player)
    {
        var p = new CharacterProgress
        {
            characterName   = player.entityName,
            level           = player.level,
            xpCombat        = player.xpCombat,
            activeTitle     = player.activeTitle,
            worldReputation = player.worldReputation,
            pvpReputation   = player.pvpReputation,
            lastMap         = SceneLoader.Instance?.CurrentMap ?? "Map_01",
            posX            = player.transform.position.x,
            posY            = player.transform.position.y,
            posZ            = player.transform.position.z,
            aeris           = AerisSystem.Instance?.Aeris ?? 0,
        };

        // ── Conditions débloquées ────────────────────────────
        if (UnlockManager.Instance != null)
            p.unlockedConditionIDs = UnlockManager.Instance.GetUnlocked();

        // ── Compteurs activité ───────────────────────────────
        var counter = player.GetActivityCounter();
        if (counter != null)
            foreach (var kv in counter.GetAll())
                p.activityCountersList.Add(new StringIntPair(kv.Key, kv.Value));

        // ── Skills débloqués ─────────────────────────────────
        foreach (var skill in player.unlockedSkills)
            if (skill != null) p.unlockedSkillNames.Add(skill.name);

        // ── Slots SkillBar (0–9) ─────────────────────────────
        if (SkillBar.Instance != null)
            for (int i = 0; i < 10; i++)
            {
                var skill = SkillBar.Instance.GetSkillAtSlot(i);
                if (skill != null)
                    p.skillBarSlots.Add(new SavedSkillSlot { slotIndex = i, skillName = skill.name });
            }

        // ── Équipements portés ───────────────────────────────
        SaveEquipped(p, player);

        // ── Inventaire (items non équipés) ───────────────────
        if (InventorySystem.Instance != null)
            foreach (var item in InventorySystem.Instance.GetAllItems())
                SaveInventoryItem(p, item);

        return p;
    }

    // ─────────────────────────────────────────────────────────
    // Sauvegarde les équipements actuellement portés
    // ─────────────────────────────────────────────────────────
    private void SaveEquipped(CharacterProgress p, Player player)
    {
        if (player.equippedWeaponInstance?.data != null)
            p.items.Add(new SavedItem {
                category   = "Weapon",
                soName     = player.equippedWeaponInstance.data.name,
                rarityRank = player.equippedWeaponInstance.rarityRank,
                upgradeLevel = player.equippedWeaponInstance.upgradeLevel,
                isEquipped = true,
                slot       = "Weapon" });

        if (player.equippedArmorInstance?.data != null)
            p.items.Add(new SavedItem {
                category   = "Armor",
                soName     = player.equippedArmorInstance.data.name,
                rarityRank = player.equippedArmorInstance.rarityRank,
                upgradeLevel = player.equippedArmorInstance.upgradeLevel,
                isEquipped = true,
                slot       = "Armor" });

        if (player.equippedHelmetInstance?.data != null)
            p.items.Add(new SavedItem {
                category   = "Helmet",
                soName     = player.equippedHelmetInstance.data.name,
                isEquipped = true,
                slot       = "Helmet" });

        if (player.equippedGlovesInstance?.data != null)
            p.items.Add(new SavedItem {
                category   = "Gloves",
                soName     = player.equippedGlovesInstance.data.name,
                isEquipped = true,
                slot       = "Gloves" });

        if (player.equippedBootsInstance?.data != null)
            p.items.Add(new SavedItem {
                category   = "Boots",
                soName     = player.equippedBootsInstance.data.name,
                isEquipped = true,
                slot       = "Boots" });

        if (player.equippedJewelryInstances != null)
            foreach (var j in player.equippedJewelryInstances)
                if (j?.data != null)
                    p.items.Add(new SavedItem {
                        category   = "Jewelry",
                        soName     = j.data.name,
                        isEquipped = true,
                        slot       = j.Slot.ToString() });

        if (player.equippedSpiritInstances?.Count > 0 && player.equippedSpiritInstances[0]?.data != null)
            p.items.Add(new SavedItem {
                category   = "Spirit",
                soName     = player.equippedSpiritInstances[0].data.name,
                isEquipped = true,
                slot       = "Spirit" });
    }

    // ─────────────────────────────────────────────────────────
    // Sauvegarde un item de l'inventaire (non équipé)
    // ─────────────────────────────────────────────────────────
    private void SaveInventoryItem(CharacterProgress p, InventoryItem item)
    {
        if (item == null) return;
        SavedItem saved = null;

        if      (item.WeaponInstance?.data      != null) saved = new SavedItem { category = "Weapon",      soName = item.WeaponInstance.data.name,      rarityRank = item.WeaponInstance.rarityRank,   upgradeLevel = item.WeaponInstance.upgradeLevel };
        else if (item.ArmorInstance?.data        != null) saved = new SavedItem { category = "Armor",       soName = item.ArmorInstance.data.name,        rarityRank = item.ArmorInstance.rarityRank,    upgradeLevel = item.ArmorInstance.upgradeLevel };
        else if (item.HelmetInstance?.data       != null) saved = new SavedItem { category = "Helmet",      soName = item.HelmetInstance.data.name };
        else if (item.GlovesInstance?.data       != null) saved = new SavedItem { category = "Gloves",      soName = item.GlovesInstance.data.name };
        else if (item.BootsInstance?.data        != null) saved = new SavedItem { category = "Boots",       soName = item.BootsInstance.data.name };
        else if (item.JewelryInstance?.data      != null) saved = new SavedItem { category = "Jewelry",     soName = item.JewelryInstance.data.name };
        else if (item.SpiritInstance?.data       != null) saved = new SavedItem { category = "Spirit",      soName = item.SpiritInstance.data.name };
        else if (item.ConsumableInstance?.data   != null) saved = new SavedItem { category = "Consumable",  soName = item.ConsumableInstance.data.name,   quantity   = item.ConsumableInstance.quantity };
        else if (item.ResourceInstance?.data     != null) saved = new SavedItem { category = "Resource",    soName = item.ResourceInstance.data.name,     quantity   = item.ResourceInstance.quantity };
        else if (item.CosmeticInstance?.data     != null) saved = new SavedItem { category = "Cosmetic",    soName = item.CosmeticInstance.data.name };

        if (saved != null)
        {
            saved.isEquipped = false;
            p.items.Add(saved);
        }
    }

    // =========================================================
    // APPLICATION — CharacterProgress → Player
    // =========================================================

    private void ApplyProgress(Player player, CharacterProgress p)
    {
        // ── Niveau & XP ──────────────────────────────────────
        // OnLevelUp() recalcule toutes les stats depuis CharacterData
        if (p.level > 1) player.OnLevelUp(p.level);
        player.xpCombat    = p.xpCombat;
        player.activeTitle = p.activeTitle;

        // ── Réputation ───────────────────────────────────────
        // On force directement les valeurs pour éviter le double-comptage
        player.AddWorldReputation(p.worldReputation - player.worldReputation);
        player.AddPvPReputation(p.pvpReputation   - player.pvpReputation);

        // ── Aeris ────────────────────────────────────────────
        if (AerisSystem.Instance != null)
        {
            int delta = p.aeris - AerisSystem.Instance.Aeris;
            if (delta > 0) AerisSystem.Instance.Add(delta);
        }

        // ── Conditions débloquées ────────────────────────────
        UnlockManager.Instance?.LoadUnlocked(p.unlockedConditionIDs);

        // ── Compteurs activité ───────────────────────────────
        var counter = player.GetActivityCounter();
        if (counter != null)
        {
            var dict = new Dictionary<string, int>();
            foreach (var pair in p.activityCountersList)
                dict[pair.key] = pair.value;
            counter.LoadFromSave(dict);
        }

        // ── Skills débloqués ─────────────────────────────────
        foreach (var skillName in p.unlockedSkillNames)
        {
            var skill = FindSOByName<SkillData>(skillName);
            if (skill != null) player.UnlockSkill(skill);
        }

        // ── Slots SkillBar ───────────────────────────────────
        if (SkillBar.Instance != null)
            foreach (var savedSlot in p.skillBarSlots)
            {
                var skill = FindSOByName<SkillData>(savedSlot.skillName);
                if (skill != null) SkillBar.Instance.SetSkillAtSlot(savedSlot.slotIndex, skill);
            }

        // ── Items — chargés en coroutine (attend 1 frame) ────
        StartCoroutine(LoadItemsDelayed(player, p));
    }

    // ─────────────────────────────────────────────────────────
    // Restauration des items — décalée d'1 frame pour laisser
    // la scène et l'InventorySystem être prêts
    // ─────────────────────────────────────────────────────────
    private IEnumerator LoadItemsDelayed(Player player, CharacterProgress p)
    {
        yield return null;

        // Vide l'inventaire et les slots équipés avant de recharger
        // pour éviter de doubler les items avec ceux de départ
        InventorySystem.Instance?.UnequipAll(player);
        InventorySystem.Instance?.ClearAll();

        foreach (var saved in p.items)
            RestoreItem(player, saved);

        // ── Position ─────────────────────────────────────────
        // Si la map sauvegardée est différente, on utilise LoadMap() (sans
        // reposition forcée) et on stocke la position pour la restaurer
        // via OnMapLoaded — évite de déclencher une nouvelle Save() en plein Load.
        if (SceneLoader.Instance != null && !string.IsNullOrEmpty(p.lastMap)
            && p.lastMap != SceneLoader.Instance.CurrentMap)
        {
            _pendingPosition    = new Vector3(p.posX, p.posY, p.posZ);
            _pendingPlayer      = player;
            _hasPendingPosition = true;
            SceneLoader.OnMapLoaded += OnTargetMapLoaded;
            SceneLoader.Instance.LoadMap(p.lastMap);
        }
        else
        {
            RepositionPlayer(player, p);
        }

        // ── Refresh UI ───────────────────────────────────────
        GameEventBus.Publish(new StatsChangedEvent { player = player });
        InventoryUI.Instance?.RefreshGrid();
        CharacterPanelUI.Instance?.Refresh();

        Debug.Log($"[LOAD] ✅ Items restaurés — {p.items.Count} entrées traitées.");
    }

    // ─────────────────────────────────────────────────────────
    private void RestoreItem(Player player, SavedItem saved)
    {
        InventoryItem item = CreateItemFromSave(saved);
        if (item == null)
        {
            Debug.LogWarning($"[LOAD] ⚠ Item introuvable : {saved.category} '{saved.soName}'");
            return;
        }

        if (saved.isEquipped)
            InventorySystem.Instance?.EquipItem(item, player);
        else
            InventorySystem.Instance?.AddItem(item);
    }

    // ─────────────────────────────────────────────────────────
    private InventoryItem CreateItemFromSave(SavedItem saved)
    {
        switch (saved.category)
        {
            case "Weapon":
                var wd = FindSOByName<WeaponData>(saved.soName);
                return wd != null ? new InventoryItem(wd.CreateDropInstance(saved.rarityRank, saved.upgradeLevel)) : null;

            case "Armor":
                var ad = FindSOByName<ArmorData>(saved.soName);
                return ad != null ? new InventoryItem(ad.CreateDropInstance(saved.rarityRank, saved.upgradeLevel)) : null;

            case "Helmet":
                var hd = FindSOByName<HelmetData>(saved.soName);
                return hd != null ? new InventoryItem(hd.CreateInstance()) : null;

            case "Gloves":
                var gd = FindSOByName<GlovesData>(saved.soName);
                return gd != null ? new InventoryItem(gd.CreateInstance()) : null;

            case "Boots":
                var bd = FindSOByName<BootsData>(saved.soName);
                return bd != null ? new InventoryItem(bd.CreateInstance()) : null;

            case "Jewelry":
                var jd = FindSOByName<JewelryData>(saved.soName);
                return jd != null ? new InventoryItem(jd.CreateInstance()) : null;

            case "Spirit":
                var sd = FindSOByName<SpiritData>(saved.soName);
                return sd != null ? new InventoryItem(new SpiritInstance(sd)) : null;

            case "Consumable":
                var cd = FindSOByName<ConsumableData>(saved.soName);
                return cd != null ? new InventoryItem(cd.CreateInstance(Mathf.Max(1, saved.quantity))) : null;

            case "Resource":
                var rd = FindSOByName<ResourceData>(saved.soName);
                return rd != null ? new InventoryItem(rd.CreateInstance(Mathf.Max(1, saved.quantity))) : null;

            case "Cosmetic":
                var cos = FindSOByName<CosmeticData>(saved.soName);
                return cos != null ? new InventoryItem(new CosmeticInstance(cos)) : null;

            default:
                Debug.LogWarning($"[LOAD] ⚠ Catégorie inconnue : '{saved.category}'");
                return null;
        }
    }

    // ─────────────────────────────────────────────────────────
    private void RepositionPlayer(Player player, CharacterProgress p)
    {
        Vector3 pos   = new Vector3(p.posX, p.posY, p.posZ);
        var     agent = player.GetComponent<UnityEngine.AI.NavMeshAgent>();
        if (agent != null) agent.Warp(pos);
        else player.transform.position = pos;
    }

    /// <summary>
    /// Appelé par SceneLoader.OnMapLoaded après un changement de map au chargement.
    /// Repositionne le joueur à sa position sauvegardée puis se désinscrit.
    /// </summary>
    private void OnTargetMapLoaded(string mapName)
    {
        SceneLoader.OnMapLoaded -= OnTargetMapLoaded;

        if (!_hasPendingPosition || _pendingPlayer == null) return;

        var agent = _pendingPlayer.GetComponent<UnityEngine.AI.NavMeshAgent>();
        if (agent != null) agent.Warp(_pendingPosition);
        else _pendingPlayer.transform.position = _pendingPosition;

        _hasPendingPosition = false;
        _pendingPlayer      = null;

        Debug.Log($"[LOAD] ✅ Repositionné après changement de map → {_pendingPosition}");
    }

    // =========================================================
    // UTILITAIRES
    // =========================================================

    /// <summary>
    /// Cherche un ScriptableObject par nom parmi tous les assets chargés en mémoire.
    /// Fonctionne sans que les SO soient dans un dossier Resources/,
    /// tant qu'ils ont été référencés au moins une fois (ex: via GameDataRegistry).
    /// </summary>
    private T FindSOByName<T>(string soName) where T : ScriptableObject
    {
        if (string.IsNullOrEmpty(soName)) return null;

        foreach (var so in Resources.FindObjectsOfTypeAll<T>())
            if (so.name == soName) return so;

        Debug.LogWarning($"[LOAD] ⚠ SO introuvable : {typeof(T).Name} '{soName}'");
        return null;
    }
}
