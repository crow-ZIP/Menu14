using UnityEngine;

[RequireComponent(typeof(Collider))]
public class SlipPatch : ObstacleBehaviour
{
    public enum Kind { Oil, Glass, Leash }

    public Kind kind = Kind.Oil;

    bool riderInside;

    void OnTriggerEnter(Collider other)
    {
        if (IsRiderCollider(other)) Check();
    }

    void OnTriggerStay(Collider other)
    {
        if (riderInside || !IsRiderCollider(other)) return;
        Check();
    }

    void OnTriggerExit(Collider other)
    {
        if (other.GetComponentInParent<ScooterController>() != null) riderInside = false;
    }

    bool IsRiderCollider(Collider other)
    {
        var scooter = other.GetComponentInParent<ScooterController>();
        return scooter != null && scooter.IsRidden && !scooter.IsFallen;
    }

    void Check()
    {
        if (Config == null || !TryGetRider(out ScooterController rider)) return;

        float speed = Mathf.Abs(rider.Speed);
        bool falls = kind switch
        {
            Kind.Oil => speed >= Config.oilStraightFallSpeed || (speed >= Config.oilTurnFallSpeed && Mathf.Abs(rider.Lean) >= Config.oilMinLean),
            Kind.Glass => speed >= Config.glassFallSpeed,
            _ => speed >= Config.leashFallSpeed,
        };
        if (!falls) return;

        riderInside = true;
        Vector3 forward = rider.transform.forward * (rider.Speed < 0f ? -1f : 1f);
        CrashCause cause = kind switch { Kind.Oil => CrashCause.Oil, Kind.Glass => CrashCause.Glass, _ => CrashCause.Leash };
        KnockOff(rider, cause, forward);
    }
}
