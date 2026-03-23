using UnityEngine;
using System.Collections.Generic;
using System.Linq;

// =============================================================
// UNLOCKMANAGER.CS — Système de déblocage événementiel
// Path : Assets/Scripts/Progression/UnlockManager.cs
// AetherTree GDD v30 — Section 16
//
// S'abonne à GameEventBus via Subscribe()/Unsubscribe() — méthodes
// idempotentes qui évitent les doubles abonnements et le désabonnement
// accidentel causé par GameEventBus.Reset() après un changement de scène.
//
// Si GameEventBus.Reset() est appelé (SceneLoader), appeler ensuite :
//   UnlockManager.Instance?.Resubscribe()
//
// ConditionData contient des ConditionEntry inline
// (plus de ConditionBase SO séparés — AllConditions.cs obsolète)
// =============================================================

public class UnlockRecord
{
    public string          conditionID;
    public System.DateTime unlockedAt;
    public int             playerLevel;
    public int             killsTotal;
    public float           timePlayed;

    public string GetDateString()  => unlockedAt.ToString("dd/MM/yyyy HH:mm");
    public string GetLevelString() => $"Niveau {playerLevel}";
    public string GetKillsString() => $"{killsTotal} kills";
    public string GetTimeString()
    {
        int h = Mathf.FloorToInt(timePlayed / 60f);
        int m = Mathf.FloorToInt(timePlayed % 60f);
        return h > 0 ? $"{h}h {m:00}" : $"{m} min";
    }
}

public class UnlockManager : MonoBehaviour
{
    public static UnlockManager Instance { get; private set; }

    [Header("Toutes les conditions du jeu")]
    [Tooltip("Clic droit sur ce composant → 'Auto-remplir allConditions' pour scanner le projet.")]
    public List<ConditionData> allConditions = new List<ConditionData>();

    [Header("Debug")]
    [Tooltip("Active les logs détaillés de matching (à désactiver en production)")]
    public bool verboseLogs = false;

    private Dictionary<string, Dictionary<int, int>> privateCounters
        = new Dictionary<string, Dictionary<int, int>>();
    private Dictionary<string, HashSet<int>> completedEntries
        = new Dictionary<string, HashSet<int>>();
    private Dictionary<string, UnlockRecord> records
        = new Dictionary<string, UnlockRecord>();

    private ElementalSystem elementalSystem;
    private Player          player;
    private ActivityCounter activityCounter;
    private bool            _initialized  = false;
    private bool            _subscribed   = false;

    // =========================================================
    // LIFECYCLE
    // =========================================================

    private void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    private void Start()
    {
        // Auto-init si Player.Start() n'a pas encore appelé Init()
        if (!_initialized)
        {
            var counter = FindObjectOfType<ActivityCounter>();
            if (counter != null)
            {
                Init(counter);
            }
            else
            {
                Debug.LogError("[UNLOCK] ActivityCounter introuvable — Init() impossible !");
            }
        }
    }

    private void OnEnable()  => Subscribe();
    private void OnDisable() => Unsubscribe();

    // =========================================================
    // ABONNEMENTS — idempotents, résistants au Reset()
    // =========================================================

    /// <summary>
    /// À appeler par SceneLoader APRÈS GameEventBus.Reset()
    /// pour restaurer les abonnements perdus par le Reset.
    /// </summary>
    public void Resubscribe()
    {
        Unsubscribe();
        Subscribe();
    }

    private void Subscribe()
    {
        if (_subscribed) return;
        _subscribed = true;

        GameEventBus.OnMobKilled    += HandleMobKilled;
        GameEventBus.OnSkillUsed    += HandleSkillUsed;
        GameEventBus.OnDamageDealt  += HandleDamageDealt;
        GameEventBus.OnZoneEntered  += HandleZoneEntered;
        GameEventBus.OnItemAction   += HandleItemAction;
        GameEventBus.OnSocialAction += HandleSocialAction;
        GameEventBus.OnPetAction    += HandlePetAction;
        GameEventBus.OnTimeAction   += HandleTimeAction;
        GameEventBus.OnMetierAction += HandleMetierAction;
        GameEventBus.OnServerEvent  += HandleServerEvent;

    
    }

    private void Unsubscribe()
    {
        if (!_subscribed) return;
        _subscribed = false;

        GameEventBus.OnMobKilled    -= HandleMobKilled;
        GameEventBus.OnSkillUsed    -= HandleSkillUsed;
        GameEventBus.OnDamageDealt  -= HandleDamageDealt;
        GameEventBus.OnZoneEntered  -= HandleZoneEntered;
        GameEventBus.OnItemAction   -= HandleItemAction;
        GameEventBus.OnSocialAction -= HandleSocialAction;
        GameEventBus.OnPetAction    -= HandlePetAction;
        GameEventBus.OnTimeAction   -= HandleTimeAction;
        GameEventBus.OnMetierAction -= HandleMetierAction;
        GameEventBus.OnServerEvent  -= HandleServerEvent;
    }

    // =========================================================
    // INIT
    // =========================================================

    public void Init(ActivityCounter counter)
    {
        // Évite la double-init (appelé par Player.Start() ET UnlockManager.Start())
        if (_initialized) return;
        _initialized = true;

        activityCounter = counter;
        player          = FindObjectOfType<Player>();
        if (player != null)
            elementalSystem = player.GetComponent<ElementalSystem>();
        else
            Debug.LogWarning("[UNLOCK] Player introuvable au Init() — les conditions nécessitant player.level ou arme seront ignorées.");

        if (allConditions.Count == 0)
            Debug.LogWarning("[UNLOCK] allConditions est vide ! Clic droit → 'Auto-remplir allConditions'.");

        foreach (var condition in allConditions)
        {
            if (condition == null) continue;
            if (string.IsNullOrEmpty(condition.conditionID))
            {
                Debug.LogWarning($"[UNLOCK] ConditionData '{condition.name}' n'a pas de conditionID !");
                continue;
            }
            if (!privateCounters.ContainsKey(condition.conditionID))
            {
                var entryCounters = new Dictionary<int, int>();
                for (int i = 0; i < condition.conditions.Count; i++)
                    entryCounters[i] = 0;
                privateCounters[condition.conditionID]  = entryCounters;
                completedEntries[condition.conditionID] = new HashSet<int>();
            }
        }

    }

    // =========================================================
    // HANDLERS GAMEEVENTBUS
    // =========================================================

    private void HandleMobKilled(MobKilledEvent e)
    {
        EvaluateAll(e);
    }

    private void HandleSkillUsed(SkillUsedEvent e)    => EvaluateAll(e);
    private void HandleDamageDealt(DamageDealtEvent e) => EvaluateAll(e);
    private void HandleZoneEntered(ZoneEvent e)        => EvaluateAll(e);
    private void HandleItemAction(ItemEvent e)          => EvaluateAll(e);
    private void HandleSocialAction(SocialEvent e)     => EvaluateAll(e);
    private void HandlePetAction(PetEvent e)           => EvaluateAll(e);
    private void HandleTimeAction(TimeEvent e)         => EvaluateAll(e);
    private void HandleMetierAction(MetierEvent e)     => EvaluateAll(e);
    private void HandleServerEvent(ServerEvent e)      => EvaluateAll(e);

    // =========================================================
    // DISPATCH INTERNE — surcharges typées (zéro alloc legacy)
    // =========================================================

    private void EvaluateAll(MobKilledEvent e)    => EvaluateAllTyped(c => EvaluateCondition(c, e));
    private void EvaluateAll(SkillUsedEvent e)    => EvaluateAllTyped(c => EvaluateCondition(c, e));
    private void EvaluateAll(DamageDealtEvent e)  => EvaluateAllTyped(c => EvaluateCondition(c, e));
    private void EvaluateAll(ZoneEvent e)         => EvaluateAllTyped(c => EvaluateCondition(c, e));
    private void EvaluateAll(ItemEvent e)         => EvaluateAllTyped(c => EvaluateCondition(c, e));
    private void EvaluateAll(SocialEvent e)       => EvaluateAllTyped(c => EvaluateCondition(c, e));
    private void EvaluateAll(PetEvent e)          => EvaluateAllTyped(c => EvaluateCondition(c, e));
    private void EvaluateAll(TimeEvent e)         => EvaluateAllTyped(c => EvaluateCondition(c, e));
    private void EvaluateAll(MetierEvent e)       => EvaluateAllTyped(c => EvaluateCondition(c, e));
    private void EvaluateAll(ServerEvent e)       => EvaluateAllTyped(c => EvaluateCondition(c, e));

    private void EvaluateAllTyped(System.Action<ConditionData> evaluate)
    {
        if (!_initialized)
        {
            Debug.LogWarning("[UNLOCK] EvaluateAll appelé avant Init() — auto-init.");
            var counter = FindObjectOfType<ActivityCounter>();
            if (counter != null) Init(counter);
            else return;
        }

        foreach (var condition in allConditions)
        {
            if (condition == null) continue;
            if (string.IsNullOrEmpty(condition.conditionID)) continue;
            if (records.ContainsKey(condition.conditionID)) continue;          // déjà débloquée
            if (!privateCounters.ContainsKey(condition.conditionID)) continue; // non initialisée
            evaluate(condition);
        }
    }

    // =========================================================
    // ÉVALUATION — noyau commun
    // =========================================================

    // Vérifie séquencement, mustBeLast, Affinity, puis délègue au matcher typé.
    // Retourne true si la sous-condition [i] vient d'être complétée.
    private bool EvaluateEntry(ConditionData condition, int i, System.Func<ConditionEntry, bool> matches)
    {
        var entry     = condition.conditions[i];
        var counters  = privateCounters[condition.conditionID];
        var completed = completedEntries[condition.conditionID];

        if (entry == null || completed.Contains(i)) return false;

        if (condition.sequence_mustBeOrdered && i > 0 && !completed.Contains(i - 1)) return false;

        if (entry.mustBeLast)
        {
            for (int j = 0; j < condition.conditions.Count; j++)
                if (j != i && !completed.Contains(j)) return false;
        }

        if (entry.type == ConditionType.Affinity)
        {
            if (EvaluateAffinity(entry)) { completed.Add(i); return true; }
            return false;
        }

        bool hit = false;
        try   { hit = matches(entry); }
        catch (System.Exception ex)
        {
            Debug.LogError($"[UNLOCK] EXCEPTION — {condition.conditionID} [{i}] : {ex.Message}\n{ex.StackTrace}");
        }

        if (!hit) return false;

        counters[i]++;

        if (counters[i] >= entry.countRequired) { completed.Add(i); return true; }
        return false;
    }

    private void FinalizeCondition(ConditionData condition)
    {
        // Guard — déjà débloquée (évite le double-unlock si deux events arrivent dans le même frame)
        if (records.ContainsKey(condition.conditionID)) return;

        var completed = completedEntries[condition.conditionID];



        if (completed.Count == condition.conditions.Count && condition.conditions.Count > 0)
            Unlock(condition);
    }

    // =========================================================
    // ÉVALUATION — surcharges typées (une par struct)
    // =========================================================

    private void EvaluateCondition(ConditionData condition, MobKilledEvent e)
    {
        for (int i = 0; i < condition.conditions.Count; i++)
        {
            var entry = condition.conditions[i];
            if (entry == null) continue;

            EvaluateEntry(condition, i, f =>
            {
                if (f.type != ConditionType.Kill) return false;

                // Filtre commun
                if (f.common_weapon != WeaponType.Any && e.killerWeapon != f.common_weapon)              return false;
                if (!string.IsNullOrEmpty(f.common_locationID) && e.locationID != f.common_locationID)  return false;
                if (f.common_playerLevelMin > 0 && player != null && player.level < f.common_playerLevelMin) return false;
                if (f.common_mustBeSolo    &&  e.isInParty)                                              return false;
                if (f.common_mustBeInGroup && !e.isInParty)                                              return false;

                // Matcher Kill
                if (f.kill_specificMob != null && e.mob != f.kill_specificMob)                          return false;
                if (f.kill_mobElement  != ElementType.Any && e.mob?.elementType != f.kill_mobElement)   return false;
                if (f.kill_withSkill   != null && e.killerSkill != f.kill_withSkill)                    return false;
                if (f.kill_withElement != ElementType.Any &&
                    e.killerSkill?.PrimaryElement != f.kill_withElement)                                 return false;
                if (f.kill_mustBeStealth && !e.wasStealth)                                              return false;
                if (f.kill_mustBeUnarmed && !e.wasUnarmed)                                              return false;
                if (f.kill_mustBeBoss   && !e.wasBoss)                                                  return false;
                if (f.kill_isPlayer     &&  e.mob != null)                                              return false;
                if (f.kill_atNight      && !IsNight())                                                  return false;
                if (f.kill_lowHP        && player != null && player.CurrentHP / player.MaxHP >= 0.2f)   return false;
                if (!string.IsNullOrEmpty(f.kill_inZone) && e.locationID != f.kill_inZone)             return false;
                return true;
            });
        }
        FinalizeCondition(condition);
    }

    private void EvaluateCondition(ConditionData condition, SkillUsedEvent e)
    {
        for (int i = 0; i < condition.conditions.Count; i++)
        {
            var entry = condition.conditions[i];
            if (entry == null) continue;

            EvaluateEntry(condition, i, f =>
            {
                if (f.type != ConditionType.SkillCast) return false;

                if (f.common_weapon != WeaponType.Any)
                {
                    var w = player?.equippedWeaponInstance?.data?.weaponType ?? WeaponType.Any;
                    if (w != f.common_weapon) return false;
                }
                if (!string.IsNullOrEmpty(f.common_locationID) && e.locationID != f.common_locationID) return false;
                if (f.common_playerLevelMin > 0 && player != null && player.level < f.common_playerLevelMin) return false;
                if (f.common_mustBeSolo    &&  e.isInParty) return false;
                if (f.common_mustBeInGroup && !e.isInParty) return false;

                var primaryElem = (e.skill != null && e.skill.elements != null && e.skill.elements.Count > 0)
                                  ? e.skill.elements[0] : ElementType.Neutral;
                bool isCombo    = e.skill != null && e.skill.elements != null && e.skill.elements.Count >= 2;

                if (f.skillcast_specificSkill != null && e.skill != f.skillcast_specificSkill)         return false;
                if (f.skillcast_element != ElementType.Any && primaryElem != f.skillcast_element)      return false;
                if (f.skillcast_mustBeCombo && !isCombo)                                               return false;
                if (!string.IsNullOrEmpty(f.skillcast_inZone) && e.locationID != f.skillcast_inZone)  return false;
                return true;
            });
        }
        FinalizeCondition(condition);
    }

    private void EvaluateCondition(ConditionData condition, DamageDealtEvent e)
    {
        for (int i = 0; i < condition.conditions.Count; i++)
        {
            var entry = condition.conditions[i];
            if (entry == null) continue;

            EvaluateEntry(condition, i, f =>
            {
                if (f.type != ConditionType.Damage) return false;

                if (f.common_weapon != WeaponType.Any)
                {
                    var w = player?.equippedWeaponInstance?.data?.weaponType ?? WeaponType.Any;
                    if (w != f.common_weapon) return false;
                }
                if (f.common_playerLevelMin > 0 && player != null && player.level < f.common_playerLevelMin) return false;

                if (e.amount < f.damage_minAmount)                                         return false;
                if (f.damage_element != ElementType.Any && e.element != f.damage_element)  return false;
                if (f.damage_isReceived != (e.target == player))                           return false;
                if (f.damage_inOneHit   && !e.isOneHit)                                   return false;
                return true;
            });
        }
        FinalizeCondition(condition);
    }

    private void EvaluateCondition(ConditionData condition, ZoneEvent e)
    {
        for (int i = 0; i < condition.conditions.Count; i++)
        {
            var entry = condition.conditions[i];
            if (entry == null) continue;

            EvaluateEntry(condition, i, f =>
            {
                if (f.type != ConditionType.Zone) return false;

                if (f.common_playerLevelMin > 0 && player != null && player.level < f.common_playerLevelMin) return false;

                if (!string.IsNullOrEmpty(f.zone_zoneID) && e.zoneID != f.zone_zoneID)    return false;
                if (e.timeSpentSeconds < f.zone_minDuration)                               return false;
                if (f.zone_mustBeAFK    && !e.isAFK)                                       return false;
                if (f.zone_atNight      && !IsNight())                                     return false;
                if (f.zone_isDungeon    && !e.isDungeon)                                   return false;
                if (f.zone_dungeonSolo  && !e.dungeonSolo)                                 return false;
                if (f.zone_dungeonNoHit && !e.dungeonNoHit)                                return false;
                if (f.zone_speedRunMax > 0 && e.dungeonTimeSeconds > f.zone_speedRunMax)  return false;
                return true;
            });
        }
        FinalizeCondition(condition);
    }

    private void EvaluateCondition(ConditionData condition, ItemEvent e)
    {
        for (int i = 0; i < condition.conditions.Count; i++)
        {
            var entry = condition.conditions[i];
            if (entry == null) continue;

            EvaluateEntry(condition, i, f =>
            {
                if (f.type != ConditionType.Item) return false;

                if (!string.IsNullOrEmpty(f.item_itemID) && e.itemID != f.item_itemID)  return false;
                if (f.item_action != ItemAction.Any && e.action != f.item_action)        return false;
                if (e.aerisAmount < f.item_minAeris)                                     return false;
                return true;
            });
        }
        FinalizeCondition(condition);
    }

    private void EvaluateCondition(ConditionData condition, SocialEvent e)
    {
        for (int i = 0; i < condition.conditions.Count; i++)
        {
            var entry = condition.conditions[i];
            if (entry == null) continue;

            EvaluateEntry(condition, i, f =>
            {
                if (f.type != ConditionType.Social) return false;

                if (f.social_action != SocialAction.Any && e.action != f.social_action) return false;
                if (f.social_mustBeInGroup && !e.isInParty)                              return false;
                return true;
            });
        }
        FinalizeCondition(condition);
    }

    private void EvaluateCondition(ConditionData condition, PetEvent e)
    {
        // PetEvent → ConditionType.Activity (pas de type Pet dédié dans ConditionType)
        for (int i = 0; i < condition.conditions.Count; i++)
        {
            var entry = condition.conditions[i];
            if (entry == null) continue;

            EvaluateEntry(condition, i, f =>
            {
                if (f.type != ConditionType.Activity) return false;

                string petActionStr = $"Pet{e.action}";
                if (!string.IsNullOrEmpty(f.activity_actionType) && petActionStr != f.activity_actionType) return false;
                return true;
            });
        }
        FinalizeCondition(condition);
    }

    private void EvaluateCondition(ConditionData condition, TimeEvent e)
    {
        for (int i = 0; i < condition.conditions.Count; i++)
        {
            var entry = condition.conditions[i];
            if (entry == null) continue;

            EvaluateEntry(condition, i, f =>
            {
                if (f.type != ConditionType.Time) return false;

                if (f.time_action != TimeAction.Any && e.action != f.time_action)  return false;
                if (e.afkMinutes   < f.time_minMinutes)                             return false;
                if (f.time_minDays > 0 && e.consecutiveDays < f.time_minDays)      return false;
                if (f.time_isNight && !IsNight())                                   return false;
                return true;
            });
        }
        FinalizeCondition(condition);
    }

    private void EvaluateCondition(ConditionData condition, MetierEvent e)
    {
        for (int i = 0; i < condition.conditions.Count; i++)
        {
            var entry = condition.conditions[i];
            if (entry == null) continue;

            EvaluateEntry(condition, i, f =>
            {
                if (f.type != ConditionType.Activity) return false;

                if (!string.IsNullOrEmpty(f.activity_actionType) && e.actionType != f.activity_actionType) return false;
                if (f.activity_levelMin > 0 && e.newLevel < f.activity_levelMin)                           return false;
                return true;
            });
        }
        FinalizeCondition(condition);
    }

    private void EvaluateCondition(ConditionData condition, ServerEvent e)
    {
        // Convention : type Social + action FirstServer dans la ConditionEntry.
        for (int i = 0; i < condition.conditions.Count; i++)
        {
            var entry = condition.conditions[i];
            if (entry == null) continue;

            EvaluateEntry(condition, i, f =>
            {
                if (f.type != ConditionType.Social)                    return false;
                if (f.social_action != SocialAction.FirstServer)       return false;
                if (!e.firstConnection)                                return false;
                return true;
            });
        }
        FinalizeCondition(condition);
    }

    // =========================================================
    // AFFINITY — condition d'état
    // =========================================================

    private bool EvaluateAffinity(ConditionEntry entry)
    {
        if (elementalSystem == null) return false;

        if (entry.affinity_multiElement != null && entry.affinity_multiElement.Count > 0)
        {
            foreach (var req in entry.affinity_multiElement)
                if (elementalSystem.GetAffinity(req.element) < req.minAffinity)
                    return false;
            return true;
        }

        if (entry.affinity_mustBeDominant &&
            elementalSystem.GetDominantElement() != entry.affinity_element)
            return false;

        return elementalSystem.GetAffinity(entry.affinity_element)    >= entry.affinity_minAffinity
            && elementalSystem.GetElementRank(entry.affinity_element) >= entry.affinity_rankMin;
    }

    // =========================================================
    // DÉBLOCAGE
    // =========================================================

    private void Unlock(ConditionData condition)
    {
        // Guard supplémentaire — ne devrait jamais arriver grâce au check dans FinalizeCondition,
        // mais protège contre tout appel direct externe.
        if (records.ContainsKey(condition.conditionID)) return;

        var record = new UnlockRecord
        {
            conditionID = condition.conditionID,
            unlockedAt  = System.DateTime.Now,
            playerLevel = activityCounter?.Get(CounterKeys.PLAYER_LEVEL)        ?? 0,
            killsTotal  = activityCounter?.Get(CounterKeys.KILLS_TOTAL)         ?? 0,
            timePlayed  = activityCounter?.Get(CounterKeys.TIME_PLAYED_MINUTES) ?? 0,
        };
        records[condition.conditionID] = record;

        Debug.Log(condition.isHidden
            ? "[UNLOCK SECRET] ** Condition mystère débloquée !"
            : $"[UNLOCK] OK {condition.displayName} — {condition.rewardDescription}");

        if (MailboxSystem.Instance == null)
        {
            Debug.LogError("[UNLOCK] MailboxSystem introuvable — récompenses non envoyées. " +
                           "Vérifie que MailboxSystem est dans la scène !");
            return;
        }

        // Lit l'arme équipée au moment du déblocage (même frame que le kill)
        WeaponType equippedWeapon = player?.equippedWeaponInstance?.data?.weaponType ?? WeaponType.Any;
        var eligibleRewards = condition.GetEligibleRewards(equippedWeapon);

        if (eligibleRewards.Count == 0)
            Debug.LogWarning($"[UNLOCK] {condition.conditionID} — aucune récompense éligible " +
                             $"pour l'arme {equippedWeapon}. Vérifie weaponType dans les rewards.");

        foreach (var reward in eligibleRewards)
            MailboxSystem.Instance.SendRewardMail(condition, reward);
    }


    /// <summary>Retourne la progression en cours de toutes les conditions non débloquées.</summary>
    public List<SavedConditionProgress> GetConditionProgresses()
    {
        var result = new List<SavedConditionProgress>();

        foreach (var condition in allConditions)
        {
            if (condition == null || string.IsNullOrEmpty(condition.conditionID)) continue;
            // Inutile de sauvegarder les conditions déjà débloquées
            if (records.ContainsKey(condition.conditionID)) continue;
            if (!privateCounters.ContainsKey(condition.conditionID)) continue;

            var counters  = privateCounters[condition.conditionID];
            var completed = completedEntries[condition.conditionID];

            // Ne sauvegarde que si au moins un compteur > 0
            bool hasProgress = false;
            foreach (var kvp in counters)
                if (kvp.Value > 0) { hasProgress = true; break; }
            if (!hasProgress) continue;

            var saved = new SavedConditionProgress { conditionID = condition.conditionID };

            for (int i = 0; i < condition.conditions.Count; i++)
            {
                saved.entryCounters .Add(counters.ContainsKey(i)  ? counters[i]         : 0);
                saved.entryCompleted.Add(completed.Contains(i));
            }

            result.Add(saved);
        }

        return result;
    }

    /// <summary>Restaure la progression des conditions depuis la sauvegarde.</summary>
    public void LoadConditionProgresses(List<SavedConditionProgress> progresses)
    {
        if (progresses == null) return;

        foreach (var saved in progresses)
        {
            if (string.IsNullOrEmpty(saved.conditionID)) continue;
            // Ne restaure pas les conditions déjà débloquées
            if (records.ContainsKey(saved.conditionID)) continue;
            if (!privateCounters.ContainsKey(saved.conditionID)) continue;

            var counters  = privateCounters[saved.conditionID];
            var completed = completedEntries[saved.conditionID];

            for (int i = 0; i < saved.entryCounters.Count; i++)
            {
                if (counters.ContainsKey(i))
                    counters[i] = saved.entryCounters[i];
                if (i < saved.entryCompleted.Count && saved.entryCompleted[i])
                    completed.Add(i);
            }
        }
    }

    // =========================================================
    // UTILITAIRES PUBLICS
    // =========================================================

    public bool               IsUnlocked(string id) => records.ContainsKey(id);
    public UnlockRecord       GetRecord(string id)  => records.TryGetValue(id, out var r) ? r : null;
    public List<UnlockRecord> GetAllRecords()       => records.Values.OrderBy(r => r.unlockedAt).ToList();
    public List<string>       GetUnlocked()         => records.Keys.ToList();

    /// <summary>
    /// Retourne le UnlockRecord associé à un SkillData.
    /// Cherche la première condition dont une reward contient ce skill.
    /// Utilisé par SkillLibraryUI pour afficher le journal d'obtention.
    /// </summary>
    public UnlockRecord GetRecordForSkill(SkillData skill)
    {
        if (skill == null) return null;
        foreach (var condition in allConditions)
        {
            if (condition == null) continue;
            var rewards = condition.GetEligibleRewards(WeaponType.Any);
            if (rewards == null) continue;
            foreach (var reward in rewards)
                if (reward?.rewardSkill == skill)
                    return GetRecord(condition.conditionID);
        }
        return null;
    }

    public void LoadUnlocked(List<string> saved)
    {
        foreach (var id in saved)
            if (!records.ContainsKey(id))
                records[id] = new UnlockRecord { conditionID = id };
    }

    private bool IsNight() => DayNightCycle.Instance?.IsNight ?? false;

    // =========================================================
    // EDITOR — AUTO-REMPLISSAGE
    // =========================================================

#if UNITY_EDITOR
    [ContextMenu("Auto-remplir allConditions")]
    private void EditorAutoFill()
    {
        allConditions.Clear();
        string[] guids = UnityEditor.AssetDatabase.FindAssets("t:ConditionData");
        foreach (string guid in guids)
        {
            string path = UnityEditor.AssetDatabase.GUIDToAssetPath(guid);
            var c = UnityEditor.AssetDatabase.LoadAssetAtPath<ConditionData>(path);
            if (c != null) allConditions.Add(c);
        }
        UnityEditor.EditorUtility.SetDirty(this);
        Debug.Log($"[UNLOCK] Auto-fill : {allConditions.Count} conditions trouvées.");
    }
#endif
}