using UnityEngine;

// One delivery order the player receives at the restaurant.
[System.Serializable]
public class DeliveryRequest
{
    public string customerName;
    public string destinationId;     // matches a delivery point in the City scene
    public float timeLimit = 120f;   // seconds
    public int reward = 100;         // dinero que da al entregarlo
    public float distance;           // metros de recorrido desde el restaurante
}

// One asset per night: Night1, Night2, Night3...
[CreateAssetMenu(fileName = "Night", menuName = "Game/Night Config")]
public class NightConfig : ScriptableObject
{
    public int nightNumber = 1;

    [Header("Reparto")]
    [Tooltip("Segundos que dura la noche. Al acabar, si llevas un encargo, tienes hasta que se entregue o se pierda")]
    public float duration = 300f;
    [Tooltip("Máximo de encargos que se pueden entregar esta noche. Los perdidos no cuentan")]
    [Min(1)] public int maxDeliveries = 5;
    [Tooltip("Multiplica lo que paga cada encargo (1 = lo que diga DeliveryConfig). Más bajo en las últimas noches")]
    public float rewardMultiplier = 1f;

    [Header("City difficulty")]
    [Tooltip("Controles fijos con agente (se eligen al azar entre los puntos de control activos)")]
    public int policeCount = 2;
    [Tooltip("Policías con moto en aceras de las afueras del barrio, en sitios distintos cada noche")]
    [Min(0)] public int motoPoliceCount;
    [Tooltip("Coches de policía que circulan con el tráfico y te detectan si pasas cerca con mercancía")]
    [Min(0)] public int patrolCarCount;
    [Range(0f, 1f)] public float trafficDensity = 0.3f;

    [Header("Goal")]
    [Tooltip("Intereses de la deuda: mínimo que hay que ganar esta noche. Solo el excedente paga la deuda")]
    public int moneyGoal = 500;
}
