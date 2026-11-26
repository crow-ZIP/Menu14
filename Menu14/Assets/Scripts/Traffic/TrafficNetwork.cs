using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Red de calles para el tráfico. Lee los tiles de carretera de Kenney que cuelgan de "Calles"
/// y monta solo un grafo de cruces: no hay que colocar waypoints a mano.
///   road-straight / road-crossing  -> une los dos lados de su eje
///   road-crossroad (y variantes)   -> une los cuatro lados
/// Si cambias las calles, el grafo se rehace solo al darle a Play.
/// Selecciona este objeto para ver los carriles en la vista Scene:
///   amarillo = cruce, rojo = salida del mapa (por ahí salen y entran coches), gris = callejón sin salida (no entran),
///   línea roja = calle cortada por un control policial (TrafficBlocker).
/// </summary>
public class TrafficNetwork : MonoBehaviour
{
    public class Node
    {
        public Vector3 position;
        public bool isCrossroad;
        public readonly List<Node> links = new List<Node>();
        public TrafficCar occupant;               // coche que está cruzando ahora mismo (solo uno a la vez)
        public bool isExit;                       // fin de calle en el borde del mapa: por aquí salen y entran coches
        public bool IsDeadEnd => links.Count == 1;
    }

    [Tooltip("Padre de los tiles de carretera (en City es 'Calles')")]
    public Transform roadsRoot;
    [Tooltip("Tamaño de un tile en metros (los de Kenney están a escala 8)")]
    public float tileSize = 8f;
    [Tooltip("Distancia del centro de la calzada al centro del carril. Se circula por la derecha")]
    public float laneOffset = 1.1f;
    [Tooltip("Puntos con los que se dibuja la curva de un giro")]
    public int turnSteps = 8;

    public List<Node> Nodes { get; } = new List<Node>();
    /// <summary>Fines de calle en el borde del mapa (los callejones sin salida de dentro no cuentan).</summary>
    public List<Node> Exits { get; } = new List<Node>();
    readonly List<(Node from, Node to)> lanes = new List<(Node, Node)>();
    readonly List<(Node a, Node b)> blockedRoads = new List<(Node, Node)>();   // cortadas por un TrafficBlocker (para los gizmos)

    // Direcciones en la cuadrícula: +X, -X, +Z, -Z  (la opuesta de d es d ^ 1)
    static readonly Vector2Int[] Dirs = { new Vector2Int(1, 0), new Vector2Int(-1, 0), new Vector2Int(0, 1), new Vector2Int(0, -1) };

    class Tile
    {
        public Vector3 position;
        public bool crossroad;
        public readonly bool[] open = new bool[4];
    }

    void Awake() => Build();

    // ---------- Construcción del grafo ----------
    public void Build()
    {
        Nodes.Clear();
        Exits.Clear();
        lanes.Clear();
        blockedRoads.Clear();
        if (roadsRoot == null)
        {
            Debug.LogWarning("TrafficNetwork: falta asignar roadsRoot (el objeto 'Calles')", this);
            return;
        }

        var tiles = new Dictionary<Vector2Int, Tile>();
        CollectTiles(roadsRoot, tiles);

        // 1) Nodos: cruces, fines de calle y curvas. Los tramos rectos intermedios no son nodo.
        var nodeAt = new Dictionary<Vector2Int, Node>();
        foreach (var kv in tiles)
        {
            if (!IsNode(kv.Key, kv.Value, tiles)) continue;
            var n = new Node { position = kv.Value.position, isCrossroad = kv.Value.crossroad };
            nodeAt[kv.Key] = n;
            Nodes.Add(n);
        }

        // 2) Desde cada nodo, avanzar por cada salida hasta el siguiente nodo
        foreach (var kv in nodeAt)
        {
            for (int d = 0; d < 4; d++)
            {
                if (!Connected(kv.Key, d, tiles)) continue;
                Vector2Int c = kv.Key;
                for (int guard = 0; guard < 1000; guard++)
                {
                    c += Dirs[d];
                    if (nodeAt.TryGetValue(c, out Node other))
                    {
                        if (other != kv.Value && !kv.Value.links.Contains(other))
                            kv.Value.links.Add(other);
                        break;
                    }
                    if (!Connected(c, d, tiles)) break;
                }
            }
        }

        // 2b) Calles cortadas por controles policiales: se quitan del grafo en los dos sentidos
        RemoveBlockedRoads();

        // 3) Salidas: fines de calle que están en el borde del mapa
        float minX = float.MaxValue, maxX = float.MinValue, minZ = float.MaxValue, maxZ = float.MinValue;
        foreach (Node n in Nodes)
        {
            minX = Mathf.Min(minX, n.position.x); maxX = Mathf.Max(maxX, n.position.x);
            minZ = Mathf.Min(minZ, n.position.z); maxZ = Mathf.Max(maxZ, n.position.z);
        }
        float eps = tileSize * 0.1f;
        foreach (Node n in Nodes)
        {
            Vector3 p = n.position;
            n.isExit = n.IsDeadEnd && (p.x < minX + eps || p.x > maxX - eps || p.z < minZ + eps || p.z > maxZ - eps);
            if (n.isExit) Exits.Add(n);
            foreach (Node l in n.links) lanes.Add((n, l));
        }
    }

    void RemoveBlockedRoads()
    {
        TrafficBlocker[] blockers = FindObjectsByType<TrafficBlocker>(FindObjectsSortMode.None);
        if (blockers.Length == 0) return;

        float roadHalfWidth = laneOffset + 1f;   // carril + medio coche
        foreach (Node n in Nodes)
        {
            for (int i = n.links.Count - 1; i >= 0; i--)
            {
                Node other = n.links[i];
                foreach (TrafficBlocker blocker in blockers)
                {
                    if (!blocker.enabled || !blocker.gameObject.activeInHierarchy || !blocker.BlocksRoad(n.position, other.position, roadHalfWidth)) continue;
                    n.links.RemoveAt(i);   // el sentido contrario se quita al recorrer 'other'
                    blockedRoads.Add((n, other));
                    break;
                }
            }
        }
    }

    void CollectTiles(Transform parent, Dictionary<Vector2Int, Tile> tiles)
    {
        foreach (Transform t in parent)
        {
            if (!t.gameObject.activeInHierarchy) continue;
            string n = t.name;
            if (!n.StartsWith("road-"))
            {
                CollectTiles(t, tiles);   // por si las calles están agrupadas en subcarpetas
                continue;
            }

            bool crossroad = n.StartsWith("road-crossroad");
            bool straight = n.StartsWith("road-straight") || n.StartsWith("road-crossing");
            if (!crossroad && !straight) continue;   // road-end, road-square, señales... no son calzada para el tráfico

            Vector2Int cell = new Vector2Int(Mathf.RoundToInt(t.position.x / tileSize), Mathf.RoundToInt(t.position.z / tileSize));
            if (!tiles.TryGetValue(cell, out Tile tile))
            {
                tile = new Tile { position = new Vector3(cell.x * tileSize, t.position.y, cell.y * tileSize) };
                tiles[cell] = tile;
            }

            if (crossroad)
            {
                tile.crossroad = true;
                for (int d = 0; d < 4; d++) tile.open[d] = true;
            }
            else
            {
                // En el modelo de Kenney la calle recta va a lo largo de su eje X local
                Vector3 axis = t.right;
                if (Mathf.Abs(axis.x) > Mathf.Abs(axis.z)) { tile.open[0] = true; tile.open[1] = true; }
                else { tile.open[2] = true; tile.open[3] = true; }
            }
        }
    }

    // Los dos tiles tienen que abrirse el uno hacia el otro
    static bool Connected(Vector2Int c, int d, Dictionary<Vector2Int, Tile> tiles)
    {
        return tiles.TryGetValue(c, out Tile a) && a.open[d]
            && tiles.TryGetValue(c + Dirs[d], out Tile b) && b.open[d ^ 1];
    }

    static bool IsNode(Vector2Int c, Tile tile, Dictionary<Vector2Int, Tile> tiles)
    {
        int degree = 0;
        bool[] con = new bool[4];
        for (int d = 0; d < 4; d++) { con[d] = Connected(c, d, tiles); if (con[d]) degree++; }
        if (degree == 0) return false;
        if (tile.crossroad || degree != 2) return true;
        bool straightLine = (con[0] && con[1]) || (con[2] && con[3]);
        return !straightLine;   // curva
    }

    // ---------- Carriles ----------
    static void Axes(Node a, Node b, out Vector3 forward, out Vector3 right)
    {
        forward = b.position - a.position;
        forward.y = 0f;
        forward.Normalize();
        right = Vector3.Cross(Vector3.up, forward);
    }

    // En los cruces el carril empieza/acaba en el borde del tile (la "línea de stop")
    float Margin(Node n) => n.isCrossroad && !n.IsDeadEnd ? tileSize * 0.5f : 0f;

    public Vector3 LaneStart(Node from, Node to)
    {
        Axes(from, to, out Vector3 f, out Vector3 r);
        return from.position + f * Margin(from) + r * laneOffset;
    }

    public Vector3 LaneEnd(Node from, Node to)
    {
        Axes(from, to, out Vector3 f, out Vector3 r);
        return to.position - f * Margin(to) + r * laneOffset;
    }

    public bool IsTurn(Node from, Node via, Node to)
    {
        Axes(from, via, out Vector3 f1, out _);
        Axes(via, to, out Vector3 f2, out _);
        return Vector3.Dot(f1, f2) < 0.9f;
    }

    /// <summary>Añade los puntos para cruzar 'via' viniendo de 'from' y saliendo hacia 'to' (curva de Bézier).</summary>
    public void AddCrossingPoints(Node from, Node via, Node to, List<Vector3> points)
    {
        Vector3 p0 = LaneEnd(from, via);
        Vector3 p2 = LaneStart(via, to);
        Axes(from, via, out _, out Vector3 r1);
        Axes(via, to, out _, out Vector3 r2);

        // Recto: punto medio. Giro: la esquina donde se cortan los dos carriles.
        Vector3 p1 = IsTurn(from, via, to) ? via.position + (r1 + r2) * laneOffset : (p0 + p2) * 0.5f;

        for (int i = 1; i <= turnSteps; i++)
        {
            float t = i / (float)turnSteps;
            float u = 1f - t;
            points.Add(u * u * p0 + 2f * u * t * p1 + t * t * p2);
        }
    }

    // Un coche puede ir hacia 'n' si no es un callejón sin salida de dentro del mapa
    static bool Drivable(Node n) => !n.IsDeadEnd || n.isExit;

    /// <summary>Siguiente cruce al azar: sin dar media vuelta y sin meterse en callejones sin salida.</summary>
    public Node RandomNext(Node from, Node via)
    {
        var options = new List<Node>(4);
        foreach (Node n in via.links)
            if (n != from && Drivable(n)) options.Add(n);
        if (options.Count == 0)
            foreach (Node n in via.links)
                if (n != from) options.Add(n);
        return options.Count > 0 ? options[Random.Range(0, options.Count)] : from;
    }

    public bool RandomLane(out Node from, out Node to)
    {
        from = to = null;
        if (lanes.Count == 0) return false;
        for (int i = 0; i < 20; i++)
        {
            (from, to) = lanes[Random.Range(0, lanes.Count)];
            if (Drivable(to)) return true;
        }
        return false;
    }

    // ---------- Vista Scene ----------
    void OnDrawGizmosSelected()
    {
        if (!Application.isPlaying) Build();

        foreach (Node n in Nodes)
        {
            if (n.isExit) Gizmos.color = Color.red;
            else if (n.IsDeadEnd) Gizmos.color = Color.gray;
            else Gizmos.color = n.occupant != null ? Color.magenta : Color.yellow;
            Gizmos.DrawSphere(n.position + Vector3.up * 0.5f, 0.6f);
        }

        Gizmos.color = Color.cyan;
        foreach (var (a, b) in lanes)
        {
            Vector3 s = LaneStart(a, b) + Vector3.up * 0.2f;
            Vector3 e = LaneEnd(a, b) + Vector3.up * 0.2f;
            Gizmos.DrawLine(s, e);
            Gizmos.DrawWireSphere(e, 0.25f);   // la bolita marca el sentido (final del carril)
        }

        Gizmos.color = Color.red;   // calles cortadas por un control policial
        foreach (var (a, b) in blockedRoads)
            Gizmos.DrawLine(a.position + Vector3.up * 0.4f, b.position + Vector3.up * 0.4f);
    }
}
