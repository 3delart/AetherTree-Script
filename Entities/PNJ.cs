using UnityEngine;
using UnityEngine.AI;
using System.Collections;
using System.Collections.Generic;

// =============================================================
// PNJ — Entité PNJ (Non-Player Character)
// Path : Assets/Scripts/Core/PNJ.cs
// AetherTree GDD v30 — §13 (PNJ Données Techniques), §19 (PNJ & Dialogues)
//
// Types gérés (GDD v30 §19.2) :
//   Merchant     → ShopUI
//   Blacksmith   → ForgeUI (TODO Phase 6)
//   Antiquarian  → RuneUI (TODO Phase 6)
//   FusionNPC    → FusionUI (TODO Phase 6)
//   CraftMaster  → MetierUI (TODO Phase 7)
//   Quest        → QuestUI (TODO Phase 7)
//   Mayor        → Dialogue conditionnel + création guilde
//   FactionNPC   → Services faction Solthars / Umbrans (TODO Phase 9)
//   HarborMaster → Navigation bateau (TODO Phase 8)
//   Guard        → IA combat mobs (rayon 200) + dialogue neutre
//   Decorative   → Dialogue lore/ambiance uniquement
//
// Mémoire joueur : le PNJ se souvient des joueurs connus via SaveSystem.
// Dialogue : SO DialogueData — stages numérotés + options cliquables (GDD §19.1).
// =============================================================

public class PNJ : Entity
{
    // ── Data ──────────────────────────────────────────────────
    [Header("Data PNJ (assigner ici)")]
    public PNJData data;

    // ── Interaction ───────────────────────────────────────────
    [Header("Interaction")]
    [Tooltip("Rayon dans lequel le joueur peut interagir avec ce PNJ")]
    public float interactionRadius = 3f;

    // ── Mémoire joueurs connus ────────────────────────────────
    // Persisté via SaveSystem — GDD v21 section 14
    private HashSet<string> knownPlayerIDs = new HashSet<string>();

    // ── Dialogue actif ────────────────────────────────────────
    private DialogueData  activeDialogue = null;
    private DialogueStage currentStage   = null;
    private Player        talkingTo      = null;

    // ── Garde — IA NavMesh ────────────────────────────────────
    private NavMeshAgent guardAgent;
    private Entity       guardTarget;
    private float        guardAttackTimer = 0f;
    private Vector3      guardSpawnPos;

    // =========================================================
    // INITIALISATION
    // =========================================================

    protected override void Awake()
    {
        if (data != null)
        {
            entityName = data.pnjName;

            // Stats uniquement pour les Gardes
            if (data.pnjType == PNJType.Guard)
            {
                maxHP   = data.baseMaxHP;
                maxMana = data.baseMaxMana;
                regenHP = data.baseRegenHP;
            }
        }

        base.Awake();

        // Charge la mémoire joueurs depuis PlayerPrefs
        LoadKnownPlayersFromPrefs();

        // Setup Garde
        if (data != null && data.pnjType == PNJType.Guard)
        {
            guardAgent    = GetComponent<NavMeshAgent>();
            guardSpawnPos = transform.position;
            if (guardAgent != null)
                guardAgent.speed = data.guardMoveSpeed;
        }
    }

    // =========================================================
    // DÉFENSES — PNJ non-combat : 0 par défaut
    // Les Gardes n'ont pas de stats de défense différenciées.
    // =========================================================

    public override float GetMeleeDefense()  => 0f;
    public override float GetRangedDefense() => 0f;
    public override float GetMagicDefense()  => 0f;

    // =========================================================
    // UPDATE
    // =========================================================

    protected override void Update()
    {
        base.Update();
        if (isDead) return;

        if (data != null && data.pnjType == PNJType.Guard)
            HandleGuardAI();
    }

    // =========================================================
    // INTERACTION JOUEUR
    // =========================================================

    /// <summary>
    /// Appelé quand un joueur interagit avec ce PNJ (touche E ou clic).
    /// Déclenche le dialogue approprié selon le type et la mémoire.
    /// </summary>
    public void Interact(Player player)
    {
        if (player == null || data == null || isDead) return;
        if (Vector3.Distance(transform.position, player.transform.position) > interactionRadius)
            return;

        talkingTo = player;

        switch (data.pnjType)
        {
            case PNJType.Merchant:     InteractMerchant(player);     break;
            case PNJType.Blacksmith:   InteractBlacksmith(player);   break;
            case PNJType.Antiquarian:  InteractAntiquarian(player);  break;
            case PNJType.FusionNPC:    InteractFusionNPC(player);    break;
            case PNJType.CraftMaster:  InteractCraftMaster(player);  break;
            case PNJType.Quest:        InteractQuest(player);        break;
            case PNJType.Guard:        InteractGuard(player);        break;
            case PNJType.Decorative:   InteractDecorative(player);   break;
            case PNJType.Mayor:        InteractMayor(player);        break;
            case PNJType.FactionNPC:   InteractFactionNPC(player);   break;
            case PNJType.HarborMaster: InteractHarborMaster(player); break;
        }

        // Mémorise le joueur après interaction — GDD v30 §19.1
        RegisterKnownPlayer(player);
    }

    // ── Marchand ──────────────────────────────────────────────
    private void InteractMerchant(Player player)
    {
        DialogueData dialogue = SelectDialogue(player);
        StartDialogue(dialogue, player);
        // ShopUI s'ouvre via DialogueAction.OpenShop depuis une option de dialogue
    }

    // ── Forgeron ──────────────────────────────────────────────
    // GDD v30 §19.3 — upgrade arme/armure, limité par data.maxUpgradeLevel
    private void InteractBlacksmith(Player player)
    {
        DialogueData dialogue = SelectDialogue(player);
        StartDialogue(dialogue, player);
        // TODO Phase 6 : ForgeUI.Instance?.Open(data, player)
        Debug.Log($"[PNJ/Forgeron] {data.pnjName} — upgrade max +{data.maxUpgradeLevel} (ForgeUI Phase 6)");
    }

    // ── Antiquaire ────────────────────────────────────────────
    // GDD v30 §19.3 — identification + insertion de runes
    private void InteractAntiquarian(Player player)
    {
        DialogueData dialogue = SelectDialogue(player);
        StartDialogue(dialogue, player);
        // TODO Phase 6 : RuneUI.Instance?.Open(data, player)
        Debug.Log($"[PNJ/Antiquaire] {data.pnjName} — identification:{data.canIdentifyRunes} insertion:{data.canInsertRunes} (RuneUI Phase 6)");
    }

    // ── PNJ Fusion ────────────────────────────────────────────
    // GDD v30 §19.3 — fusion Gants & Bottes S0→S6
    private void InteractFusionNPC(Player player)
    {
        DialogueData dialogue = SelectDialogue(player);
        StartDialogue(dialogue, player);
        // TODO Phase 6 : FusionUI.Instance?.Open(player)
        Debug.Log("[PNJ/Fusion] FusionUI Phase 6");
    }

    // ── Maître de Métier ──────────────────────────────────────
    // GDD v30 §19.2 — déblocage activités (Bûcheron, Pêcheur...)
    private void InteractCraftMaster(Player player)
    {
        DialogueData dialogue = SelectDialogue(player);
        StartDialogue(dialogue, player);
        // TODO Phase 7 : MetierUI.Instance?.Open(player)
        Debug.Log("[PNJ/MaîtreMétier] MetierUI Phase 7");
    }

    // ── Quête ─────────────────────────────────────────────────
    private void InteractQuest(Player player)
    {
        DialogueData dialogue = SelectDialogue(player);
        if (dialogue != null)
            StartDialogue(dialogue, player);
    }

    // ── Garde ─────────────────────────────────────────────────
    private void InteractGuard(Player player)
    {
        if (data.defaultDialogue != null)
            StartDialogue(data.defaultDialogue, player);
    }

    // ── Décoratif ─────────────────────────────────────────────
    private void InteractDecorative(Player player)
    {
        DialogueData dialogue = SelectDialogue(player);
        if (dialogue != null)
            StartDialogue(dialogue, player);
    }

    // ── Maire ─────────────────────────────────────────────────
    // GDD v30 §19.5 — dialogue conditionnel en 4 stages
    private void InteractMayor(Player player)
    {
        if (player.CanCreateGuild())
        {
            DialogueData dialogue = data.guildUnlockDialogue != null
                ? data.guildUnlockDialogue
                : data.defaultDialogue;
            StartDialogue(dialogue, player);
        }
        else
        {
            DialogueData dialogue = data.guildNotReadyDialogue != null
                ? data.guildNotReadyDialogue
                : data.defaultDialogue;
            StartDialogue(dialogue, player);
        }
    }

    // ── PNJ Faction ───────────────────────────────────────────
    // GDD v30 §19.6 — Solthars / Umbrans, hostile si faction adverse
    private void InteractFactionNPC(Player player)
    {
        // TODO Phase 9 : vérifier player.faction vs data.faction
        // Si faction adverse → hostileDialogue, sinon → SelectDialogue normal
        if (data.hostileDialogue != null)
        {
            // Placeholder — toujours le dialogue hostile tant que FactionSystem n'existe pas
            // TODO : if (player.faction != data.faction) StartDialogue(data.hostileDialogue, player); else ...
        }
        DialogueData dialogue = SelectDialogue(player);
        StartDialogue(dialogue, player);
        Debug.Log($"[PNJ/Faction] {data.pnjName} ({data.faction}) — FactionSystem Phase 9");
    }

    // ── Capitaine de Port ─────────────────────────────────────
    // GDD v30 §19.3 — navigation bateau, départ toutes les 5-15 min
    private void InteractHarborMaster(Player player)
    {
        DialogueData dialogue = SelectDialogue(player);
        StartDialogue(dialogue, player);
        // TODO Phase 8 : HarborUI.Instance?.Open(data.availableDestinations, player)
        Debug.Log("[PNJ/Capitaine] HarborUI Phase 8");
    }

    // =========================================================
    // SÉLECTION DU DIALOGUE
    // GDD v30 §19.1 — dialogue selon mémoire + réputation
    // =========================================================

    /// <summary>
    /// Sélectionne le dialogue approprié selon :
    /// 1. Réputation Monde si seuil atteint (prioritaire)
    /// 2. Joueur connu (déjà parlé)
    /// 3. Dialogue par défaut
    /// </summary>
    private DialogueData SelectDialogue(Player player)
    {
        if (data == null) return null;

        if (data.reputationDialogueThreshold > 0 &&
            data.highReputationDialogue != null &&
            player.worldReputationRank >= data.reputationDialogueThreshold)
            return data.highReputationDialogue;

        if (data.knownPlayerDialogue != null && IsKnownPlayer(player))
            return data.knownPlayerDialogue;

        return data.defaultDialogue;
    }

    // =========================================================
    // SYSTÈME DE DIALOGUE
    // =========================================================

    private void StartDialogue(DialogueData dialogue, Player player)
    {
        if (dialogue == null)
        {
            Debug.LogWarning($"[PNJ] {data.pnjName} : aucun DialogueData assigné.");
            return;
        }

        activeDialogue = dialogue;
        currentStage   = dialogue.GetFirstStage();

        if (currentStage == null)
        {
            Debug.LogWarning($"[PNJ] {data.pnjName} : DialogueData sans stage.");
            return;
        }

        // Saute les stages "skipIfKnown" si le joueur est déjà connu
        if (IsKnownPlayer(player))
        {
            while (currentStage != null && currentStage.skipIfKnown)
            {
                // Avance vers le premier stage non-skip via la première option disponible
                if (currentStage.options != null && currentStage.options.Count > 0)
                {
                    int nextID = currentStage.options[0].nextStageID;
                    currentStage = nextID >= 0 ? dialogue.GetStage(nextID) : null;
                }
                else break;
            }
        }

        if (currentStage == null)
        {
            Debug.LogWarning($"[PNJ] {data.pnjName} : plus aucun stage après skip.");
            return;
        }

        DialogueUI.Instance?.OpenDialogue(this, currentStage, player);
        Debug.Log($"[PNJ] {data.pnjName} → Stage {currentStage.stageID} : {currentStage.text}");
    }

    /// <summary>
    /// Avance vers le stage suivant selon l'option choisie par le joueur.
    /// Appelé par DialogueUI quand le joueur clique sur une option.
    /// </summary>
    public void SelectOption(DialogueOption option, Player player)
    {
        if (option == null || activeDialogue == null) return;

        HandleDialogueAction(option.action, player);

        if (option.nextStageID == -1)
        {
            EndDialogue();
            return;
        }

        DialogueStage nextStage = activeDialogue.GetStage(option.nextStageID);
        if (nextStage == null)
        {
            Debug.LogWarning($"[PNJ] Stage {option.nextStageID} introuvable dans {activeDialogue.dialogueName}");
            EndDialogue();
            return;
        }

        currentStage = nextStage;

        // GDD v30 §19.1 — distribue les récompenses à l'entrée du stage
        GrantStageRewards(currentStage, player);

        if (currentStage.options == null || currentStage.options.Count == 0)
        {
            DialogueUI.Instance?.ShowStage(currentStage);
            // Ne ferme que si ce n'est PAS un stage dynamique
            if (!currentStage.isDynamicQuestStage)
                EndDialogue();
            return;
        }

        DialogueUI.Instance?.ShowStage(currentStage);
        Debug.Log($"[PNJ] → Stage {currentStage.stageID} : {currentStage.text}");
    }

    /// <summary>
    /// Distribue les récompenses définies sur le stage — GDD v30 §19.1.
    /// Appelé une fois par visite du stage (à chaque SelectOption qui y mène).
    /// </summary>
    private void GrantStageRewards(DialogueStage stage, Player player)
    {
        if (stage == null || player == null) return;

        if (stage.rewardXP > 0)
        {
            player.AddCombatXP(stage.rewardXP);
            Debug.Log($"[PNJ] Récompense XP : +{stage.rewardXP}");
        }

        if (stage.rewardAeris > 0)
        {
            // TODO : player.AddAeris(stage.rewardAeris) — via CurrencySystem Phase 5
            Debug.Log($"[PNJ] Récompense Aeris : +{stage.rewardAeris} (CurrencySystem Phase 5)");
        }

        if (!string.IsNullOrEmpty(stage.rewardItemID))
        {
            // TODO : InventorySystem.Instance?.AddItem(player, stage.rewardItemID) Phase 5
            Debug.Log($"[PNJ] Récompense item : {stage.rewardItemID} (InventorySystem Phase 5)");
        }

        if (stage.rewardWorldRep != 0)
        {
            player.AddWorldReputation(stage.rewardWorldRep);
            Debug.Log($"[PNJ] Récompense réputation : {stage.rewardWorldRep:+#;-#;0}");
        }
    }

    private void HandleDialogueAction(DialogueAction action, Player player)
    {
        switch (action)
        {
            case DialogueAction.OpenShop:
                ShopUI.Instance?.OpenShop(data, player);
                break;

            case DialogueAction.OpenForge:
                // TODO Phase 6 : ForgeUI.Instance?.Open(data, player)
                Debug.Log("[PNJ] OpenForge — ForgeUI Phase 6");
                break;

            case DialogueAction.OpenRuneUI:
                // TODO Phase 6 : RuneUI.Instance?.Open(data, player)
                Debug.Log("[PNJ] OpenRuneUI — RuneUI Phase 6");
                break;

            case DialogueAction.OpenFusionUI:
                // TODO Phase 6 : FusionUI.Instance?.Open(player)
                Debug.Log("[PNJ] OpenFusionUI — FusionUI Phase 6");
                break;

            case DialogueAction.OpenMetierUI:
                // TODO Phase 7 : MetierUI.Instance?.Open(player)
                Debug.Log("[PNJ] OpenMetierUI — MetierUI Phase 7");
                break;

            case DialogueAction.OpenQuestLog:
                // TODO Phase 7 : QuestUI.Instance?.OpenQuestLog(data.availableQuests, player)
                Debug.Log("[PNJ] OpenQuestLog — QuestUI Phase 7");
                break;

            case DialogueAction.OpenHarborUI:
                // TODO Phase 8 : HarborUI.Instance?.Open(data.availableDestinations, player)
                Debug.Log("[PNJ] OpenHarborUI — HarborUI Phase 8");
                break;

            case DialogueAction.TriggerGuildCreation:
                TryCreateGuild(player);
                break;

            case DialogueAction.CloseDialogue:
                EndDialogue();
                break;

            case DialogueAction.None:
            default:
                break;
        }
    }

    private void EndDialogue()
    {
        activeDialogue = null;
        currentStage   = null;
        talkingTo      = null;
        DialogueUI.Instance?.CloseDialogue();
    }

    // =========================================================
    // CRÉATION DE GUILDE — GDD v30 §19.5
    // =========================================================

    private void TryCreateGuild(Player player)
    {
        if (!player.CanCreateGuild())
        {
            Debug.Log("[PNJ/Maire] Condition non remplie — 20 membres uniques requis.");
            return;
        }

        int cost = data.guildCreationCost;
        // TODO: vérifier Aeris joueur >= cost
        // TODO: GuildSystem.Instance?.CreateGuild(player, cost)
        Debug.Log($"[PNJ/Maire] Création de guilde débloquée — Coût : {cost} Aeris (à implémenter Phase 9)");
    }

    // =========================================================
    // MÉMOIRE JOUEURS — GDD v30 §19.1
    // =========================================================

    /// <summary>Enregistre un joueur comme connu par ce PNJ.</summary>
    public void RegisterKnownPlayer(Player player)
    {
        if (player == null) return;
        string id = player.entityName;
        if (knownPlayerIDs.Add(id))
        {
            SaveKnownPlayersToPrefs();
            Debug.Log($"[PNJ] {data.pnjName} mémorise le joueur : {id}");
        }
    }

    // ── Persistance PlayerPrefs ────────────────────────────────
    private string PrefsKey => $"PNJ_Known_{data?.name ?? gameObject.name}";

    private void SaveKnownPlayersToPrefs()
    {
        string joined = string.Join("|", knownPlayerIDs);
        PlayerPrefs.SetString(PrefsKey, joined);
        PlayerPrefs.Save();
    }

    private void LoadKnownPlayersFromPrefs()
    {
        string saved = PlayerPrefs.GetString(PrefsKey, "");
        if (string.IsNullOrEmpty(saved)) return;
        knownPlayerIDs.Clear();
        foreach (string id in saved.Split('|'))
            if (!string.IsNullOrEmpty(id)) knownPlayerIDs.Add(id);
    }

    /// <summary>True si ce PNJ a déjà rencontré ce joueur.</summary>
    public bool IsKnownPlayer(Player player)
    {
        if (player == null) return false;
        return knownPlayerIDs.Contains(player.entityName); // TODO: player.playerID
    }

    /// <summary>Charge la mémoire depuis le SaveSystem. Appelé à l'init.</summary>
    public void LoadKnownPlayers(List<string> savedIDs)
    {
        knownPlayerIDs.Clear();
        foreach (string id in savedIDs)
            knownPlayerIDs.Add(id);
    }

    // =========================================================
    // IA GARDE — GDD v30 §19.2
    // Cible les mobs proches dans un rayon de 200 unités
    // =========================================================

    private void HandleGuardAI()
    {
        if (guardAgent == null) return;

        guardAttackTimer -= Time.deltaTime;

        if (guardTarget == null || guardTarget.isDead)
            guardTarget = FindClosestMob();

        if (guardTarget == null || guardTarget.isDead)
        {
            if (Vector3.Distance(transform.position, guardSpawnPos) > 2f)
                guardAgent.SetDestination(guardSpawnPos);
            else
                guardAgent.ResetPath();
            return;
        }

        float distToTarget = Vector3.Distance(transform.position, guardTarget.transform.position);

        if (distToTarget <= data.guardAttackRange)
        {
            guardAgent.ResetPath();
            LookAt(guardTarget.transform);

            if (guardAttackTimer <= 0f)
            {
                guardTarget.TakeDamage(data.guardAttackDamage, ElementType.Neutral, this);
                guardAttackTimer = data.guardAttackCooldown;
            }
        }
        else
        {
            guardAgent.SetDestination(guardTarget.transform.position);
        }
    }

    private Entity FindClosestMob()
    {
        Collider[] hits    = Physics.OverlapSphere(transform.position, data.guardAggroRadius);
        Entity     closest = null;
        float      minDist = float.MaxValue;

        foreach (Collider col in hits)
        {
            Mob mob = col.GetComponent<Mob>();
            if (mob == null || mob.isDead) continue;

            float dist = Vector3.Distance(transform.position, mob.transform.position);
            if (dist < minDist)
            {
                minDist = dist;
                closest = mob;
            }
        }
        return closest;
    }

    // =========================================================
    // MORT & RESPAWN — GDD v30 §19.2
    // =========================================================

    protected override void Die()
    {
        // Seuls les Gardes meurent — les autres types de PNJ sont invulnérables
        if (data == null || data.pnjType != PNJType.Guard) return;

        base.Die();

        if (guardAgent != null)
            guardAgent.ResetPath();

        if (data.respawnDelay > 0f)
            StartCoroutine(RespawnCoroutine());

        Debug.Log($"[PNJ] {data.pnjName} mort — respawn dans {data.respawnDelay}s");
    }

    /// <summary>
    /// Coroutine de respawn — désactive le rendu et la collision sans désactiver
    /// le GameObject, car les coroutines s'arrêtent si SetActive(false) est appelé.
    /// GDD v30 §19.2.
    /// </summary>
    private IEnumerator RespawnCoroutine()
    {
        // Masque le PNJ sans désactiver le GameObject (sinon la coroutine s'arrête)
        foreach (Renderer r in GetComponentsInChildren<Renderer>())   r.enabled = false;
        foreach (Collider c in GetComponentsInChildren<Collider>())   c.enabled = false;
        if (guardAgent != null) guardAgent.enabled = false;

        yield return new WaitForSeconds(data.respawnDelay);

        // Respawn
        isDead             = false;
        currentHP          = maxHP;
        transform.position = guardSpawnPos;

        foreach (Renderer r in GetComponentsInChildren<Renderer>())   r.enabled = true;
        foreach (Collider c in GetComponentsInChildren<Collider>())   c.enabled = true;
        if (guardAgent != null) { guardAgent.enabled = true; guardAgent.Warp(guardSpawnPos); }

        Debug.Log($"[PNJ] {data.pnjName} respawné.");
    }

    // =========================================================
    // UTILITAIRES
    // =========================================================

    private void LookAt(Transform target)
    {
        if (target == null) return;
        Vector3 dir = target.position - transform.position;
        dir.y = 0f;
        if (dir.sqrMagnitude > 0.001f)
            transform.rotation = Quaternion.LookRotation(dir);
    }

    // =========================================================
    // ACCESSEURS
    // =========================================================

    public PNJType        PNJType       => data != null ? data.pnjType : global::PNJType.Decorative;
    public string         PNJName       => data != null ? data.pnjName : entityName;
    public bool           IsTalking     => talkingTo != null;
    public DialogueStage  CurrentStage  => currentStage;

    // =========================================================
    // GIZMOS
    // =========================================================

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position, interactionRadius);

        if (data != null && data.pnjType == PNJType.Guard)
        {
            Gizmos.color = new Color(1f, 0.5f, 0f, 0.4f);
            Gizmos.DrawWireSphere(transform.position, data.guardAggroRadius);
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(transform.position, data.guardAttackRange);
        }
    }
}