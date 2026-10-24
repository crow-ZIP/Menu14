using System.Collections;
using UnityEngine;

// Lives in the City scene. Reads the current night and sets the city up for it.
public class CitySpawner : MonoBehaviour
{
    [SerializeField] private GameObject policePrefab;
    [SerializeField] private Transform[] policeSpawnPoints;

    private IEnumerator Start()
    {
        // Wait until the Persistent scene is ready (matters when you press Play from City).
        while (GameManager.Instance == null) yield return null;

        NightConfig night = GameManager.Instance.CurrentNight;

        int count = Mathf.Min(night.policeCount, policeSpawnPoints.Length);
        for (int i = 0; i < count; i++)
            Instantiate(policePrefab, policeSpawnPoints[i].position, policeSpawnPoints[i].rotation);

        Debug.Log($"Night {night.nightNumber}: {count} police, {night.deliveries.Count} deliveries.");
    }
}
