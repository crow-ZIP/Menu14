using System.Collections.Generic;
using UnityEngine;

// One delivery order the player receives at the restaurant.
[System.Serializable]
public class DeliveryRequest
{
    public string customerName;
    public string destinationId;     // matches a delivery point in the City scene
    public float timeLimit = 120f;   // seconds
    public int reward = 100;         // dinero que da al entregarlo
}

// One asset per night: Night1, Night2, Night3...
[CreateAssetMenu(fileName = "Night", menuName = "Game/Night Config")]
public class NightConfig : ScriptableObject
{
    public int nightNumber = 1;
    public List<DeliveryRequest> deliveries = new List<DeliveryRequest>();

    [Header("City difficulty")]
    public int policeCount = 2;
    [Range(0f, 1f)] public float trafficDensity = 0.3f;

    [Header("Goal")]
    [Tooltip("Intereses de la deuda: mínimo que hay que ganar esta noche. Solo el excedente paga la deuda")]
    public int moneyGoal = 500;
}
