using UnityEngine;
using TMPro;

// =============================================================
// AERISUI — Affiche le montant d'Aeris du joueur
// Path : Assets/Scripts/UI/AerisUI.cs
// AetherTree GDD v30
//
// Setup Unity :
//   Poser sur AerisPanel.
//   Assigner aerisCountText → AerisCount (TextMeshPro).
// =============================================================
public class AerisUI : MonoBehaviour
{
    public static AerisUI Instance { get; private set; }

    [Header("Texte du montant")]
    public TextMeshProUGUI aerisCountText;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    private void Start()
    {
        if (AerisSystem.Instance != null)
            AerisSystem.Instance.OnAerisChanged += Refresh;

        Refresh(AerisSystem.Instance?.Aeris ?? 0);
    }

    private void OnDestroy()
    {
        if (AerisSystem.Instance != null)
            AerisSystem.Instance.OnAerisChanged -= Refresh;
    }

    public void Refresh(int amount)
    {
        if (aerisCountText != null)
            aerisCountText.text = $"{amount:N0}";
    }
}
