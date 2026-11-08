using System.Collections;
using System.Text;
using TMPro;
using UnityEngine;

/// <summary>
/// Lista de encargos de la noche para el móvil: un pedido por línea con su estado
/// (pendiente, en curso, entregado o perdido). Todo va en un solo texto de TextMeshPro.
/// </summary>
public class OrderListUI : MonoBehaviour
{
    [Tooltip("Opcional: título encima de la lista")]
    [SerializeField] private TMP_Text title;
    [SerializeField] private TMP_Text list;

    [Header("Colores")]
    [SerializeField] private Color pendingColor = Color.white;
    [SerializeField] private Color inProgressColor = new Color(1f, 0.8f, 0.2f);     // amarillo
    [SerializeField] private Color deliveredColor = new Color(0.45f, 0.85f, 0.45f); // verde
    [SerializeField] private Color lostColor = new Color(1f, 0.35f, 0.3f);          // rojo

    private IEnumerator Start()
    {
        // Igual que los otros HUD: esperar a que exista el GameManager
        while (GameManager.Instance == null) yield return null;

        GameManager.Instance.OnOrdersChanged += Refresh;
        Refresh();
    }

    private void OnDestroy()
    {
        if (GameManager.Instance != null)
            GameManager.Instance.OnOrdersChanged -= Refresh;
    }

    private void Refresh()
    {
        GameManager gm = GameManager.Instance;
        var orders = gm.CurrentNight.deliveries;

        if (title != null) title.text = $"Encargos · Noche {gm.CurrentNight.nightNumber}";
        if (list == null) return;

        if (orders.Count == 0)
        {
            list.text = "No hay encargos esta noche";
            return;
        }

        var sb = new StringBuilder();
        for (int i = 0; i < orders.Count; i++)
        {
            DeliveryRequest order = orders[i];
            GameManager.OrderStatus status = gm.GetOrderStatus(i);

            string line = $"{order.customerName} ({order.destinationId}) · {order.reward} €";
            if (status == GameManager.OrderStatus.Delivered) line = $"<s>{line}</s>";   // tachado

            string hex = ColorUtility.ToHtmlStringRGB(ColorFor(status));
            sb.AppendLine($"<color=#{hex}>{line}\n<size=70%>{LabelFor(status)}</size></color>");
        }
        list.text = sb.ToString();
    }

    private Color ColorFor(GameManager.OrderStatus status) => status switch
    {
        GameManager.OrderStatus.InProgress => inProgressColor,
        GameManager.OrderStatus.Delivered => deliveredColor,
        GameManager.OrderStatus.Lost => lostColor,
        _ => pendingColor,
    };

    private static string LabelFor(GameManager.OrderStatus status) => status switch
    {
        GameManager.OrderStatus.InProgress => "EN CURSO",
        GameManager.OrderStatus.Delivered => "ENTREGADO",
        GameManager.OrderStatus.Lost => "PERDIDO",
        _ => "PENDIENTE",
    };
}
