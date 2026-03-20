using UnityEngine.AI;
using UnityEngine;

// =============================================================
// TARGETINGSYSTEM.CS — Ciblage & Auto-attaque
// Path : Assets/Scripts/Systems/TargetingSystem.cs
// AetherTree GDD v31 — Section 8 / 21
//
// Flow auto-attaque (GDD §8.1) :
//   TargetingSystem gère le TIMER de cadence (AttackSpeed de l'arme).
//   L'EXÉCUTION passe toujours par SkillBar.TryUseSlot(0).
//
// Flow interaction PNJ (GDD §19) :
//   Clic gauche sur PNJ → Select (premier clic)
//   Clic gauche sur PNJ déjà sélectionné → Interact si dans le rayon
//   Le PNJ gère lui-même la vérification du rayon d'interaction.
// =============================================================

public class TargetingSystem : MonoBehaviour
{
    public static TargetingSystem Instance { get; private set; }

    [Header("Couleurs de sélection")]
    public Color colorSelected = new Color(1f, 0.5f, 0f); // Orange
    public Color colorEngaged  = new Color(1f, 0f,   0f); // Rouge

    private Entity selectedTarget;
    private Entity engagedTarget;
    private Outline selectedOutline;
    private Outline engagedOutline;

    private Player player;
    private Coroutine _approachPNJCoroutine;
    private ElementalSystem elementalSystem;
    private bool  autoAttacking   = false;
    private float autoAttackTimer = 0f;

    private void Awake()
    {
        if (Instance != null) { Destroy(this); return; }
        Instance = this;
    }

    private void Start()
    {
        player = FindObjectOfType<Player>();
        if (player != null)
            elementalSystem = player.GetComponent<ElementalSystem>();

        if (player == null)
            Debug.LogError("[TARGETING] Player non trouvé !");
    }

    private void Update()
    {
        HandleInput();

        if (autoAttacking && engagedTarget != null && !engagedTarget.isDead)
        {
            autoAttackTimer -= Time.deltaTime;
            if (autoAttackTimer <= 0f)
            {
                PerformAutoAttack();
                float speed = player.equippedWeaponInstance != null
                    ? player.equippedWeaponInstance.AttackSpeed
                    : 1.2f;
                autoAttackTimer = speed > 0f ? 1f / speed : 1f;
            }
        }
        else if (engagedTarget != null && engagedTarget.isDead)
        {
            Deselect();
        }
    }

    private void HandleInput()
    {
        if (GameControls.Deselect) { Deselect(); return; }

        // Ferme le dialogue si ouvert et appui sur Echap
        if (DialogueUI.Instance != null && DialogueUI.Instance.IsOpen)
        {
            if (Input.GetKeyDown(KeyCode.Escape))
                DialogueUI.Instance.CloseDialogue();
            return; // bloque les autres inputs pendant un dialogue
        }

        if (Input.GetKeyDown(KeyCode.Tab))
        {
            if (engagedTarget != null) ToggleAutoAttack();
            return;
        }

        if (!GameControls.TargetClick) return;
        if (UIManager.Instance != null && UIManager.Instance.BlocksWorldInput()) return;

        Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
        if (!Physics.Raycast(ray, out RaycastHit hit, 100f)) { Deselect(); return; }

        if (hit.collider.CompareTag("Ground")) { Deselect(); return; }

        Entity entity = hit.collider.GetComponentInParent<Entity>();
        if (entity == null || entity == player) { Deselect(); return; }

        // ── PNJ — interaction dialogue ────────────────────────
        PNJ pnj = entity as PNJ;
        if (pnj != null)
        {
            if (entity == selectedTarget)
            {
                float dist = Vector3.Distance(player.transform.position, pnj.transform.position);
                if (dist <= pnj.interactionRadius)
                {
                    // Dans le rayon → interaction immédiate
                    pnj.Interact(player);
                }
                else
                {
                    // Trop loin → auto-approche puis interaction
                    ApproachPNJ(pnj);
                }
            }
            else
            {
                // Premier clic → sélection simple
                Select(entity);
            }
            return;
        }

        // ── Mob / Boss — flow combat normal ───────────────────
        if (entity == selectedTarget && engagedTarget != entity)
            Engage(entity);
        else
            Select(entity);
    }

    public void Select(Entity entity)
    {
        if (selectedOutline != null) selectedOutline.enabled = false;

        selectedTarget  = entity;
        selectedOutline = entity.GetComponent<Outline>()
                       ?? entity.gameObject.AddComponent<Outline>();

        selectedOutline.OutlineColor = colorSelected;
        selectedOutline.OutlineWidth = 4f;
        selectedOutline.enabled      = true;

        TargetPanel.Instance?.Show(entity);
    }

    public void Engage(Entity entity)
    {
        if (engagedOutline != null)
            engagedOutline.OutlineColor = colorSelected;

        engagedTarget  = entity;
        engagedOutline = entity.GetComponent<Outline>()
                      ?? entity.gameObject.AddComponent<Outline>();

        engagedOutline.OutlineColor = colorEngaged;
        engagedOutline.OutlineWidth = 4f;
        engagedOutline.enabled      = true;

        autoAttacking   = true;
        autoAttackTimer = 0f;
    }

    public void EngageFromSkill(Entity entity)
    {
        Select(entity);
        Engage(entity);
    }

    /// <summary>
    /// Point d'entrée unifié pour déclencher un skill depuis la SkillBar.
    /// </summary>
    public void TryExecuteSkill(SkillData skill)
    {
        if (skill == null || SkillSystem.Instance == null) return;

        if (skill.targetType == TargetType.GroundTarget)
        {
            Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
            if (Physics.Raycast(ray, out RaycastHit hit, 100f))
            {
                SkillSystem.Instance.SetGroundTargetPoint(hit.point);
                SkillSystem.Instance.Execute(skill, null);
            }
            else
            {
                Debug.LogWarning($"[TARGETING] {skill.skillName} (GroundTarget) : aucune surface détectée.");
            }
            return;
        }

        Entity target = engagedTarget ?? selectedTarget;
        SkillSystem.Instance.Execute(skill, target);
    }

    /// <summary>
    /// Déclenche l'auto-attaque via SkillBar.TryUseSlot(0).
    /// </summary>
    private void PerformAutoAttack()
    {
        if (player == null || SkillBar.Instance == null) return;

        // Stun — bloque l'auto-attaque (GDD §21bis.1)
        if (player.statusEffects != null && player.statusEffects.isStunned) return;

        bool dodged = CombatSystem.Instance.RollDodge(
            player.stats.dodge,
            player.stats.precision);
        if (dodged)
        {
            FloatingText.Spawn("ESQUIVE", engagedTarget.transform.position, Color.white);
            return;
        }

        SkillBar.Instance.TryUseSlot(0);
    }

    private void ToggleAutoAttack()
    {
        autoAttacking = !autoAttacking;
        Debug.Log($"[TARGETING] Auto-attaque : {autoAttacking}");
    }

    // =========================================================
    // AUTO-APPROCHE PNJ
    // =========================================================

    private void ApproachPNJ(PNJ pnj)
    {
        if (_approachPNJCoroutine != null)
            StopCoroutine(_approachPNJCoroutine);
        _approachPNJCoroutine = StartCoroutine(ApproachAndInteract(pnj));
    }

    private System.Collections.IEnumerator ApproachAndInteract(PNJ pnj)
    {
        NavMeshAgent agent = player.GetComponent<NavMeshAgent>();
        if (agent == null) yield break;

        // Déplace le joueur vers le PNJ
        agent.SetDestination(pnj.transform.position);

        // Attend d'être dans le rayon d'interaction
        float timeout = 10f;
        float elapsed = 0f;
        while (elapsed < timeout)
        {
            float dist = Vector3.Distance(player.transform.position, pnj.transform.position);
            if (dist <= pnj.interactionRadius)
            {
                agent.ResetPath();
                pnj.Interact(player);
                yield break;
            }
            elapsed += Time.deltaTime;
            yield return null;
        }

        // Timeout — annule l'approche
        agent.ResetPath();
        _approachPNJCoroutine = null;
    }

    public void Deselect()
    {
        if (selectedOutline != null) selectedOutline.enabled = false;
        if (engagedOutline  != null) engagedOutline.enabled  = false;

        selectedTarget  = null;
        engagedTarget   = null;
        selectedOutline = null;
        engagedOutline  = null;
        autoAttacking   = false;

        // Annule l'approche PNJ en cours
        if (_approachPNJCoroutine != null)
        {
            StopCoroutine(_approachPNJCoroutine);
            _approachPNJCoroutine = null;
        }

        TargetPanel.Instance?.Hide();
    }

    public Entity GetEngagedTarget()  => engagedTarget;
    public Entity GetSelectedTarget() => selectedTarget;
}
