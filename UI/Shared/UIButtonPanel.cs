using UnityEngine;
using UnityEngine.UI;

// =============================================================
// UIBUTTONPANEL.CS — Panel de boutons d'interface
// AetherTree GDD v21
// =============================================================

public class UIButtonPanel : MonoBehaviour
{
    public static UIButtonPanel Instance { get; private set; }

    [Header("Boutons")]
    public Button inventoryButton;
    public Button characterButton;
    public Button skillLibraryButton;
    public Button questsButton;
    public Button socialsButton;
    public Button settingsButton;
    public Button recipeButton;

    // ─────────────────────────────────────────────────────────
    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    private void Start()
    {
        if (inventoryButton    != null) inventoryButton   .onClick.AddListener(OpenInventory);
        if (characterButton    != null) characterButton   .onClick.AddListener(OpenCharacter);
        if (skillLibraryButton != null) skillLibraryButton.onClick.AddListener(OpenSkillLibrary);
        if (questsButton       != null) questsButton      .onClick.AddListener(OpenQuests);
        if (socialsButton      != null) socialsButton     .onClick.AddListener(OpenSocials);
        if (settingsButton     != null) settingsButton    .onClick.AddListener(OpenSettings);
        if (recipeButton       != null) recipeButton      .onClick.AddListener(OpenRecipe);
    }

    // =========================================================
    // ACTIONS — tout passe par UIManager.TogglePanel()
    // =========================================================

    public void OpenInventory()    => UIManager.Instance?.TogglePanel("Inventory");
    public void OpenCharacter()    => UIManager.Instance?.TogglePanel("Character");
    public void OpenSkillLibrary() => UIManager.Instance?.TogglePanel("SkillLibrary");
    public void OpenQuests()       => UIManager.Instance?.TogglePanel("Quests");
    public void OpenSocials()      => UIManager.Instance?.TogglePanel("Social");
    public void OpenSettings()     => UIManager.Instance?.TogglePanel("Settings");
    public void OpenRecipe()       => UIManager.Instance?.TogglePanel("Recipe");
}