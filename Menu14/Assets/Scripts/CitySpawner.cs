using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// Lives in the City scene. Reads the current night and sets the city up for it.
public class CitySpawner : MonoBehaviour
{
    [SerializeField] private GameObject policePrefab;
    [SerializeField] private Transform[] policeSpawnPoints;
    [Tooltip("Objeto 'Trafico' de la escena. Saca más o menos coches según el trafficDensity de la noche")]
    [SerializeField] private TrafficSpawner traffic;
    [Tooltip("Coche de policía que circula con el tráfico (patrolCarCount de la noche)")]
    [SerializeField] private GameObject patrolCarPrefab;

    private IEnumerator Start()
    {
        // Wait until the Persistent scene is ready (matters when you press Play from City).
        while (GameManager.Instance == null) yield return null;

        NightConfig night = GameManager.Instance.CurrentNight;

        var points = new List<Transform>();
        foreach (Transform p in policeSpawnPoints)
            if (p != null && p.gameObject.activeInHierarchy) points.Add(p);

        int count = Mathf.Min(night.policeCount, points.Count);
        for (int i = 0; i < count; i++)
        {
            int pick = Random.Range(i, points.Count);
            (points[i], points[pick]) = (points[pick], points[i]);
            Instantiate(policePrefab, points[i].position, points[i].rotation);
        }

        if (traffic != null)
        {
            traffic.SpawnCars(night.trafficDensity);
            traffic.SpawnPatrolCars(patrolCarPrefab, night.patrolCarCount);
        }

        Debug.Log($"Night {night.nightNumber}: {count} police, {night.patrolCarCount} patrol cars, traffic {night.trafficDensity:0.0}, max {night.maxDeliveries} deliveries.");
    }
}
