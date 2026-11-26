using UnityEngine;

public enum CrashCause { Pothole, Curb, Obstacle }

[RequireComponent(typeof(ScooterController), typeof(ScooterMount))]
public class ScooterCrash : MonoBehaviour
{
    public CrashConfig config;

    public static event System.Action<CrashCause> Crashed;

    ScooterController scooter;
    ScooterMount mount;
    Collider body;

    void Awake()
    {
        scooter = GetComponent<ScooterController>();
        mount = GetComponent<ScooterMount>();
        body = GetComponent<Collider>();
    }

    void OnEnable() => scooter.CurbHit += HandleCurbHit;
    void OnDisable() => scooter.CurbHit -= HandleCurbHit;

    void OnTriggerEnter(Collider other)
    {
        if (config == null || !scooter.IsRidden) return;
        if (other.GetComponentInParent<Pothole>() == null) return;

        if (Mathf.Abs(scooter.Speed) >= config.potholeFallSpeed) Fall(CrashCause.Pothole, Forward());
    }

    void HandleCurbHit(float impact)
    {
        if (config == null || !scooter.IsRidden) return;
        if (impact >= config.curbFallSpeed) Fall(CrashCause.Curb, Forward());
    }

    void OnCollisionEnter(Collision collision)
    {
        if (config == null || !scooter.IsRidden || body == null) return;

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

    Vector3 Forward() => transform.forward * (scooter.Speed < 0f ? -1f : 1f);

    void Fall(CrashCause cause, Vector3 direction)
    {
        float factor = cause == CrashCause.Obstacle ? config.bounceSpeedFactor : config.launchSpeedFactor;
        float launch = Mathf.Max(Mathf.Abs(scooter.Speed) * factor, config.minLaunchSpeed);
        mount.ThrowOff(config.remountDelay, direction * launch + Vector3.up * config.launchUpSpeed);

        if (GameManager.Instance != null)
            GameManager.Instance.DamageActiveOrder(config.integrityLossPerFall);

        Crashed?.Invoke(cause);
    }
}
