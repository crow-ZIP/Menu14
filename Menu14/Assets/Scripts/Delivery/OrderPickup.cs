using UnityEngine;

// Zona del restaurante: al entrar con el patinete, recoges el siguiente pedido.
public class OrderPickup : MonoBehaviour
{
    void OnTriggerEnter(Collider other)
    {
        // ¿Es el patinete lo que ha entrado?
        if (other.GetComponentInParent<ScooterController>() == null) return;

        var gm = GameManager.Instance;
        if (gm == null) return;

        if (gm.HasActiveOrder)
            Debug.Log($"Ya llevas un pedido: {gm.ActiveOrder.customerName}");
        else if (gm.TakeNextOrder())
            Debug.Log($"Pedido recogido: {gm.ActiveOrder.customerName} → {gm.ActiveOrder.destinationId}");
        else
            Debug.Log("No quedan pedidos esta noche");
    }
}