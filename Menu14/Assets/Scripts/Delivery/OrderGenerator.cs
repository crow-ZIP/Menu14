using System.Collections.Generic;
using UnityEngine;

public class OrderGenerator
{
    readonly List<DeliveryPoint> points = new List<DeliveryPoint>();
    readonly HashSet<string> usedDestinations = new HashSet<string>();
    Vector3 origin;

    public int PointCount => points.Count;

    public void Reset()
    {
        usedDestinations.Clear();
        points.Clear();
        points.AddRange(Object.FindObjectsByType<DeliveryPoint>(FindObjectsSortMode.None));

        OrderPickup pickup = Object.FindFirstObjectByType<OrderPickup>();
        origin = pickup != null ? pickup.transform.position : Vector3.zero;
    }

    public void MarkUsed(DeliveryRequest order)
    {
        if (order != null) usedDestinations.Add(order.destinationId);
    }

    public DeliveryRequest Create(DeliveryConfig config, NightConfig night, ICollection<DeliveryRequest> busy)
    {
        points.RemoveAll(p => p == null);
        if (points.Count == 0) return null;

        var busyDestinations = new HashSet<string>();
        var busyNames = new HashSet<string>();
        foreach (DeliveryRequest order in busy)
        {
            busyDestinations.Add(order.destinationId);
            busyNames.Add(order.customerName);
        }

        var candidates = points.FindAll(p => !busyDestinations.Contains(p.destinationId)
                                             && !usedDestinations.Contains(p.destinationId));
        if (candidates.Count == 0)
            candidates = points.FindAll(p => !busyDestinations.Contains(p.destinationId));
        if (candidates.Count == 0) return null;

        DeliveryPoint point = candidates[Random.Range(0, candidates.Count)];
        float distance = RouteDistance(origin, point.transform.position);

        float reward = (config.baseReward + config.rewardPerMeter * distance) * night.rewardMultiplier;
        float time = config.baseTime + config.secondsPerMeter * distance;

        return new DeliveryRequest
        {
            customerName = PickName(config.customerNames, busyNames),
            destinationId = point.destinationId,
            distance = distance,
            reward = RoundTo(reward, config.rewardRounding),
            timeLimit = RoundTo(time, config.timeRounding),
        };
    }

    static float RouteDistance(Vector3 a, Vector3 b) => Mathf.Abs(a.x - b.x) + Mathf.Abs(a.z - b.z);

    static int RoundTo(float value, int step) => Mathf.Max(step, Mathf.RoundToInt(value / step) * step);

    static string PickName(string[] names, HashSet<string> busyNames)
    {
        if (names == null || names.Length == 0) return "Cliente";

        var free = new List<string>();
        foreach (string n in names)
            if (!busyNames.Contains(n)) free.Add(n);

        return free.Count > 0 ? free[Random.Range(0, free.Count)] : names[Random.Range(0, names.Length)];
    }
}
