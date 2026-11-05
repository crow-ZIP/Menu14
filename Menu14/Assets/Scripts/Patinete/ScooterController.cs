using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Control arcade del patinete en primera persona.
/// Usa la acción global "Player/Move": adelante = acelerar, atrás = frenar / marcha atrás,
/// izquierda/derecha = inclinarse (la inclinación hace girar). Funciona con teclado y con mando.
/// Solo responde a la entrada mientras alguien va montado (lo decide ScooterMount).
/// </summary>
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

    [Header("Referencias")]
    [Tooltip("Hijo con el pivote en el suelo que se inclina (modelo, manillar y cámara)")]
    public Transform leanPivot;
    [Tooltip("Hijo de leanPivot que lleva la cámara; compensa parte de la inclinación")]
    public Transform cameraPivot;

    public float Speed => currentSpeed;    // Útil para un HUD o sonido
    public float Lean => leanAmount;       // -1 izquierda, +1 derecha
    public bool IsRidden { get; private set; }

    Rigidbody rb;
    InputAction moveAction;
    float currentSpeed;
    float leanAmount;
    float throttleInput, brakeInput, leanInput;

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        rb.interpolation = RigidbodyInterpolation.Interpolate;
        rb.constraints = RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;
    }

    void Start()
    {
        moveAction = InputSystem.actions.FindAction("Player/Move");
    }

    /// <summary>Lo llama ScooterMount al montar (true) y al desmontar (false).</summary>
    public void SetRidden(bool ridden)
    {
        IsRidden = ridden;
        if (!ridden) throttleInput = brakeInput = leanInput = 0f;
    }

    void Update()
    {
        ReadInput();

        // Inclinación visual: solo rota el hijo, la física no se inclina
        float roll = -leanAmount * maxLeanAngle;
        if (leanPivot != null)
            leanPivot.localRotation = Quaternion.Euler(0f, 0f, roll);

        // La cámara deshace parte de la inclinación para no marear
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

        // Valores analógicos: con mando se puede acelerar o inclinarse a medias
        Vector2 move = moveAction.ReadValue<Vector2>();
        throttleInput = Mathf.Clamp01(move.y);
        brakeInput = Mathf.Clamp01(-move.y);
        leanInput = Mathf.Clamp(move.x, -1f, 1f);
    }

    void FixedUpdate()
    {
        float dt = Time.fixedDeltaTime;

        // 0) Si un choque o un obstáculo nos ha frenado de verdad, la velocidad interna baja con él.
        //    Así no se acumula velocidad empujando contra una pared.
        //    En las subidas la velocidad real tiene parte vertical: se cuenta para no frenar en rampas.
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
        v.y = rb.linearVelocity.y;
        rb.linearVelocity = v;
        rb.angularVelocity = Vector3.zero;
    }
}
