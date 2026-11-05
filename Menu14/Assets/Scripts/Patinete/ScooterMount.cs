using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Montar y desmontar del patinete con la acción global "Player/Interact" (E o botón norte del mando).
/// A pie: si el jugador está cerca, al pulsar se desactiva el Player y se activan la cámara y los brazos del patinete.
/// Montado: si vas casi parado, al pulsar te bajas a un lado libre y vuelve el Player.
/// Solo hay una cámara activa a la vez.
/// </summary>
[RequireComponent(typeof(ScooterController))]
public class ScooterMount : MonoBehaviour
{
    [Header("Jugador a pie")]
    [Tooltip("Si se deja vacío, se busca el objeto con la etiqueta Player al empezar")]
    public GameObject player;

    [Header("Montar")]
    [Tooltip("Distancia máxima (m) entre el jugador y el patinete para poder montar")]
    public float mountDistance = 2f;

    [Header("Desmontar")]
    [Tooltip("Solo puedes bajarte si vas más despacio que esto (m/s)")]
    public float maxDismountSpeed = 1.5f;
    [Tooltip("Distancia (m) desde el centro del patinete a la que aparece el jugador al bajarse")]
    public float dismountOffset = 1.1f;
    [Tooltip("Capas que bloquean el sitio donde aparece el jugador al bajarse")]
    public LayerMask dismountBlockMask = ~0;

    [Header("Objetos del conductor")]
    [Tooltip("Se activan al montar y se desactivan al bajarse (la cámara del patinete y los brazos)")]
    public GameObject[] riderOnly;

    // Medidas del CharacterController del Player (center 0.9, height 1.8, radius 0.35)
    const float PlayerRadius = 0.35f;
    const float PlayerHeight = 1.8f;

    ScooterController scooter;
    Collider scooterCollider;
    InputAction interactAction;

    void Awake()
    {
        scooter = GetComponent<ScooterController>();
        scooterCollider = GetComponent<Collider>();
    }

    void Start()
    {
        interactAction = InputSystem.actions.FindAction("Player/Interact");
        if (player == null) player = GameObject.FindWithTag("Player");
        SetRiderObjects(false);
        scooter.SetRidden(false);
    }

    void Update()
    {
        // WasPressedThisFrame no espera al "Hold" de la acción: responde al pulsar
        if (interactAction == null || !interactAction.WasPressedThisFrame()) return;

        if (scooter.IsRidden) TryDismount();
        else if (CanMount()) Mount();
    }

    bool CanMount()
    {
        if (player == null || !player.activeInHierarchy) return false;
        Vector3 offset = player.transform.position - transform.position;
        offset.y = 0f;
        return offset.magnitude <= mountDistance;
    }

    void Mount()
    {
        // Al desactivar el Player, PlayerLook libera el ratón: hay que volver a bloquearlo después
        player.SetActive(false);
        SetRiderObjects(true);
        scooter.SetRidden(true);
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    void TryDismount()
    {
        if (Mathf.Abs(scooter.Speed) > maxDismountSpeed) return;
        if (!FindDismountPosition(out Vector3 feet)) return;

        scooter.SetRidden(false);
        SetRiderObjects(false);

        // El Player está desactivado, así que se puede mover sin pelearse con su CharacterController
        player.transform.SetPositionAndRotation(feet, Quaternion.Euler(0f, transform.eulerAngles.y, 0f));
        player.SetActive(true);
    }

    /// <summary>Prueba a la izquierda, a la derecha y detrás del patinete, y devuelve el primer hueco libre.</summary>
    bool FindDismountPosition(out Vector3 feet)
    {
        float groundY = scooterCollider != null ? scooterCollider.bounds.min.y : transform.position.y;
        Vector3 center = new Vector3(transform.position.x, groundY, transform.position.z);
        Vector3[] directions = { -transform.right, transform.right, -transform.forward };

        foreach (Vector3 dir in directions)
        {
            Vector3 candidate = center + dir * dismountOffset;
            // Un poco por encima del suelo para que la cápsula no toque el propio suelo
            Vector3 bottom = candidate + Vector3.up * (PlayerRadius + 0.05f);
            Vector3 top = candidate + Vector3.up * (PlayerHeight - PlayerRadius);
            if (!Physics.CheckCapsule(bottom, top, PlayerRadius, dismountBlockMask, QueryTriggerInteraction.Ignore))
            {
                feet = candidate;
                return true;
            }
        }

        feet = default;
        return false;
    }

    void SetRiderObjects(bool active)
    {
        foreach (GameObject go in riderOnly)
            if (go != null) go.SetActive(active);
    }
}
