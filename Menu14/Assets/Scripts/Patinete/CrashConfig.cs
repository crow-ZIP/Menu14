using UnityEngine;

[CreateAssetMenu(fileName = "CrashConfig", menuName = "Game/Crash Config")]
public class CrashConfig : ScriptableObject
{
    [Header("Consecuencias")]
    [Tooltip("Parte del pedido que se pierde en cada caída (0,25 = 25 %)")]
    [Range(0f, 1f)] public float integrityLossPerFall = 0.25f;
    [Tooltip("Segundos sin poder volver a montar después de caerte")]
    public float remountDelay = 4.5f;

    [Header("Baches")]
    [Tooltip("Velocidad (m/s) a partir de la cual pasar por un bache te tira")]
    public float potholeFallSpeed = 4f;

    [Header("Bordillos")]
    [Tooltip("Velocidad (m/s) de frente contra el bordillo a partir de la cual te caes. Más despacio, lo subes")]
    public float curbFallSpeed = 5f;

    [Header("Choques")]
    [Tooltip("Velocidad (m/s) de frente contra una pared u obstáculo a partir de la cual te caes. Rozarlo de lado no cuenta")]
    public float obstacleFallSpeed = 3.5f;
    [Tooltip("Parte del rebote que te devuelve hacia atrás al chocar (el resto es la velocidad mínima)")]
    public float bounceSpeedFactor = 0.5f;

    [Header("Salir despedido")]
    [Tooltip("Parte de la velocidad del patinete con la que sale despedido el jugador")]
    public float launchSpeedFactor = 0.7f;
    [Tooltip("Velocidad horizontal mínima (m/s) con la que sale despedido")]
    public float minLaunchSpeed = 2.5f;
    [Tooltip("Velocidad hacia arriba (m/s) del rebote")]
    public float launchUpSpeed = 3.5f;
}
