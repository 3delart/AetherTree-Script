using System.Collections.Generic;
using UnityEngine;

// =============================================================
// INVENTORYSYSTEM.CS — Gestion des items en mémoire
// Path : Assets/Scripts/Systems/InventorySystem.cs
// AetherTree GDD v30
//
// Conteneur runtime pour tous les items du joueur (non équipés).
// Chaque item est stocké comme un InventoryItem (wrapper générique).
// Publie OnInventoryChanged après chaque modification.
//
// Usage :
//   InventorySystem.Instance.AddItem(new InventoryItem(weaponInstance))
//   InventorySystem.Instance.RemoveItem(item)
//   InventorySystem.Instance.GetItems(EquipmentSlot.Weapon)
// =============================================================

public class InventorySystem : MonoBehaviour
{
    public static InventorySystem Instance { get; private set; }

    // Taille max de l'inventaire (GDD — à ajuster)
    public const int MAX_SLOTS = 80;

    private readonly List<InventoryItem> _items = new List<InventoryItem>();

    public System.Action OnInventoryChanged;

    private void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;
    }

    // ── Lecture ───────────────────────────────────────────────

    public List<InventoryItem> GetItems(EquipmentSlot slot)
    {
        var result = new List<InventoryItem>();
        foreach (var item in _items)
            if (item.Slot == slot) result.Add(item);
        return result;
    }

    public int  Count  => _items.Count;
    public bool IsFull => _items.Count >= MAX_SLOTS;

    public List<InventoryItem> GetConsommables()
    {
        var result = new List<InventoryItem>();
        foreach (var item in _items)
            if (item.ItemCategory == InventoryCategory.Consommable) result.Add(item);
        return result;
    }

    public List<InventoryItem> GetRessources()
    {
        var result = new List<InventoryItem>();
        foreach (var item in _items)
            if (item.ItemCategory == InventoryCategory.Ressource) result.Add(item);
        return result;
    }

    public List<InventoryItem> GetCosmetiques()
    {
        var result = new List<InventoryItem>();
        foreach (var item in _items)
            if (item.ItemCategory == InventoryCategory.Cosmetique) result.Add(item);
        return result;
    }

    // ── Accès complet ─────────────────────────────────────────

    /// <summary>Retourne une copie de tous les items (pour la sauvegarde).</summary>
    public List<InventoryItem> GetAllItems() => new List<InventoryItem>(_items);

    // ── Recherche par instance ────────────────────────────────

    /// <summary>Trouve l'InventoryItem existant dans _items qui contient cette instance.</summary>
    public InventoryItem GetItemByInstance(object instance)
    {
        if (instance == null) return null;
        foreach (var item in _items)
        {
            if (item.WeaponInstance    == instance) return item;
            if (item.ArmorInstance     == instance) return item;
            if (item.HelmetInstance    == instance) return item;
            if (item.GlovesInstance    == instance) return item;
            if (item.BootsInstance     == instance) return item;
            if (item.JewelryInstance   == instance) return item;
            if (item.SpiritInstance    == instance) return item;
            if (item.ConsumableInstance == instance) return item;
            if (item.ResourceInstance  == instance) return item;
        }
        return null;
    }

    // ── Ajout ─────────────────────────────────────────────────

    public bool AddItem(InventoryItem item)
    {
        if (item == null) return false;

        // ── Stacking — Ressources ────────────────────────────
        if (item.ResourceInstance != null)
        {
            var existing = FindStackableResource(item.ResourceInstance.data);
            if (existing != null)
            {
                int overflow = existing.ResourceInstance.Add(item.ResourceInstance.quantity);
                OnInventoryChanged?.Invoke();

                // S'il reste un overflow (stack plein), on crée un nouveau slot
                if (overflow > 0)
                    return AddItem(new InventoryItem(item.ResourceInstance.data.CreateInstance(overflow)));

                return true;
            }
        }

        // ── Stacking — Consommables ──────────────────────────
        if (item.ConsumableInstance != null)
        {
            var existing = FindStackableConsumable(item.ConsumableInstance.data);
            if (existing != null)
            {
                int overflow = existing.ConsumableInstance.Add(item.ConsumableInstance.quantity);
                OnInventoryChanged?.Invoke();

                if (overflow > 0)
                    return AddItem(new InventoryItem(item.ConsumableInstance.data.CreateInstance(overflow)));

                return true;
            }
        }

        // ── Pas de stack existant — nouveau slot ─────────────
        if (IsFull)
        {
            Debug.LogWarning("[INVENTORY] Inventaire plein !");
            return false;
        }

        _items.Add(item);
        OnInventoryChanged?.Invoke();
        return true;
    }

    /// <summary>Cherche un slot ressource du même SO avec de la place disponible.</summary>
    private InventoryItem FindStackableResource(ResourceData data)
    {
        if (data == null) return null;
        foreach (var item in _items)
            if (item.ResourceInstance?.data == data && item.ResourceInstance.quantity < item.ResourceInstance.MaxStack)
                return item;
        return null;
    }

    /// <summary>Cherche un slot consommable du même SO avec de la place disponible.</summary>
    private InventoryItem FindStackableConsumable(ConsumableData data)
    {
        if (data == null) return null;
        foreach (var item in _items)
            if (item.ConsumableInstance?.data == data && item.ConsumableInstance.quantity < item.ConsumableInstance.MaxStack)
                return item;
        return null;
    }

    // ── Suppression ───────────────────────────────────────────

    public bool RemoveItem(InventoryItem item)
    {
        if (item == null || !_items.Contains(item)) return false;
        _items.Remove(item);
        OnInventoryChanged?.Invoke();
        return true;
    }

    // ── Équipement depuis l'inventaire ────────────────────────

    /// <summary>
    /// Équipe l'item sur le joueur et le retire de l'inventaire.
    /// L'item actuellement équipé est retourné dans l'inventaire.
    /// </summary>
    public bool EquipItem(InventoryItem item, Player player)
    {
        if (item == null || player == null) return false;

        switch (item.Slot)
        {
            case EquipmentSlot.Weapon:
                if (item.WeaponInstance == null) return false;
                if (player.equippedWeaponInstance?.data != null)
                    AddItem(new InventoryItem(player.equippedWeaponInstance));
                player.EquipWeapon(item.WeaponInstance);
                break;

            case EquipmentSlot.Armor:
                if (item.ArmorInstance == null) return false;
                if (player.equippedArmorInstance?.data != null)
                    AddItem(new InventoryItem(player.equippedArmorInstance));
                player.EquipArmor(item.ArmorInstance);
                break;

            case EquipmentSlot.Helmet:
                if (item.HelmetInstance == null) return false;
                if (player.equippedHelmetInstance != null)
                    if (player.equippedHelmetInstance?.data != null) AddItem(new InventoryItem(player.equippedHelmetInstance));
                player.EquipHelmet(item.HelmetInstance);
                break;

            case EquipmentSlot.Gloves:
                if (item.GlovesInstance == null) return false;
                if (player.equippedGlovesInstance != null)
                    if (player.equippedGlovesInstance?.data != null) AddItem(new InventoryItem(player.equippedGlovesInstance));
                player.EquipGloves(item.GlovesInstance);
                break;

            case EquipmentSlot.Boots:
                if (item.BootsInstance == null) return false;
                if (player.equippedBootsInstance != null)
                    if (player.equippedBootsInstance?.data != null) AddItem(new InventoryItem(player.equippedBootsInstance));
                player.EquipBoots(item.BootsInstance);
                break;

            case EquipmentSlot.Ring:
            case EquipmentSlot.Necklace:
            case EquipmentSlot.Bracelet:
                if (item.JewelryInstance == null) return false;
                // Cherche le bijou du même slot déjà équipé
                JewelryInstance existing = player.equippedJewelryInstances?.Find(
                    j => j != null && j.Slot == item.JewelryInstance.Slot);
                if (existing != null)
                {
                    player.UnequipJewelry(existing);
                    AddItem(new InventoryItem(existing));
                }
                player.EquipJewelry(item.JewelryInstance);
                break;

            case EquipmentSlot.Spirit:
                if (item.SpiritInstance == null) return false;
                if (player.equippedSpiritInstances?.Count > 0)
                {
                    var oldSpirit = player.equippedSpiritInstances[0];
                    player.UnequipSpirit(oldSpirit);
                    AddItem(new InventoryItem(oldSpirit));
                }
                player.EquipSpirit(item.SpiritInstance);
                break;

            default:
                Debug.LogWarning($"[INVENTORY] Slot {item.Slot} non géré.");
                return false;
        }

        // Retire l'item de l'inventaire — cherche par référence puis par contenu
        if (!RemoveItem(item))
            RemoveItemByContent(item);

        OnInventoryChanged?.Invoke();
        return true;
    }

    /// <summary>
    /// Retire un item en comparant le contenu (instance) plutôt que la référence.
    /// Utile quand l'item vient d'un drag depuis le CharacterPanel (nouveau wrapper).
    /// </summary>
    private void RemoveItemByContent(InventoryItem item)
    {
        InventoryItem toRemove = null;

        foreach (var existing in _items)
        {
            if (item.WeaponInstance   != null && existing.WeaponInstance   == item.WeaponInstance)   { toRemove = existing; break; }
            if (item.ArmorInstance    != null && existing.ArmorInstance    == item.ArmorInstance)    { toRemove = existing; break; }
            if (item.HelmetInstance   != null && existing.HelmetInstance   == item.HelmetInstance)   { toRemove = existing; break; }
            if (item.GlovesInstance   != null && existing.GlovesInstance   == item.GlovesInstance)   { toRemove = existing; break; }
            if (item.BootsInstance    != null && existing.BootsInstance    == item.BootsInstance)    { toRemove = existing; break; }
            if (item.JewelryInstance  != null && existing.JewelryInstance  == item.JewelryInstance)  { toRemove = existing; break; }
            if (item.SpiritInstance   != null && existing.SpiritInstance   == item.SpiritInstance)   { toRemove = existing; break; }
            if (item.ConsumableInstance != null && existing.ConsumableInstance == item.ConsumableInstance) { toRemove = existing; break; }
            if (item.ResourceInstance != null && existing.ResourceInstance == item.ResourceInstance)  { toRemove = existing; break; }
        }

        if (toRemove != null) _items.Remove(toRemove);
    }

    /// <summary>Déséquipe l'item du joueur et le place dans l'inventaire.</summary>
    public void UnequipToInventory(EquipmentSlot slot, Player player)
    {
        if (player == null) return;

        switch (slot)
        {
            case EquipmentSlot.Weapon:
                if (player.equippedWeaponInstance != null)
                { AddItem(new InventoryItem(player.equippedWeaponInstance)); player.UnequipWeapon(); }
                break;
            case EquipmentSlot.Armor:
                if (player.equippedArmorInstance != null)
                { AddItem(new InventoryItem(player.equippedArmorInstance)); player.UnequipArmor(); }
                break;
            case EquipmentSlot.Helmet:
                if (player.equippedHelmetInstance != null)
                { AddItem(new InventoryItem(player.equippedHelmetInstance)); player.UnequipHelmet(); }
                break;
            case EquipmentSlot.Gloves:
                if (player.equippedGlovesInstance != null)
                { AddItem(new InventoryItem(player.equippedGlovesInstance)); player.UnequipGloves(); }
                break;
            case EquipmentSlot.Boots:
                if (player.equippedBootsInstance != null)
                { AddItem(new InventoryItem(player.equippedBootsInstance)); player.UnequipBoots(); }
                break;
        }
    }

    // ── Sauvegarde / Chargement ───────────────────────────────

    /// <summary>
    /// Déséquipe tous les slots du joueur sans remettre les items dans l'inventaire.
    /// Appelé par SaveSystem avant de recharger une sauvegarde — évite les doublons
    /// avec les items de départ équipés dans Player.Awake().
    /// </summary>
    public void UnequipAll(Player player)
    {
        if (player == null) return;

        player.UnequipWeapon();
        player.UnequipArmor();
        player.UnequipHelmet();
        player.UnequipGloves();
        player.UnequipBoots();

        // Bijoux — copie de la liste pour éviter de modifier pendant l'itération
        if (player.equippedJewelryInstances != null)
        {
            var copy = new System.Collections.Generic.List<JewelryInstance>(player.equippedJewelryInstances);
            foreach (var j in copy)
                if (j != null) player.UnequipJewelry(j);
        }

        // Esprits
        if (player.equippedSpiritInstances != null)
        {
            var copy = new System.Collections.Generic.List<SpiritInstance>(player.equippedSpiritInstances);
            foreach (var s in copy)
                if (s != null) player.UnequipSpirit(s);
        }

    }

    /// <summary>
    /// Vide complètement l'inventaire (items non équipés).
    /// Appelé par SaveSystem avant de recharger une sauvegarde.
    /// </summary>
    public void ClearAll()
    {
        _items.Clear();
        OnInventoryChanged?.Invoke();

    }
}

// =============================================================
// INVENTORYCATEGORY — catégorie principale d'un item
// =============================================================
public enum InventoryCategory
{
    Equipement,  // Weapon, Armor, Helmet, Gloves, Boots, Jewelry, Spirit
    Consommable, // Potion, Pierre de donjon, Téléportation
    Ressource,   // Matériaux craft, Ingrédients cuisine, Drops mobs
    Cosmetique,  // HeadSkin, BodySkin
}

// =============================================================
// INVENTORYITEM — Wrapper générique pour tout type d'item
// =============================================================
public class InventoryItem
{
    // ── Instances équipement ──────────────────────────────────
    public WeaponInstance     WeaponInstance     { get; private set; }
    public ArmorInstance      ArmorInstance      { get; private set; }
    public HelmetInstance     HelmetInstance     { get; private set; }
    public GlovesInstance     GlovesInstance     { get; private set; }
    public BootsInstance      BootsInstance      { get; private set; }
    public JewelryInstance    JewelryInstance    { get; private set; }
    public SpiritInstance     SpiritInstance     { get; private set; }

    // ── Instances consommables ────────────────────────────────
    public ConsumableInstance ConsumableInstance { get; private set; }
    public RuneInstance       RuneInstance       { get; private set; }
    public GemInstance        GemInstance        { get; private set; }

    // ── Instances ressources ──────────────────────────────────
    public ResourceInstance   ResourceInstance   { get; private set; }

    // ── Instances cosmétiques ─────────────────────────────────
    public CosmeticInstance   CosmeticInstance   { get; private set; }

    // ── Catégorie & Slot ──────────────────────────────────────
    public InventoryCategory ItemCategory { get; private set; }
    public EquipmentSlot     Slot         { get; private set; }

    // ── Nom ───────────────────────────────────────────────────
    public string Name
    {
        get
        {
            if (WeaponInstance     != null) return WeaponInstance.WeaponName;
            if (ArmorInstance      != null) return ArmorInstance.ArmorName;
            if (HelmetInstance     != null) return HelmetInstance.HelmetName;
            if (GlovesInstance     != null) return GlovesInstance.GlovesName;
            if (BootsInstance      != null) return BootsInstance.BootsName;
            if (JewelryInstance    != null) return JewelryInstance.JewelryName;
            if (SpiritInstance     != null) return SpiritInstance.SpiritName;
            if (ConsumableInstance != null) return ConsumableInstance.Name;
            if (RuneInstance       != null) return RuneInstance.RuneName;
            if (GemInstance        != null) return GemInstance.GemName;
            if (ResourceInstance   != null) return ResourceInstance.Name;
            if (CosmeticInstance   != null) return CosmeticInstance.Name;
            return "???";
        }
    }

    // ── Icône ─────────────────────────────────────────────────
    public Sprite Icon
    {
        get
        {
            if (WeaponInstance?.data     != null) return WeaponInstance.Icon;
            if (ArmorInstance?.data      != null) return ArmorInstance.Icon;
            if (HelmetInstance?.data     != null) return HelmetInstance.Icon;
            if (GlovesInstance?.data     != null) return GlovesInstance.Icon;
            if (BootsInstance?.data      != null) return BootsInstance.Icon;
            if (JewelryInstance?.data    != null) return JewelryInstance.Icon;
            if (SpiritInstance?.data     != null) return SpiritInstance.Icon;
            if (ConsumableInstance?.data != null) return ConsumableInstance.Icon;
            if (RuneInstance?.data       != null) return RuneInstance.Icon;
            if (GemInstance?.data        != null) return GemInstance.Icon;
            if (ResourceInstance?.data   != null) return ResourceInstance.Icon;
            if (CosmeticInstance?.data   != null) return CosmeticInstance.Icon;
            return null;
        }
    }

    // ── Rareté / Quantité (affiché dans Count sur la cellule) ─
    public string CountLabel
    {
        get
        {
            if (WeaponInstance     != null) return WeaponInstance.RarityLabel;
            if (ArmorInstance      != null) return ArmorInstance.RarityLabel;
            if (RuneInstance       != null) return RuneInstance.RarityLabel;
            if (GemInstance        != null) return $"Lv{GemInstance.GemLevel}";
            if (ConsumableInstance != null) return ConsumableInstance.quantity > 1
                                                   ? $"x{ConsumableInstance.quantity}" : "";
            if (ResourceInstance   != null) return ResourceInstance.quantity > 1
                                                   ? $"x{ResourceInstance.quantity}" : "";
            return "";
        }
    }

    // ── Rétrocompat ───────────────────────────────────────────
    public string RarityLabel => CountLabel;

    /// <summary>Texte affiché dans la cellule inventaire — quantité pour stackables, rien pour équipements.</summary>
    public string CellLabel
    {
        get
        {
            if (ConsumableInstance != null) return ConsumableInstance.quantity > 1 ? $"x{ConsumableInstance.quantity}" : "";
            if (ResourceInstance   != null) return ResourceInstance.quantity   > 1 ? $"x{ResourceInstance.quantity}"   : "";
            return ""; // équipements, gemmes, runes — rien
        }
    }

    // ── Constructeurs équipement ──────────────────────────────
    public InventoryItem(WeaponInstance i)
    { WeaponInstance = i; Slot = EquipmentSlot.Weapon; ItemCategory = InventoryCategory.Equipement; }

    public InventoryItem(ArmorInstance i)
    { ArmorInstance = i; Slot = EquipmentSlot.Armor; ItemCategory = InventoryCategory.Equipement; }

    public InventoryItem(HelmetInstance i)
    { HelmetInstance = i; Slot = EquipmentSlot.Helmet; ItemCategory = InventoryCategory.Equipement; }

    public InventoryItem(GlovesInstance i)
    { GlovesInstance = i; Slot = EquipmentSlot.Gloves; ItemCategory = InventoryCategory.Equipement; }

    public InventoryItem(BootsInstance i)
    { BootsInstance = i; Slot = EquipmentSlot.Boots; ItemCategory = InventoryCategory.Equipement; }

    public InventoryItem(JewelryInstance i)
    {
        JewelryInstance = i;
        ItemCategory    = InventoryCategory.Equipement;
        Slot = i.Slot == JewelrySlot.Ring     ? EquipmentSlot.Ring
             : i.Slot == JewelrySlot.Necklace ? EquipmentSlot.Necklace
                                              : EquipmentSlot.Bracelet;
    }

    public InventoryItem(SpiritInstance i)
    { SpiritInstance = i; Slot = EquipmentSlot.Spirit; ItemCategory = InventoryCategory.Equipement; }

    // ── Constructeurs consommables ────────────────────────────
    public InventoryItem(ConsumableInstance i)
    { ConsumableInstance = i; Slot = EquipmentSlot.Cosmetic; ItemCategory = InventoryCategory.Consommable; }

    public InventoryItem(RuneInstance i)
    { RuneInstance = i; Slot = EquipmentSlot.Cosmetic; ItemCategory = InventoryCategory.Consommable; }

    public InventoryItem(GemInstance i)
    { GemInstance = i; Slot = EquipmentSlot.Cosmetic; ItemCategory = InventoryCategory.Consommable; }

    // ── Constructeurs ressources ──────────────────────────────
    public InventoryItem(ResourceInstance i)
    { ResourceInstance = i; Slot = EquipmentSlot.Cosmetic; ItemCategory = InventoryCategory.Ressource; }

    // ── Constructeurs cosmétiques ─────────────────────────────
    public InventoryItem(CosmeticInstance i)
    {
        CosmeticInstance = i;
        Slot             = EquipmentSlot.Cosmetic;
        ItemCategory     = InventoryCategory.Cosmetique;
    }
}
