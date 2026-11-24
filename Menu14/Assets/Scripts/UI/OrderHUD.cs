using System.Collections;
using TMPro;
using UnityEngine;

// Encargo actual: se escribe en el móvil (label) y, si se asigna, también en la pantalla principal (screenLabel).
// Que screenLabel solo se vea con el móvil cerrado lo decide PhoneUI (campo Closed Hint).
public class OrderHUD : MonoBehaviour
{
    [SerializeField] private TMP_Text label;
    [Tooltip("Opcional: copia del mismo texto en la pantalla principal")]
    [SerializeField] private TMP_Text screenLabel;
    [SerializeField] private DeliveryTimer timer;

    [Tooltip("Segundos que se ve el aviso al entregar o perder un encargo")]
    [SerializeField] private float messageDuration = 3f;
    [Tooltip("Por debajo de estos segundos, el tiempo se pinta en rojo")]
    [SerializeField] private float warningTime = 20f;

    private DeliveryRequest shownOrder;
    private DeliveryRequest lostOrder;
    private string message;
    private float messageTimer;
    private int lostFrame = -1;

    private IEnumerator Start()
    {
        ScooterCrash.Crashed += HandleCrash;
        while (GameManager.Instance == null) yield return null;
        GameManager.Instance.OnOrderLost += HandleOrderLost;
    }

    private void OnDestroy()
    {
        ScooterCrash.Crashed -= HandleCrash;
        if (GameManager.Instance != null)
            GameManager.Instance.OnOrderLost -= HandleOrderLost;
    }

    private void HandleOrderLost(DeliveryRequest order)
    {
        lostOrder = order;
        lostFrame = Time.frameCount;

        GameManager gm = GameManager.Instance;
        bool expired = timer != null && timer.TimeLeft <= 0f;
        if (gm != null && gm.OrderIntegrity <= 0.001f)
            ShowMessage($"El encargo de {order.customerName} ha quedado destrozado");
        else if (expired)
            ShowMessage($"{order.customerName} se ha cansado de esperar");
        else
            ShowMessage($"La policía te ha quitado el encargo de {order.customerName}");
    }

    private void HandleCrash(CrashCause cause)
    {
        string what = cause switch
        {
            CrashCause.Pothole => "¡Un bache! Te has caído del patinete",
            CrashCause.Curb => "¡El bordillo! Te has caído del patinete",
            _ => "¡Te has estampado! Te has caído del patinete",
        };

        GameManager gm = GameManager.Instance;
        if (lostFrame == Time.frameCount)
            ShowMessage($"{what}\n{message}");
        else if (gm != null && gm.HasActiveOrder)
            ShowMessage($"{what}\nEl encargo de {gm.ActiveOrder.customerName} queda al {IntegrityPercent(gm)} %");
        else
            ShowMessage(what);
    }

    private void ShowMessage(string text)
    {
        message = text;
        messageTimer = messageDuration;
    }

    private void Update()
    {
        GameManager gm = GameManager.Instance;
        if (gm == null || (label == null && screenLabel == null)) return;

        // Si el encargo desaparece sin haberse perdido, es que se ha entregado
        if (shownOrder != null && gm.ActiveOrder != shownOrder && lostOrder != shownOrder && !gm.IsGameOver)
            ShowMessage($"Entregado a {shownOrder.customerName}");
        shownOrder = gm.ActiveOrder;

        string text;
        if (messageTimer > 0f)
        {
            messageTimer -= Time.deltaTime;
            text = message;
        }
        else if (gm.HasActiveOrder)
        {
            text = $"Encargo de {gm.ActiveOrder.customerName} · {FormatTime()}";
            if (gm.OrderIntegrity < 1f) text += $" · {IntegrityPercent(gm)} %";
        }
        else
        {
            text = gm.HasDeliveriesLeft
                ? "Recoge el siguiente encargo en la puerta del Dragón Negro"
                : "No quedan encargos esta noche";
        }

        SetText(text);
    }

    private static int IntegrityPercent(GameManager gm) => Mathf.RoundToInt(gm.OrderIntegrity * 100f);

    private void SetText(string text)
    {
        if (label != null) label.text = text;
        if (screenLabel != null) screenLabel.text = text;
    }

    private string FormatTime()
    {
        if (timer == null || !timer.IsRunning) return "";

        int seconds = Mathf.CeilToInt(timer.TimeLeft);
        string time = $"{seconds / 60}:{seconds % 60:00}";
        return timer.TimeLeft <= warningTime ? $"<color=#FF5A4F>{time}</color>" : time;
    }
}
