using UnityEngine;

/// <summary>
/// Punto de entrega en la ciudad. Acepta el pedido actual si su destinationId coincide.
/// Se entrega al entrar el jugador, a pie o en patinete.
/// Necesita un collider marcado como Is Trigger en el mismo objeto.
/// </summary>
public class DeliveryPoint : MonoBehaviour
{
    public string destinationId = "Entrega1";

    [Tooltip("Opcional: hijo que se enciende cuando este es el destino del pedido actual (sin collider)")]
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
        if (!PlayerLocator.IsPlayer(other)) return;

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
