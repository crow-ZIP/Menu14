using System.Collections.Generic;
using TMPro;
using UnityEngine;

/// <summary>
/// Interfaz del ordenador del restaurante. Muestra los encargos de la noche, uno por fila,
/// y deja aceptar los pendientes. Ponlo en la raíz del prefab PC_Pedidos y arrastra ese objeto
/// al campo "Orders Screen" del OrderTerminal (PantallaPedidos): él se encarga de abrirlo y cerrarlo.
/// </summary>
public class OrderScreenUI : MonoBehaviour
{
    [Tooltip("Opcional: el OrderTerminal que abre esta pantalla. Si se deja vacío, se busca solo")]
    [SerializeField] private OrderTerminal terminal;

    [SerializeField] private TMP_Text title;
    [Tooltip("Línea debajo del título que explica si se puede aceptar un encargo ahora")]
    [SerializeField] private TMP_Text notice;
    [Tooltip("Donde se crean las filas (con un Vertical Layout Group)")]
    [SerializeField] private Transform rowsParent;
    [Tooltip("Fila de ejemplo, desactivada. Se copia una vez por encargo")]
    [SerializeField] private OrderRowUI rowTemplate;
    [SerializeField] private UnityEngine.UI.Button closeButton;
    [Tooltip("Botón para terminar la noche. Solo se puede pulsar si no quedan encargos o ya están pagados los intereses")]
    [SerializeField] private UnityEngine.UI.Button endNightButton;

    [Header("Colores")]
    [SerializeField] private Color pendingColor = Color.white;
    [SerializeField] private Color acceptedColor = new Color(0.45f, 0.75f, 1f);     // azul
    [SerializeField] private Color inProgressColor = new Color(1f, 0.8f, 0.2f);     // amarillo
    [SerializeField] private Color deliveredColor = new Color(0.45f, 0.85f, 0.45f); // verde
    [SerializeField] private Color lostColor = new Color(1f, 0.35f, 0.3f);          // rojo

    private readonly List<OrderRowUI> rows = new List<OrderRowUI>();

    private void Awake()
    {
        if (terminal == null) terminal = GetComponentInParent<OrderTerminal>();
        if (terminal == null) terminal = FindAnyObjectByType<OrderTerminal>();
        if (rowTemplate != null) rowTemplate.gameObject.SetActive(false);
        if (closeButton != null) closeButton.onClick.AddListener(Close);
        if (endNightButton != null) endNightButton.onClick.AddListener(EndNight);
    }

    // La pantalla está desactivada casi siempre: se apunta al evento solo mientras se ve.
    private void OnEnable()
    {
        if (GameManager.Instance == null) return;
        GameManager.Instance.OnOrdersChanged += Refresh;
        Refresh();
    }

    private void OnDisable()
    {
        if (GameManager.Instance != null)
            GameManager.Instance.OnOrdersChanged -= Refresh;
    }

    private void Refresh()
    {
        GameManager gm = GameManager.Instance;
        var orders = gm.CurrentNight.deliveries;

        if (title != null) title.text = $"Encargos · Noche {gm.CurrentNight.nightNumber}";
        if (notice != null) notice.text = NoticeText(gm);
        if (endNightButton != null) endNightButton.interactable = gm.CanEndNight;

        // Una fila por encargo: se reutilizan las que ya existen y se crean las que falten
        while (rows.Count < orders.Count)
        {
            OrderRowUI row = Instantiate(rowTemplate, rowsParent);
            int index = rows.Count;   // copia para el botón
            row.acceptButton.onClick.AddListener(() => Accept(index));
            rows.Add(row);
        }

        for (int i = 0; i < rows.Count; i++)
        {
            bool used = i < orders.Count;
            rows[i].gameObject.SetActive(used);
            if (!used) continue;

            DeliveryRequest order = orders[i];
            GameManager.OrderStatus status = gm.GetOrderStatus(i);

            string line = $"<b>{order.customerName}</b> ({order.destinationId}) · {order.reward} €";
            if (status == GameManager.OrderStatus.Delivered) line = $"<s>{line}</s>";
            rows[i].label.text = $"{line}\n<size=70%>{LabelFor(status)}</size>";
            rows[i].label.color = ColorFor(status);

            // El botón solo aparece en los pendientes, y solo se puede pulsar si ahora se puede aceptar
            bool pending = status == GameManager.OrderStatus.Pending;
            rows[i].acceptButton.gameObject.SetActive(pending);
            rows[i].acceptButton.interactable = pending && gm.CanAcceptOrder;
        }
    }

    private void Accept(int index)
    {
        if (GameManager.Instance != null && GameManager.Instance.AcceptOrder(index))
        {
            Notifications.Show("Encargo aceptado: recógelo en el estante");
            Close();
        }
    }

    private void EndNight()
    {
        GameManager gm = GameManager.Instance;
        if (gm == null || !gm.CanEndNight) return;

        Close();             // devuelve el control al jugador antes de cambiar de escena
        gm.CompleteNight();
    }

    private void Close()
    {
        if (terminal != null) terminal.Close();
        else gameObject.SetActive(false);
    }

    private static string NoticeText(GameManager gm)
    {
        if (gm.HasActiveOrder) return "Ya llevas un encargo: entrégalo antes de aceptar otro";
        if (gm.HasAcceptedOrder) return "Tienes un encargo aceptado: recógelo en el estante";
        if (gm.FirstPendingOrder() < 0) return "No quedan encargos esta noche: ya puedes terminarla";
        if (gm.NightInterestPaid) return "Intereses pagados: elige un encargo o termina la noche";
        return "Elige un encargo para aceptarlo";
    }

    private Color ColorFor(GameManager.OrderStatus status) => status switch
    {
        GameManager.OrderStatus.Accepted => acceptedColor,
        GameManager.OrderStatus.InProgress => inProgressColor,
        GameManager.OrderStatus.Delivered => deliveredColor,
        GameManager.OrderStatus.Lost => lostColor,
        _ => pendingColor,
    };

    private static string LabelFor(GameManager.OrderStatus status) => status switch
    {
        GameManager.OrderStatus.Accepted => "ACEPTADO · RECÓGELO EN EL ESTANTE",
        GameManager.OrderStatus.InProgress => "EN CURSO",
        GameManager.OrderStatus.Delivered => "ENTREGADO",
        GameManager.OrderStatus.Lost => "PERDIDO",
        _ => "PENDIENTE",
    };
}
