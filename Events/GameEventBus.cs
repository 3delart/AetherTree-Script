using System;

// =============================================================
// GAMEEVENTBUS.CS — Bus d'événements central (découplage)
// Path : Assets/Scripts/Events/GameEventBus.cs
// AetherTree GDD v30 — Architecture v1.0
//
// Principe : personne ne se connaît directement.
//   Publisher  → GameEventBus.Publish(monEvent)
//   Subscriber → GameEventBus.OnXxx += MonHandler  (dans OnEnable)
//                GameEventBus.OnXxx -= MonHandler  (dans OnDisable)
//
// Reset() est appelé par SceneLoader.LoadMapRoutine() à chaque
// changement de map — nettoie tous les abonnements pour éviter
// les références mortes vers des objets détruits.
//
// ⚠ RÈGLE D'OR : ne jamais appeler Publish() depuis un handler
//   du même event (risque de boucle infinie).
//
// Abonnements attendus (voir doc Refactoring §5-7) :
//   UnlockManager  → OnMobKilled, OnSkillUsed, OnDamageDealt,
//                    OnZoneEntered, OnItemAction, OnSocialAction,
//                    OnPetAction, OnTimeAction, OnMetierAction, OnServerEvent
//   XPSystem       → OnMobKilled
//   LootManager    → OnMobKilled
//   Player         → OnMobKilled (optionnel — si besoin de réagir côté joueur)
// =============================================================

public static class GameEventBus
{
    // ── Events ───────────────────────────────────────────────

    /// <summary>Un mob est mort. Publisher : Mob.Die()</summary>
    public static event Action<MobKilledEvent>   OnMobKilled;

    /// <summary>Des dégâts ont été infligés. Publisher : CombatSystem</summary>
    public static event Action<DamageDealtEvent> OnDamageDealt;

    /// <summary>Un skill a été utilisé. Publisher : SkillSystem.Execute()</summary>
    public static event Action<SkillUsedEvent>   OnSkillUsed;

    /// <summary>Le joueur est mort. Publisher : Player.Die()</summary>
    public static event Action<PlayerDeathEvent> OnPlayerDeath;

    /// <summary>Le joueur entre/quitte une zone. Publisher : Player.OnEnterZone() [TODO]</summary>
    public static event Action<ZoneEvent>        OnZoneEntered;

    /// <summary>Action sur un item. Publisher : InventorySystem / CraftSystem [TODO]</summary>
    public static event Action<ItemEvent>        OnItemAction;

    /// <summary>Action sociale. Publisher : Player social methods</summary>
    public static event Action<SocialEvent>      OnSocialAction;

    /// <summary>Action pet. Publisher : PetSystem [TODO]</summary>
    public static event Action<PetEvent>         OnPetAction;

    /// <summary>Événement temporel/session. Publisher : TimeSystem [TODO]</summary>
    public static event Action<TimeEvent>        OnTimeAction;

    /// <summary>Action métier. Publisher : Metier systems [TODO]</summary>
    public static event Action<MetierEvent>      OnMetierAction;

    /// <summary>Événement serveur. Publisher : ConnectionManager [TODO]</summary>
    public static event Action<ServerEvent>      OnServerEvent;

    /// <summary>Stats du joueur recalculées. Publisher : PlayerStats.RecalculateStats()</summary>
    public static event Action<StatsChangedEvent> OnStatsChanged;

    /// <summary>Événement quête. Publisher : QuestSystem</summary>
    public static event Action<QuestEvent> OnQuestAction;


    // ── Publish ──────────────────────────────────────────────

    public static void Publish(MobKilledEvent e)   => OnMobKilled?.Invoke(e);
    public static void Publish(DamageDealtEvent e) => OnDamageDealt?.Invoke(e);
    public static void Publish(SkillUsedEvent e)   => OnSkillUsed?.Invoke(e);
    public static void Publish(PlayerDeathEvent e) => OnPlayerDeath?.Invoke(e);
    public static void Publish(ZoneEvent e)        => OnZoneEntered?.Invoke(e);
    public static void Publish(ItemEvent e)        => OnItemAction?.Invoke(e);
    public static void Publish(SocialEvent e)      => OnSocialAction?.Invoke(e);
    public static void Publish(PetEvent e)         => OnPetAction?.Invoke(e);
    public static void Publish(TimeEvent e)        => OnTimeAction?.Invoke(e);
    public static void Publish(MetierEvent e)      => OnMetierAction?.Invoke(e);
    public static void Publish(ServerEvent e)      => OnServerEvent?.Invoke(e);
    public static void Publish(StatsChangedEvent e) => OnStatsChanged?.Invoke(e);
    public static void Publish(QuestEvent e)        => OnQuestAction?.Invoke(e);


    // ── Reset ────────────────────────────────────────────────

    /// <summary>
    /// Nettoie tous les abonnements.
    /// Appelé par SceneLoader.LoadMapRoutine() à chaque changement de map.
    /// Empêche les références mortes vers des MonoBehaviours détruits.
    /// </summary>
    public static void Reset()
    {
        OnMobKilled   = null;
        OnDamageDealt = null;
        OnSkillUsed   = null;
        OnPlayerDeath = null;
        OnZoneEntered = null;
        OnItemAction  = null;
        OnSocialAction = null;
        OnPetAction   = null;
        OnTimeAction  = null;
        OnMetierAction = null;
        OnServerEvent = null;
        OnStatsChanged = null;
        OnQuestAction  = null;
        XPSystem.Instance?.Resubscribe();
        UnlockManager.Instance?.Resubscribe();
        CharacterPanelUI.Instance?.Resubscribe();
        LootManager.Instance?.Resubscribe();
        AerisSystem.Instance?.Resubscribe();
        QuestSystem.Instance?.Resubscribe();
    }
}