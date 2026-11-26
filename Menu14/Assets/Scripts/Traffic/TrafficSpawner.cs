using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Crea los coches de la ciudad. Lo llama CitySpawner al empezar la noche con el trafficDensity del NightConfig:
///   coches = maxCars * trafficDensity   (Night1 = 0.3, Night2 = 0.6...)
/// En carModels puedes arrastrar directamente los .fbx del CarKit de Kenney: se les añade solo
/// el Rigidbody, el BoxCollider y el TrafficCar. Si arrastras un prefab que ya tenga TrafficCar, se usa tal cual.
/// </summary>
public class TrafficSpawner : MonoBehaviour
{
    [SerializeField] private TrafficNetwork network;

    [Header("Coches")]
    [Tooltip("Modelos de coche (sedan, taxi, suv, van...)")]
    [SerializeField] private GameObject[] carModels;
    [Tooltip("Escala de los modelos de Kenney (los coches aparcados de City están a 1.5)")]
    [SerializeField] private float modelScale = 1.5f;
    [Tooltip("Coches que salen con trafficDensity = 1")]
    [SerializeField] private int maxCars = 25;

    [Header("Dónde aparecen")]
    [Tooltip("Ningún coche aparece ni reaparece más cerca del jugador que esto")]
    [SerializeField] private float minPlayerDistance = 25f;
    [Tooltip("Distancia mínima entre coches al aparecer")]
    [SerializeField] private float minCarSpacing = 8f;

    readonly List<TrafficCar> cars = new List<TrafficCar>();
    public IReadOnlyList<TrafficCar> Cars => cars;

    /// <summary>Crea los coches de esta noche. density va de 0 (sin tráfico) a 1 (maxCars).</summary>
    public void SpawnCars(float density)
    {
        if (network == null || carModels == null || carModels.Length == 0)
        {
            Debug.LogWarning("TrafficSpawner: falta la TrafficNetwork o los modelos de coche", this);
            return;
        }
        if (network.Nodes.Count == 0) network.Build();

        int count = Mathf.RoundToInt(maxCars * Mathf.Clamp01(density));
        for (int i = 0; i < count; i++)
        {
            TrafficCar car = CreateCar(carModels[Random.Range(0, carModels.Length)]);
            if (PlaceOnRandomLane(car)) cars.Add(car);
            else Destroy(car.gameObject);   // no cabe en ningún sitio
        }
        Debug.Log($"Tráfico: {cars.Count} coches (densidad {density:0.00})");
    }

    TrafficCar CreateCar(GameObject model)
    {
        // Prefab ya preparado
        if (model.GetComponent<TrafficCar>() != null)
            return Instantiate(model, transform).GetComponent<TrafficCar>();

        // Modelo suelto (.fbx): envolverlo en un objeto con física
        var root = new GameObject("Coche_" + model.name);
        root.transform.SetParent(transform, false);
        GameObject mesh = Instantiate(model, root.transform);
        mesh.transform.localPosition = Vector3.zero;
        mesh.transform.localRotation = Quaternion.identity;
        mesh.transform.localScale = model.transform.localScale * modelScale;

        // Caja que envuelve el modelo (el root está en el origen, así que el mundo = local)
        Bounds b = new Bounds(root.transform.position, Vector3.zero);
        bool first = true;
        foreach (Renderer r in mesh.GetComponentsInChildren<Renderer>())
        {
            if (first) { b = r.bounds; first = false; }
            else b.Encapsulate(r.bounds);
        }
        var col = root.AddComponent<BoxCollider>();
        col.center = b.center - root.transform.position;
        col.size = b.size;

        root.AddComponent<Rigidbody>();
        return root.AddComponent<TrafficCar>();
    }

    public void RebuildNetwork()
    {
        network.Build();
        foreach (TrafficCar car in cars)
        {
            if (car == null) continue;
            car.gameObject.SetActive(true);
            if (!PlaceOnRandomLane(car)) Recycle(car);
        }
    }

    bool PlaceOnRandomLane(TrafficCar car)
    {
        for (int attempt = 0; attempt < 30; attempt++)
        {
            if (!network.RandomLane(out var from, out var to)) return false;
            Vector3 a = network.LaneStart(from, to);
            Vector3 b = network.LaneEnd(from, to);
            if (Vector3.Distance(a, b) < 2f) continue;   // tramos de cero metros entre dos cruces pegados

            Vector3 p = Vector3.Lerp(a, b, Random.Range(0.1f, 0.9f));
            if (!IsFree(p, car)) continue;
            car.Place(network, this, from, to, p);
            return true;
        }
        return false;
    }

    /// <summary>
    /// Vuelve a meter un coche en la ciudad: lo llama el propio coche al salir por un borde del mapa
    /// o cuando lleva mucho rato atascado. Entra por una salida del mapa libre y lejos del jugador.
    /// </summary>
    public void Recycle(TrafficCar car)
    {
        car.ReleaseCrossroad();

        List<TrafficNetwork.Node> ends = network.Exits;
        int start = Random.Range(0, Mathf.Max(1, ends.Count));
        for (int i = 0; i < ends.Count; i++)
        {
            TrafficNetwork.Node from = ends[(start + i) % ends.Count];
            TrafficNetwork.Node to = from.links[0];
            Vector3 p = network.LaneStart(from, to);
            if (!IsFree(p, car)) continue;
            car.Place(network, this, from, to, p);
            return;
        }

        if (PlaceOnRandomLane(car)) return;

        // Ahora mismo no cabe: se esconde y lo vuelve a intentar en un rato
        car.gameObject.SetActive(false);
        StartCoroutine(RetryLater(car));
    }

    IEnumerator RetryLater(TrafficCar car)
    {
        while (car != null && !car.gameObject.activeSelf)
        {
            yield return new WaitForSeconds(2f);
            if (car == null) yield break;
            car.gameObject.SetActive(true);
            if (!PlaceOnRandomLane(car)) car.gameObject.SetActive(false);
        }
    }

    public bool FarFromPlayer(Vector3 p)
    {
        Collider player = PlayerLocator.Current;
        return player == null || Vector3.Distance(player.transform.position, p) >= minPlayerDistance;
    }

    bool IsFree(Vector3 p, TrafficCar ignore)
    {
        if (!FarFromPlayer(p)) return false;
        if (LaneBlock.Covers(p, 3f)) return false;
        foreach (TrafficCar c in cars)
        {
            if (c == null || c == ignore || !c.gameObject.activeSelf) continue;
            if (Vector3.Distance(c.transform.position, p) < minCarSpacing) return false;
        }
        return true;
    }
}
