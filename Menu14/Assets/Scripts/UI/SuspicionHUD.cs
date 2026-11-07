using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Muestra la sospecha general de la partida (se mantiene entre noches).
/// El texto y la barra son opcionales. La barra es una Image con Image Type = Filled.
/// </summary>
public class SuspicionHUD : MonoBehaviour
{
    [SerializeField] private TMP_Text label;
    [SerializeField] private Image bar;

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

    private void Refresh(int value)
    {
        int max = GameManager.Instance.MaxSuspicion;
        if (label != null) label.text = $"Sospecha: {value} / {max}";
        if (bar != null) bar.fillAmount = max > 0 ? Mathf.Clamp01((float)value / max) : 0f;
    }
}
