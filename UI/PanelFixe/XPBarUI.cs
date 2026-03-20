using UnityEngine;
using UnityEngine.UI;
using TMPro;

// =============================================================
// XPBARUI.CS — Barre d'expérience
// AetherTree GDD v21
//
// SETUP HIERARCHY :
//   XPBarPanel
//     ├── XPBar          (Slider)  ← xpBar
//     ├── PlayerLevel    (TMP)     ← levelText   "Level 5"
//     └── PlayerXp       (TMP)     ← xpText      "1250 / 3000"
//
// XP requise via CharacterData.GetXPThreshold(level)
// =============================================================
public class XPBarUI : MonoBehaviour
{
    public static XPBarUI Instance { get; private set; }

    [Header("Références")]
    public Slider          xpBar;
    public TextMeshProUGUI levelText;
    public TextMeshProUGUI xpText;

    private Player _player;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    private void Start()
    {
        _player = FindObjectOfType<Player>();
        if (_player == null) Debug.LogWarning("[XPBarUI] Player introuvable !");
        Refresh();
    }

    private void Update() => Refresh();

    public void Refresh()
    {
        if (_player == null) return;

        int level    = _player.level;
        int current  = _player.xpCombat;
        int required = _player.xpToNextLevel;

        if (xpBar != null)
        {
            xpBar.minValue = 0;
            xpBar.maxValue = required > 0 ? required : 1;
            xpBar.value    = current;
        }

        if (levelText != null) levelText.text = $"Level {level}";
        if (xpText    != null) xpText.text    = $"{current} / {required}";
    }
}
