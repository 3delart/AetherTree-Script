using UnityEngine;

// =============================================================
// ShopUI — Interface de shop PNJ
// AetherTree GDD v21 — Section 14 / 16
//
// Ouvre la fenêtre d'achat d'un marchand PNJ.
// Appelé via DialogueAction.OpenShop dans PNJ.cs.
//
// ⚠ STUB — implémentation complète Phase 7 (PNJ & Quêtes)
// =============================================================

public class ShopUI : MonoBehaviour
{
    public static ShopUI Instance { get; private set; }

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    /// <summary>
    /// Ouvre la fenêtre de shop pour un PNJ marchand.
    /// Affiche shopItems + shopSkills depuis PNJData.
    /// </summary>
    public void OpenShop(PNJData pnjData, Player player)
    {
        // TODO Phase 7 : afficher les items et skills disponibles
        // Vérifier requiredWorldReputationRank par entrée (section 20.2)
        Debug.Log($"[ShopUI] OpenShop — {pnjData?.pnjName} pour {player?.entityName}");
    }

    /// <summary>Ferme la fenêtre de shop.</summary>
    public void CloseShop()
    {
        // TODO Phase 7
        Debug.Log("[ShopUI] CloseShop");
    }
}
