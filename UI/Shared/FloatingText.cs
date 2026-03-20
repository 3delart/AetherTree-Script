using System.Collections;
using UnityEngine;
using TMPro;

// =============================================================
// FLOATINGTEXT.CS — Textes flottants au-dessus des entités
// AetherTree GDD v18
// =============================================================

public class FloatingText : MonoBehaviour
{
    [Header("Paramètres")]
    public float duration   = 1.2f;
    public float riseSpeed  = 1.5f;
    public float fadeStart  = 0.6f;

    private TextMeshPro _tmp;
    private float       _timer;
    private Color       _color;

    private void Awake()
    {
        _tmp = GetComponent<TextMeshPro>();
        if (_tmp == null) _tmp = gameObject.AddComponent<TextMeshPro>();
        _tmp.alignment  = TextAlignmentOptions.Center;
        _tmp.fontSize   = 4f;
        _tmp.sortingOrder = 10;
    }

    public void Init(string text, Color color)
    {
        _tmp.text  = text;
        _tmp.color = color;
        _color     = color;
        _timer     = 0f;
    }

    private void Update()
    {
        _timer += Time.deltaTime;
        transform.position += Vector3.up * riseSpeed * Time.deltaTime;

        // Toujours face à la caméra
        if (Camera.main != null)
            transform.forward = Camera.main.transform.forward;

        // Fade out
        if (_timer >= fadeStart)
        {
            float alpha = 1f - (_timer - fadeStart) / (duration - fadeStart);
            _tmp.color = new Color(_color.r, _color.g, _color.b, Mathf.Clamp01(alpha));
        }

        if (_timer >= duration)
            Destroy(gameObject);
    }

    // ── API statique ─────────────────────────────────────────
    private static GameObject _prefab;

    /// <summary>Spawne un FloatingText à la position donnée.</summary>
    /// <param name="heightOffset">Décalage vertical depuis worldPos (défaut 1.8f — au-dessus d'un personnage debout).</param>
    public static FloatingText Spawn(string text, Vector3 worldPos, Color color, float heightOffset = 1.8f)
    {
        var go = new GameObject("FloatingText");
        go.transform.position = worldPos + Vector3.up * heightOffset;
        var ft = go.AddComponent<FloatingText>();
        ft.Init(text, color);
        return ft;
    }
}
