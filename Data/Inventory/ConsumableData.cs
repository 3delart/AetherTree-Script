using UnityEngine;
using System.Collections.Generic;

// =============================================================
// CONSUMABLEDATA.CS — ScriptableObject template de consommable
// Path : Assets/Scripts/Data/Inventory/ConsumableData.cs
// AetherTree GDD v30
//
// Types de consommables :
//   Potion        — restaure HP/Mana, applique un BuffData
//   DungeonStone  — pierre d'accès à un donjon
//   TeleportItem  — téléportation vers une zone
//   Rune          — wrapper pour RuneInstance (géré séparément)
//   Gem           — wrapper pour GemInstance  (géré séparément)
//   Other         — effet spécial custom
//
// Usage :
//   ConsumableData SO → CreateInstance() → ConsumableInstance
//   Player.UseConsumable(instance) → applique l'effet
// =============================================================

public enum ConsumableType
{
    Potion,       // Restaure HP et/ou Mana, applique un BuffData
    DungeonStone, // Ouvre l'accès à un donjon spécifique
    TeleportItem, // Téléporte vers une zone
    Other,        // Effet custom
}

[CreateAssetMenu(fileName = "Consumable_", menuName = "AetherTree/Inventory/ConsumableData")]
public class ConsumableData : ScriptableObject
{
    // ── Identité ──────────────────────────────────────────────
    [Header("Identité")]
    public string          consumableName = "Consumable";
    public ConsumableType  consumableType = ConsumableType.Potion;
    public Sprite          icon;
    public GameObject      prefab;
    [TextArea]
    public string          description    = "";

    // ── Potion ────────────────────────────────────────────────
    [Header("Potion (si consumableType = Potion)")]
    [Tooltip("HP restaurés. 0 = pas de soin HP.")]
    public float healHP   = 0f;
    [Tooltip("Mana restaurée. 0 = pas de soin Mana.")]
    public float healMana = 0f;
    [Tooltip("Buff appliqué à l'utilisation (optionnel).")]
    public BuffData buffEffect;
    [Tooltip("Cooldown avant de pouvoir réutiliser cette potion (secondes).")]
    public float cooldown = 30f;

    // ── Pierre de donjon ──────────────────────────────────────
    [Header("Pierre de donjon (si consumableType = DungeonStone)")]
    [Tooltip("ID du donjon accessible avec cette pierre.")]
    public string dungeonID = "";
    [Tooltip("Niveau requis pour utiliser cette pierre.")]
    public int    requiredLevel = 1;

    // ── Téléportation ─────────────────────────────────────────
    [Header("Téléportation (si consumableType = TeleportItem)")]
    [Tooltip("ID de la zone de destination.")]
    public string targetZoneID = "";

    // ── Stackable ─────────────────────────────────────────────
    [Header("Stack")]
    [Tooltip("Quantité max par stack dans l'inventaire.")]
    public int maxStack = 99;


    // ── Utilitaires ───────────────────────────────────────────
    public ConsumableInstance CreateInstance(int quantity = 1)
        => new ConsumableInstance(this, Mathf.Clamp(quantity, 1, maxStack));
}

// =============================================================
// CONSUMABLEINSTANCE — données runtime d'un consommable
// =============================================================
[System.Serializable]
public class ConsumableInstance
{
    public ConsumableData data;
    public int            quantity = 1;

    public ConsumableInstance(ConsumableData source, int qty = 1)
    {
        data     = source;
        quantity = qty;
    }

    public string Name      => data?.consumableName ?? "Consumable";
    public Sprite Icon      => data?.icon;
    public int    MaxStack  => data?.maxStack ?? 99;
    public bool   IsEmpty   => quantity <= 0;

    /// <summary>Ajoute une quantité au stack. Retourne le surplus si dépassement.</summary>
    public int Add(int amount)
    {
        int total   = quantity + amount;
        quantity    = Mathf.Min(total, MaxStack);
        return Mathf.Max(0, total - MaxStack);
    }

    /// <summary>Retire une quantité. Retourne false si stock insuffisant.</summary>
    public bool Remove(int amount = 1)
    {
        if (quantity < amount) return false;
        quantity -= amount;
        return true;
    }
}
