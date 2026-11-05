using UnityEngine;
using UnityEngine.InputSystem; // Si usas el Input Manager antiguo, mira el comentario en ReadInput()

/// <summary>
/// Control simple de patinete en primera persona.
/// W = acelerar, S = frenar, A/D = inclinarse (la inclinación hace girar).
/// </summary>
[RequireComponent(typeof(Rigidbody))]
public class ScooterController : MonoBehaviour
{
    [Header("Velocidad")]
    public float maxSpeed = 8f;        // m/s (~29 km/h)
    public float acceleration = 3f;    // m/s² al pulsar W
    public float brakeForce = 8f;      // m/s² al pulsar S
    public float rollingDrag = 1f;     // m/s² de frenado natural sin pulsar nada

    [Header("Marcha atrás")]
    public float maxReverseSpeed = 2f;     // m/s, más lenta que hacia delante
    public float reverseAcceleration = 1.5f;
    public float stopThreshold = 0.1f;     // por debajo de esto se considera parado

    [Header("Inclinación y giro")]
    public float maxLeanAngle = 20f;   // grados de inclinación máxima
    public float leanSpeed = 2.5f;     // rapidez al inclinarse (0→1 por segundo)
    public float returnSpeed = 1.5f;   // rapidez al volver al centro al soltar
    public float turnSpeed = 70f;      // grados/s de giro con inclinación máxima

    [Header("Referencias")]
    public Transform leanPivot;        // Hijo que se inclina (modelo + cámara)

    public float Speed => currentSpeed;    // Útil para un HUD o sonido
    public float Lean => leanAmount;       // -1 izquierda, +1 derecha

    Rigidbody rb;
    float currentSpeed;
    float leanAmount;
    bool throttle, brake;
    float leanInput;

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        rb.interpolation = RigidbodyInterpolation.Interpolate;
        rb.constraints = RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;
    }

    void Update()
    {
        ReadInput();

        // Inclinación visual: solo rota el hijo, la física no se inclina
        if (leanPivot != null)
            leanPivot.localRotation = Quaternion.Euler(0f, 0f, -leanAmount * maxLeanAngle);
    }

    void ReadInput()
    {
        var kb = Keyboard.current;
        if (kb == null) return;

        throttle = kb.wKey.isPressed;
        brake = kb.sKey.isPressed;

        leanInput = 0f;
        if (kb.aKey.isPressed) leanInput -= 1f;
        if (kb.dKey.isPressed) leanInput += 1f;

        // Input Manager antiguo:
        // throttle = Input.GetKey(KeyCode.W);
        // brake = Input.GetKey(KeyCode.S);
        // leanInput = (Input.GetKey(KeyCode.D) ? 1f : 0f) - (Input.GetKey(KeyCode.A) ? 1f : 0f);
    }

    void FixedUpdate()
    {
        float dt = Time.fixedDeltaTime;

        // 1) Velocidad (positiva = adelante, negativa = marcha atrás)
        if (throttle)
        {
            if (currentSpeed < -stopThreshold)
                currentSpeed += brakeForce * dt;        // W yendo hacia atrás = frenar
            else
                currentSpeed += acceleration * dt;      // acelerar hacia delante
        }
        else if (brake)
        {
            if (currentSpeed > stopThreshold)
                currentSpeed -= brakeForce * dt;        // S yendo hacia delante = frenar
            else
                currentSpeed -= reverseAcceleration * dt; // parado + S = marcha atrás
        }
        else
        {
            // Sin teclas: frena solo hacia 0, en ambos sentidos
            currentSpeed = Mathf.MoveTowards(currentSpeed, 0f, rollingDrag * dt);
        }
        currentSpeed = Mathf.Clamp(currentSpeed, -maxReverseSpeed, maxSpeed);

        // 2) Inclinación (va hacia la tecla pulsada o vuelve al centro)
        float rate = leanInput != 0f ? leanSpeed : returnSpeed;
        leanAmount = Mathf.MoveTowards(leanAmount, leanInput, rate * dt);

        // 3) Giro: depende de la inclinación y solo si te mueves
        // Con velocidad negativa el giro se invierte, como en un vehículo real
        float speedFactor = Mathf.Clamp(currentSpeed / (maxSpeed * 0.3f), -1f, 1f);
        float yaw = leanAmount * turnSpeed * speedFactor * dt;
        rb.MoveRotation(rb.rotation * Quaternion.Euler(0f, yaw, 0f));

        // 4) Mover hacia delante conservando la gravedad
        Vector3 v = transform.forward * currentSpeed;
        v.y = rb.linearVelocity.y;          // En Unity 2022 o anterior: rb.velocity
        rb.linearVelocity = v;
        rb.angularVelocity = Vector3.zero;
    }

    void OnCollisionEnter(Collision col)
    {
        // Choque frontal fuerte: pierde velocidad
        Vector3 moveDir = transform.forward * Mathf.Sign(currentSpeed);
        if (Vector3.Dot(col.GetContact(0).normal, moveDir) < -0.7f)
            currentSpeed *= 0.2f;
    }
}