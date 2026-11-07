using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerLook : MonoBehaviour
{
    [Tooltip("La cámara hija del jugador")]
    public Transform cameraPivot;

    [Tooltip("Grados por píxel de movimiento del ratón")]
    public float mouseSensitivity = 0.1f;

    [Tooltip("Grados por segundo con el joystick derecho del mando")]
    public float gamepadSensitivity = 180f;

    public float minPitchAngle = -80f;
    public float maxPitchAngle = 80f;

    private InputAction lookAction;
    private float pitchAngle;

    void Start()
    {
        lookAction = InputSystem.actions.FindAction("Player/Look");
    }

    void OnEnable()
    {
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    void OnDisable()
    {
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    void Update()
    {
        Vector2 look = lookAction.ReadValue<Vector2>();

        // El ratón da píxeles por fotograma; el stick, una dirección que se escala por el tiempo
        bool fromMouse = lookAction.activeControl != null && lookAction.activeControl.device is Pointer;
        Vector2 delta = fromMouse ? look * mouseSensitivity : look * gamepadSensitivity * Time.deltaTime;

        transform.Rotate(0f, delta.x, 0f);

        pitchAngle = Mathf.Clamp(pitchAngle - delta.y, minPitchAngle, maxPitchAngle);
        cameraPivot.localRotation = Quaternion.Euler(pitchAngle, 0f, 0f);
    }
}
