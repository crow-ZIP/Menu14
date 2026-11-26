using System.Collections;
using UnityEngine;

// Lives in the City scene. Reads the current night and sets the city up for it.
public class CitySpawner : MonoBehaviour
{
    [SerializeField] private GameObject policePrefab;
    [SerializeField] private Transform[] policeSpawnPoints;
    [Tooltip("Objeto 'Trafico' de la escena. Saca más o menos coches según el trafficDensity de la noche")]
    [SerializeField] private TrafficSpawner traffic;

    private IEnumerator Start()
    {
        // Wait until the Persistent scene is ready (matters when you press Play from City).
        while (GameManager.Instance == null) yield return null;

        NightConfig night = GameManager.Instance.CurrentNight;

        int count = Mathf.Min(night.policeCount, policeSpawnPoints.Length);
        for (int i = 0; i < count; i++)
            Instantiate(policePrefab, policeSpawnPoints[i].position, policeSpawnPoints[i].rotation);

        if (traffic != null) traffic.SpawnCars(night.trafficDensity);

        Debug.Log($"Night {night.nightNumber}: {count} police, traffic {night.trafficDensity:0.0}, {night.deliveries.Count} deliveries.");
    }
}
