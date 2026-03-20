using UnityEngine;

// =============================================================
// COSMETICDATA.CS — ScriptableObject template de cosmétique
// Path : Assets/Scripts/Data/Inventory/CosmeticData.cs
// AetherTree GDD v30
//
// Slots cosmétiques :
//   HeadSkin — skin de tête (remplace le visuel du casque)
//   BodySkin — skin de corps (remplace le visuel de l'armure)
//
// Les cosmétiques n'ont aucun effet sur les stats.
// Ils sont équipés dans EquipmentSlot.Cosmetic.
// =============================================================

public enum CosmeticSlot { HeadSkin, BodySkin }

[CreateAssetMenu(fileName = "Cosmetic_", menuName = "AetherTree/Inventory/CosmeticData")]
public class CosmeticData : ScriptableObject
{
    // ── Identité ──────────────────────────────────────────────
    [Header("Identité")]
    public string       cosmeticName = "Cosmetic";
    public CosmeticSlot cosmeticSlot = CosmeticSlot.HeadSkin;
    public Sprite       icon;
    [TextArea]
    public string       description  = "";

    // ── Visuel 3D ─────────────────────────────────────────────
    [Header("Visuel 3D")]
    [Tooltip("Prefab du skin 3D appliqué sur le personnage.")]
    public GameObject skinPrefab;

    // ── Utilitaires ───────────────────────────────────────────
    public CosmeticInstance CreateInstance() => new CosmeticInstance(this);
}

// =============================================================
// COSMETICINSTANCE — données runtime d'un cosmétique
// =============================================================
[System.Serializable]
public class CosmeticInstance
{
    public CosmeticData data;

    public CosmeticInstance(CosmeticData source) { data = source; }

    public string       Name         => data?.cosmeticName ?? "Cosmetic";
    public Sprite       Icon         => data?.icon;
    public CosmeticSlot Slot         => data?.cosmeticSlot ?? CosmeticSlot.HeadSkin;
    public GameObject   SkinPrefab   => data?.skinPrefab;
}
