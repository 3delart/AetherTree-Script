using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class DeathScreenUI : MonoBehaviour
{
    public static DeathScreenUI Instance;

    [Header("Panel")]
    public GameObject deathPanel;

    [Header("Texts")]
    public TextMeshProUGUI deathText;
    public TextMeshProUGUI timerText;

    void Awake()
    {
        Instance = this;
        if (deathPanel != null)
            deathPanel.SetActive(false);
    }

    public static void Show()
    {
        if (Instance == null) { Debug.LogError("[DEATHUI] Instance null !"); return; }
        if (Instance.deathPanel != null)
            Instance.deathPanel.SetActive(true);
        if (Instance.deathText != null)
            Instance.deathText.text = "Vous êtes mort...";
    }

    public static void Hide()
    {
        if (Instance == null) return;
        if (Instance.deathPanel != null)
            Instance.deathPanel.SetActive(false);
    }

    public static void UpdateTimer(int seconds)
    {
        if (Instance == null) return;
        if (Instance.timerText != null)
            Instance.timerText.text =
                "Respawn dans " + seconds + " secondes";
    }
}