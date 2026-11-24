using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Rigidbody))]
public class ScooterController : MonoBehaviour
{
    [Header("Velocidad")]
    public float maxSpeed = 8f;        // m/s (~29 km/h)
    public float acceleration = 3f;    // m/s² con el acelerador a fondo
    public float brakeForce = 8f;      // m/s² con el freno a fondo
    public float rollingDrag = 1f;     // m/s² de frenado natural sin pulsar nada

    [Header("Marcha atrás")]
    public float maxReverseSpeed = 2f;     // m/s, más lenta que hacia delante
    public float reverseAcceleration = 1.5f;
    public float stopThreshold = 0.1f;     // por debajo de esto se considera parado

    [Header("Choques")]
    [Tooltip("m/s que la velocidad real puede quedarse por debajo de la deseada antes de considerar que algo nos frena")]
    public float blockedTolerance = 0.5f;

    [Header("Inclinación y giro")]
    public float maxLeanAngle = 20f;   // grados de inclinación máxima
    public float leanSpeed = 2.5f;     // rapidez al inclinarse (0→1 por segundo)
    public float returnSpeed = 1.5f;   // rapidez al volver al centro al soltar
    public float turnSpeed = 70f;      // grados/s de giro con inclinación máxima

    [Header("Cámara")]
    [Tooltip("Parte de la inclinación que nota la cámara: 1 = toda, 0 = nada (horizonte siempre recto)")]
    [Range(0f, 1f)] public float cameraRollFactor = 0.5f;

    [Header("Bordillos")]
    [Tooltip("Altura máxima (m) de un escalón que el patinete sube solo. Lo que sea más alto es una pared")]
    public float maxStepHeight = 0.3f;
    [Tooltip("Parte de la velocidad que se conserva al subir un bordillo")]
    [Range(0f, 1f)] public float stepSpeedKeep = 0.85f;
    [Tooltip("Capas que cuentan como suelo, bordillos y paredes")]
    public LayerMask groundMask = ~0;

    [Header("Caída")]
    [Tooltip("Grados que se tumba el patinete cuando te caes")]
    public float fallenLeanAngle = 80f;
    [Tooltip("Segundos que tarda en tumbarse")]
    public float fallDuration = 0.35f;

    [Header("Referencias")]
    [Tooltip("Hijo con el pivote en el suelo que se inclina (modelo, manillar y cámara)")]
    public Transform leanPivot;
    [Tooltip("Hijo de leanPivot que lleva la cámara; compensa parte de la inclinación")]
    public Transform cameraPivot;

    public float Speed => currentSpeed;    // Útil para un HUD o sonido
    public float Lean => leanAmount;       // -1 izquierda, +1 derecha
    public bool IsRidden { get; private set; }
    public bool IsFallen { get; private set; }

    public event System.Action<float> CurbHit;

    Rigidbody rb;
    CapsuleCollider body;
    InputAction moveAction;
    float currentSpeed;
    float leanAmount;
    float throttleInput, brakeInput, leanInput;

    Vector3 leanPivotRestPosition;
    float fallSide;
    float fallLift;
    float fallProgress;
    float stepHoldTime;

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        body = GetComponent<CapsuleCollider>();
        rb.interpolation = RigidbodyInterpolation.Interpolate;
        rb.constraints = RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;
        if (leanPivot != null) leanPivotRestPosition = leanPivot.localPosition;
    }

    void Start()
    {
        moveAction = InputSystem.actions.FindAction("Player/Move");
    }

    public void SetRidden(bool ridden)
    {
        IsRidden = ridden;
        if (!ridden) throttleInput = brakeInput = leanInput = 0f;
    }

    public void Fall()
    {
        fallSide = ChooseFallSide();
        IsFallen = true;
        fallProgress = 0f;
        currentSpeed = 0f;
        leanAmount = 0f;
        rb.linearVelocity = new Vector3(0f, rb.linearVelocity.y, 0f);
        fallLift = ComputeFallLift();
    }

    public void GetUp() => IsFallen = false;

    void Update()
    {
        ReadInput();
        if (leanPivot == null) return;

        float roll;
        if (IsFallen)
        {
            fallProgress = Mathf.MoveTowards(fallProgress, 1f, Time.deltaTime / Mathf.Max(fallDuration, 0.01f));
            float t = fallProgress * fallProgress;
            roll = fallSide * fallenLeanAngle * t;
            leanPivot.localPosition = leanPivotRestPosition + Vector3.up * (fallLift * t);
        }
        else
        {
            // Inclinación visual: solo rota el hijo, la física no se inclina
            roll = -leanAmount * maxLeanAngle;
            leanPivot.localPosition = leanPivotRestPosition;
        }
        leanPivot.localRotation = Quaternion.Euler(0f, 0f, roll);

        if (cameraPivot != null)
            cameraPivot.localRotation = Quaternion.Euler(0f, 0f, -roll * (1f - cameraRollFactor));
    }

    void ReadInput()
    {
        if (!IsRidden || moveAction == null)
        {
            throttleInput = brakeInput = leanInput = 0f;
            return;
        }

        Vector2 move = moveAction.ReadValue<Vector2>();
        throttleInput = Mathf.Clamp01(move.y);
        brakeInput = Mathf.Clamp01(-move.y);
        leanInput = Mathf.Clamp(move.x, -1f, 1f);
    }

    void FixedUpdate()
    {
        float dt = Time.fixedDeltaTime;

        // Si algo nos frena, currentSpeed baja con la velocidad real (la subida de una rampa no cuenta como frenazo)
        Vector3 vel = rb.linearVelocity;
        float forwardSpeed = Vector3.Dot(vel, transform.forward);
        float climb = Mathf.Max(vel.y, 0f);
        float realSpeed = Mathf.Sign(forwardSpeed) * Mathf.Sqrt(forwardSpeed * forwardSpeed + climb * climb);
        if (currentSpeed > 0f && currentSpeed - realSpeed > blockedTolerance)
            currentSpeed = Mathf.Max(realSpeed, 0f);
        else if (currentSpeed < 0f && realSpeed - currentSpeed > blockedTolerance)
            currentSpeed = Mathf.Min(realSpeed, 0f);

        // 1) Velocidad (positiva = adelante, negativa = marcha atrás)
        if (throttleInput > 0f)
        {
            if (currentSpeed < -stopThreshold)
                currentSpeed += brakeForce * throttleInput * dt;        // adelante yendo hacia atrás = frenar
            else
                currentSpeed += acceleration * throttleInput * dt;      // acelerar hacia delante
        }
        else if (brakeInput > 0f)
        {
            if (currentSpeed > stopThreshold)
                currentSpeed -= brakeForce * brakeInput * dt;           // atrás yendo hacia delante = frenar
            else
                currentSpeed -= reverseAcceleration * brakeInput * dt;  // parado + atrás = marcha atrás
        }
        else
        {
            // Sin pulsar nada: frena solo hacia 0, en ambos sentidos
            currentSpeed = Mathf.MoveTowards(currentSpeed, 0f, rollingDrag * dt);
        }
        currentSpeed = Mathf.Clamp(currentSpeed, -maxReverseSpeed, maxSpeed);

        if (stepHoldTime > 0f)
        {
            stepHoldTime -= dt;
            if (stepHoldTime <= 0f || IsFallen) EndStepHold();
        }
        else
        {
            TryStepUp(dt);
        }

        if (IsFallen)
        {
            rb.angularVelocity = Vector3.zero;
            return;
        }

        // 2) Inclinación (va hacia la entrada o vuelve al centro)
        float rate = leanInput != 0f ? leanSpeed : returnSpeed;
        leanAmount = Mathf.MoveTowards(leanAmount, leanInput, rate * dt);

        // 3) Giro: depende de la inclinación y solo si te mueves
        // Con velocidad negativa el giro se invierte, como en un vehículo real
        float speedFactor = Mathf.Clamp(currentSpeed / (maxSpeed * 0.3f), -1f, 1f);
        float yaw = leanAmount * turnSpeed * speedFactor * dt;
        rb.MoveRotation(rb.rotation * Quaternion.Euler(0f, yaw, 0f));

        // 4) Mover hacia delante conservando la gravedad
        Vector3 v = transform.forward * currentSpeed;
        v.y = stepHoldTime > 0f ? 0f : rb.linearVelocity.y;
        rb.linearVelocity = v;
        rb.angularVelocity = Vector3.zero;
    }

    void EndStepHold()
    {
        stepHoldTime = 0f;
        rb.useGravity = true;
    }

    void TryStepUp(float dt)
    {
        if (body == null || IsFallen || Mathf.Abs(currentSpeed) < 0.05f) return;

        Vector3 dir = transform.forward * Mathf.Sign(currentSpeed);
        float bottom = body.bounds.min.y;
        float reach = body.radius + Mathf.Abs(currentSpeed) * dt + 0.1f;
        Vector3 basePoint = new Vector3(rb.position.x, bottom + 0.04f, rb.position.z);
        float[] offsets = { 0f, -body.radius * 0.7f, body.radius * 0.7f };

        foreach (float offset in offsets)
        {
            Vector3 origin = basePoint + transform.right * offset;
            if (!Physics.Raycast(origin, dir, out RaycastHit side, reach, groundMask, QueryTriggerInteraction.Ignore)) continue;
            if (side.normal.y > 0.7f) continue;

            Vector3 probe = side.point + dir * 0.05f + Vector3.up * (maxStepHeight + 0.04f);
            if (!Physics.Raycast(probe, Vector3.down, out RaycastHit top, maxStepHeight + 0.04f, groundMask, QueryTriggerInteraction.Ignore)) continue;

            float step = top.point.y - bottom;
            if (step < 0.02f || step > maxStepHeight) continue;

            Vector3 normal = side.normal;
            normal.y = 0f;
            float impact = Mathf.Abs(currentSpeed) * Mathf.Max(0f, Vector3.Dot(dir, -normal.normalized));
            CurbHit?.Invoke(impact);
            if (IsFallen) return;

            rb.position += Vector3.up * (step + 0.01f);
            rb.linearVelocity = new Vector3(rb.linearVelocity.x, 0f, rb.linearVelocity.z);
            currentSpeed *= stepSpeedKeep;

            float overEdge = Vector3.Distance(new Vector3(origin.x, 0f, origin.z), new Vector3(side.point.x, 0f, side.point.z)) + 0.1f;
            stepHoldTime = Mathf.Min(overEdge / Mathf.Max(Mathf.Abs(currentSpeed), 0.3f), 0.8f);
            rb.useGravity = false;
            return;
        }
    }

    float ChooseFallSide()
    {
        float preferred = leanAmount > 0.1f ? -1f : leanAmount < -0.1f ? 1f : -1f;
        if (body == null) return preferred;

        Vector3 origin = new Vector3(rb.position.x, body.bounds.min.y + 0.5f, rb.position.z);
        foreach (float side in new[] { preferred, -preferred })
        {
            Vector3 towards = -side * transform.right;
            if (!Physics.Raycast(origin, towards, 1.4f, groundMask, QueryTriggerInteraction.Ignore))
                return side;
        }
        return preferred;
    }

    float ComputeFallLift()
    {
        if (leanPivot == null || body == null) return 0f;

        Quaternion savedRotation = leanPivot.localRotation;
        Vector3 savedPosition = leanPivot.localPosition;
        leanPivot.localRotation = Quaternion.Euler(0f, 0f, fallSide * fallenLeanAngle);
        leanPivot.localPosition = leanPivotRestPosition;

        bool found = false;
        Bounds bounds = default;
        foreach (Renderer r in leanPivot.GetComponentsInChildren<Renderer>())
        {
            if (cameraPivot != null && r.transform.IsChildOf(cameraPivot)) continue;
            if (!found) { bounds = r.bounds; found = true; }
            else bounds.Encapsulate(r.bounds);
        }

        leanPivot.localRotation = savedRotation;
        leanPivot.localPosition = savedPosition;
        if (!found) return 0f;

        float bottom = body.bounds.min.y;
        float ground = bottom;
        Vector3 min = bounds.min, max = bounds.max, c = bounds.center;
        Vector3[] samples =
        {
            new Vector3(min.x, 0f, min.z), new Vector3(min.x, 0f, max.z),
            new Vector3(max.x, 0f, min.z), new Vector3(max.x, 0f, max.z),
            new Vector3(c.x, 0f, c.z),
        };
        foreach (Vector3 s in samples)
        {
            Vector3 origin = new Vector3(s.x, bottom + maxStepHeight + 0.1f, s.z);
            if (Physics.Raycast(origin, Vector3.down, out RaycastHit hit, maxStepHeight + 0.4f, groundMask, QueryTriggerInteraction.Ignore))
                ground = Mathf.Max(ground, hit.point.y);
        }

        return Mathf.Max(0f, ground - min.y + 0.01f);
    }
}
