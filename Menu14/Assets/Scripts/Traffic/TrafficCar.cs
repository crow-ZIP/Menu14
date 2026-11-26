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

    [Header("Esquivar obstáculos")]
    [Tooltip("Velocidad (m/s) al pasar por el carril contrario para esquivar un obstáculo")]
    public float passSpeed = 4f;
    [Tooltip("Metros libres que necesita en el carril contrario, más allá del obstáculo, para meterse")]
    public float passClearance = 30f;
    [Tooltip("Metros por segundo que se desplaza de lado al cambiar de carril")]
    public float laneChangeRate = 2.5f;
    [Tooltip("A esta distancia (m) del obstáculo empieza a cambiarse de carril si el contrario está libre")]
    public float passStartDistance = 8f;

    public static readonly List<TrafficCar> All = new List<TrafficCar>();

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
    Vector3 lanePosition;
    float sideShift, sideTarget;
    LaneBlock passing;

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
        lanePosition = position;
        sideShift = sideTarget = 0f;
        passing = null;
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
        Vector3 pos = lanePosition;
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

        if (!inCrossroad) limit = Mathf.Min(limit, LaneBlockLimit(pos));

        IsWaitingAtCrossroad = false;
        if (inCrossroad)
        {
            if (turning) limit = Mathf.Min(limit, turnSpeed);
        }
        else if (!to.IsDeadEnd)
        {
            float toLine = Vector3.Distance(pos, path[path.Count - 1]);
            bool haveCrossroad = reserved == to || (toLine < ReserveDistance && ExitClear() && TryReserve(to));
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
        if (inCrossroad) sideTarget = 0f;
        float lateralRate = laneChangeRate * Mathf.Clamp01(speed / passSpeed + 0.25f);
        sideShift = Mathf.MoveTowards(sideShift, sideTarget, lateralRate * dt);
        lanePosition = pos;
        pos += LaneRight() * sideShift;

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
            if (reserved != to && !ExitClear()) return false;
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

    void OnEnable() => All.Add(this);

    void OnDisable()
    {
        All.Remove(this);
        ReleaseCrossroad();
    }

    Vector3 LaneForward()
    {
        Vector3 f = to.position - from.position;
        f.y = 0f;
        return f.normalized;
    }

    Vector3 LaneRight() => Vector3.Cross(Vector3.up, LaneForward());

    float LaneBlockLimit(Vector3 pos)
    {
        Vector3 f = LaneForward();
        float halfCar = box.size.z * 0.5f;

        if (passing != null)
        {
            float along = Vector3.Dot(passing.transform.position - pos, f);
            if (along + passing.HalfLength + halfCar + 1f < 0f)
            {
                passing = null;
                sideTarget = 0f;
                return maxSpeed;
            }
            return passSpeed;
        }

        LaneBlock block = BlockAhead(pos, f, halfCar, out float distance);
        if (block == null) return maxSpeed;

        if (block.canPass && distance <= passStartDistance && OppositeLaneClear(pos, f, block, halfCar))
        {
            passing = block;
            sideTarget = -2f * network.laneOffset;
            return passSpeed;
        }
        return StopSpeed(distance - safeGap);
    }

    bool ExitClear()
    {
        if (next == null) return true;
        Vector3 start = network.LaneStart(to, next);
        if (LaneBlock.Covers(start, 3f)) return false;
        Vector3 f = next.position - to.position;
        f.y = 0f;
        f.Normalize();
        Vector3 r = Vector3.Cross(Vector3.up, f);
        foreach (TrafficCar other in All)
        {
            if (other == this || !other.isActiveAndEnabled || other.speed > 1f) continue;
            Vector3 v = other.position - start;
            float along = Vector3.Dot(v, f);
            if (Mathf.Abs(Vector3.Dot(v, r)) < 1.2f && along > -1f && along < 6f) return false;
        }
        return true;
    }

    LaneBlock BlockAhead(Vector3 pos, Vector3 f, float halfCar, out float distance)
    {
        Vector3 r = Vector3.Cross(Vector3.up, f);
        float segmentLeft = Vector3.Dot(path[path.Count - 1] - pos, f);
        LaneBlock best = null;
        distance = float.MaxValue;
        foreach (LaneBlock block in LaneBlock.Active)
        {
            Vector3 v = block.transform.position - pos;
            if (Mathf.Abs(Vector3.Dot(v, r)) > block.width * 0.5f) continue;
            float along = Vector3.Dot(v, f);
            if (along + block.HalfLength < 0f || along - block.HalfLength > segmentLeft) continue;
            float d = along - block.HalfLength - halfCar;
            if (d < distance) { distance = d; best = block; }
        }
        return best;
    }

    bool OppositeLaneClear(Vector3 pos, Vector3 f, LaneBlock block, float halfCar)
    {
        Vector3 r = Vector3.Cross(Vector3.up, f);
        float blockAlong = Vector3.Dot(block.transform.position - pos, f);
        float behind = -halfCar - 3f;
        float ahead = blockAlong + block.HalfLength + passClearance;
        float oppositeCenter = -2f * network.laneOffset;

        foreach (TrafficCar other in All)
        {
            if (other == this || !other.isActiveAndEnabled) continue;
            Vector3 v = other.position - pos;
            float lateral = Vector3.Dot(v, r);
            float along = Vector3.Dot(v, f);
            if (Mathf.Abs(lateral - oppositeCenter) < 2f && along > behind && along < ahead) return false;
        }

        foreach (LaneBlock other in LaneBlock.Active)
        {
            if (other == block) continue;
            Vector3 v = other.transform.position - pos;
            float lateral = Vector3.Dot(v, r);
            float along = Vector3.Dot(v, f);
            if (Mathf.Abs(lateral - oppositeCenter) < 2f && along > behind && along < blockAlong + block.HalfLength + 8f) return false;
        }

        return to.IsDeadEnd || TryReserve(to);
    }

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
