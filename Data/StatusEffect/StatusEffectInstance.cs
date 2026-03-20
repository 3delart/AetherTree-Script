using UnityEngine;

// =============================================================
// StatusEffectInstance — base runtime d'un effet actif sur une entité
// Path : Assets/Scripts/Data/StatusEffect/StatusEffectInstance.cs
// AetherTree GDD v30 — Section 21bis
// =============================================================

public abstract class StatusEffectInstance
{
    public StatusEffectData data;
    public Entity           source;
    public float            remainingTime;

    public bool IsExpired => remainingTime <= 0f;

    protected StatusEffectInstance(StatusEffectData data, Entity source)
    {
        this.data     = data;
        this.source   = source;
        remainingTime = data.duration;
    }

    /// <summary>Remet la durée à zéro — même effet appliqué à nouveau.</summary>
    public void Refresh() => remainingTime = data.duration;

    /// <summary>Tick appelé par StatusEffectSystem chaque frame.</summary>
    public abstract void Tick(Entity target, float deltaTime);
}

// =============================================================
// DebuffInstance — runtime d'un debuff actif
// =============================================================
public class DebuffInstance : StatusEffectInstance
{
    public DebuffData DebuffData => (DebuffData)data;
    public DebuffType DebuffType => DebuffData.debuffType;

    public DebuffInstance(DebuffData data, Entity source) : base(data, source) { }

    public override void Tick(Entity target, float deltaTime)
    {
        if (target == null || target.isDead) return;

        remainingTime -= deltaTime;

        switch (DebuffType)
        {
            case DebuffType.Burn:
            case DebuffType.Poison:
            case DebuffType.Bleed:
                float dmg = DebuffData.damagePerSecond * deltaTime;
                if (dmg > 0f)
                    target.TakeDamage(dmg, DebuffData.damageElement, source);
                break;

            // Freeze, Slow, Root, Stun, Fear, Sleep, Shocked, Silence :
            // gérés via flags sur StatusEffectSystem (OnApply / OnExpire)
            // Knockback, Blind, ManaDrain, ArmorBreak :
            // gérés via flags sur StatusEffectSystem (OnApply / OnExpire)
        }
    }
}

// =============================================================
// BuffInstance — runtime d'un buff actif
// =============================================================
public class BuffInstance : StatusEffectInstance
{
    public BuffData BuffData => (BuffData)data;
    public BuffType BuffType => BuffData.buffType;

    public float remainingShield;

    public BuffInstance(BuffData data, Entity source) : base(data, source)
    {
        remainingShield = data.shieldAmount;
    }

    public override void Tick(Entity target, float deltaTime)
    {
        if (target == null || target.isDead) return;

        remainingTime -= deltaTime;

        switch (BuffType)
        {
            case BuffType.RegenHP:
                float heal = BuffData.healPerSecond * deltaTime;
                if (heal > 0f) target.Heal(heal);
                break;

            // Shield, DefenseUp, DodgeUp, Haste, AttackUp :
            // gérés via flags sur StatusEffectSystem (OnApply / OnExpire)
        }
    }

    /// <summary>Absorbe des dégâts. Retourne les dégâts résiduels.</summary>
    public float AbsorbDamage(float incomingDamage)
    {
        if (remainingShield <= 0f) return incomingDamage;
        float absorbed  = Mathf.Min(remainingShield, incomingDamage);
        remainingShield -= absorbed;
        return incomingDamage - absorbed;
    }
}
