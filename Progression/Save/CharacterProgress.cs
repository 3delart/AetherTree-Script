using UnityEngine;
using System.Collections.Generic;

// =============================================================
// CHARACTERPROGRESS — Données complètes de sauvegarde
// Path : Assets/Scripts/Progression/Save/CharacterProgress.cs
// AetherTree GDD v30 — Section 38.4
// =============================================================

[System.Serializable]
public class StringIntPair
{
    public string key;
    public int    value;
    public StringIntPair(string k, int v) { key = k; value = v; }
}

[System.Serializable]
public class StringFloatPair
{
    public string key;
    public float  value;
    public StringFloatPair(string k, float v) { key = k; value = v; }
}

[System.Serializable]
public class SavedItem
{
    public string category;    // "Weapon","Armor","Helmet","Gloves","Boots","Jewelry","Spirit","Consumable","Resource","Cosmetic"
    public string soName;      // nom du SO pour le retrouver via Resources.FindObjectsOfTypeAll
    public int    rarityRank;  // rareté — aligné sur WeaponInstance.rarityRank / ArmorInstance.rarityRank
    public int    upgradeLevel;// niveau d'amélioration (+0 à +10) — Weapon et Armor uniquement
    public int    quantity;    // pour les stackables (Consumable, Resource)
    public bool   isEquipped;  // true = porté sur le joueur au moment de la sauvegarde
    public string slot;        // "Weapon","Armor","Ring","Necklace","Bracelet","Spirit" si isEquipped
}

[System.Serializable]
public class SavedSkillSlot
{
    public int    slotIndex;
    public string skillName;
}

[System.Serializable]
public class CharacterProgress
{
    // ① Identité & Progression
    public string characterName = "";
    public int    level         = 1;
    public int    xpCombat      = 0;
    public string activeTitle   = "";

    // ② Réputation
    public int worldReputation = 0;
    public int pvpReputation   = 0;

    // ③ Position & Map
    public string lastMap = "Map_01";
    public float  posX    = 0f;
    public float  posY    = 0f;
    public float  posZ    = 0f;

    // ④ Aeris
    public int aeris = 0;

    // ⑤ Équipements + Inventaire (tous dans une liste)
    public List<SavedItem> items = new List<SavedItem>();

    // ⑥ Skills débloqués
    public List<string> unlockedSkillNames = new List<string>();

    // ⑦ Slots SkillBar (0–9)
    public List<SavedSkillSlot> skillBarSlots = new List<SavedSkillSlot>();

    // ⑧ Conditions débloquées (UnlockManager)
    public List<string> unlockedConditionIDs = new List<string>();

    // ⑨ Compteurs activité (List au lieu de Dictionary — non sérialisable par JsonUtility)
    public List<StringIntPair> activityCountersList = new List<StringIntPair>();
}
