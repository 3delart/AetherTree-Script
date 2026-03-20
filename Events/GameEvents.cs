using System.Collections.Generic;
using UnityEngine;

// =============================================================
// GAMEEVENTS.CS — Toutes les structs d'événements du jeu
// Path : Assets/Scripts/Events/GameEvents.cs
// AetherTree GDD v30 — Architecture v1.0
//
// Remplace GameEvent.cs (classes avec héritage → structs indépendantes)
// Raison : structs = zéro allocation heap, pas de GC pressure
//          en combat AoE avec beaucoup d'events/seconde.
//
// Publié via GameEventBus.Publish(). Jamais instancié directement
// en dehors du publisher désigné (voir commentaire de chaque struct).
//
// Publishers :
//   MobKilledEvent     → Mob.Die()
//   DamageDealtEvent   → CombatSystem (après calcul final)
//   SkillUsedEvent     → SkillSystem.Execute()
//   PlayerDeathEvent   → Player.Die()
//   ZoneEvent          → Player.OnEnterZone()  [TODO]
//   ItemEvent          → Inventory / CraftSystem [TODO]
//   SocialEvent        → Player social methods
//   PetEvent           → PetSystem [TODO]
//   TimeEvent          → TimeSystem [TODO]
//   MetierEvent        → Metier systems [TODO]
//   ServerEvent        → ConnectionManager [TODO]
// =============================================================


// ── Kill ─────────────────────────────────────────────────────
// Publié par : Mob.Die()
// Abonnés    : UnlockManager, XPSystem, LootManager, Player (optionnel)
public struct MobKilledEvent
{
    public MobData          mob;
    public SkillData        killerSkill;    // Dernier skill utilisé par le killerPlayer (peut être null si DoT pur)
    public WeaponType       killerWeapon;   // Arme du killerPlayer au moment du kill
    public List<Player>     eligiblePlayers; // Joueurs ayant infligé ≥ 10% des dégâts
    public Vector3          deathPosition;
    public bool             wasStealth;
    public bool             wasUnarmed;
    public bool             wasBoss;
    public string           locationID;
    public bool             isInParty;
}


// ── Damage ───────────────────────────────────────────────────
// Publié par : CombatSystem, après le calcul final
// Abonnés    : UnlockManager (conditions one-hit, element kill...)
public struct DamageDealtEvent
{
    public float        amount;
    public ElementType  element;
    public Entity       source;
    public Entity       target;
    public bool         isCrit;
    public bool         isOneHit;   // true si amount >= target.MaxHP (kill en un coup)
}


// ── Skill utilisé ────────────────────────────────────────────
// Publié par : SkillSystem.Execute()
// Abonnés    : UnlockManager (conditions SkillCast, combo...)
public struct SkillUsedEvent
{
    public SkillData    skill;
    public Entity       target;
    public Entity       caster;
    public ElementType  primaryElement;
    public bool         isCombo;        // skill.elements.Count >= 2
    public string       locationID;
    public bool         isInParty;
}


// ── Mort du joueur ───────────────────────────────────────────
// Publié par : Player.Die()
// Abonnés    : UnlockManager (conditions mort), RespawnSystem
public struct PlayerDeathEvent
{
    public ElementType  cause;
    public Entity       killer;         // null si mort environnementale / DoT
    public float        hpAtDeath;      // toujours 0 normalement, garde pour debug
    public DeathContext context;        // OpenWorld / Dungeon / PvP
}

public enum DeathContext { OpenWorld, Dungeon, PvP }


// ── Zone ─────────────────────────────────────────────────────
// Publié par : Player.OnEnterZone() [TODO — pas encore implémenté]
// Abonnés    : UnlockManager (conditions exploration, donjon...)
public struct ZoneEvent
{
    public string   zoneID;
    public float    timeSpentSeconds;
    public bool     isAFK;
    public bool     isDungeon;
    public bool     dungeonSolo;
    public bool     dungeonNoHit;
    public float    dungeonTimeSeconds;
}


// ── Item ─────────────────────────────────────────────────────
// Publié par : InventorySystem / CraftSystem [TODO]
// Abonnés    : UnlockManager (conditions craft, collect...)
public enum ItemAction { Any = -1, Pickup, Craft, Use, Sell, Buy, Drop }

public struct ItemEvent
{
    public string       itemID;
    public ItemAction   action;
    public int          quantity;
    public int          aerisAmount;  // §35.1 : monnaie = Aeris (pas gold)
}


// ── Social ───────────────────────────────────────────────────
// Publié par : Player (OnPlayerMet, Duel, Trade...)
// Abonnés    : UnlockManager (conditions social, guilde...)
public enum SocialAction
{
    Any = -1, MeetPlayer, GroupUp, GroupLeader, Duel, DuelWin,
    GuildJoin, Revive, Trade, FirstServer
}

public struct SocialEvent
{
    public SocialAction action;
    public string       otherPlayerID;
    public bool         firstOnServer;
    public bool         isInParty;
}


// ── Pet ──────────────────────────────────────────────────────
// Publié par : PetSystem [TODO]
// Abonnés    : UnlockManager (conditions pet)
public enum PetAction { Any = -1, Capture, Release, LevelUp, Feed, Talk, Revive }

public struct PetEvent
{
    public PetAction    action;
    public MobData      mob;
    public string       npcID;
}


// ── Temps / Session ──────────────────────────────────────────
// Publié par : TimeSystem / ConnectionManager [TODO]
// Abonnés    : UnlockManager (conditions session, jours consécutifs...)
public enum TimeAction { Any = -1, Login, Logout, AFK, DayStart, NightStart, ConsecutiveDay }

public struct TimeEvent
{
    public TimeAction   action;
    public float        afkMinutes;
    public int          consecutiveDays;
    public int          nightsPlayed;
}


// ── Métier / Activité ────────────────────────────────────────
// Publié par : Systèmes de métiers [TODO]
// Abonnés    : UnlockManager (conditions métier)
public struct MetierEvent
{
    public string   metierID;
    public string   actionType;
    public int      newLevel;
}


// ── Serveur ──────────────────────────────────────────────────
// Publié par : ConnectionManager [TODO]
// Abonnés    : UnlockManager (première connexion serveur)
public struct ServerEvent
{
    public bool firstConnection;
}


// ── Stats changées ───────────────────────────────────────────
// Publié par : PlayerStats.RecalculateStats()
// Abonnés    : CharacterPanelUI (refresh affichage stats)
public struct StatsChangedEvent
{
    public Player player;
}

// ── Quête ────────────────────────────────────────────────────
// Publié par : QuestSystem
// Abonnés    : QuestTrackerUI, QuestJournalUI, UnlockManager
public enum QuestAction
{
    Accepted,
    ObjectiveUpdated,
    Completed,
    TurnedIn,
    Failed,
}

public struct QuestEvent
{
    public QuestData      quest;
    public QuestAction    action;
    public int            objectiveIndex; // -1 si non applicable
    public Player         player;
}
