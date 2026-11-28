using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ObstacleSpawner : MonoBehaviour
{
    public ObstacleConfig config;
    [Tooltip("Padre de los puntos (ObstacleSpot) donde pueden salir obstáculos")]
    public Transform spotsRoot;
    [Tooltip("Para rehacer las calles del tráfico si la noche corta alguna (manifestación)")]
    public TrafficSpawner traffic;
    [Tooltip("Solo para probar: si es mayor que 0, saca los obstáculos de esta noche en vez de la actual")]
    [Min(0)] public int testNight;

    public static event System.Action<string, string> Announced;
    public static string LastSender { get; private set; }
    public static string LastMessage { get; private set; }

    readonly List<GameObject> spawned = new List<GameObject>();
    readonly List<Vector3> usedPositions = new List<Vector3>();
    readonly List<Vector3> laneBlockPositions = new List<Vector3>();
    readonly HashSet<ObstacleSpot> usedSpots = new HashSet<ObstacleSpot>();
    List<Vector3> keepAwayPositions;
    GameObject fixedEvent;

    IEnumerator Start()
    {
        while (GameManager.Instance == null) yield return null;
        Spawn(testNight > 0 ? testNight - 1 : GameManager.Instance.CurrentNightIndex);
    }

    public void Spawn(int nightIndex)
    {
        Clear();
        if (config == null || spotsRoot == null) return;

        int night = nightIndex + 1;
        ObstacleConfig.NightEvent nightEvent = config.EventForNight(night);
        var spots = new List<ObstacleSpot>(spotsRoot.GetComponentsInChildren<ObstacleSpot>());

        if (nightEvent != null && nightEvent.fixedPrefab != null)
        {
            fixedEvent = Instantiate(nightEvent.fixedPrefab, Vector3.zero, Quaternion.identity, transform);
            spawned.Add(fixedEvent);
            Physics.SyncTransforms();
            spots.RemoveAll(OverlapsFixedEvent);
            if (fixedEvent.GetComponentInChildren<TrafficBlocker>() != null && traffic != null)
                traffic.RebuildNetwork();
        }

        NightConfig nightConfig = GameManager.Instance != null ? GameManager.Instance.GetNight(nightIndex) : null;
        if (nightConfig != null) PlaceMotoPolice(nightConfig.motoPoliceCount, spots);

        if (nightEvent != null && nightEvent.extraTrafficDensity > 0f && traffic != null)
            traffic.SpawnCars(nightEvent.extraTrafficDensity);

        if (nightEvent != null && nightEvent.extras != null)
            foreach (ObstacleConfig.ExtraObstacle extra in nightEvent.extras)
                for (int i = 0; i < extra.count; i++)
                    if (!TryPlace(extra.prefab, extra.name, extra.zones, spots)) break;

        int perTypeLimit = config.types != null ? config.types.Length : 0;
        var perType = new int[perTypeLimit];
        PlaceKind(ObstacleKind.Evento, config.EventsForNight(nightIndex), night, perType, spots, AnyZone);
        int classics = config.ClassicsForNight(nightIndex);
        int onRoad = Mathf.RoundToInt(classics * config.classicsOnRoadShare);
        int placedOnRoad = PlaceKind(ObstacleKind.Clasico, onRoad, night, perType, spots, ObstacleZone.Calzada | ObstacleZone.Callejon);
        PlaceKind(ObstacleKind.Clasico, classics - placedOnRoad, night, perType, spots, AnyZone);

        Debug.Log($"Noche {night}: {spawned.Count} obstáculos" + (nightEvent != null ? $" (noche especial: {nightEvent.name})" : ""));

        if (nightEvent != null && !string.IsNullOrEmpty(nightEvent.message))
        {
            LastSender = nightEvent.sender;
            LastMessage = nightEvent.message;
            Announced?.Invoke(nightEvent.sender, nightEvent.message);
        }
        else
        {
            LastSender = LastMessage = null;
        }
    }

    void OnDestroy()
    {
        LastSender = LastMessage = null;
    }

    public void Clear()
    {
        foreach (GameObject o in spawned)
            if (o != null) Destroy(o);
        spawned.Clear();
        usedPositions.Clear();
        laneBlockPositions.Clear();
        usedSpots.Clear();
        fixedEvent = null;
    }

    const ObstacleZone AnyZone = ObstacleZone.Acera | ObstacleZone.Callejon | ObstacleZone.Plaza | ObstacleZone.Calzada;

    int PlaceKind(ObstacleKind kind, int wanted, int night, int[] perType, List<ObstacleSpot> spots, ObstacleZone zones)
    {
        if (config.types == null) return 0;
        int placed = 0;
        foreach (ObstacleSpot spot in WeightedOrder(spots))
        {
            if (placed >= wanted) break;
            if ((spot.zone & zones) == 0 || !SpotIsFree(spot)) continue;

            int type = PickType(kind, spot.zone, night, perType);
            if (type < 0) continue;

            ObstacleConfig.ObstacleType t = config.types[type];
            if (!PlaceAt(t.prefab, t.name, spot)) continue;
            perType[type]++;
            placed++;
        }
        return placed;
    }

    void PlaceMotoPolice(int count, List<ObstacleSpot> spots)
    {
        if (count <= 0 || config.motoPolicePrefab == null) return;

        var candidates = spots.FindAll(s => s.zone == ObstacleZone.Acera
                                            && FlatDistance(s.transform.position, config.mapCenter) >= config.motoPoliceMinCenterDistance
                                            && !NearDeliveryOrPickup(s.transform.position));

        int placed = 0;
        while (placed < count && candidates.Count > 0)
        {
            int i = Random.Range(0, candidates.Count);
            ObstacleSpot spot = candidates[i];
            candidates.RemoveAt(i);
            if (SpotIsFree(spot) && PlaceAt(config.motoPolicePrefab, "Policía con moto", spot)) placed++;
        }
    }

    static float FlatDistance(Vector3 a, Vector3 b) => Vector2.Distance(new Vector2(a.x, a.z), new Vector2(b.x, b.z));

    bool NearDeliveryOrPickup(Vector3 position)
    {
        if (keepAwayPositions == null)
        {
            keepAwayPositions = new List<Vector3>();
            foreach (DeliveryPoint p in FindObjectsByType<DeliveryPoint>(FindObjectsSortMode.None)) keepAwayPositions.Add(p.transform.position);
            OrderPickup pickup = FindFirstObjectByType<OrderPickup>();
            if (pickup != null) keepAwayPositions.Add(pickup.transform.position);
        }
        return TooClose(position, keepAwayPositions, config.motoPoliceKeepAway);
    }

    bool TryPlace(GameObject prefab, string label, ObstacleZone zones, List<ObstacleSpot> spots)
    {
        if (prefab == null) return false;
        bool police = prefab.GetComponentInChildren<PoliceOfficer>() != null;
        foreach (ObstacleSpot spot in WeightedOrder(spots))
            if ((spot.zone & zones) != 0 && SpotIsFree(spot) && !(police && NearDeliveryOrPickup(spot.transform.position))
                && PlaceAt(prefab, label, spot)) return true;
        return false;
    }

    bool PlaceAt(GameObject prefab, string label, ObstacleSpot spot)
    {
        bool blocksLane = prefab.GetComponent<LaneBlock>() != null;
        if (blocksLane && TooClose(spot.transform.position, laneBlockPositions, config.minDistanceOnRoad)) return false;

        GameObject obstacle = Instantiate(prefab, spot.transform.position, spot.transform.rotation, transform);
        obstacle.name = $"{label} ({spot.name})";
        foreach (ObstacleBehaviour behaviour in obstacle.GetComponentsInChildren<ObstacleBehaviour>())
            behaviour.Setup(config, spot);

        spawned.Add(obstacle);
        usedSpots.Add(spot);
        usedPositions.Add(spot.transform.position);
        if (blocksLane) laneBlockPositions.Add(spot.transform.position);
        return true;
    }

    bool SpotIsFree(ObstacleSpot spot)
    {
        return !usedSpots.Contains(spot) && !TooClose(spot.transform.position, usedPositions, config.minDistanceBetween);
    }

    bool OverlapsFixedEvent(ObstacleSpot spot)
    {
        Vector3 half = new Vector3(spot.area.x * 0.5f + 1f, 1f, spot.area.y * 0.5f + 1f);
        foreach (Collider c in Physics.OverlapBox(spot.transform.position + Vector3.up, half, spot.transform.rotation, ~0, QueryTriggerInteraction.Collide))
            if (c.transform.IsChildOf(fixedEvent.transform)) return true;
        foreach (TrafficBlocker blocker in fixedEvent.GetComponentsInChildren<TrafficBlocker>())
        {
            Bounds area = blocker.Area;
            area.Expand(new Vector3(4f, 100f, 4f));
            if (area.Contains(spot.transform.position)) return true;
        }
        foreach (LaneBlock block in fixedEvent.GetComponentsInChildren<LaneBlock>())
        {
            Vector3 local = block.transform.InverseTransformPoint(spot.transform.position);
            if (Mathf.Abs(local.x) < block.HalfLength + 30f && Mathf.Abs(local.z) < block.width * 0.5f + 2f) return true;
        }
        return false;
    }

    List<ObstacleSpot> WeightedOrder(List<ObstacleSpot> spots)
    {
        var keyed = new List<(float key, ObstacleSpot spot)>(spots.Count);
        foreach (ObstacleSpot s in spots)
            keyed.Add((-Mathf.Log(Mathf.Max(Random.value, 1e-6f)) / SpotWeight(s), s));
        keyed.Sort((a, b) => a.key.CompareTo(b.key));
        return keyed.ConvertAll(k => k.spot);
    }

    float SpotWeight(ObstacleSpot spot)
    {
        Vector3 d = spot.transform.position - config.mapCenter;
        d.y = 0f;
        float w = Mathf.Exp(-d.magnitude / Mathf.Max(1f, config.centerFalloff));
        if (spot.zone == ObstacleZone.Callejon) w *= config.alleyWeight;
        return Mathf.Max(0.0001f, w);
    }

    int PickType(ObstacleKind kind, ObstacleZone zone, int night, int[] perType)
    {
        float total = 0f;
        for (int i = 0; i < config.types.Length; i++)
            if (Allowed(i, kind, zone, night, perType)) total += config.types[i].weight;
        if (total <= 0f) return -1;

        float roll = Random.value * total;
        for (int i = 0; i < config.types.Length; i++)
        {
            if (!Allowed(i, kind, zone, night, perType)) continue;
            roll -= config.types[i].weight;
            if (roll <= 0f) return i;
        }
        return -1;
    }

    bool Allowed(int i, ObstacleKind kind, ObstacleZone zone, int night, int[] perType)
    {
        ObstacleConfig.ObstacleType t = config.types[i];
        return t.prefab != null && t.weight > 0f && t.kind == kind
            && (t.zones & zone) != 0
            && night >= t.firstNight
            && (t.maxPerNight == 0 || perType[i] < t.maxPerNight);
    }

    static bool TooClose(Vector3 position, List<Vector3> used, float minDistance)
    {
        foreach (Vector3 p in used)
            if (Vector3.Distance(p, position) < minDistance) return true;
        return false;
    }
}
