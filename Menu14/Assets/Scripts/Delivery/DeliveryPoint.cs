using UnityEngine;

// Punto de entrega en la ciudad. Acepta pedidos cuyo destinationId coincida.
public class DeliveryPoint : MonoBehaviour
{
    public string destinationId = "Entrega1";

    [Tooltip("Opcional: objeto que se enciende cuando este es el destino del pedido actual")]
    public GameObject marker;

    void Update()
    {
        if (marker == null) return;

        var gm = GameManager.Instance;
        bool isTarget = gm != null && gm.HasActiveOrder
                        && gm.ActiveOrder.destinationId == destinationId;

        if (marker.activeSelf != isTarget) marker.SetActive(isTarget);
    }

    void OnTriggerEnter(Collider other)
    {
        if (other.GetComponentInParent<ScooterController>() == null) return;

        var gm = GameManager.Instance;
        if (gm == null || !gm.HasActiveOrder) return;

        if (gm.ActiveOrder.destinationId == destinationId)
        {
            Debug.Log($"¡Entregado a {gm.ActiveOrder.customerName}!");
            gm.CompleteOrder();
        }
        else
        {
            Debug.Log("Este no es el destino del pedido");
        }
    }
}