using UnityEngine;
public class SettingsUI : MonoBehaviour
{
    public static SettingsUI Instance { get; private set; }
    public GameObject panel;
    private void Awake() { if (Instance != null) { Destroy(gameObject); return; } Instance = this; if (panel != null) panel.SetActive(false); }
    public void Open()   { if (panel != null) panel.SetActive(true); }
    public void Close()  { if (panel != null) panel.SetActive(false); }
    public void Toggle() { if (panel != null) panel.SetActive(!panel.activeSelf); }
}
