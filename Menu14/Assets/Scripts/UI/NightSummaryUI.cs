using System.Collections;
using TMPro;
using UnityEngine;

/// <summary>
/// Pantalla de resumen entre noches (escena NightSummary).
/// Enseña lo ganado, los intereses, lo que ha ido a la deuda y los encargos de la noche que acaba de terminar.
/// Los botones llaman a SceneButtons: "Siguiente noche" → GoToCity, "Salir al menú" → BackToMenu.
/// </summary>
public class NightSummaryUI : MonoBehaviour
{
    [SerializeField] private TMP_Text title;
    [SerializeField] private TMP_Text details;
    [SerializeField] private Color surplusColor = new Color(0.45f, 0.85f, 0.45f);   // verde

    private IEnumerator Start()
    {
        // Esperar al GameManager (por si se hace Play directamente en esta escena)
        while (GameManager.Instance == null) yield return null;
        Refresh(GameManager.Instance);
    }

    private void Refresh(GameManager gm)
    {
        if (title != null) title.text = $"Noche {gm.LastNightNumber} completada";
        if (details == null) return;

        string green = ColorUtility.ToHtmlStringRGB(surplusColor);
        details.text =
            $"Ganado esta noche: {gm.LastNightEarned} €\n" +
            $"Intereses: -{gm.LastNightInterest} €\n" +
            $"A la deuda: <color=#{green}>+{gm.LastNightSurplus} €</color>\n\n" +
            $"Deuda pagada: {gm.TotalMoney} / {gm.TotalMoneyGoal} €\n" +
            $"<size=80%>Encargos: {gm.LastNightDelivered} entregados · {gm.LastNightLost} perdidos</size>";
    }
}
