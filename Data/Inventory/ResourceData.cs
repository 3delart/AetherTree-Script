using UnityEngine;

// =============================================================
// RESOURCEDATA.CS — ScriptableObject template de ressource
// Path : Assets/Scripts/Data/Inventory/ResourceData.cs
// AetherTree GDD v30
//
// Types de ressources :
//   CraftMaterial — bois, minerai, tissu, cuir...
//   CookIngredient — ingrédients de cuisine
//   MobDrop        — ressources droppées par des mobs (os, écailles...)
//   Other          — ressource générique
//
// Toutes les ressources sont stackables.
// =============================================================

public enum ResourceType
{
    CraftMaterial,  // Matériaux de craft (bois, minerai, tissu...)
    CookIngredient, // Ingrédients de cuisine
    MobDrop,        // Drop de mob (os, écailles, fourrure...)
    Other,
}

[CreateAssetMenu(fileName = "Resource_", menuName = "AetherTree/Inventory/ResourceData")]
public class ResourceData : ScriptableObject
{
    // ── Identité ──────────────────────────────────────────────
    [Header("Identité")]
    public string       resourceName = "Resource";
    public ResourceType resourceType = ResourceType.CraftMaterial;
    public Sprite       icon;
    public GameObject   prefab;
    [TextArea]
    public string       description  = "";

    // ── Stack ─────────────────────────────────────────────────
    [Header("Stack")]
    [Tooltip("Quantité max par stack dans l'inventaire.")]
    public int maxStack = 999;

    // ── Valeur ────────────────────────────────────────────────
    [Header("Valeur")]
    [Tooltip("Prix de vente de base en Aeris.")]
    public int sellPrice = 1;

    // ── Utilitaires ───────────────────────────────────────────
    public ResourceInstance CreateInstance(int quantity = 1)
        => new ResourceInstance(this, Mathf.Clamp(quantity, 1, maxStack));
}

// =============================================================
// RESOURCEINSTANCE — données runtime d'une ressource
// =============================================================
[System.Serializable]
public class ResourceInstance
{
    public ResourceData data;
    public int          quantity = 1;

    public ResourceInstance(ResourceData source, int qty = 1)
    {
        data     = source;
        quantity = qty;
    }

    public string       Name        => data?.resourceName ?? "Resource";
    public Sprite       Icon        => data?.icon;
    public ResourceType Type        => data?.resourceType ?? ResourceType.Other;
    public int          MaxStack    => data?.maxStack     ?? 999;
    public int          SellPrice   => data?.sellPrice    ?? 1;
    public bool         IsEmpty     => quantity <= 0;

    public int Add(int amount)
    {
        int total = quantity + amount;
        quantity  = Mathf.Min(total, MaxStack);
        return Mathf.Max(0, total - MaxStack);
    }

    public bool Remove(int amount = 1)
    {
        if (quantity < amount) return false;
        quantity -= amount;
        return true;
    }
}
