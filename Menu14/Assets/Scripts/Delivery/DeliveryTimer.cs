using UnityEngine;

public class DeliveryTimer : MonoBehaviour
{
    public float TimeLeft { get; private set; }
    public bool IsRunning => trackedOrder != null;

    DeliveryRequest trackedOrder;

    void Update()
    {
        GameManager gm = GameManager.Instance;
        if (gm == null) return;

        
        if (gm.ActiveOrder != trackedOrder)
        {
            trackedOrder = gm.ActiveOrder;
            if (trackedOrder != null) TimeLeft = trackedOrder.timeLimit;
        }

        if (trackedOrder == null || gm.IsGameOver) return;

        TimeLeft = Mathf.Max(0f, TimeLeft - Time.deltaTime);
        if (TimeLeft <= 0f) gm.ExpireOrder();
    }
}
