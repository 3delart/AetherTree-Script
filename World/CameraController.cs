using UnityEngine;

// =============================================================
// CAMERACONTROLLER.CS — Caméra TPS orbitale
// AetherTree GDD v21
//
// Contrôles :
//   Alt + Clic droit maintenu → rotation (yaw + pitch)
//   Molette                   → zoom
//
// Initialisation :
//   La référence au Player est injectée par SceneLoader.RepositionPlayer()
//   via SetTarget() — jamais assignée manuellement dans l'Inspector.
// =============================================================

public class CameraController : MonoBehaviour
{
    [Header("Target")]
    public Transform target;

    [Header("Distance")]
    public float distance        = 5f;
    public float minDistance     = 2f;
    public float maxDistance     = 15f;
    public float zoomSpeed       = 2f;
    public float zoomSmoothSpeed = 8f;

    [Header("Rotation")]
    public float rotationSpeed    = 5f;
    public float minVerticalAngle = 15f;
    public float maxVerticalAngle = 75f;

    [Header("Zoom Pitch")]
    public float pitchAtMinDistance = 25f;
    public float pitchAtMaxDistance = 55f;

    private float _currentYaw;
    private float _currentPitch  = 35f;
    private float _targetDistance;

    // =========================================================
    // INIT
    // =========================================================

    private void Start()
    {
        _currentYaw     = transform.eulerAngles.y;
        _targetDistance = distance;
        _currentPitch   = Mathf.Clamp(_currentPitch, minVerticalAngle, maxVerticalAngle);
    }

    /// <summary>
    /// Appelé par SceneLoader.RepositionPlayer() après chaque chargement de map.
    /// Injecte la référence Player et réinitialise le yaw sur l'orientation actuelle.
    /// </summary>
    public void SetTarget(Transform t)
    {
        target      = t;
        _currentYaw = transform.eulerAngles.y;
    }

    // =========================================================
    // UPDATE
    // =========================================================

    private void LateUpdate()
    {
        if (target == null) return;

        HandleRotation();
        HandleZoom();
        UpdatePosition();
    }

    // =========================================================
    // ROTATION — Alt + Clic droit
    // =========================================================

    private void HandleRotation()
    {
        if (Input.GetMouseButton(1) && Input.GetKey(KeyCode.LeftAlt))
        {
            float mouseX = Input.GetAxisRaw("Mouse X") * rotationSpeed;
            float mouseY = Input.GetAxisRaw("Mouse Y") * rotationSpeed;

            _currentYaw   += mouseX;
            _currentPitch -= mouseY;
            _currentPitch  = Mathf.Clamp(_currentPitch, minVerticalAngle, maxVerticalAngle);
        }
    }

    // =========================================================
    // ZOOM — Molette
    // =========================================================

    private void HandleZoom()
    {
        float scroll = Input.GetAxisRaw("Mouse ScrollWheel");

        if (Mathf.Abs(scroll) > 0.001f)
        {
            _targetDistance -= scroll * zoomSpeed * 10f;
            _targetDistance  = Mathf.Clamp(_targetDistance, minDistance, maxDistance);

            // Pitch automatique selon distance — seulement si pas en rotation manuelle
            if (!Input.GetMouseButton(1))
            {
                float t           = Mathf.InverseLerp(minDistance, maxDistance, _targetDistance);
                float targetPitch = Mathf.Lerp(pitchAtMinDistance, pitchAtMaxDistance, t);
                _currentPitch     = Mathf.Lerp(_currentPitch, targetPitch, Time.deltaTime * 3f);
                _currentPitch     = Mathf.Clamp(_currentPitch, minVerticalAngle, maxVerticalAngle);
            }
        }

        // Zoom fluide
        distance = Mathf.Lerp(distance, _targetDistance, Time.deltaTime * zoomSmoothSpeed);
    }

    // =========================================================
    // POSITION
    // =========================================================

    private void UpdatePosition()
    {
        // Hauteur dynamique selon zoom
        float heightT       = Mathf.InverseLerp(minDistance, maxDistance, distance);
        float dynamicHeight = Mathf.Lerp(0.5f, 1.8f, heightT);

        Vector3    lookAt   = target.position + Vector3.up * dynamicHeight;
        Quaternion rotation = Quaternion.Euler(_currentPitch, _currentYaw, 0f);
        Vector3    offset   = rotation * new Vector3(0f, 0f, -distance);

        transform.position = lookAt + offset;
        transform.LookAt(lookAt);
    }
}