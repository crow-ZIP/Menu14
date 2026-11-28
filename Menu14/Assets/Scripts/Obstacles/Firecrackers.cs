using System.Collections;
using UnityEngine;

public class Firecrackers : ObstacleBehaviour
{
    [Tooltip("Petardo desactivado que se copia en cada lanzamiento")]
    public GameObject crackerTemplate;
    [Tooltip("Hijo del petardo con la chispa de la mecha")]
    public string sparkName = "Chispa";
    [Tooltip("Hijo del petardo con el fogonazo de la explosión")]
    public string flashName = "Fogonazo";
    [Tooltip("Nochevieja: tiran siempre a la carretera, más a menudo y desde más lejos")]
    public bool towardRoad;

    float nextThrow;

    public override void Setup(ObstacleConfig config, ObstacleSpot spot)
    {
        base.Setup(config, spot);
        nextThrow = Time.time + NextInterval();
        if (crackerTemplate != null) crackerTemplate.SetActive(false);
    }

    void Update()
    {
        if (Config == null || crackerTemplate == null || Time.time < nextThrow) return;
        nextThrow = Time.time + NextInterval();

        float range = towardRoad ? Config.partyFirecrackerRange : Config.firecrackerRange;
        float scatter = towardRoad ? Config.partyFirecrackerScatter : Config.firecrackerScatter;

        Vector3 target;
        if (TryGetRider(out ScooterController rider) && DistanceToRider(rider, transform.position) <= range)
        {
            Vector2 miss = Random.insideUnitCircle * scatter;
            target = ClampThrow(rider.transform.position + new Vector3(miss.x, 0f, miss.y));
        }
        else if (towardRoad)
        {
            target = RandomRoadPoint();
        }
        else
        {
            target = Spot.RandomPointInArea();
        }

        StartCoroutine(Throw(target));
    }

    float NextInterval()
    {
        Vector2 interval = towardRoad ? Config.partyFirecrackerInterval : Config.firecrackerInterval;
        return Random.Range(interval.x, interval.y);
    }

    Vector3 RandomRoadPoint()
    {
        float edge = Spot.area.y * 0.5f;
        var local = new Vector3(Random.Range(-0.5f, 0.5f) * Spot.area.x, 0f, Random.Range(edge + 0.6f, edge + Config.firecrackerRoadReach));
        return ClampThrow(Spot.transform.TransformPoint(local));
    }

    Vector3 ClampThrow(Vector3 world)
    {
        Transform t = Spot.transform;
        Vector3 local = t.InverseTransformPoint(world);
        float reach = Spot.zone == ObstacleZone.Acera ? Config.firecrackerRoadReach : 0f;
        local.x = Mathf.Clamp(local.x, -Spot.area.x * 0.5f, Spot.area.x * 0.5f);
        local.z = Mathf.Clamp(local.z, -Spot.area.y * 0.5f, Spot.area.y * 0.5f + reach);
        local.y = 0f;
        Vector3 p = t.TransformPoint(local);
        float best = float.MaxValue;
        foreach (RaycastHit hit in Physics.RaycastAll(p + Vector3.up * 2f, Vector3.down, 4f, ~0, QueryTriggerInteraction.Ignore))
        {
            Collider c = hit.collider;
            if (c.GetComponentInParent<ScooterController>() != null || c.GetComponentInParent<PlayerMovement>() != null || c.GetComponentInParent<TrafficCar>() != null) continue;
            if (hit.distance < best) { best = hit.distance; p.y = hit.point.y; }
        }
        return p;
    }

    IEnumerator Throw(Vector3 target)
    {
        GameObject cracker = Instantiate(crackerTemplate, target, Quaternion.Euler(0f, Random.Range(0f, 360f), 0f), transform);
        cracker.SetActive(true);
        Transform spark = cracker.transform.Find(sparkName);
        Transform flash = cracker.transform.Find(flashName);
        Light sparkLight = spark != null ? spark.GetComponentInChildren<Light>() : null;
        if (flash != null) flash.gameObject.SetActive(false);

        float fuse = Config.firecrackerFuse;
        for (float t = 0f; t < fuse; t += Time.deltaTime)
        {
            if (spark != null) spark.localScale = Vector3.one * Random.Range(0.6f, 1.2f);
            if (sparkLight != null) sparkLight.intensity = Random.Range(0.5f, 2f);
            yield return null;
        }

        if (spark != null) spark.gameObject.SetActive(false);
        if (flash != null) flash.gameObject.SetActive(true);
        AudioManager.PlayAt(Sfx.Firecracker, target);

        if (TryGetRider(out ScooterController rider) && DistanceToRider(rider, target) <= Config.firecrackerBlastRadius)
        {
            Vector3 away = rider.transform.position - target;
            away.y = 0f;
            KnockOff(rider, CrashCause.Firecracker, away.sqrMagnitude > 0.01f ? away.normalized : rider.transform.forward);
        }

        Light flashLight = flash != null ? flash.GetComponentInChildren<Light>() : null;
        const float flashTime = 0.3f;
        for (float t = 0f; t < flashTime; t += Time.deltaTime)
        {
            float k = t / flashTime;
            if (flash != null) flash.localScale = Vector3.one * Mathf.Lerp(0.3f, 1.6f, k);
            if (flashLight != null) flashLight.intensity = Mathf.Lerp(8f, 0f, k);
            yield return null;
        }

        Destroy(cracker);
    }
}
