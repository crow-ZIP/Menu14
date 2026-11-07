using UnityEngine;

public class PoliceOfficer : MonoBehaviour
{
    [Header("Visión")]
    public float viewDistance = 12f;
    [Range(1f, 360f)] public float viewAngle = 70f;   // ángulo total del cono
    public float eyeHeight = 1.6f;
    public LayerMask visionMask = ~0;                 // qué capas bloquean la vista (edificios, coches...)

    [Header("Detección")]
    public float timeToDetect = 1.25f;   // segundos viéndote antes de pillarte
    public float forgetSpeed = 0.5f;     // a qué ritmo baja la barra si te escondes
    public float cooldown = 5f;          // tras pillarte, te ignora este tiempo
    public int suspicionPenalty = 20;
    [Tooltip("Fianza: dinero que se resta del ganado esta noche al pillarte (0 = sin fianza)")]
    public int bailCost = 0;

    public float DetectionProgress => detection / timeToDetect;
    public bool SeesPlayer { get; private set; }

    float detection;
    float cooldownTimer;

    void Update()
    {
        if (cooldownTimer > 0f)
        {
            cooldownTimer -= Time.deltaTime;
            detection = 0f;
            SeesPlayer = false;
            return;
        }

        GameManager gm = GameManager.Instance;
        bool carrying = gm != null && gm.HasActiveOrder && !gm.IsGameOver;

        SeesPlayer = carrying && CanSeePlayer();
        if (SeesPlayer) detection += Time.deltaTime;
        else detection -= forgetSpeed * Time.deltaTime;
        detection = Mathf.Clamp(detection, 0f, timeToDetect);

        if (detection >= timeToDetect) Catch();
    }

    bool CanSeePlayer()
    {
        // 0) Dónde está el jugador ahora: a pie el Player, montado el patinete
        Collider playerCollider = PlayerLocator.Current;
        if (playerCollider == null) return false;

        Vector3 eye = transform.position + Vector3.up * eyeHeight;
        Vector3 toPlayer = playerCollider.bounds.center - eye;
        float dist = toPlayer.magnitude;

        // 1) Distancia
        if (dist > viewDistance) return false;

        // 2) Ángulo (en horizontal)
        Vector3 flat = new Vector3(toPlayer.x, 0f, toPlayer.z);
        if (Vector3.Angle(transform.forward, flat) > viewAngle * 0.5f) return false;

        // 3) Línea de visión: lo primero que toque el rayo tiene que ser el jugador
        //    (un patinete aparcado tapa la vista como cualquier otro objeto)
        if (Physics.Raycast(eye, toPlayer / dist, out RaycastHit hit, dist + 0.5f, visionMask, QueryTriggerInteraction.Ignore))
            return PlayerLocator.IsPlayer(hit.collider);

        return false;
    }

    void Catch()
    {
        detection = 0f;
        cooldownTimer = cooldown;
        if (GameManager.Instance != null)
            GameManager.Instance.PlayerCaught(suspicionPenalty, bailCost);
    }

    // Dibuja el cono en la vista Scene (rojo = te está viendo)
    void OnDrawGizmosSelected()
    {
        Vector3 eye = transform.position + Vector3.up * eyeHeight;
        Gizmos.color = SeesPlayer ? Color.red : Color.yellow;
        Vector3 left = Quaternion.Euler(0f, -viewAngle * 0.5f, 0f) * transform.forward;
        Vector3 right = Quaternion.Euler(0f, viewAngle * 0.5f, 0f) * transform.forward;
        Gizmos.DrawLine(eye, eye + left * viewDistance);
        Gizmos.DrawLine(eye, eye + right * viewDistance);
        Gizmos.DrawLine(eye + left * viewDistance, eye + transform.forward * viewDistance);
        Gizmos.DrawLine(eye + right * viewDistance, eye + transform.forward * viewDistance);
    }
}
