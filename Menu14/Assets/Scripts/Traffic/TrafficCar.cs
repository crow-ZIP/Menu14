using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Coche de tráfico. Va de cruce en cruce por la TrafficNetwork, siempre por su carril (derecha).
///  - Frena si tiene delante otro coche, al jugador o un patinete.
///  - En cada cruce espera a que esté libre (solo cruza un coche a la vez).
///  - Al llegar al borde del mapa, el TrafficSpawner lo vuelve a meter por otra entrada.
/// Lo crea TrafficSpawner; no hace falta ponerlo a mano en la escena.
/// </summary>
[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(BoxCollider))]
public class TrafficCar : MonoBehaviour
{
    [Header("Velocidad")]
    public float maxSpeed = 7f;       // m/s (~25 km/h)
    public float turnSpeed = 3.5f;    // m/s al girar en un cruce
    public float acceleration = 3f;   // m/s²
    public float brakeForce = 8f;     // m/s²

    [Header("Sensor")]
    [Tooltip("Hasta dónde mira hacia delante")]
    public float detectDistance = 7f;
    [Tooltip("Hueco que deja con lo que tiene delante")]
    public float safeGap = 1.5f;
    public float sensorHeight = 0.6f;

    [Header("Atascos")]
    [Tooltip("Si lleva este tiempo parado y el jugador está lejos, reaparece en otro sitio")]
    public float stuckTime = 10f;

    public float Speed => speed;
    /// <summary>Parado en la línea de stop esperando a que se libere el cruce.</summary>
    public bool IsWaitingAtCrossroad { get; private set; }

    const float ReserveDistance = 4f;   // a esta distancia de la línea intenta reservar el cruce

    TrafficNetwork network;
    TrafficSpawner spawner;
    Rigidbody rb;
    BoxCollider box;

    TrafficNetwork.Node from, to, next;   // va de 'from' a 'to' y luego girará hacia 'next'
    TrafficNetwork.Node reserved;
    bool inCrossroad, turning, teleported;
    // Posición "de verdad" del coche. No se lee rb.position: justo después de recolocar el coche
    // el Rigidbody puede devolver todavía la posición vieja y el coche cruzaría la ciudad en línea recta.
    Vector3 position;
    Quaternion rotation;
    readonly List<Vector3> path = new List<Vector3>();
    int pathIndex;
    float speed, stuckTimer;

    static readonly RaycastHit[] hits = new RaycastHit[16];

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        rb.isKinematic = true;   // lo movemos nosotros; empuja al patinete si se le cruza
        rb.useGravity = false;
        rb.interpolation = RigidbodyInterpolation.Interpolate;
        box = GetComponent<BoxCollider>();
    }

    /// <summary>Coloca el coche en el carril from→to, en la posición 'position'.</summary>
    public void Place(TrafficNetwork net, TrafficSpawner owner, TrafficNetwork.Node laneFrom, TrafficNetwork.Node laneTo, Vector3 position)
    {
        network = net;
        spawner = owner;
        ReleaseCrossroad();
        inCrossroad = false;
        speed = 0f;
        stuckTimer = 0f;
        teleported = true;

        Vector3 dir = laneTo.position - laneFrom.position;
        dir.y = 0f;
        this.position = position;
        rotation = Quaternion.LookRotation(dir);

        // Teletransporte limpio: sin interpolación este paso, para que no se dibuje el trayecto
        rb.interpolation = RigidbodyInterpolation.None;
        transform.SetPositionAndRotation(position, rotation);
        rb.position = position;
        rb.rotation = rotation;

        StartSegment(laneFrom, laneTo);
    }

    void StartSegment(TrafficNetwork.Node a, TrafficNetwork.Node b)
    {
        from = a;
        to = b;
        next = to.IsDeadEnd ? null : network.RandomNext(from, to);
        turning = next != null && network.IsTurn(from, to, next);
        path.Clear();
        path.Add(network.LaneEnd(from, to));
        pathIndex = 0;
    }

    void FixedUpdate()
    {
        if (network == null || from == null) return;
        float dt = Time.fixedDeltaTime;
        Vector3 pos = position;
        teleported = false;

        // 0) Atascos: si lleva mucho parado y el jugador está lejos, se recoloca en otro sitio
        stuckTimer = speed < 0.05f ? stuckTimer + dt : 0f;
        if (stuckTimer > stuckTime && spawner != null && spawner.FarFromPlayer(pos))
        {
            spawner.Recycle(this);
            return;
        }

        // 1) Velocidad máxima permitida ahora mismo
        float limit = maxSpeed;
        float obstacle = SenseObstacle();
        if (obstacle - safeGap <= 0f) speed = 0f;          // algo pegado delante: frenazo en seco
        limit = Mathf.Min(limit, StopSpeed(obstacle - safeGap));

        IsWaitingAtCrossroad = false;
        if (inCrossroad)
        {
            if (turning) limit = Mathf.Min(limit, turnSpeed);
        }
        else if (!to.IsDeadEnd)
        {
            float toLine = Vector3.Distance(pos, path[path.Count - 1]);
            bool haveCrossroad = reserved == to || (toLine < ReserveDistance && TryReserve(to));
            if (haveCrossroad)
            {
                if (turning) limit = Mathf.Min(limit, Mathf.Sqrt(turnSpeed * turnSpeed + 2f * brakeForce * toLine));
            }
            else
            {
                limit = Mathf.Min(limit, StopSpeed(toLine));   // parar en la línea de stop
                IsWaitingAtCrossroad = toLine < 1f;
            }
        }

        float rate = limit > speed ? acceleration : brakeForce * 2f;
        speed = Mathf.MoveTowards(speed, limit, rate * dt);

        // 2) Avanzar por los puntos del camino
        float remaining = speed * dt;
        for (int guard = 0; guard < 16; guard++)
        {
            if (pathIndex >= path.Count)
            {
                if (!AdvanceRoute()) break;
                continue;
            }
            Vector3 target = path[pathIndex];
            float d = Vector3.Distance(pos, target);
            if (d > remaining)
            {
                pos += (target - pos) / d * remaining;
                break;
            }
            pos = target;
            remaining -= d;
            pathIndex++;
        }

        // Si el spawner lo acaba de recolocar (salió por un borde del mapa), no tocar nada más:
        // un MovePosition con la posición vieja lo devolvería al borde y cruzaría la ciudad en línea recta.
        if (teleported || !gameObject.activeSelf) return;

        // 3) Mirar hacia donde se mueve
        Vector3 delta = pos - position;
        delta.y = 0f;
        Quaternion rot = rotation;
        if (delta.sqrMagnitude > 0.000001f)
            rot = Quaternion.Slerp(rot, Quaternion.LookRotation(delta), 12f * dt);
        position = pos;
        rotation = rot;
        rb.MovePosition(pos);
        rb.MoveRotation(rot);
        rb.interpolation = RigidbodyInterpolation.Interpolate;   // por si se quitó al recolocarlo
    }

    // Se ha acabado el camino actual: decidir qué viene ahora. Devuelve false si hay que quedarse quieto.
    bool AdvanceRoute()
    {
        if (!inCrossroad)
        {
            // Fin del tramo
            if (to.IsDeadEnd)
            {
                if (spawner != null) spawner.Recycle(this);
                return false;
            }
            if (!TryReserve(to)) return false;   // cruce ocupado: esperar en la línea

            path.Clear();
            pathIndex = 0;
            network.AddCrossingPoints(from, to, next, path);
            inCrossroad = true;
            return true;
        }

        // Fin del cruce: liberarlo y seguir por el tramo siguiente
        ReleaseCrossroad();
        inCrossroad = false;
        StartSegment(to, next);
        return true;
    }

    bool TryReserve(TrafficNetwork.Node node)
    {
        if (node.occupant != null && node.occupant != this) return false;
        node.occupant = this;
        reserved = node;
        return true;
    }

    public void ReleaseCrossroad()
    {
        if (reserved != null && reserved.occupant == this) reserved.occupant = null;
        reserved = null;
    }

    void OnDisable() => ReleaseCrossroad();

    float StopSpeed(float distance) => Mathf.Sqrt(2f * brakeForce * Mathf.Max(0f, distance));

    // Distancia a lo primero que importa delante (float.MaxValue si no hay nada)
    float SenseObstacle()
    {
        Vector3 fwd = transform.forward;
        Vector3 size = box.size;
        Vector3 front = transform.TransformPoint(box.center + Vector3.forward * (size.z * 0.5f - 0.3f));
        front.y = position.y + sensorHeight;
        Vector3 half = new Vector3(size.x * 0.35f, 0.3f, 0.1f);
        float range = inCrossroad ? detectDistance * 0.5f : detectDistance;

        int count = Physics.BoxCastNonAlloc(front, half, fwd, hits, transform.rotation, range, ~0, QueryTriggerInteraction.Ignore);
        float best = float.MaxValue;
        for (int i = 0; i < count; i++)
        {
            Collider c = hits[i].collider;
            if (c.transform.IsChildOf(transform) || !Matters(c)) continue;
            // distance = 0 si ya lo estaba tocando al empezar el barrido
            best = Mathf.Min(best, Mathf.Max(0f, hits[i].distance - 0.3f));
        }
        return best;
    }

    bool Matters(Collider c)
    {
        // El jugador (a pie o en patinete) y los patinetes aparcados en la calzada
        if (PlayerLocator.IsPlayer(c) || c.GetComponentInParent<ScooterController>() != null) return true;

        TrafficCar other = c.GetComponentInParent<TrafficCar>();
        if (other == null) return false;   // edificios, farolas, coches aparcados... no le importan

        // Dentro de un cruce no hace caso a los que esperan en otra línea de stop: si no, se bloquearían mutuamente
        if (inCrossroad && other.IsWaitingAtCrossroad && Vector3.Dot(other.transform.forward, transform.forward) < 0.7f)
            return false;
        return true;
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.green;
        for (int i = pathIndex; i < path.Count; i++)
            Gizmos.DrawWireSphere(path[i] + Vector3.up * 0.3f, 0.2f);
        Gizmos.color = Color.red;
        Vector3 eye = transform.position + Vector3.up * sensorHeight;
        Gizmos.DrawLine(eye, eye + transform.forward * detectDistance);
    }
}
