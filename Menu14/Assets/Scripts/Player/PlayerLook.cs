using UnityEngine;
using UnityEngine.InputSystem;

// Movimiento de la cabeza en primera persona.
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
        Cursor.lockState = CursorLockMode.Locked; // ratón fijo en el centro; Esc lo libera en el editor
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

        // El ratón ya da un desplazamiento por fotograma; el joystick da una dirección
        // que hay que escalar por el tiempo transcurrido.
        bool fromMouse = lookAction.activeControl != null && lookAction.activeControl.device is Pointer;
        Vector2 delta = fromMouse ? look * mouseSensitivity : look * gamepadSensitivity * Time.deltaTime;

        transform.Rotate(0f, delta.x, 0f); // yaw: gira todo el jugador

        pitchAngle = Mathf.Clamp(pitchAngle - delta.y, minPitchAngle, maxPitchAngle);
        cameraPivot.localRotation = Quaternion.Euler(pitchAngle, 0f, 0f); // pitch: solo la cámara
    }
}
