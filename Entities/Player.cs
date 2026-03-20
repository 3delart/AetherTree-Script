using UnityEngine;
using System.Collections;
using System.Collections.Generic;

// =============================================================
// PLAYER — Entité joueur
// Path : Assets/Scripts/Core/Player.cs
// AetherTree GDD v30 — §4 (Entity), §5 (Joueur), §5.6 (Mort), §10 (Réputation)
//
// CharacterData SO = seule source de vérité pour les stats de base.
// PlayerStats    = seule source de vérité pour les stats de combat.
//
// Flow équipement :
//   EquipWeapon(instance) → stats.RecalculateStats(this)
//   CombatSystem lit player.stats (+ applique buffs à la volée)
//
// Flow level up :
//   OnLevelUp(n) → met à jour player.level → RecalculateStats()
//   RecalculateStats() recalcule baseMaxHP = baseMaxHP + hpPerLevel × (level-1)
//   ⚠ OnLevelUp() ne touche JAMAIS directement à maxHP/maxMana/regenHP/regenMana
//   Tout passe par RecalculateStats() pour éviter le double-comptage.
//
// Flow skills de départ — GDD §8.3 + §16.2 :
//   Awake() → EquipWeapon(startingWeapon)
//   Start() → UnlockStartingBasicAttack() → WeaponTypeRegistry → unlockedSkills[0]
//           → UnlockSkill(characterData.startingSkills)
//   SetStartingSkillBar() → slot 0 = BasicAttack | slots 1-8 = Actifs | slot 9 = Ultime
//
//   La BasicAttack est un skill comme les autres — elle ne change PAS
//   quand on change d'arme. Elle se débloque une seule fois au niveau 1
//   (physique) puis via conditions in-game (élémentaires).
//   Le joueur choisit librement laquelle équiper au slot 0 (drag & drop).
//
// Communication : tout passe par GameEventBus.Publish() — plus aucun
// appel direct à UnlockManager, XPSystem ou LootManager.
// =============================================================

[RequireComponent(typeof(ActivityCounter))]
[RequireComponent(typeof(ElementalSystem))]
[RequireComponent(typeof(StatusEffectSystem))]
[RequireComponent(typeof(SkillSystem))]
[RequireComponent(typeof(TargetingSystem))]
[RequireComponent(typeof(PlayerController))]
[RequireComponent(typeof(SkillBar))]
[RequireComponent(typeof(CombatSystem))]
[RequireComponent(typeof(LootApproach))]
[RequireComponent(typeof(UnityEngine.AI.NavMeshAgent))]
[RequireComponent(typeof(UnityEngine.CapsuleCollider))]
public class Player : Entity
{
    // ── Template ──────────────────────────────────────────────
    [Header("Template personnage (assigner ici)")]
    public CharacterData characterData;

    // ── Stats de combat agrégées ──────────────────────────────
    public PlayerStats stats { get; private set; } = new PlayerStats();

    // =========================================================
    // ÉQUIPEMENT — slots GDD v30 §5.3
    // =========================================================

    [HideInInspector] public WeaponInstance        equippedWeaponInstance;
    [HideInInspector] public ArmorInstance         equippedArmorInstance;
    [HideInInspector] public HelmetInstance        equippedHelmetInstance;
    [HideInInspector] public GlovesInstance        equippedGlovesInstance;
    [HideInInspector] public BootsInstance         equippedBootsInstance;
    [HideInInspector] public List<JewelryInstance> equippedJewelryInstances = new List<JewelryInstance>();
    [HideInInspector] public List<SpiritInstance>  equippedSpiritInstances  = new List<SpiritInstance>();

    public WeaponData equippedWeapon => equippedWeaponInstance?.data;

    // ── Catégorie & ArmorType (immuables après création) ──────
    [HideInInspector] public WeaponCategory weaponCategory = WeaponCategory.Melee;
    [HideInInspector] public ArmorType      armorType      = ArmorType.Melee;

    // ── Mouvement ─────────────────────────────────────────────
    [HideInInspector] public float moveSpeed = 5f;

    // ── Skills ────────────────────────────────────────────────
    [HideInInspector] public List<SkillData> startingSkills = new List<SkillData>();
    [HideInInspector] public List<SkillData> unlockedSkills = new List<SkillData>();

    // ── Progression ───────────────────────────────────────────
    [HideInInspector] public int level         = 1;
    [HideInInspector] public int xpCombat      = 0;
    [HideInInspector] public int xpToNextLevel = 100;

    // ── Titre actif ───────────────────────────────────────────
    [HideInInspector] public string activeTitle = "";

    // ── Réputation — GDD v21 section 20 ──────────────────────
    [HideInInspector] public int worldReputation     = 0;
    [HideInInspector] public int pvpReputation       = 0;
    [HideInInspector] public int worldReputationRank = 0;
    [HideInInspector] public int pvpReputationRank   = 0;

    private static readonly int[] WorldRepThresholds = { 0, 100, 300, 700, 1500, 3000 };
    private static readonly int[] PvPRepThresholds   = { 0, 50, 150, 300, 550, 900, 1400, 2100, 3000, 4200, 6000 };

    // ── Contexte combat ───────────────────────────────────────
    [HideInInspector] public SkillData lastSkillUsed = null;
    [HideInInspector] public bool      isInStealth   = false;
    [HideInInspector] public string    currentZoneID = "";

    // ── Guilde ────────────────────────────────────────────────
    [HideInInspector] public HashSet<string> uniqueGroupMembersLed = new HashSet<string>();

    // ── Composants ────────────────────────────────────────────
    private ActivityCounter activityCounter;
    private ElementalSystem elementalSystem;

    // =========================================================
    // INITIALISATION
    // =========================================================

    protected override void Awake()
    {
        // Garantit l'initialisation même après désérialisation Unity
        if (equippedJewelryInstances == null)
            equippedJewelryInstances = new List<JewelryInstance>();

        activityCounter = GetComponent<ActivityCounter>();
        elementalSystem = GetComponent<ElementalSystem>();

        if (characterData != null)
        {
            entityName = characterData.characterName;
            moveSpeed  = characterData.baseMoveSpeed;

            weaponCategory = characterData.WeaponCategory;
            armorType      = characterData.ArmorType;

            if (characterData.startingWeapon != null)
                EquipWeapon(characterData.startingWeapon.CreateDropInstance(0, 0));
            else
                stats.RecalculateStats(this); // initialise HP/Mana depuis CharacterData
        }
        else
        {
            Debug.LogWarning("[PLAYER] Aucun CharacterData assigné !");
            stats.RecalculateStats(this);
        }

        base.Awake();
    }

    private void Start()
    {
        // ── 1. BasicAttack de départ — GDD §16.2 niveau 1 ────
        // Débloquée en premier pour apparaître en tête de la SkillLibrary.
        // Source : WeaponTypeRegistry → famille de l'arme choisie à la création.
        // La BasicAttack ne change PAS quand on change d'arme — elle est possédée
        // comme n'importe quel skill.
        UnlockStartingBasicAttack();

        // ── 2. Skills de départ (CharacterData ou fallback Player) ──
        var skills = (characterData != null && characterData.startingSkills.Count > 0)
            ? characterData.startingSkills : startingSkills;

        foreach (var skill in skills)
            UnlockSkill(skill);

        // ── 3. Init UnlockManager ─────────────────────────────
        if (UnlockManager.Instance != null)
            UnlockManager.Instance.Init(activityCounter);

        StartCoroutine(SetStartingSkillBar());
        RefreshTitle();
    }

    /// <summary>
    /// Débloque la BasicAttack physique de la famille d'arme de départ.
    /// GDD §16.2 — déblocage automatique niveau 1.
    /// Source : WeaponTypeRegistry → famille de l'arme équipée au départ.
    /// </summary>
    private void UnlockStartingBasicAttack()
    {
        var registry = WeaponTypeRegistry.Instance;
        if (registry == null)
        {
            Debug.LogWarning("[PLAYER] WeaponTypeRegistry introuvable — BasicAttack de départ non débloquée. Vérifie GameDataRegistry sur _Managers.");
            return;
        }

        // Le joueur a toujours une arme équipée — choisie à la création (GDD §2.1)
        if (equippedWeapon == null)
        {
            Debug.LogWarning("[PLAYER] Aucune arme équipée au départ — BasicAttack non débloquée. Vérifie characterData.startingWeapon.");
            return;
        }

        WeaponType family     = equippedWeapon.weaponType.GetStartingFamily();
        SkillData  basicAttack = registry.GetBasicAttackSkill(family);

        if (basicAttack == null)
        {
            Debug.LogWarning($"[PLAYER] Aucune BasicAttack trouvée pour la famille {family} dans WeaponTypeRegistry. Vérifie le SO WeaponTypeRegistry.");
            return;
        }

        UnlockSkill(basicAttack);
       
    }

    private IEnumerator SetStartingSkillBar()
    {
        yield return null;
        if (SkillBar.Instance == null) yield break;

        // ── Slot 0 — BasicAttack de la famille de départ ─────
        // GDD §8.1 : slot 0 réservé aux BasicAttack.
        // On place la première BasicAttack débloquée (physique de départ).
        var startingBasicAttack = unlockedSkills.Find(s =>
            s != null && (s.skillType == SkillType.BasicAttack || s.HasTag(SkillTag.BasicAttack)));

        if (startingBasicAttack != null)
            SkillBar.Instance.SetSkillAtSlot(0, startingBasicAttack);

        // ── Slots 1-8 / Slot 9 — autres skills de départ ─────
        var skills = (characterData != null && characterData.startingSkills.Count > 0)
            ? characterData.startingSkills : startingSkills;

        int slot = 1; // démarre à 1 — slot 0 réservé BasicAttack
        foreach (var skill in skills)
        {
            if (skill == null) continue;
            // Saute les BasicAttack — déjà placées au slot 0
            if (skill.skillType == SkillType.BasicAttack || skill.HasTag(SkillTag.BasicAttack)) continue;

            if (skill.skillType == SkillType.Ultimate)
                SkillBar.Instance.SetSkillAtSlot(9, skill);
            else if (slot <= 8)
                SkillBar.Instance.SetSkillAtSlot(slot++, skill);
        }
    }

    // =========================================================
    // DÉFENSES — Entity abstracts
    // =========================================================

    public override float GetMeleeDefense()
    {
        float base_ = stats.meleeDefense;
        if (statusEffects != null) base_ += statusEffects.GetBuffDefenseBonus();
        return base_;
    }
    public override float GetRangedDefense()
    {
        float base_ = stats.rangedDefense;
        if (statusEffects != null) base_ += statusEffects.GetBuffDefenseBonus();
        return base_;
    }
    public override float GetMagicDefense()
    {
        float base_ = stats.magicDefense;
        if (statusEffects != null) base_ += statusEffects.GetBuffDefenseBonus();
        return base_;
    }

    // =========================================================
    // ÉQUIPEMENT
    // =========================================================

    public void EquipWeapon(WeaponInstance instance)
    {
        if (instance == null) return;
        equippedWeaponInstance = instance;
        stats.RecalculateStats(this);
    }

    public void UnequipWeapon()
    {
        equippedWeaponInstance = null;
        stats.RecalculateStats(this);
        Debug.Log("[PLAYER] Arme déséquipée");
        // TODO : Publish UnarmedEvent via GameEventBus quand UnarmedEvent migré en struct
    }

    public void EquipArmor(ArmorInstance instance)
    {
        if (instance == null) return;

        if (instance.data.armorType != armorType)
        {
            Debug.LogWarning($"[PLAYER] Armure incompatible — " +
                             $"{instance.data.armorName} ({instance.data.armorType}) " +
                             $"ne peut pas être équipée par un joueur {armorType}.");
            return;
        }

        equippedArmorInstance = instance;
        stats.RecalculateStats(this);
    }

    public void UnequipArmor()  { equippedArmorInstance = null; stats.RecalculateStats(this); }
    public void EquipHelmet(HelmetInstance instance)  { if (instance == null) return; equippedHelmetInstance = instance; stats.RecalculateStats(this); }
    public void UnequipHelmet() { equippedHelmetInstance = null; stats.RecalculateStats(this); }
    public void EquipGloves(GlovesInstance instance)  { if (instance == null) return; equippedGlovesInstance = instance; stats.RecalculateStats(this); }
    public void UnequipGloves() { equippedGlovesInstance = null; stats.RecalculateStats(this); }
    public void EquipBoots(BootsInstance instance)    { if (instance == null) return; equippedBootsInstance = instance; stats.RecalculateStats(this); }
    public void UnequipBoots()  { equippedBootsInstance = null; stats.RecalculateStats(this); }

    public void EquipJewelry(JewelryInstance instance)
    {
        if (instance == null || equippedJewelryInstances.Contains(instance)) return;

        // GDD v30 §5.1 — 3 bijoux max, un par slot (Ring / Necklace / Bracelet)
        foreach (var j in equippedJewelryInstances)
        {
            if (j.Slot == instance.Slot)
            {
                Debug.LogWarning($"[PLAYER] Slot bijou {instance.Slot} déjà occupé par {j.JewelryName}.");
                return;
            }
        }

        equippedJewelryInstances.Add(instance);
        stats.RecalculateStats(this);
    }

    public void UnequipJewelry(JewelryInstance instance)
    {
        if (equippedJewelryInstances.Remove(instance))
            stats.RecalculateStats(this);
    }

    public void EquipSpirit(SpiritInstance instance)
    {
        if (instance == null || equippedSpiritInstances.Contains(instance)) return;

        // GDD v30 §5.3 — 1 esprit actif uniquement
        if (equippedSpiritInstances.Count >= 1)
        {
            Debug.LogWarning($"[PLAYER] Esprit déjà actif ({equippedSpiritInstances[0].data?.name}). Déséquipez-le d'abord.");
            return;
        }

        equippedSpiritInstances.Add(instance);
        stats.RecalculateStats(this);
    }

    public void UnequipSpirit(SpiritInstance instance)
    {
        if (equippedSpiritInstances.Remove(instance))
            stats.RecalculateStats(this);
    }

    // =========================================================
    // SKILLS
    // =========================================================

    /// <summary>
    /// Débloque un skill et l'ajoute à unlockedSkills.
    /// Appelé par UnlockStartingBasicAttack(), Start(), et UnlockManager.
    /// </summary>
    public void UnlockSkill(SkillData skill)
    {
        if (skill == null || unlockedSkills.Contains(skill)) return;
        unlockedSkills.Add(skill);
        SkillLibraryUI.Instance?.RefreshIfOpen();
    }

    // =========================================================
    // DÉGÂTS & MORT
    // =========================================================

    public override void TakeDamage(float amount, ElementType sourceElement = ElementType.Neutral, Entity source = null)
    {
        float hpBefore  = currentHP;
        string srcName  = source != null ? source.entityName : "inconnu";

        
        base.TakeDamage(amount, sourceElement, source);

        
        activityCounter.Increment("DAMAGE_TAKEN_TOTAL", (int)amount);

        // Publish découplé — UnlockManager s'abonne pour les conditions dégâts reçus
        GameEventBus.Publish(new DamageDealtEvent
        {
            amount   = amount,
            element  = sourceElement,
            source   = source,
            target   = this,
            isCrit   = false,
            isOneHit = false,
        });

        if (hpBefore > 1f && currentHP <= 1f)
            activityCounter.Increment(CounterKeys.SURVIVE_1HP);

        // OnHitEffects (slot ④) — déclenchés quand on reçoit un coup
        if (source != null)
            ApplyOnHitEffects(amount, source);
    }

    private void ApplyOnHitEffects(float damageTaken, Entity attacker)
    {
        var allOnHit = new List<List<OnHitEffectEntry>>();

        if (equippedWeaponInstance?.data != null) allOnHit.Add(equippedWeaponInstance.OnHitEffects);
        if (equippedArmorInstance?.data  != null) allOnHit.Add(equippedArmorInstance.OnHitEffects);
        if (equippedHelmetInstance?.data != null) allOnHit.Add(equippedHelmetInstance.OnHitEffects);
        if (equippedGlovesInstance?.data != null) allOnHit.Add(equippedGlovesInstance.OnHitEffects);
        if (equippedBootsInstance?.data  != null) allOnHit.Add(equippedBootsInstance.OnHitEffects);
        if (equippedJewelryInstances != null)
            foreach (var j in equippedJewelryInstances)
                if (j?.data != null) allOnHit.Add(j.OnHitEffects);

        foreach (var list in allOnHit)
        {
            if (list == null) continue;
            foreach (var entry in list)
            {
                if (entry?.effect == null || !entry.Roll()) continue;

                switch (entry.effect.effectType)
                {
                    case OnHitEffectType.Thorns:
                        // GDD v30 §6.2 slot ④ — pierceDefense non implémenté côté CombatSystem.
                        attacker.TakeDamage(entry.effect.thornsDamage, entry.effect.reflectElement, this);
                        Debug.Log($"[ONHIT] Thorns — {entry.effect.thornsDamage:F0} dmg sur {attacker.entityName}");
                        break;

                    case OnHitEffectType.ReflectPercent:
                        float reflected = damageTaken * entry.effect.reflectPercent;
                        attacker.TakeDamage(reflected, entry.effect.reflectElement, this);
                        Debug.Log($"[ONHIT] Reflect {entry.effect.reflectPercent:P0} — {reflected:F0} dmg sur {attacker.entityName}");
                        break;

                    case OnHitEffectType.HealOnHit:
                        float healAmt = entry.effect.GetHealAmount(MaxHP);
                        Heal(healAmt);
                        Debug.Log($"[ONHIT] HealOnHit — +{healAmt:F0} HP");
                        break;

                    case OnHitEffectType.CounterDebuff:
                        if (entry.effect.counterDebuff != null && attacker.statusEffects != null)
                        {
                            bool applied = attacker.statusEffects.TryApplyDebuff(entry.effect.counterDebuff, this);
                            if (applied) Debug.Log($"[ONHIT] CounterDebuff — {entry.effect.counterDebuff.effectName} sur {attacker.entityName}");
                        }
                        break;

                    case OnHitEffectType.CounterBuff:
                        if (entry.effect.counterBuff != null && statusEffects != null)
                        {
                            statusEffects.ApplyBuff(entry.effect.counterBuff, this);
                            Debug.Log($"[ONHIT] CounterBuff — {entry.effect.counterBuff.effectName} sur {entityName}");
                        }
                        break;
                }
            }
        }
    }

    protected override void Die()
    {
        base.Die();

        // Publish découplé — RespawnSystem et UnlockManager s'abonnent
        GameEventBus.Publish(new PlayerDeathEvent
        {
            cause     = ElementType.Neutral,   // TODO : passer l'élément du dernier coup
            killer    = null,                   // TODO : tracker le dernier attaquant
            hpAtDeath = currentHP,
            context   = DeathContext.OpenWorld, // TODO : détecter contexte PvP / Donjon
        });

        // ⚠ Pas de pénalité réputation ici.
        // RespawnSystem appelle OnOpenWorldDeath() ou OnPvPDeath() selon le contexte.
        RespawnSystem.Instance?.TriggerDeath();
    }

    // =========================================================
    // KILLS
    // =========================================================

    // RegisterKill() SUPPRIMÉ — Mob.Die() publie MobKilledEvent via GameEventBus.
    // UnlockManager, XPSystem, LootManager s'abonnent indépendamment.
    //
    // Les compteurs activityCounter (KILLS_TOTAL etc.) sont désormais
    // mis à jour par un abonnement dans Player.OnEnable() → HandleMobKilled().

    private void OnEnable()
    {
        GameEventBus.OnMobKilled += HandleMobKilled;
    }

    private void OnDisable()
    {
        GameEventBus.OnMobKilled -= HandleMobKilled;
    }

    private void HandleMobKilled(MobKilledEvent e)
    {
        // Ne réagit que si ce joueur est éligible
        if (e.eligiblePlayers == null || !e.eligiblePlayers.Contains(this)) return;
        if (e.mob == null) return;

        // Mise à jour des compteurs locaux
        activityCounter.Increment(CounterKeys.KILLS_TOTAL);
        activityCounter.Increment($"KILLS_{e.mob.elementType.ToString().ToUpper()}_MOB");
        activityCounter.Increment($"KILLS_MOB_{e.mob.mobName.ToUpper().Replace(" ", "_")}");

        lastSkillUsed = null;
    }

    // =========================================================
    // SKILLS — usage
    // =========================================================

    // Appelé depuis SkillSystem.Execute() AVANT le calcul des dégâts.
    // NE publie plus EmitSkillCast — SkillSystem.Execute() publie SkillUsedEvent.
    public void UseSkill(SkillData skill, Entity target = null)
    {
        if (skill == null) return;
        lastSkillUsed = skill;

        if (!skill.IsNeutral)
        {
            // GDD §7.3 : BasicAttack = 0.25 cast-équivalent | Sort actif = 1.0
            bool isBasic = skill.skillType == SkillType.BasicAttack
                        || skill.HasTag(SkillTag.BasicAttack);

            foreach (var element in skill.elements)
                elementalSystem.RegisterCast(element, isBasicAttack: isBasic);
        }
        else
        {
            // Skill Neutre : enregistre Neutre pour maintenir la fenêtre
            bool isBasic = skill.skillType == SkillType.BasicAttack
                        || skill.HasTag(SkillTag.BasicAttack);
            elementalSystem.RegisterCast(ElementType.Neutral, isBasicAttack: isBasic);
        }

        RefreshTitle();
    }

    // RegisterDamageDealt() SUPPRIMÉ — SkillSystem publie DamageDealtEvent via GameEventBus.

    // =========================================================
    // TITRE ACTIF — GDD v21 section 2.3
    // =========================================================

    public void RefreshTitle()
    {
        if (elementalSystem == null) return;

        bool        hasDual  = SkillBar.Instance?.HasDualElementSkillEquipped() ?? false;
        TitleMode   mode     = elementalSystem.GetTitleMode(hasDual);
        ElementType dominant = elementalSystem.GetDominantElement();

        activeTitle = mode switch
        {
            TitleMode.Neutral      => "Aventurier",
            TitleMode.Equilibriste => "Équilibriste",
            TitleMode.Mono         => dominant.GetLabel(),
            TitleMode.Dual         => "Dual",
            _                      => "Aventurier"
        };
        // TODO section 2.3 : table complète arme + élément → titre exact
    }

    // =========================================================
    // RÉPUTATION MONDE — GDD v21 section 20.2
    // =========================================================

    public void AddWorldReputation(int amount)
    {
        worldReputation = Mathf.Max(0, worldReputation + amount);
        RefreshWorldReputationRank();
    }

    private void RefreshWorldReputationRank()
    {
        worldReputationRank = 0;
        for (int i = WorldRepThresholds.Length - 1; i >= 0; i--)
            if (worldReputation >= WorldRepThresholds[i]) { worldReputationRank = i; break; }
    }

    public float GetHdVListingFeeRate()
    {
        float[] fees = { 0.02f, 0.015f, 0.01f, 0.0075f, 0.005f, 0.0025f };
        return fees[Mathf.Clamp(worldReputationRank, 0, fees.Length - 1)];
    }

    public int GetHdVSlotCount()
    {
        int[] slots = { 3, 5, 8, 12, 16, 20 };
        return slots[Mathf.Clamp(worldReputationRank, 0, slots.Length - 1)];
    }

    // =========================================================
    // RÉPUTATION PVP — GDD v21 section 20.3
    // =========================================================

    public void AddPvPReputation(int amount)
    {
        pvpReputation = Mathf.Max(0, pvpReputation + amount);
        RefreshPvPReputationRank();
    }

    private void RefreshPvPReputationRank()
    {
        pvpReputationRank = 0;
        for (int i = PvPRepThresholds.Length - 1; i >= 0; i--)
            if (pvpReputation >= PvPRepThresholds[i]) { pvpReputationRank = i; break; }
    }

    // GDD v30 §5.6 — Mort monde ouvert (PvE) : −1 worldReputation (pas pvpReputation)
    public void OnOpenWorldDeath() { activityCounter.Increment("DEATHS_OPEN_WORLD"); AddWorldReputation(-1); }
    // GDD v30 §5.6 — Mort en zone PvP monde ouvert : −2 pvpReputation
    public void OnPvPDeath()       { activityCounter.Increment("PVP_DEATHS");        AddPvPReputation(-2); }

    public void OnPvPKill(string id)  { activityCounter.Increment("PVP_KILLS");       AddPvPReputation(2);  }
    public void OnDuelWon(string id)  { activityCounter.Increment("DUELS_WON");       GameEventBus.Publish(new SocialEvent { action = SocialAction.DuelWin, otherPlayerID = id }); AddPvPReputation(3); }
    public void OnDuelLost(string id) { activityCounter.Increment("DUELS_LOST");      AddPvPReputation(-1); }
    public void OnFreeArenaTop3()     { activityCounter.Increment("FREE_ARENA_TOP3"); AddPvPReputation(4); }
    public void OnPvPReportValidated()   { AddPvPReputation(-10); }
    public void OnWorldReportValidated() { AddWorldReputation(-5); }

    public void OnArenaResult(bool won)
    {
        if (won) { activityCounter.Increment("ARENA_WINS");         AddPvPReputation(5);  }
        else     { activityCounter.Increment("ARENA_LOSSES");       AddPvPReputation(-2); }
    }

    public void OnBattlefieldResult(bool won)
    {
        if (won) { activityCounter.Increment("BATTLEFIELD_WINS");   AddPvPReputation(6);  }
        else     { activityCounter.Increment("BATTLEFIELD_LOSSES"); AddPvPReputation(-2); }
    }

    // =========================================================
    // GUILDE — GDD v21 section 17.1
    // =========================================================

    public void RegisterGroupMemberLed(string playerID)
    {
        if (string.IsNullOrEmpty(playerID)) return;
        uniqueGroupMembersLed.Add(playerID);
        activityCounter.Set("GROUP_LEADER_UNIQUE_COUNT", uniqueGroupMembersLed.Count);
        GameEventBus.Publish(new SocialEvent { action = SocialAction.GroupLeader, otherPlayerID = playerID });
        Debug.Log($"[PLAYER] Membres uniques dirigés : {uniqueGroupMembersLed.Count}/20");
    }

    public bool CanCreateGuild() => uniqueGroupMembersLed.Count >= 20;

    // =========================================================
    // PROGRESSION
    // =========================================================

    /// <summary>
    /// Level up — met à jour player.level puis délègue à RecalculateStats().
    /// ⚠ Ne touche PAS directement à maxHP/maxMana/regenHP/regenMana/moveSpeed.
    /// RecalculateStats() recalcule tout depuis CharacterData + level.
    /// </summary>
    public void OnLevelUp(int newLevel)
    {
        level = newLevel;
        activityCounter.Set("PLAYER_LEVEL", newLevel);

        // RecalculateStats() se charge de maxHP = baseMaxHP + hpPerLevel × (level-1)
        stats.RecalculateStats(this);

        // HP/Mana remplis complètement au level up
        currentHP   = maxHP;
        currentMana = maxMana;

        RefreshTitle();
    }

    public void AddCombatXP(int amount)
    {
        xpCombat += amount;
        xpToNextLevel = characterData != null
            ? characterData.GetXPThreshold(level)
            : XPSystem.CalculateXPForLevel(level);

        if (xpCombat >= xpToNextLevel)
        {
            xpCombat -= xpToNextLevel;
            OnLevelUp(level + 1);
        }
    }

    // =========================================================
    // RÉSURRECTION — GDD v21 section 23.2
    // =========================================================

    public void Revive(float hpPercent = 0.30f, float manaPercent = 0.30f)
    {
        isDead      = false;
        currentHP   = maxHP   * hpPercent;
        currentMana = maxMana * manaPercent;
    }

    public void ReviveAtRespawnPoint()
    {
        isDead      = false;
        currentHP   = maxHP;
        currentMana = maxMana;
        // TODO: TeleportSystem.Instance?.RespawnAtPoint(this)
    }

    // =========================================================
    // ÉVÉNEMENTS DIVERS
    // =========================================================

    public void OnEnterZone(string zoneID)
    {
        currentZoneID = zoneID;
        activityCounter.Increment($"ZONE_{zoneID}");
        GameEventBus.Publish(new ZoneEvent { zoneID = zoneID, timeSpentSeconds = 0f, isAFK = false });
    }

    public void OnZoneTick(string zoneID, float duration, bool isAFK)
        => GameEventBus.Publish(new ZoneEvent { zoneID = zoneID, timeSpentSeconds = duration, isAFK = isAFK });

    public void OnItemConsumed(string itemID, bool wastePotion = false)
        => activityCounter.Increment(wastePotion ? "POTIONS_WASTED" : "POTIONS_USED");

    public void OnItemCrafted(string itemID)
    {
        activityCounter.Increment(CounterKeys.CRAFTS_TOTAL);
        GameEventBus.Publish(new ItemEvent { itemID = itemID, action = ItemAction.Craft, quantity = 1 });
    }

    public void OnItemSold(string itemID, int aeris)
    {
        activityCounter.Increment("ITEMS_SOLD");
        activityCounter.Increment("AERIS_EARNED_TOTAL", aeris);
        GameEventBus.Publish(new ItemEvent { itemID = itemID, action = ItemAction.Sell, quantity = 1, aerisAmount = aeris });
    }

    public void OnHdVTransactionCompleted()  { activityCounter.Increment("HDV_TRANSACTIONS");  AddWorldReputation(1);  }
    public void OnHdVListingCancelled()      { activityCounter.Increment("HDV_CANCELLATIONS"); AddWorldReputation(-1); }
    public void OnPlayerMet(string playerID) { activityCounter.Increment(CounterKeys.PLAYERS_MET); GameEventBus.Publish(new SocialEvent { action = SocialAction.MeetPlayer, otherPlayerID = playerID }); }

    public void OnActivitySessionCompleted(string activityID)
    {
        activityCounter.Increment($"ACTIVITY_{activityID}");
        int today = activityCounter.GetToday("WORLD_REP_ACTIVITY_DAILY");
        if (today < 5) { activityCounter.IncrementToday("WORLD_REP_ACTIVITY_DAILY"); AddWorldReputation(1); }
        GameEventBus.Publish(new MetierEvent { metierID = activityID, actionType = "session_complete", newLevel = 0 });
    }

    public void OnPetCaptured(MobData mob)                   { activityCounter.Increment("PETS_CAPTURED");    GameEventBus.Publish(new PetEvent { action = PetAction.Capture, mob = mob }); }
    public void OnAnimalCaressed(string id)                  { activityCounter.Increment("ANIMALS_CARESSED"); GameEventBus.Publish(new PetEvent { action = PetAction.Talk, npcID = id }); }
    public void OnMetierAction(string m, string a, int l=0)  => GameEventBus.Publish(new MetierEvent { metierID = m, actionType = a, newLevel = l });
    public void OnServerConnect(bool isFirst)                { if (isFirst) activityCounter.Increment("FIRST_ON_SERVER"); GameEventBus.Publish(new ServerEvent { firstConnection = isFirst }); }

    // ── Accesseurs ────────────────────────────────────────────
    public ActivityCounter GetActivityCounter() => activityCounter;
    public ElementalSystem GetElementalSystem()  => elementalSystem;

    public float GetElementPoints(ElementType element)
        => stats.GetElementalPoints(element);

    // ── Setters Entity — appelés par PlayerStats.RecalculateStats() ──
    public void SetMaxHP(float value)
    {
        maxHP     = Mathf.Max(1f, value);
        currentHP = Mathf.Clamp(currentHP, 0f, maxHP);
    }

    public void SetMaxMana(float value)
    {
        maxMana     = Mathf.Max(0f, value);
        currentMana = Mathf.Clamp(currentMana, 0f, maxMana);
    }

    public void SetRegenHP(float value)   => regenHP   = Mathf.Max(0f, value);
    public void SetRegenMana(float value) => regenMana = Mathf.Max(0f, value);

    /// <summary>MoveSpeed base depuis CharacterData + bonus équipement. Appelé par RecalculateStats().</summary>
    public void SetMoveSpeed(float value) => moveSpeed = Mathf.Max(0f, value);
}
