using UnityEngine;

public class DeliveryTimer : MonoBehaviour
{
    public float TimeLeft { get; private set; }
    public bool IsRunning => trackedOrder != null;

    [Tooltip("Con estos segundos restantes sale el aviso de darse prisa")]
    [SerializeField] float hurryWarning = 20f;

    DeliveryRequest trackedOrder;
    // Compartido: hay más de un DeliveryTimer en la escena y el aviso solo debe salir una vez
    static DeliveryRequest warnedOrder;

    void Update()
    {
        GameManager gm = GameManager.Instance;
        if (gm == null) return;

        
        if (gm.ActiveOrder != trackedOrder)
        {
            trackedOrder = gm.ActiveOrder;
            if (trackedOrder != null) TimeLeft = trackedOrder.timeLimit;
            if (trackedOrder != null && warnedOrder == trackedOrder) warnedOrder = null;   // encargo recién recogido
        }

        if (trackedOrder == null || gm.IsGameOver) return;

        TimeLeft = Mathf.Max(0f, TimeLeft - Time.deltaTime);
        if (TimeLeft <= hurryWarning && TimeLeft > 0f && warnedOrder != trackedOrder && trackedOrder.timeLimit > hurryWarning)
        {
            warnedOrder = trackedOrder;
            AudioManager.Play(Sfx.Hurry);
            Notifications.Show($"¡Date prisa! Quedan {Mathf.CeilToInt(hurryWarning)} segundos", NoticeKind.Warning);
        }
        if (TimeLeft <= 0f) gm.ExpireOrder();
    }
}
