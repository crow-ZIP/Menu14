using UnityEngine;
using UnityEngine.InputSystem;

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

    // Las mismas medidas que el CharacterController del Player
    const float PlayerRadius = 0.35f;
    const float PlayerHeight = 1.8f;

    public float RemountTimeLeft => Mathf.Max(0f, remountTime - Time.time);
    public float RemountDuration { get; private set; }
    public bool CanMountNow => CanMount();

    const float ThrowLift = 0.2f;

    ScooterController scooter;
    Collider scooterCollider;
    InputAction interactAction;
    float remountTime;

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
        // WasPressedThisFrame no espera al Hold que tiene Interact
        if (interactAction == null || !interactAction.WasPressedThisFrame()) return;

        if (scooter.IsRidden) TryDismount();
        else if (CanMount()) Mount();
    }

    bool CanMount()
    {
        if (player == null || !player.activeInHierarchy) return false;
        if (RemountTimeLeft > 0f) return false;
        Vector3 offset = player.transform.position - transform.position;
        offset.y = 0f;
        return offset.magnitude <= mountDistance;
    }

    void Mount()
    {
        player.SetActive(false);
        SetRiderObjects(true);
        scooter.GetUp();
        scooter.SetRidden(true);
        // PlayerLook libera el ratón en OnDisable
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    void TryDismount()
    {
        if (Mathf.Abs(scooter.Speed) > maxDismountSpeed) return;
        if (!FindDismountPosition(out Vector3 feet)) return;

        Dismount(feet);
    }

    public void ThrowOff(float remountDelay, Vector3 launchVelocity)
    {
        if (!scooter.IsRidden) return;

        Vector3 horizontal = new Vector3(launchVelocity.x, 0f, launchVelocity.z);
        Vector3 ahead = horizontal.sqrMagnitude > 0.01f ? horizontal.normalized : transform.forward;
        Vector3[] directions = { ahead, -transform.right, transform.right, -ahead };

        if (!FindFreeSpot(directions, ThrowLift, out Vector3 feet, out Vector3 side))
        {
            feet = GroundCenter() + Vector3.up * ThrowLift;
            side = ahead;
        }
        if (side != ahead)
            horizontal = (ahead + side).normalized * horizontal.magnitude;

        scooter.Fall();
        Dismount(feet);
        remountTime = Time.time + remountDelay;
        RemountDuration = remountDelay;

        var movement = player.GetComponent<PlayerMovement>();
        if (movement != null) movement.Launch(horizontal + Vector3.up * launchVelocity.y);
    }

    Vector3 GroundCenter()
    {
        float groundY = scooterCollider != null ? scooterCollider.bounds.min.y : transform.position.y;
        return new Vector3(transform.position.x, groundY, transform.position.z);
    }

    void Dismount(Vector3 feet)
    {
        scooter.SetRidden(false);
        SetRiderObjects(false);

        // Se mueve antes de activarlo para que el CharacterController no lo impida
        player.transform.SetPositionAndRotation(feet, Quaternion.Euler(0f, transform.eulerAngles.y, 0f));
        player.SetActive(true);
    }

    bool FindDismountPosition(out Vector3 feet)
    {
        Vector3[] directions = { -transform.right, transform.right, -transform.forward };
        return FindFreeSpot(directions, 0f, out feet, out _);
    }

    bool FindFreeSpot(Vector3[] directions, float lift, out Vector3 feet, out Vector3 chosen)
    {
        Vector3 center = GroundCenter() + Vector3.up * lift;

        foreach (Vector3 dir in directions)
        {
            Vector3 candidate = center + dir * dismountOffset;
            Vector3 bottom = candidate + Vector3.up * (PlayerRadius + 0.05f);
            Vector3 top = candidate + Vector3.up * (PlayerHeight - PlayerRadius);
            if (!Physics.CheckCapsule(bottom, top, PlayerRadius, dismountBlockMask, QueryTriggerInteraction.Ignore))
            {
                feet = candidate;
                chosen = dir;
                return true;
            }
        }

        feet = default;
        chosen = default;
        return false;
    }

    void SetRiderObjects(bool active)
    {
        foreach (GameObject go in riderOnly)
            if (go != null) go.SetActive(active);
    }
}
