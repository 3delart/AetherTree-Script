using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;

// =============================================================
// CONSOBARUI.CS — Barre des 3 consommables quickslot
// AetherTree GDD v18
//
// Glisser les 3 GameObjects ConsoSlot1…3 dans slots[].
// Les enfants (ItemIcon, CD, Key) trouvés par nom.
// Utilisation : clic OU F1 / F2 / F3
// =============================================================

public class ConsoBarUI : MonoBehaviour
{
    public static ConsoBarUI Instance { get; private set; }

    [Header("Slots — glisser les 3 GameObjects ici")]
    public GameObject[] slots = new GameObject[3];

    private ConsoSlotBarUI[] _slotUIs = new ConsoSlotBarUI[3];

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;

        for (int i = 0; i < slots.Length; i++)
        {
            if (slots[i] == null) continue;
            Transform t = slots[i].transform;

            ConsoSlotBarUI slot = slots[i].GetComponent<ConsoSlotBarUI>();
            if (slot == null) slot = slots[i].AddComponent<ConsoSlotBarUI>();

            slot.itemIcon     = t.Find("ItemIcon")?.GetComponent<Image>();
            slot.cdOverlay    = t.Find("CD")      ?.GetComponent<Image>();
            slot.keyText      = t.Find("Key")     ?.GetComponent<TextMeshProUGUI>();
            slot.Init();

            _slotUIs[i] = slot;

            // Drop zone — accepte les consommables depuis l'inventaire
            var drop = slots[i].GetComponent<ConsoDropSlot>();
            if (drop == null) drop = slots[i].AddComponent<ConsoDropSlot>();
            drop.slotIndex = i;

            // Clic → utilise le slot
            var btn = slots[i].GetComponent<Button>();
            if (btn != null)
            {
                int captured = i;
                btn.onClick.AddListener(() => TryUseSlot(captured));
            }
        }
    }

    private void Start() => RefreshAll();

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.F1)) TryUseSlot(0);
        if (Input.GetKeyDown(KeyCode.F2)) TryUseSlot(1);
        if (Input.GetKeyDown(KeyCode.F3)) TryUseSlot(2);
    }

    public void TryUseSlot(int index)
    {
        if (index < 0 || index >= _slotUIs.Length || _slotUIs[index] == null) return;
        // TODO : brancher sur InventorySystem
        Debug.Log($"[ConsoBarUI] Slot {index} utilisé (InventorySystem non implémenté).");
    }

    public void RefreshAll()
    {
        for (int i = 0; i < _slotUIs.Length; i++)
            _slotUIs[i]?.SetConso(null, 0);
    }

    public void AssignConso(int index, ConsumableData conso, int quantity)
    {
        if (index >= 0 && index < _slotUIs.Length)
            _slotUIs[index]?.SetConso(conso, quantity);
    }

    public void UpdateQuantity(int index, int quantity)
    {
        if (index >= 0 && index < _slotUIs.Length)
            _slotUIs[index]?.UpdateQuantity(quantity);
    }

    /// <summary>Assigne une ConsumableInstance au slot (appelé par ConsoDropSlot).</summary>
    public void AssignConsoInstance(int index, ConsumableInstance conso)
    {
        if (index < 0 || index >= _slotUIs.Length) return;
        _slotUIs[index]?.SetConsoInstance(conso);
    }
}

// =============================================================
// CONSOSLOTBARUI — attaché automatiquement sur chaque slot
// =============================================================
public class ConsoSlotBarUI : MonoBehaviour
{
    [HideInInspector] public Image           itemIcon;
    [HideInInspector] public Image           cdOverlay;
    [HideInInspector] public TextMeshProUGUI keyText;
    [HideInInspector] public TextMeshProUGUI quantityText;

    private ConsumableData _currentConso;
    public  ConsumableData CurrentConso => _currentConso;

    private static readonly Color EmptyColor  = new Color(0f, 0f, 0f, 0.4f);
    private static readonly Color OnCooldown  = new Color(0f, 0f, 0f, 0.6f);
    private static readonly Color DimmedColor = new Color(0.5f, 0.5f, 0.5f, 1f);

    public void Init()
    {
        if (cdOverlay == null) return;
        cdOverlay.type       = Image.Type.Filled;
        cdOverlay.fillMethod = Image.FillMethod.Radial360;
        cdOverlay.fillAmount = 0f;
        cdOverlay.gameObject.SetActive(false);
    }

    private ConsumableInstance _currentInstance;
    public ConsumableInstance CurrentInstance => _currentInstance;

    /// <summary>Assigne une ConsumableInstance depuis l'inventaire (drag & drop).</summary>
    public void SetConsoInstance(ConsumableInstance instance)
    {
        _currentInstance = instance;
        _currentConso    = instance?.data;

        if (itemIcon != null)
        {
            if (instance == null)
            {
                itemIcon.sprite  = null;
                itemIcon.color   = EmptyColor;
                itemIcon.enabled = false;
            }
            else
            {
                itemIcon.sprite  = instance.Icon;
                itemIcon.color   = Color.white;
                itemIcon.enabled = true;
            }
        }
        UpdateQuantity(instance?.quantity ?? 0);
    }

    public void SetConso(ConsumableData conso, int quantity)
    {
        _currentConso    = conso;
        _currentInstance = conso != null ? conso.CreateInstance(quantity) : null;

        if (itemIcon != null)
        {
            if (conso == null)
            {
                itemIcon.sprite  = null;
                itemIcon.color   = EmptyColor;
                itemIcon.enabled = false;
            }
            else
            {
                itemIcon.sprite  = conso.icon;
                itemIcon.color   = Color.white;
                itemIcon.enabled = true;
            }
        }
        UpdateQuantity(quantity);
    }

    public void UpdateQuantity(int quantity)
    {
        if (quantityText == null) return;
        quantityText.gameObject.SetActive(quantity > 1);
        quantityText.text = quantity > 1 ? $"x{quantity}" : "";
    }

    public void SetCooldown(float remaining, float total)
    {
        bool onCD = remaining > 0f && total > 0f;
        if (cdOverlay != null) { cdOverlay.gameObject.SetActive(onCD); cdOverlay.fillAmount = onCD ? remaining / total : 0f; cdOverlay.color = onCD ? OnCooldown : Color.clear; }
        if (itemIcon  != null && _currentConso != null) itemIcon.color = onCD ? DimmedColor : Color.white;
    }
}

// =============================================================
// CONSODROPSLOT — Drop zone sur chaque slot de la ConsoBar
// Poser automatiquement par ConsoBarUI.Awake() sur chaque slot.
// Accepte un InventoryItem de type ConsumableInstance.
// =============================================================
public class ConsoDropSlot : MonoBehaviour, IDropHandler
{
    [HideInInspector] public int slotIndex = 0;

    private Player _player;

    private void Start() => _player = UnityEngine.Object.FindObjectOfType<Player>();

    public void OnDrop(PointerEventData e)
    {
        var item = InventoryUI.DraggedItem;
        if (item == null) return;

        // Accepte uniquement les consommables
        if (item.ConsumableInstance == null)
        {
            UnityEngine.Debug.Log($"[CONSO DROP] Seuls les consommables peuvent être glissés ici.");
            return;
        }

        var conso = item.ConsumableInstance;
        ConsoBarUI.Instance?.AssignConsoInstance(slotIndex, conso);
        UnityEngine.Debug.Log($"[CONSO DROP] {conso.Name} assigné au slot {slotIndex}.");
    }
}