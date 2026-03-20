using System.Collections.Generic;
using UnityEngine;

// =============================================================
// CONDITIONDATA.CS — AetherTree GDD v21
//
// Règle "None = Any" :
//   kill_specificMob  == null  → n'importe quel mob
//   kill_withSkill    == null  → n'importe quel skill
//   kill_mobElement   == Neutral → n'importe quel élément
//   kill_withElement  == Neutral → n'importe quel élément de skill
//   common_weapon     == Any   → n'importe quelle arme
//   common_locationID == ""    → n'importe quelle zone
// =============================================================

public enum ConditionType
{
    Kill      = 0,
    Affinity  = 1,
    SkillCast = 2,
    Damage    = 3,
    Zone      = 4,
    Item      = 5,
    Social    = 6,
    Activity  = 7,
    Time      = 8,
}

// =============================================================
// CONDITIONENTRY
// =============================================================
[System.Serializable]
public class ConditionEntry
{
    [Header("Type")]
    public ConditionType type = ConditionType.Kill;

    // ── Filtre commun ─────────────────────────────────────────
    [Header("Filtre commun")]
    [Tooltip("Any = toutes les armes")]
    public WeaponType common_weapon        = WeaponType.Any;
    [Tooltip("Vide = partout")]
    public string     common_locationID    = "";
    [Tooltip("0 = pas de restriction")]
    public int        common_playerLevelMin = 0;
    public bool       common_mustBeSolo    = false;
    public bool       common_mustBeInGroup = false;

    // ── Compteur ──────────────────────────────────────────────
    [Header("Compteur")]
    public int  countRequired = 1;
    public bool mustBeLast    = false;

    // ── Kill ─────────────────────────────────────────────────
    [Header("— Kill —")]
    [Tooltip("null = n'importe quel mob")]
    public MobData     kill_specificMob   = null;
    [Tooltip("Any = n'importe quel élément")]
    public ElementType kill_mobElement    = ElementType.Any;
    [Tooltip("null = n'importe quel skill")]
    public SkillData   kill_withSkill     = null;
    [Tooltip("Any = n'importe quel élément")]
    public ElementType kill_withElement   = ElementType.Any;
    public bool        kill_mustBeBoss    = false;
    public bool        kill_isPlayer      = false;
    public bool        kill_mustBeStealth = false;
    public bool        kill_mustBeUnarmed = false;
    public bool        kill_lowHP         = false;
    public bool        kill_atNight       = false;
    public string      kill_inZone        = "";

    // ── Affinity ─────────────────────────────────────────────
    [Header("— Affinity —")]
    public ElementType affinity_element        = ElementType.Fire;
    public float       affinity_minAffinity    = 0.25f;
    public int         affinity_rankMin        = 1;
    public bool        affinity_mustBeDominant = false;
    public List<ElementAffinityReq> affinity_multiElement = new List<ElementAffinityReq>();

    // ── SkillCast ────────────────────────────────────────────
    [Header("— SkillCast —")]
    [Tooltip("null = n'importe quel skill")]
    public SkillData   skillcast_specificSkill = null;
    [Tooltip("Any = n'importe quel élément")]
    public ElementType skillcast_element       = ElementType.Any;
    public bool        skillcast_mustBeCombo   = false;
    public string      skillcast_inZone        = "";

    // ── Damage ───────────────────────────────────────────────
    [Header("— Damage —")]
    public float       damage_minAmount  = 0f;
    [Tooltip("Any = n'importe quel élément")]
    public ElementType damage_element    = ElementType.Any;
    public bool        damage_isReceived = false;
    public bool        damage_inOneHit   = false;

    // ── Zone ─────────────────────────────────────────────────
    [Header("— Zone —")]
    [Tooltip("Vide = n'importe quelle zone")]
    public string zone_zoneID       = "";
    public float  zone_minDuration  = 0f;
    public bool   zone_mustBeAFK    = false;
    public bool   zone_atNight      = false;
    public bool   zone_isDungeon    = false;
    public bool   zone_dungeonSolo  = false;
    public bool   zone_dungeonNoHit = false;
    public float  zone_speedRunMax  = 0f;

    // ── Item ─────────────────────────────────────────────────
    [Header("— Item —")]
    [Tooltip("Vide = n'importe quel item")]
    public string     item_itemID  = "";
    public ItemAction item_action  = ItemAction.Any;
    public int        item_minAeris = 0;

    // ── Social ───────────────────────────────────────────────
    [Header("— Social —")]
    public SocialAction social_action        = SocialAction.Any;
    public bool         social_mustBeInGroup = false;

    // ── Activity ──────────────────────────────────────────────
    [Header("— Activity —")]
    [Tooltip("Vide = n'importe quelle activité (nage, pêche, marche, récolte...)")]
    public string activity_actionType = "";
    [Tooltip("0 = pas de restriction de niveau")]
    public int    activity_levelMin   = 0;

    // ── Time ─────────────────────────────────────────────────
    [Header("— Time —")]
    public TimeAction time_action     = TimeAction.Any;
    public float      time_minMinutes = 0f;
    public int        time_minDays    = 0;
    public bool       time_isNight    = false;
}

// =============================================================
// REWARDTYPE
// =============================================================
public enum RewardType
{
    None, Skill, SkillAndTitle, StatBonus, Title,
    Equipment, Resource, Consumable, Recipe, Other,
}

// =============================================================
// CONDITIONREWARD
// =============================================================
[System.Serializable]
public class ConditionReward
{
    [Header("Filtre arme")]
    [Tooltip("Any = tout le monde reçoit ce reward")]
    public WeaponType weaponType = WeaponType.Any;

    [Header("Type")]
    public RewardType rewardType = RewardType.None;

    [Header("Skill / Passif")]
    public SkillData rewardSkill;

    [Header("Titre")]
    public string rewardTitle;

    [Header("Item → Inventaire")]
    public string rewardItemID;
    public int    rewardItemQuantity = 1;

    [Header("Recette")]
    public string rewardRecipeID;

    [Header("Description (affiché dans le mail)")]
    [TextArea] public string rewardDescription;
}

// =============================================================
// CONDITIONDATA SO
// =============================================================
[CreateAssetMenu(fileName = "Condition_", menuName = "AetherTree/Progression/ConditionData")]
public class ConditionData : ScriptableObject
{
    [Header("Identifiant unique")]
    public string conditionID;

    [Header("Sous-conditions (toutes doivent être remplies)")]
    public List<ConditionEntry> conditions = new List<ConditionEntry>();
    [Tooltip("Les sous-conditions doivent être remplies dans l'ordre")]
    public bool sequence_mustBeOrdered = false;

    [Header("Récompenses")]
    public List<ConditionReward> rewards = new List<ConditionReward>();
    [TextArea] public string rewardDescription;

    [Header("Affichage")]
    public bool   isHidden    = false;
    public string displayName;
    [TextArea] public string description;
    public Sprite icon;

    public List<ConditionReward> GetEligibleRewards(WeaponType equippedWeapon)
    {
        var result = new List<ConditionReward>();
        if (rewards == null) return result;
        foreach (var reward in rewards)
        {
            if (reward == null || reward.rewardType == RewardType.None) continue;
            if (reward.weaponType == WeaponType.Any || reward.weaponType == equippedWeapon)
                result.Add(reward);
        }
        return result;
    }
}
