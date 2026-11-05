using System.Collections;
using TMPro;
using UnityEngine;

/// <summary>
/// Muestra el contador de sospecha. Ponlo en un TextMeshPro - Text (UI) del Canvas de la ciudad.
/// </summary>
public class SuspicionHUD : MonoBehaviour
{
    [SerializeField] private TMP_Text label;

    private IEnumerator Start()
    {
        if (label == null) label = GetComponent<TMP_Text>();

        // Igual que CitySpawner: esperar a que exista el GameManager
        while (GameManager.Instance == null) yield return null;

        GameManager.Instance.OnSuspicionChanged += Refresh;
        Refresh(GameManager.Instance.Suspicion);
    }

    private void OnDestroy()
    {
        if (GameManager.Instance != null)
            GameManager.Instance.OnSuspicionChanged -= Refresh;
    }

    private void Refresh(int value) => label.text = $"Sospecha: {value}";
}
