using UnityEngine;

public enum ObstacleKind { Clasico, Evento }

[CreateAssetMenu(fileName = "ObstacleConfig", menuName = "Game/Obstacle Config")]
public class ObstacleConfig : ScriptableObject
{
    [System.Serializable]
    public class ObstacleType
    {
        public string name;
        public GameObject prefab;
        [Tooltip("Clásico: aceite, resaltos, cristales... Evento: una situación con personajes")]
        public ObstacleKind kind = ObstacleKind.Clasico;
        [Tooltip("Sitios donde puede salir")]
        public ObstacleZone zones = ObstacleZone.Acera;
        [Tooltip("Primera noche en la que puede salir (1 = martes)")]
        [Min(1)] public int firstNight = 1;
        [Tooltip("Probabilidad relativa frente a los demás tipos de su clase")]
        [Min(0f)] public float weight = 1f;
        [Tooltip("Máximo de este tipo en una misma noche (0 = sin límite)")]
        [Min(0)] public int maxPerNight;
    }

    [System.Serializable]
    public class ExtraObstacle
    {
        public string name;
        public GameObject prefab;
        public ObstacleZone zones = ObstacleZone.Acera;
        [Min(0)] public int count = 1;
    }

    [System.Serializable]
    public class NightEvent
    {
        public string name;
        [Tooltip("Noche en la que pasa (1 = martes)")]
        [Min(1)] public int night = 4;
        [Tooltip("Quién manda el aviso al empezar la noche")]
        public string sender = "El Paisa";
        [TextArea] public string message;
        [Tooltip("Opcional: prefab que se coloca en el origen del mundo (manifestación, fuegos artificiales...)")]
        public GameObject fixedPrefab;
        [Tooltip("Obstáculos que salen esa noche además de los normales")]
        public ExtraObstacle[] extras;
        [Tooltip("Tráfico extra esa noche (misma escala que trafficDensity de NightConfig: 0,4 = 10 coches más)")]
        [Range(0f, 1f)] public float extraTrafficDensity;
    }

    [Header("Cantidad")]
    [Tooltip("Obstáculos clásicos de cada noche: el primero es la noche 1. Las noches que pasen del final usan el último valor")]
    public int[] classicsPerNight = { 6, 10, 15, 20, 26 };
    [Tooltip("Eventos de cada noche: el primero es la noche 1")]
    public int[] eventsPerNight = { 1, 2, 4, 5, 7 };
    [Tooltip("Distancia mínima (m) entre dos obstáculos de la misma noche")]
    public float minDistanceBetween = 9f;
    [Tooltip("Distancia mínima (m) entre dos obstáculos que cortan un carril, para que los coches los esquiven de uno en uno")]
    public float minDistanceOnRoad = 30f;
    [Tooltip("Parte de los clásicos que sale en la calzada o en los callejones (aceite, resaltos...)")]
    [Range(0f, 1f)] public float classicsOnRoadShare = 0.5f;
    public ObstacleType[] types;

    [Header("Reparto por el mapa")]
    [Tooltip("Centro del barrio (la plaza)")]
    public Vector3 mapCenter = new Vector3(0f, 0f, 4f);
    [Tooltip("Cada estos metros que te alejas del centro, un punto es unas 2,7 veces menos probable. Más pequeño = todo más concentrado en el centro")]
    public float centerFalloff = 25f;
    [Tooltip("Multiplicador para los callejones")]
    public float alleyWeight = 2f;

    [Header("Noches especiales")]
    public NightEvent[] specialNights;

    [Header("Mancha de aceite")]
    [Tooltip("Si pasas girando por encima a más de esta velocidad (m/s), derrapas y te caes")]
    public float oilTurnFallSpeed = 3f;
    [Tooltip("Inclinación mínima del patinete (0-1) que cuenta como girar")]
    [Range(0f, 1f)] public float oilMinLean = 0.3f;
    [Tooltip("A esta velocidad (m/s) te caes aunque vayas recto")]
    public float oilStraightFallSpeed = 6f;

    [Header("Cristales")]
    [Tooltip("Si pasas por encima a más de esta velocidad (m/s), pinchas y te caes")]
    public float glassFallSpeed = 3f;

    [Header("Correa extensible")]
    [Tooltip("Si cruzas la correa a más de esta velocidad (m/s), te enganchas y te caes")]
    public float leashFallSpeed = 1.5f;

    [Header("Perro suelto")]
    [Tooltip("Metros a los que el perro te ve y sale a por ti")]
    public float dogDetectRadius = 7f;
    [Tooltip("Velocidad (m/s) del perro al perseguirte. El patinete va a 8 como mucho")]
    public float dogChaseSpeed = 6f;
    [Tooltip("Velocidad (m/s) del perro al pasear")]
    public float dogWanderSpeed = 1.2f;
    [Tooltip("Metros a los que te muerde")]
    public float dogBiteDistance = 1.1f;
    [Tooltip("Segundos que te persigue antes de cansarse")]
    public float dogGiveUpTime = 4f;
    [Tooltip("Segundos que tarda en volver a perseguir a alguien")]
    public float dogCooldown = 6f;

    [Header("Petardos")]
    [Tooltip("Metros a los que te ven y te tiran un petardo")]
    public float firecrackerRange = 9f;
    [Tooltip("Segundos entre petardo y petardo (mínimo y máximo)")]
    public Vector2 firecrackerInterval = new Vector2(2.5f, 5f);
    [Tooltip("Segundos que tarda en explotar desde que cae (la mecha avisa)")]
    public float firecrackerFuse = 1.2f;
    [Tooltip("Metros alrededor de la explosión en los que te caes del susto")]
    public float firecrackerBlastRadius = 2.2f;
    [Tooltip("Metros de error al apuntar")]
    public float firecrackerScatter = 1.5f;
    [Tooltip("Metros que pueden lanzar el petardo hacia la calzada desde la acera")]
    public float firecrackerRoadReach = 4f;

    [Header("Petardos de Nochevieja (tiran a la carretera)")]
    [Tooltip("Segundos entre petardo y petardo (mínimo y máximo)")]
    public Vector2 partyFirecrackerInterval = new Vector2(1.2f, 2.6f);
    [Tooltip("Metros a los que te ven y te apuntan")]
    public float partyFirecrackerRange = 14f;
    [Tooltip("Metros de error al apuntar")]
    public float partyFirecrackerScatter = 1.2f;

    public int ClassicsForNight(int nightIndex) => Pick(classicsPerNight, nightIndex);
    public int EventsForNight(int nightIndex) => Pick(eventsPerNight, nightIndex);

    public NightEvent EventForNight(int night)
    {
        if (specialNights == null) return null;
        foreach (NightEvent e in specialNights)
            if (e != null && e.night == night) return e;
        return null;
    }

    static int Pick(int[] values, int index)
    {
        if (values == null || values.Length == 0) return 0;
        return values[Mathf.Clamp(index, 0, values.Length - 1)];
    }
}
