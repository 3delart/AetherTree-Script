using UnityEngine;

// =============================================================
// ENTITY.CS — Classe de base pour toutes les entités vivantes
// Path : Assets/Scripts/Core/Entity.cs
// AetherTree GDD v30 — §4 (Entity)
//
// Héritiers directs : Player, Mob, PNJ, Pet
//
// Champs runtime    : protected (accessibles depuis les sous-classes)
// Propriétés UI     : public en lecture seule (MaxHP, CurrentHP, etc.)
// Méthodes override : GetMeleeDefense(), GetRangedDefense(), GetMagicDefense()
//
// ⚠ Régénération passive (GDD v30 §4.2) :
//   Joueur UNIQUEMENT — regenHP et regenMana restent à 0f sur Mob/PNJ/Pet.
//   Le tick tourne sur toutes les entités pour ne pas complexifier Update(),
//   mais n'a aucun effet si regenHP == 0f && regenMana == 0f (cas Mob/PNJ).
// =============================================================

[RequireComponent(typeof(StatusEffectSystem))]
public abstract class Entity : MonoBehaviour
{
    // ── Identité ──────────────────────────────────────────────
    [Header("Identité")]
    public string entityName = "Entity";

    // ── Stats de base (runtime) ───────────────────────────────
    protected float maxHP     = 100f;
    protected float maxMana   = 50f;
    /// <summary>
    /// Régénération HP par seconde. GDD v30 §4.2 — joueur uniquement.
    /// Doit rester à 0f sur Mob, PNJ et Pet (valeur par défaut).
    /// </summary>
    protected float regenHP   = 0f;
    /// <summary>
    /// Régénération Mana par seconde. GDD v30 §4.2 — joueur uniquement.
    /// Doit rester à 0f sur Mob, PNJ et Pet (valeur par défaut).
    /// </summary>
    protected float regenMana = 0f;

    // ── Stats courantes ───────────────────────────────────────
    protected float currentHP;
    protected float currentMana;

    // ── État ──────────────────────────────────────────────────
    public bool isDead { get; protected set; } = false;

    // ── StatusEffectSystem ────────────────────────────────────
    // GDD v30 §4.5 — présent sur toutes les entités ([RequireComponent] ci-dessus).
    public StatusEffectSystem statusEffects { get; private set; }

    // ── Timer régénération ────────────────────────────────────
    private float _regenTimer = 0f;
    private const float REGEN_TICK = 1f;

    // =========================================================
    // PROPRIÉTÉS PUBLIQUES EN LECTURE SEULE (pour l'UI)
    // =========================================================

    public float MaxHP       => maxHP;
    public float CurrentHP   => currentHP;
    public float MaxMana     => maxMana;
    public float CurrentMana => currentMana;
    public float RegenHP     => regenHP;
    public float RegenMana   => regenMana;

    /// <summary>HP courant exprimé en [0..1]. Utile pour les barres et conditions.</summary>
    public float HPPercent  => maxHP   > 0f ? currentHP   / maxHP   : 0f;

    /// <summary>Mana courant exprimé en [0..1].</summary>
    public float ManaPercent => maxMana > 0f ? currentMana / maxMana : 0f;

    // =========================================================
    // INITIALISATION
    // =========================================================

    protected virtual void Awake()
    {
        currentHP    = maxHP;
        currentMana  = maxMana;
        statusEffects = GetComponent<StatusEffectSystem>();
    }

    // =========================================================
    // UPDATE — régénération passive
    // =========================================================

    protected virtual void Update()
    {
        if (isDead) return;

        _regenTimer += Time.deltaTime;
        if (_regenTimer >= REGEN_TICK)
        {
            _regenTimer = 0f;
            ApplyRegen();
        }
    }

    // GDD v30 §4.2 — pas d'effet si regenHP == 0f && regenMana == 0f (Mob/PNJ/Pet).
    private void ApplyRegen()
    {
        if (regenHP   > 0f) Heal(regenHP);
        if (regenMana > 0f) RecoverMana(regenMana);
    }

    // =========================================================
    // DÉFENSES — virtuelles : chaque sous-classe définit la sienne
    // =========================================================

    public abstract float GetMeleeDefense();
    public abstract float GetRangedDefense();
    public abstract float GetMagicDefense();

    /// <summary>Alias legacy — retourne GetMeleeDefense().</summary>
    public float GetPhysicalDefense() => GetMeleeDefense();

    // =========================================================
    // DÉGÂTS
    // =========================================================

    /// <summary>
    /// Inflige des dégâts bruts (déjà calculés par CombatSystem).
    /// Override dans Player et Mob pour ajouter des effets spécifiques.
    /// </summary>
    public virtual void TakeDamage(float amount,
                                   ElementType sourceElement = ElementType.Neutral,
                                   Entity source = null)
    {
        if (isDead || amount <= 0f) return;

        // Invincible — annule les dégâts | Sleep — réveil au premier dégât
        if (statusEffects != null && statusEffects.OnTakeDamage()) return;

        // Shield — absorbe les dégâts en priorité
        if (statusEffects != null)
            amount = statusEffects.AbsorbWithShield(amount);

        if (amount <= 0f) return;

        currentHP = Mathf.Max(0f, currentHP - amount);

        if (currentHP <= 0f)
            Die();
    }

    // =========================================================
    // SOIN
    // =========================================================

    public virtual void Heal(float amount)
    {
        if (isDead || amount <= 0f) return;
        currentHP = Mathf.Min(maxHP, currentHP + amount);
    }

    // =========================================================
    // MANA
    // =========================================================

    public virtual void SpendMana(float amount)
    {
        currentMana = Mathf.Max(0f, currentMana - amount);
    }

    public virtual void RecoverMana(float amount)
    {
        if (isDead) return;
        currentMana = Mathf.Min(maxMana, currentMana + amount);
    }

    public bool HasMana(float amount) => currentMana >= amount;

    // =========================================================
    // MORT
    // =========================================================

    protected virtual void Die()
    {
        if (isDead) return;
        isDead    = true;
        currentHP = 0f;
    }
}
