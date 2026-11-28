using UnityEngine;

[CreateAssetMenu(fileName = "DeliveryConfig", menuName = "Game/Delivery Config")]
public class DeliveryConfig : ScriptableObject
{
    [Header("Pantalla de encargos")]
    [Tooltip("Ofertas que se ven a la vez en la pantalla del PC")]
    [Min(1)] public int offersOnScreen = 3;

    [Header("Pago")]
    [Tooltip("Lo que paga un encargo aunque esté al lado del restaurante (antes del multiplicador de la noche)")]
    public int baseReward = 40;
    [Tooltip("€ extra por cada metro de recorrido desde el restaurante")]
    public float rewardPerMeter = 0.8f;
    [Tooltip("El pago se redondea a múltiplos de esto")]
    [Min(1)] public int rewardRounding = 5;

    [Header("Tiempo de cada encargo")]
    [Tooltip("Segundos que tiene cualquier encargo, esté donde esté")]
    public float baseTime = 30f;
    [Tooltip("Segundos extra por cada metro de recorrido desde el restaurante")]
    public float secondsPerMeter = 0.5f;
    [Tooltip("El tiempo se redondea a múltiplos de esto")]
    [Min(1)] public int timeRounding = 5;

    [Header("Fin de la noche")]
    [Tooltip("Segundos entre que acaba la noche y sale el resumen")]
    public float nightEndDelay = 2f;

    [Header("Clientes")]
    public string[] customerNames =
    {
        "Ana", "Luis", "Marta", "Pepe", "Lucía", "Toni", "Carmen", "Vicent", "Amparo", "Rubén",
        "Nuria", "Paco", "Rosa", "Iván", "Elena", "Jordi", "Sara", "Quique", "Laura", "Manolo",
    };
}
