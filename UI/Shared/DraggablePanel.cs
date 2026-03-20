using UnityEngine;
using UnityEngine.EventSystems;

// =============================================================
// DRAGGABLEPANEL.CS — Panel déplaçable avec sauvegarde position
// AetherTree — à poser sur tout panel secondaire déplaçable
// =============================================================
// SETUP :
//   1. Ajouter ce script sur le GameObject racine du panel
//   2. Assigner dragHandle (zone cliquable pour drag, ex: header)
//      → Si dragHandle est null, le panel entier est draggable
//   3. Le panel doit avoir un RectTransform
//   4. Le Canvas parent doit être en mode "Screen Space - Overlay"
// =============================================================

[RequireComponent(typeof(RectTransform))]
public class DraggablePanel : MonoBehaviour, IPointerDownHandler, IBeginDragHandler, IDragHandler
{
    [Header("Zone de drag (optionnel — null = panel entier)")]
    public RectTransform dragHandle;

    [Header("Contraindre aux bords de l'écran")]
    public bool clampToScreen = true;

    // Clé unique pour PlayerPrefs — générée automatiquement depuis le nom du GameObject
    private string _prefsKey;
    private RectTransform _rect;
    private Canvas _canvas;
    private Vector2 _dragOffset;

    // ─────────────────────────────────────────────
    private void Awake()
    {
        _rect     = GetComponent<RectTransform>();
        _canvas   = GetComponentInParent<Canvas>();
        _prefsKey = "PanelPos_" + gameObject.name;

        LoadPosition();
    }

    // ─────────────────────────────────────────────
    // Mise au premier plan au clic
    public void OnPointerDown(PointerEventData eventData)
    {
        transform.SetAsLastSibling();
    }

    // ─────────────────────────────────────────────
    public void OnBeginDrag(PointerEventData eventData)
    {
        if (!IsDragAllowed(eventData)) return;

        // Offset entre la position du panel et le curseur au moment du clic
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            _canvas.transform as RectTransform,
            eventData.position,
            eventData.pressEventCamera,
            out Vector2 localCursor
        );
        _dragOffset = _rect.anchoredPosition - localCursor;
    }

    // ─────────────────────────────────────────────
    public void OnDrag(PointerEventData eventData)
    {
        if (!IsDragAllowed(eventData)) return;

        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            _canvas.transform as RectTransform,
            eventData.position,
            eventData.pressEventCamera,
            out Vector2 localCursor
        );

        Vector2 targetPos = localCursor + _dragOffset;

        if (clampToScreen)
            targetPos = ClampToCanvas(targetPos);

        _rect.anchoredPosition = targetPos;
        SavePosition();
    }

    // ─────────────────────────────────────────────
    // Vérifie que le drag part bien depuis le dragHandle (si assigné)
    private bool IsDragAllowed(PointerEventData eventData)
    {
        if (dragHandle == null) return true;
        return RectTransformUtility.RectangleContainsScreenPoint(
            dragHandle, eventData.position, eventData.pressEventCamera
        );
    }

    // ─────────────────────────────────────────────
    // Contraint la position dans les limites du Canvas
    private Vector2 ClampToCanvas(Vector2 pos)
    {
        var canvasRect = _canvas.transform as RectTransform;
        Vector2 canvasSize  = canvasRect.sizeDelta;
        Vector2 panelSize   = _rect.sizeDelta;
        Vector2 pivot       = _rect.pivot;

        float minX = -canvasSize.x * 0.5f + panelSize.x * pivot.x;
        float maxX =  canvasSize.x * 0.5f - panelSize.x * (1f - pivot.x);
        float minY = -canvasSize.y * 0.5f + panelSize.y * pivot.y;
        float maxY =  canvasSize.y * 0.5f - panelSize.y * (1f - pivot.y);

        return new Vector2(Mathf.Clamp(pos.x, minX, maxX), Mathf.Clamp(pos.y, minY, maxY));
    }

    // ─────────────────────────────────────────────
    private void SavePosition()
    {
        Vector2 pos = _rect.anchoredPosition;
        PlayerPrefs.SetFloat(_prefsKey + "_x", pos.x);
        PlayerPrefs.SetFloat(_prefsKey + "_y", pos.y);
        PlayerPrefs.Save();
    }

    private void LoadPosition()
    {
        if (!PlayerPrefs.HasKey(_prefsKey + "_x")) return;

        float x = PlayerPrefs.GetFloat(_prefsKey + "_x");
        float y = PlayerPrefs.GetFloat(_prefsKey + "_y");
        _rect.anchoredPosition = new Vector2(x, y);
    }

    // ─────────────────────────────────────────────
    /// <summary>Remet le panel à sa position par défaut et efface la sauvegarde.</summary>
    public void ResetPosition(Vector2 defaultPos)
    {
        _rect.anchoredPosition = defaultPos;
        PlayerPrefs.DeleteKey(_prefsKey + "_x");
        PlayerPrefs.DeleteKey(_prefsKey + "_y");
        PlayerPrefs.Save();
    }
}
