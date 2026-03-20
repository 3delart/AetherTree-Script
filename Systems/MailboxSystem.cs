using UnityEngine;
using System.Collections.Generic;

// =============================================================
// MailMessage — Un message dans la boîte mail du joueur
// AetherTree GDD v21 — Panel Social / Messagerie
//
// Sources :
//   Serveur → récompenses de conditions débloquées
//   Joueur  → messages manuels entre joueurs (Phase suivante)
//
// Flow récompense :
//   UnlockManager.Unlock() → MailboxSystem.SendRewardMail()
//   → Panel Social onglet Messagerie affiche le mail
//   → Joueur clique "Récupérer" → MailboxSystem.ClaimReward()
//   → Reward distribuée selon RewardType
// =============================================================

[System.Serializable]
public class MailMessage
{
    // ── Identité ──────────────────────────────────────────────
    public string         mailID;
    public string         senderName;     // "Serveur AetherTree" ou nom joueur
    public bool           isFromServer;
    public System.DateTime sentAt;

    // ── Contenu ───────────────────────────────────────────────
    public string         subject;
    [TextArea] public string body;

    // ── Récompense attachée ───────────────────────────────────
    public MailReward     reward;         // null = pas de récompense
    public bool           rewardClaimed;  // true = déjà récupérée

    // ── État ──────────────────────────────────────────────────
    public bool           isRead;

    // ── Helpers ───────────────────────────────────────────────
    public bool HasReward        => reward != null && reward.rewardType != RewardType.None;
    public bool CanClaim         => HasReward && !rewardClaimed;
    public string SentDateString => sentAt.ToString("dd/MM/yyyy HH:mm");
}

// =============================================================
// MailReward — Récompense attachée à un mail
// =============================================================
[System.Serializable]
public class MailReward
{
    public RewardType rewardType = RewardType.None;

    // Selon le type :
    public SkillData   rewardSkill;        // Skill + SkillAndTitle + StatBonus (passif)
    public string      rewardTitle;        // Title + SkillAndTitle
    public string      rewardRecipeID;     // Recipe
    public string      rewardItemID;       // Equipment + Resource + Consumable
    public int         rewardItemQuantity = 1;
    public string      rewardDescription;  // Affiché dans le mail
}

// =============================================================
// MailboxSystem — Singleton gérant la boîte mail du joueur
// AetherTree GDD v21 — Panel Social onglet Messagerie
// =============================================================
public class MailboxSystem : MonoBehaviour
{
    public static MailboxSystem Instance { get; private set; }

    private List<MailMessage> messages = new List<MailMessage>();

    private void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    // =========================================================
    // ENVOI
    // =========================================================

    /// <summary>
    /// Envoie un mail de récompense serveur suite au déblocage d'une condition.
    /// Appelé par UnlockManager.Unlock().
    /// </summary>
    public void SendRewardMail(ConditionData condition, ConditionReward condReward)
    {
        string subject = condition.isHidden
            ? "Vous avez accompli quelque chose d'exceptionnel !"
            : $"Récompense débloquée : {condition.displayName}";

        string body = string.IsNullOrEmpty(condition.description)
            ? condReward.rewardDescription
            : $"{condition.description}\n\n{condReward.rewardDescription}";

        var mailReward = new MailReward
        {
            rewardType        = condReward.rewardType,
            rewardSkill       = condReward.rewardSkill,
            rewardTitle       = condReward.rewardTitle,
            rewardRecipeID    = condReward.rewardRecipeID,
            rewardItemID      = condReward.rewardItemID,
            rewardItemQuantity = condReward.rewardItemQuantity,
            rewardDescription  = condReward.rewardDescription,
        };

        var mail = new MailMessage
        {
            mailID       = $"reward_{condition.conditionID}_{System.DateTime.Now.Ticks}",
            senderName   = "Serveur AetherTree",
            isFromServer = true,
            sentAt       = System.DateTime.Now,
            subject      = subject,
            body         = body,
            reward       = mailReward,
            rewardClaimed = false,
            isRead       = false,
        };

        messages.Add(mail);
        Debug.Log($"[MAILBOX] Mail envoyé : {subject}");

        // Notifier l'UI si ouverte
        SocialUI.Instance?.OnNewMail(mail);
    }

    // =========================================================
    // RÉCUPÉRATION DE RÉCOMPENSE
    // =========================================================

    /// <summary>
    /// Distribue la récompense d'un mail et le marque comme récupéré.
    /// Appelé quand le joueur clique "Récupérer" dans le Panel Social.
    /// </summary>
    public bool ClaimReward(string mailID)
    {
        var mail = GetMail(mailID);
        if (mail == null)
        {
            Debug.LogWarning($"[MAILBOX] Mail introuvable : {mailID}");
            return false;
        }
        if (!mail.CanClaim)
        {
            Debug.LogWarning($"[MAILBOX] Récompense déjà récupérée : {mailID}");
            return false;
        }

        Player player = FindObjectOfType<Player>();
        if (player == null)
        {
            Debug.LogWarning("[MAILBOX] Player introuvable — impossible de distribuer la récompense.");
            return false;
        }

        DistributeReward(mail.reward, player);
        mail.rewardClaimed = true;
        mail.isRead        = true;

        Debug.Log($"[MAILBOX] Récompense récupérée : {mail.subject}");
        SocialUI.Instance?.RefreshMail(mail);
        return true;
    }

    private void DistributeReward(MailReward reward, Player player)
    {
        if (reward == null) return;

        switch (reward.rewardType)
        {
            // ── Skill → SkillLibrary ──────────────────────────
            case RewardType.Skill:
                if (reward.rewardSkill != null)
                {
                    player.UnlockSkill(reward.rewardSkill);
                    SkillLibraryUI.Instance?.RefreshIfOpen();
                    Debug.Log($"[MAILBOX] Skill ajouté à la bibliothèque : {reward.rewardSkill.skillName}");
                }
                break;

            // ── Skill + Titre ─────────────────────────────────
            case RewardType.SkillAndTitle:
                if (reward.rewardSkill != null)
                {
                    player.UnlockSkill(reward.rewardSkill);
                    SkillLibraryUI.Instance?.RefreshIfOpen();
                }
                if (!string.IsNullOrEmpty(reward.rewardTitle))
                    Debug.Log($"[MAILBOX] Titre débloqué : {reward.rewardTitle}");
                    // TODO: TitleSystem.Instance?.UnlockTitle(reward.rewardTitle, player)
                break;

            // ── Titre seul ────────────────────────────────────
            case RewardType.Title:
                if (!string.IsNullOrEmpty(reward.rewardTitle))
                    Debug.Log($"[MAILBOX] Titre débloqué : {reward.rewardTitle}");
                    // TODO: TitleSystem.Instance?.UnlockTitle(reward.rewardTitle, player)
                break;

            // ── Équipement → Inventaire ───────────────────────
            case RewardType.Equipment:
                if (!string.IsNullOrEmpty(reward.rewardItemID))
                {
                    Debug.Log($"[MAILBOX] Équipement ajouté à l'inventaire : {reward.rewardItemID}");
                    // TODO: InventorySystem.Instance?.AddEquipment(reward.rewardItemID, player)
                }
                break;

            // ── Ressource / Consommable → Inventaire ──────────
            case RewardType.Resource:
            case RewardType.Consumable:
                if (!string.IsNullOrEmpty(reward.rewardItemID))
                {
                    Debug.Log($"[MAILBOX] Item ×{reward.rewardItemQuantity} ajouté à l'inventaire : {reward.rewardItemID}");
                    // TODO: InventorySystem.Instance?.AddItem(reward.rewardItemID, reward.rewardItemQuantity, player)
                }
                break;

            // ── Recette → Système craft ───────────────────────
            case RewardType.Recipe:
                if (!string.IsNullOrEmpty(reward.rewardRecipeID))
                {
                    Debug.Log($"[MAILBOX] Recette débloquée : {reward.rewardRecipeID}");
                    // TODO: CraftSystem.Instance?.UnlockRecipe(reward.rewardRecipeID, player)
                }
                break;

            // ── StatBonus permanent → SkillLibrary (passif) ───
            case RewardType.StatBonus:
                if (reward.rewardSkill != null)
                {
                    player.UnlockSkill(reward.rewardSkill); // skill de type Permanent
                    SkillLibraryUI.Instance?.RefreshIfOpen();
                    Debug.Log($"[MAILBOX] Bonus permanent débloqué : {reward.rewardSkill.skillName}");
                }
                break;

            case RewardType.None:
            default:
                Debug.Log($"[MAILBOX] Récompense sans distribution : {reward.rewardDescription}");
                break;
        }
    }

        public void RestoreMail(MailMessage mail)
    {
        if (mail == null || string.IsNullOrEmpty(mail.mailID)) return;
 
        // Évite les doublons
        if (messages.Exists(m => m.mailID == mail.mailID)) return;
 
        messages.Add(mail);
    }
 


    // =========================================================
    // ACCESSEURS
    // =========================================================

    public List<MailMessage> GetAllMails()     => messages;
    public int               UnreadCount()     => messages.FindAll(m => !m.isRead).Count;
    public int               UnclaimedCount()  => messages.FindAll(m => m.CanClaim).Count;

    public MailMessage GetMail(string mailID)
        => messages.Find(m => m.mailID == mailID);

    public void MarkAsRead(string mailID)
    {
        var mail = GetMail(mailID);
        if (mail != null) mail.isRead = true;
    }

    public void DeleteMail(string mailID)
    {
        var mail = GetMail(mailID);
        if (mail != null && !mail.CanClaim)
            messages.Remove(mail);
        else if (mail?.CanClaim == true)
            Debug.LogWarning("[MAILBOX] Impossible de supprimer un mail avec récompense non récupérée.");
    }
}