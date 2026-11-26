using UnityEngine;

public enum CrashCause { Pothole, Curb, Obstacle, Oil, Glass, Dog, Firecracker, Leash }

[RequireComponent(typeof(ScooterController), typeof(ScooterMount))]
public class ScooterCrash : MonoBehaviour
{
    public CrashConfig config;

    public static event System.Action<CrashCause> Crashed;

    public bool IsInvulnerable
    {
        get
        {
            UpdateGrace();
            return Time.time < graceUntil;
        }
    }
    public float GraceTimeLeft => Mathf.Max(0f, graceUntil - Time.time);

    ScooterController scooter;
    ScooterMount mount;
    Collider body;
    bool wasRidden;
    bool fellSinceMount;
    float graceUntil;

    void Awake()
    {
        scooter = GetComponent<ScooterController>();
        mount = GetComponent<ScooterMount>();
        body = GetComponent<Collider>();
    }

    void OnEnable() => scooter.CurbHit += HandleCurbHit;
    void OnDisable() => scooter.CurbHit -= HandleCurbHit;

    void Update() => UpdateGrace();

    void UpdateGrace()
    {
        bool ridden = scooter.IsRidden;
        if (ridden && !wasRidden && fellSinceMount && config != null)
        {
            graceUntil = Time.time + config.remountGrace;
            fellSinceMount = false;
        }
        wasRidden = ridden;
    }

    bool CanFall() => config != null && scooter.IsRidden && !IsInvulnerable;

    void OnTriggerEnter(Collider other)
    {
        if (!CanFall()) return;
        Pothole pothole = other.GetComponentInParent<Pothole>();
        if (pothole == null) return;

        float limit = pothole.speedBump ? config.speedBumpFallSpeed : config.potholeFallSpeed;
        if (Mathf.Abs(scooter.Speed) >= limit) Fall(CrashCause.Pothole, Forward());
    }

    void HandleCurbHit(float impact)
    {
        if (!CanFall()) return;
        if (impact >= config.curbFallSpeed) Fall(CrashCause.Curb, Forward());
    }

    void OnCollisionEnter(Collision collision)
    {
        if (!CanFall() || body == null) return;

        float height = collision.collider.bounds.max.y - body.bounds.min.y;
        if (height <= scooter.maxStepHeight) return;

        Vector3 dir = Forward();
        Vector3 center = body.bounds.center;
        for (int i = 0; i < collision.contactCount; i++)
        {
            ContactPoint contact = collision.GetContact(i);
            Vector3 normal = contact.normal;
            if (Mathf.Abs(normal.y) > 0.7f) continue;
            normal.y = 0f;

            Vector3 toContact = contact.point - center;
            toContact.y = 0f;
            if (toContact.sqrMagnitude < 0.0001f || Vector3.Dot(dir, toContact.normalized) < 0.5f) continue;

            float impact = Mathf.Abs(scooter.Speed) * Mathf.Abs(Vector3.Dot(dir, normal.normalized));
            if (impact >= config.obstacleFallSpeed)
            {
                Fall(CrashCause.Obstacle, -dir);
                return;
            }
        }
    }

    public void KnockOff(CrashCause cause, Vector3 direction)
    {
        if (!CanFall() || scooter.IsFallen) return;
        Fall(cause, direction);
    }

    Vector3 Forward() => transform.forward * (scooter.Speed < 0f ? -1f : 1f);

    void Fall(CrashCause cause, Vector3 direction)
    {
        float factor = cause == CrashCause.Obstacle ? config.bounceSpeedFactor : config.launchSpeedFactor;
        float launch = Mathf.Max(Mathf.Abs(scooter.Speed) * factor, config.minLaunchSpeed);
        fellSinceMount = true;
        mount.ThrowOff(config.remountDelay, direction * launch + Vector3.up * config.launchUpSpeed);

        if (GameManager.Instance != null)
            GameManager.Instance.DamageActiveOrder(config.integrityLossPerFall);

        Crashed?.Invoke(cause);
    }
}
