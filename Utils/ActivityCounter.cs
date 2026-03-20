using UnityEngine;
using System.Collections.Generic;

// =============================================================
// ACTIVITYCOUNTER.CS — Compteurs d'activité runtime par joueur
// AetherTree GDD v21 — Section 12
// =============================================================

public class ActivityCounter : MonoBehaviour
{
    private Dictionary<string, int> counters = new Dictionary<string, int>();

    // ── Compteurs journaliers — GDD v21 section 20.2 ─────────
    // Utilisés pour les plafonds quotidiens (ex: +5 rep/jour max)
    private Dictionary<string, int> dailyCounters  = new Dictionary<string, int>();
    private string                  lastResetDate   = "";

    public void Increment(string key, int amount = 1)
    {
        if (!counters.ContainsKey(key))
            counters[key] = 0;
        counters[key] += amount;
    }

    public int Get(string key)
        => counters.ContainsKey(key) ? counters[key] : 0;

    public void Set(string key, int value)
        => counters[key] = value;

    public bool Has(string key)
        => counters.ContainsKey(key);

    public Dictionary<string, int> GetAll()
        => counters;

    public void LoadFromSave(Dictionary<string, int> saved)
        => counters = new Dictionary<string, int>(saved);

    // ── Compteurs journaliers ─────────────────────────────────

    /// <summary>Lit le compteur journalier pour une clé. Reset automatique à minuit.</summary>
    public int GetToday(string key)
    {
        CheckDailyReset();
        return dailyCounters.ContainsKey(key) ? dailyCounters[key] : 0;
    }

    /// <summary>Incrémente le compteur journalier pour une clé.</summary>
    public void IncrementToday(string key, int amount = 1)
    {
        CheckDailyReset();
        if (!dailyCounters.ContainsKey(key)) dailyCounters[key] = 0;
        dailyCounters[key] += amount;
    }

    private void CheckDailyReset()
    {
        string today = System.DateTime.Now.ToString("yyyy-MM-dd");
        if (lastResetDate == today) return;
        lastResetDate = today;
        dailyCounters.Clear();
    }
}

// =============================================================
// COUNTERKEYS — Constantes string pour les clés de compteurs
// =============================================================
public static class CounterKeys
{
    // ── Kills ─────────────────────────────────────────────────
    public const string KILLS_TOTAL         = "KILLS_TOTAL";
    public const string KILLS_NEUTRAL_MOB   = "KILLS_NEUTRAL_MOB";
    public const string KILLS_FIRE_MOB      = "KILLS_FIRE_MOB";
    public const string KILLS_WATER_MOB     = "KILLS_WATER_MOB";
    public const string KILLS_EARTH_MOB     = "KILLS_EARTH_MOB";
    public const string KILLS_NATURE_MOB    = "KILLS_NATURE_MOB";
    public const string KILLS_LIGHTNING_MOB = "KILLS_LIGHTNING_MOB";
    public const string KILLS_DARKNESS_MOB  = "KILLS_DARKNESS_MOB";
    public const string KILLS_LIGHT_MOB     = "KILLS_LIGHT_MOB";


    // ── Combat ────────────────────────────────────────────────
    public const string DAMAGE_BLOCKED      = "DAMAGE_BLOCKED";
    public const string DAMAGE_DEALT_TOTAL  = "DAMAGE_DEALT_TOTAL";
    public const string DAMAGE_TAKEN_TOTAL  = "DAMAGE_TAKEN_TOTAL";
    public const string SURVIVE_1HP         = "SURVIVE_1HP";
    public const string HEADSHOTS           = "HEADSHOTS";
    public const string BACKSTABS           = "BACKSTABS";
    public const string DEATHS_TOTAL        = "DEATHS_TOTAL";

    // ── Social & Activités ────────────────────────────────────
    public const string PETS_CAPTURED       = "PETS_CAPTURED";
    public const string CRAFTS_TOTAL        = "CRAFTS_TOTAL";
    public const string PLAYERS_MET         = "PLAYERS_MET";
    public const string RESURRECTS_GIVEN    = "RESURRECTS_GIVEN";

    // ── Progression ───────────────────────────────────────────
    public const string PLAYER_LEVEL        = "PLAYER_LEVEL";
    public const string TIME_PLAYED_MINUTES = "TIME_PLAYED_MINUTES";

    // ── Serveur & Social ──────────────────────────────────────
    public const string FIRST_ON_SERVER     = "FIRST_ON_SERVER";

    // ── PvP ───────────────────────────────────────────────────
    public const string DEATHS_OPEN_WORLD   = "DEATHS_OPEN_WORLD";
    public const string PVP_DEATHS          = "PVP_DEATHS";
    public const string PVP_KILLS           = "PVP_KILLS";
    public const string DUELS_WON           = "DUELS_WON";
    public const string DUELS_LOST          = "DUELS_LOST";
    public const string FREE_ARENA_TOP3     = "FREE_ARENA_TOP3";
    public const string ARENA_WINS          = "ARENA_WINS";
    public const string ARENA_LOSSES        = "ARENA_LOSSES";
    public const string BATTLEFIELD_WINS    = "BATTLEFIELD_WINS";
    public const string BATTLEFIELD_LOSSES  = "BATTLEFIELD_LOSSES";

    // ── HdV ───────────────────────────────────────────────────
    public const string HDV_TRANSACTIONS    = "HDV_TRANSACTIONS";
    public const string HDV_CANCELLATIONS   = "HDV_CANCELLATIONS";

    // ── Items ─────────────────────────────────────────────────
    public const string ITEMS_SOLD          = "ITEMS_SOLD";
    public const string AERIS_EARNED_TOTAL  = "AERIS_EARNED_TOTAL";
    public const string POTIONS_USED        = "POTIONS_USED";
    public const string POTIONS_WASTED      = "POTIONS_WASTED";
    public const string ANIMALS_CARESSED    = "ANIMALS_CARESSED";
}
