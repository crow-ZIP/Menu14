using System.Collections;
using TMPro;
using UnityEngine;

// Provisional hasta que llegue el móvil de T11
public class OrderHUD : MonoBehaviour
{
    [SerializeField] private TMP_Text label;
    [SerializeField] private DeliveryTimer timer;

    [Tooltip("Segundos que se ve el aviso al entregar o perder un encargo")]
    [SerializeField] private float messageDuration = 3f;
    [Tooltip("Por debajo de estos segundos, el tiempo se pinta en rojo")]
    [SerializeField] private float warningTime = 20f;

    private DeliveryRequest shownOrder;
    private DeliveryRequest lostOrder;
    private string message;
    private float messageTimer;

    private IEnumerator Start()
    {
        while (GameManager.Instance == null) yield return null;
        GameManager.Instance.OnOrderLost += HandleOrderLost;
    }

    private void OnDestroy()
    {
        if (GameManager.Instance != null)
            GameManager.Instance.OnOrderLost -= HandleOrderLost;
    }

    private void HandleOrderLost(DeliveryRequest order)
    {
        lostOrder = order;
        bool expired = timer != null && timer.TimeLeft <= 0f;
        ShowMessage(expired
            ? $"{order.customerName} se ha cansado de esperar"
            : $"La policía te ha quitado el encargo de {order.customerName}");
    }

    private void ShowMessage(string text)
    {
        message = text;
        messageTimer = messageDuration;
    }

    private void Update()
    {
        GameManager gm = GameManager.Instance;
        if (gm == null || label == null) return;

        // Si el encargo desaparece sin haberse perdido, es que se ha entregado
        if (shownOrder != null && gm.ActiveOrder != shownOrder && lostOrder != shownOrder && !gm.IsGameOver)
            ShowMessage($"Entregado a {shownOrder.customerName}");
        shownOrder = gm.ActiveOrder;

        if (messageTimer > 0f)
        {
            messageTimer -= Time.deltaTime;
            label.text = message;
        }
        else if (gm.HasActiveOrder)
        {
            label.text = $"Encargo de {gm.ActiveOrder.customerName} · {FormatTime()}";
        }
        else
        {
            label.text = gm.HasDeliveriesLeft
                ? "Recoge el siguiente encargo en la puerta del Dragón Negro"
                : "No quedan encargos esta noche";
        }
    }

    private string FormatTime()
    {
        if (timer == null || !timer.IsRunning) return "";

        int seconds = Mathf.CeilToInt(timer.TimeLeft);
        string time = $"{seconds / 60}:{seconds % 60:00}";
        return timer.TimeLeft <= warningTime ? $"<color=#FF5A4F>{time}</color>" : time;
    }
}
