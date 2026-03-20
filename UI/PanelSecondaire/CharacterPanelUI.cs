using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;

// =============================================================
// CHARACTERPANELUI.CS — Panel personnage complet
// Path : Assets/Scripts/UI/CharacterPanelUI.cs
// AetherTree GDD v30
//
// Affiche en temps réel :
//   ① Infos      — nom+titre, level, XP, réputation, HP/Mana, PvP stats
//   ② Attack     — arme, dégâts min/max, précision, grade
//   ③ Defense    — armure, défenses mêlée/distance/magie, grade, esquive
//   ④ Element    — élément actif, rang, affinité %
//   ⑤ Resist     — 7 résistances élémentaires
//   ⑥ Equipment  — icônes des 10 slots d'équipement
//
// Se rafraîchit via GameEventBus (StatsChangedEvent) ou via
// Refresh() appelé manuellement depuis d'autres systèmes.
//
// Setup Unity :
//   Poser ce script sur CharacterPanel.
//   Assigner tous les champs dans l'Inspector.
// =============================================================

public class CharacterPanelUI : MonoBehaviour
{
    public static CharacterPanelUI Instance { get; private set; }

    // ── Références ────────────────────────────────────────────
    private Player          _player;
    private ElementalSystem _elemental;

    // =========================================================
    // ① INFOS
    // =========================================================
    [Header("① Infos")]
    public TextMeshProUGUI nameText;
    public TextMeshProUGUI titleText;
    public TextMeshProUGUI levelText;
    public TextMeshProUGUI xpText;
    public TextMeshProUGUI worldReputationText;
    public TextMeshProUGUI pvpReputationText;
    public TextMeshProUGUI hpText;
    public TextMeshProUGUI manaText;
    public TextMeshProUGUI pvpKillsText;
    public TextMeshProUGUI pvpDeathsText;

    [Header("Bouton fermeture")]
    public UnityEngine.UI.Button closeButton;

    // =========================================================
    // ② ATTACK
    // =========================================================
    [Header("② Attack")]
    public TextMeshProUGUI weaponNameText;
    public TextMeshProUGUI weaponDamageText;
    public TextMeshProUGUI weaponAccuracyText;

    // =========================================================
    // ③ DEFENSE
    // =========================================================
    [Header("③ Defense")]
    public TextMeshProUGUI armorNameText;
    public TextMeshProUGUI meleDefenseText;
    public TextMeshProUGUI rangedDefenseText;
    public TextMeshProUGUI magicDefenseText;
    public TextMeshProUGUI armorDodgeText;

    // =========================================================
    // ④ ELEMENT
    // =========================================================
    [Header("④ Element")]
    [Tooltip("ElementActif — nom de l élément dominant")]
    public TextMeshProUGUI elementActifText;
    [Tooltip("ElementRank — rang 0-5")]
    public TextMeshProUGUI elementRankText;
    [Tooltip("ElementFlat — points élémentaires d équipement (ex: 42 pts)")]
    public TextMeshProUGUI elementFlatText;
    [Tooltip("ElementPourcent — affinité % depuis ElementalSystem (ex: 67%)")]
    public TextMeshProUGUI elementPourcentText;

    // =========================================================
    // ⑤ RÉSISTANCES
    // =========================================================
    [Header("⑤ Résistances (7 éléments)")]
    public TextMeshProUGUI resistNeutralText;
    public TextMeshProUGUI resistFireText;
    public TextMeshProUGUI resistWaterText;
    public TextMeshProUGUI resistEarthText;
    public TextMeshProUGUI resistNatureText;
    public TextMeshProUGUI resistLightningText;
    public TextMeshProUGUI resistDarknessText;
    public TextMeshProUGUI resistLightText;

    // =========================================================
    // ⑥ ÉQUIPEMENTS — icônes des slots
    // =========================================================
    [Header("⑥ Equipment slots (icônes)")]
    public Image slotWeapon;
    public Image slotArmor;
    public Image slotHelmet;
    public Image slotGloves;
    public Image slotBoots;
    public Image slotRing;
    public Image slotNecklace;
    public Image slotBracelet;
    public Image slotSpirit;
    public Image slotCard;    // cosmétique HeadSkin / BodySkin / Card

    // ── Couleur slot vide ─────────────────────────────────────
    private static readonly Color EmptySlotColor = new Color(0.2f, 0.2f, 0.2f, 0.5f);

    // =========================================================
    // INIT
    // =========================================================

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    private void Start()
    {
        _player    = FindObjectOfType<Player>();
        _elemental = _player?.GetComponent<ElementalSystem>();

        if (_player == null)
            Debug.LogWarning("[CharacterPanelUI] Player introuvable !");

        if (closeButton != null)
            closeButton.onClick.AddListener(Close);

        Resubscribe();
        gameObject.SetActive(false); // Panel fermé au démarrage
    }

    private void OnDestroy()
    {
        GameEventBus.OnStatsChanged -= OnStatsChangedHandler;
    }

    /// <summary>
    /// Re-subscribe après GameEventBus.Reset() (changement de map).
    /// Appelé par GameEventBus.Reset() et Start().
    /// </summary>
    public void Resubscribe()
    {
        GameEventBus.OnStatsChanged -= OnStatsChangedHandler; // évite les doublons
        GameEventBus.OnStatsChanged += OnStatsChangedHandler;
    }

    private void OnStatsChangedHandler(StatsChangedEvent e) => Refresh();

    // =========================================================
    // REFRESH COMPLET
    // =========================================================

    public void Refresh()
    {
        // Lazy init — récupère les références si manquantes
        if (_player == null)
            _player = UnityEngine.Object.FindObjectOfType<Player>();
        if (_player == null) return;

        if (_elemental == null)
            _elemental = _player.GetComponent<ElementalSystem>();

        RefreshInfos();
        RefreshAttack();
        RefreshDefense();
        RefreshElement();
        RefreshResistances();
        RefreshEquipmentSlots();
    }

    // =========================================================
    // ① INFOS
    // =========================================================

    private void RefreshInfos()
    {
        SetText(nameText,            _player.entityName);
        SetText(titleText,           _player.activeTitle);
        SetText(levelText,           $"Lv. {_player.level}");
        SetText(xpText,              $"{_player.xpCombat} / {_player.xpToNextLevel} XP");
        SetText(worldReputationText, $"{_player.worldReputation}  (rang {_player.worldReputationRank})");
        SetText(pvpReputationText,   $"{_player.pvpReputation}  (rang {_player.pvpReputationRank})");
        SetText(hpText,              $"{Mathf.CeilToInt(_player.CurrentHP)} / {Mathf.CeilToInt(_player.MaxHP)}");
        SetText(manaText,            $"{Mathf.CeilToInt(_player.CurrentMana)} / {Mathf.CeilToInt(_player.MaxMana)}");

        // PvP stats via ActivityCounter
        var ac = _player.GetActivityCounter();
        if (ac != null)
        {
            SetText(pvpKillsText,  ac.Get("PVP_KILLS").ToString());
            SetText(pvpDeathsText, ac.Get("PVP_DEATHS").ToString());
        }
    }

    // =========================================================
    // ② ATTACK
    // =========================================================

    private void RefreshAttack()
    {
        var w = _player.equippedWeaponInstance;
        if (w == null)
        {
            SetText(weaponNameText,     "—");
            SetText(weaponDamageText,   "—");
            SetText(weaponAccuracyText, "—");
            return;
        }

        SetText(weaponNameText,     w.WeaponName);
        SetText(weaponDamageText,   $"{Mathf.RoundToInt(w.FinalDamageMin)} – {Mathf.RoundToInt(w.FinalDamageMax)}");
        SetText(weaponAccuracyText, $"{Mathf.RoundToInt(w.FinalPrecision)}");
    }

    // =========================================================
    // ③ DEFENSE
    // =========================================================

    private void RefreshDefense()
    {
        var a = _player.equippedArmorInstance;
        SetText(armorNameText, a?.data != null ? a.ArmorName : "—");

        var s = _player?.stats;
        if (s == null) return;
        SetText(meleDefenseText,   $"{Mathf.RoundToInt(s.meleeDefense)}");
        SetText(rangedDefenseText, $"{Mathf.RoundToInt(s.rangedDefense)}");
        SetText(magicDefenseText,  $"{Mathf.RoundToInt(s.magicDefense)}");
        SetText(armorDodgeText,    $"{Mathf.RoundToInt(s.dodge)}");
    }

    // =========================================================
    // ④ ELEMENT
    // =========================================================

    private void RefreshElement()
    {
        if (_elemental == null) return;
        if (_player?.stats == null) return;

        ElementType dominant  = _elemental.GetDominantElement();
        float       affinity  = _elemental.GetAffinity(dominant);
        int         rank      = _elemental.GetElementRank(dominant);
        float       flatPts   = _player.stats.GetElementalPoints(dominant);

        // ElementActif — nom coloré selon l élément
        if (elementActifText != null)
        {
            elementActifText.text  = dominant.GetLabel();
            elementActifText.color = dominant.GetColor();
        }

        SetText(elementRankText,     $"Rang {rank}");
        SetText(elementFlatText,     $"{Mathf.RoundToInt(flatPts)} pts");
        SetText(elementPourcentText, $"{affinity * 100f:F0}%");
    }

    // =========================================================
    // ⑤ RÉSISTANCES
    // =========================================================

    private void RefreshResistances()
    {
        var s = _player?.stats;
        if (s == null) return;
        SetResistText(resistNeutralText,   s.GetResistance(ElementType.Neutral));
        SetResistText(resistFireText,      s.GetResistance(ElementType.Fire));
        SetResistText(resistWaterText,     s.GetResistance(ElementType.Water));
        SetResistText(resistEarthText,     s.GetResistance(ElementType.Earth));
        SetResistText(resistNatureText,    s.GetResistance(ElementType.Nature));
        SetResistText(resistLightningText, s.GetResistance(ElementType.Lightning));
        SetResistText(resistDarknessText,  s.GetResistance(ElementType.Darkness));
        SetResistText(resistLightText,     s.GetResistance(ElementType.Light));
    }

    private void SetResistText(TextMeshProUGUI label, float value)
    {
        if (label == null) return;
        label.text = $"{value * 100f:F0}%";
        // Coloration : vert si résistance, rouge si vulnérabilité
        label.color = value > 0f ? Color.green : value < 0f ? Color.red : Color.white;
    }

    // =========================================================
    // ⑥ ÉQUIPEMENTS
    // =========================================================

    private void RefreshEquipmentSlots()
    {
        if (_player == null) return;
        SetSlotIcon(slotWeapon,   _player.equippedWeaponInstance?.data  != null ? _player.equippedWeaponInstance.Icon  : null);
        SetSlotIcon(slotArmor,    _player.equippedArmorInstance?.data   != null ? _player.equippedArmorInstance.Icon   : null);
        SetSlotIcon(slotHelmet,   _player.equippedHelmetInstance?.data  != null ? _player.equippedHelmetInstance.Icon  : null);
        SetSlotIcon(slotGloves,   _player.equippedGlovesInstance?.data  != null ? _player.equippedGlovesInstance.Icon  : null);
        SetSlotIcon(slotBoots,    _player.equippedBootsInstance?.data   != null ? _player.equippedBootsInstance.Icon   : null);
        SetSlotIcon(slotSpirit,   _player.equippedSpiritInstances?.Count > 0 && _player.equippedSpiritInstances[0]?.data != null
                                  ? _player.equippedSpiritInstances[0].Icon : null);

        // Bijoux
        Sprite ring = null, necklace = null, bracelet = null;
        if (_player.equippedJewelryInstances != null)
        {
            foreach (var j in _player.equippedJewelryInstances)
            {
                if (j == null) continue;
                if (j.Slot == JewelrySlot.Ring)     ring     = j.Icon;
                if (j.Slot == JewelrySlot.Necklace) necklace = j.Icon;
                if (j.Slot == JewelrySlot.Bracelet) bracelet = j.Icon;
            }
        }
        SetSlotIcon(slotRing,     ring);
        SetSlotIcon(slotNecklace, necklace);
        SetSlotIcon(slotBracelet, bracelet);

        slotWeapon.GetComponent<TooltipTrigger>()?.SetItem(_player.equippedWeaponInstance != null? new InventoryItem(_player.equippedWeaponInstance) : null);

        slotArmor.GetComponent<TooltipTrigger>()?.SetItem( _player.equippedArmorInstance != null ? new InventoryItem(_player.equippedArmorInstance) : null);
        slotHelmet.GetComponent<TooltipTrigger>()?.SetItem(_player.equippedHelmetInstance != null ? new InventoryItem(_player.equippedHelmetInstance) : null);
        slotGloves.GetComponent<TooltipTrigger>()?.SetItem(_player.equippedGlovesInstance != null ? new InventoryItem(_player.equippedGlovesInstance) : null);
        slotBoots.GetComponent<TooltipTrigger>()?.SetItem( _player.equippedBootsInstance != null ? new InventoryItem(_player.equippedBootsInstance) : null);
            if (_player.equippedSpiritInstances != null && _player.equippedSpiritInstances.Count > 0)
                slotSpirit.GetComponent<TooltipTrigger>()?.SetItem( new InventoryItem(_player.equippedSpiritInstances[0]) );
    
            if (_player.equippedJewelryInstances != null)
            {
                foreach (var j in _player.equippedJewelryInstances)
                {
                    if (j == null) continue;
                    if (j.Slot == JewelrySlot.Ring)
                        slotRing.GetComponent<TooltipTrigger>()?.SetItem(new InventoryItem(j));
                    if (j.Slot == JewelrySlot.Necklace)
                        slotNecklace.GetComponent<TooltipTrigger>()?.SetItem(new InventoryItem(j));
                    if (j.Slot == JewelrySlot.Bracelet)
                        slotBracelet.GetComponent<TooltipTrigger>()?.SetItem(new InventoryItem(j));
                }
            }
        
    }

    [Tooltip("Sprite affiché dans les slots vides (optionnel — grisé si null)")]
    public Sprite emptySlotSprite;

    private void SetSlotIcon(Image img, Sprite icon)
    {
        if (img == null) return;
        img.enabled = true; // toujours visible
        if (icon == null)
        {
            img.sprite = emptySlotSprite; // sprite vide ou null
            img.color  = EmptySlotColor;
        }
        else
        {
            img.sprite = icon;
            img.color  = Color.white;
        }
    }

    // =========================================================
    // TOGGLE PANEL
    // =========================================================

    public void Toggle()
    {
        bool next = !gameObject.activeSelf;
        gameObject.SetActive(next);
        if (next) Refresh();
    }

    public void Open()  { gameObject.SetActive(true);  Refresh(); }
    public void Close() { gameObject.SetActive(false); }

    // =========================================================
    // UTILITAIRE
    // =========================================================

    private void SetText(TextMeshProUGUI label, string value)
    {
        if (label != null) label.text = value ?? "";
    }
}

// StatsChangedEvent défini dans GameEvents.cs