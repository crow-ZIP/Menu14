using UnityEngine;

public abstract class ObstacleBehaviour : MonoBehaviour
{
    protected ObstacleConfig Config { get; private set; }
    protected ObstacleSpot Spot { get; private set; }

    static ScooterController scooter;
    static ScooterCrash crash;

    public virtual void Setup(ObstacleConfig config, ObstacleSpot spot)
    {
        Config = config;
        Spot = spot;
    }

    protected static bool TryGetRider(out ScooterController rider)
    {
        if (scooter == null)
        {
            scooter = FindFirstObjectByType<ScooterController>();
            crash = scooter != null ? scooter.GetComponent<ScooterCrash>() : null;
        }
        rider = scooter;
        return rider != null && rider.IsRidden && !rider.IsFallen && (crash == null || !crash.IsInvulnerable);
    }

    protected static float DistanceToRider(ScooterController rider, Vector3 point)
    {
        Collider body = rider.GetComponent<Collider>();
        Vector3 closest = body != null ? body.ClosestPoint(point) : rider.transform.position;
        closest.y = point.y = 0f;
        return Vector3.Distance(closest, point);
    }

    protected static void KnockOff(ScooterController rider, CrashCause cause, Vector3 direction)
    {
        var crash = rider.GetComponent<ScooterCrash>();
        if (crash != null) crash.KnockOff(cause, direction);
    }
}
