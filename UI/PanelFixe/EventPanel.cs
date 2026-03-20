using UnityEngine;
using System.Collections.Generic;
// TODO — À implémenter (voir GDD v18)
public class EventPanel : MonoBehaviour
{
    public static EventPanel Instance { get; private set; }
    private void Awake() { if (Instance != null) { Destroy(gameObject); return; } Instance = this; }
}
