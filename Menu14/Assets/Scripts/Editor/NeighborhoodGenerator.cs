using System.Collections.Generic;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Coloca el barrio de Menú 14 (T06 + T19) en la escena abierta (City) con los assets de Kenney
/// (Assets/ThirdParty/Kenney). Menú: Menú 14 → Generar barrio.
/// Borra el objeto raíz "Barrio" y lo vuelve a crear, así que no metas objetos propios dentro. Semilla fija.
///
/// Trazado en una cuadrícula de 8 m (un tramo de calle de Kenney), +X = este, +Z = norte:
///   3x3 manzanas de 40x40; la central es la plaza. Calles de 8 m; la avenida (z 20..36) tiene 16 m.
///   Ruta rápida: avenida y calles junto a la plaza, con los controles policiales.
///   Ruta lenta: callejones (manzanas oeste y sur) y la ronda exterior, con más baches.
/// Con primitivas solo lo que no tiene Kenney: Dragón Negro (interior hueco), fuente, árboles, bancos y baches.
/// </summary>
public static class NeighborhoodGenerator
{
    const float Tile = 8f, CurbHeight = 0.15f, Sidewalk = 2.5f, AlleySidewalk = 1.2f, AlleyWidth = 5f, Depth = 10f;
    const float BuildingScale = 7.5f, CarScale = 1.5f, PersonScale = 2.4f, RoadScale = 8f;
    const float MinX = -76f, MaxX = 76f, MinZ = -76f, MaxZ = 84f, AvenueZ = 28f;
    static readonly float[] StreetX = { -72f, -24f, 24f, 72f };          // centros de las calles N-S
    static readonly float[] StreetZ = { -72f, -24f, 80f };                // centros de las calles E-O (más la avenida)
    static readonly Vector2[] BlocksX = { new(-68f, -28f), new(-20f, 20f), new(28f, 68f) };
    static readonly Vector2[] BlocksZ = { new(-68f, -28f), new(-20f, 20f), new(36f, 76f) };
    const string K = "Assets/ThirdParty/Kenney/";
    const string MaterialFolder = "Assets/Materials/Barrio";

    static readonly string[] Buildings = { "a", "b", "c", "d", "e", "f", "g", "h", "i", "j", "k", "l", "m" };
    static readonly string[] Cars = { "sedan", "suv", "taxi", "van", "hatchback-sports", "sedan-sports", "suv-luxury", "delivery", "truck" };
    static readonly string[] Neighbors = { "female-a", "female-b", "female-c", "female-d", "female-e", "female-f", "male-a", "male-b", "male-d", "male-e", "male-f" };
    static readonly (string text, Color color)[] NeonSigns =
    {
        ("BAR", new Color(1f, 0.3f, 0.6f)), ("CAFÉ BAR", new Color(1f, 0.6f, 0.15f)), ("BODEGA", new Color(0.3f, 0.8f, 1f)),
        ("CERVECERÍA", new Color(1f, 0.85f, 0.2f)), ("PIZZERÍA", new Color(1f, 0.35f, 0.2f)), ("FARMACIA", new Color(0.2f, 1f, 0.35f)),
        ("ESTANCO", new Color(0.95f, 0.75f, 0.3f)),
    };

    static System.Random rng;
    static Dictionary<string, Material> materials;
    static Dictionary<string, GameObject> models;
    static List<Rect> reserved;
    static List<Vector3> keepClear;

    [MenuItem("Menú 14/Generar barrio")]
    public static void GenerateMenu() => Generate();

    public static string Generate()
    {
        rng = new System.Random(14);
        materials = new Dictionary<string, Material>();
        models = new Dictionary<string, GameObject>();
        reserved = new List<Rect>();
        keepClear = new List<Vector3>();

        var old = GameObject.Find("Barrio");
        if (old != null) Object.DestroyImmediate(old);
        var root = new GameObject("Barrio").transform;

        var gameplay = Group("Juego", root);
        BuildMarkers(Group("Marcadores", gameplay));
        BuildDeliveries(Group("Entregas", gameplay));
        var controls = BuildControls(Group("Controles", gameplay));

        BuildDragonNegro(Group("DragonNegro", root));
        BuildAssociation(Group("Asociacion", root));
        BuildRoads(Group("Calles", root));
        int buildings = BuildBlocks(Group("Manzanas", root));
        buildings += BuildPerimeter(Group("Perimetro", root));
        BuildPlaza(Group("Plaza", root));
        BuildStreetProps(Group("Mobiliario", root));
        BuildPotholes(Group("Baches", root));
        BuildBackground(Group("Fondo", root));
        BuildLimits(Group("Limites", root));

        SetupEnvironment();
        SetupSpawner(controls);
        foreach (var t in root.GetComponentsInChildren<Transform>(true)) t.gameObject.isStatic = !t.IsChildOf(gameplay);
        EditorSceneManager.MarkSceneDirty(root.gameObject.scene);

        string log = $"Barrio generado: {root.GetComponentsInChildren<Transform>(true).Length} objetos, {buildings} edificios.\n";
        Debug.Log(log);
        return log;
    }

    // =====================================================================
    // Calles (tramos de Kenney) y suelo
    // =====================================================================

    static void BuildRoads(Transform g)
    {
        // Colisión del asfalto a y = 0 (los tramos de Kenney son solo visuales) y terreno visual debajo
        Box("SueloColision", g, new Vector3(0f, -0.25f, 4f), new Vector3(400f, 0.5f, 400f), null);
        Box("Terreno", g, new Vector3(0f, -0.3f, 4f), new Vector3(600f, 0.5f, 600f), Mat("Asfalto"), false);

        var crossX = new List<float>(StreetX);
        var crossZ = new List<float>(StreetZ) { AvenueZ - 4f, AvenueZ + 4f };
        for (float x = MinX + 4f; x < MaxX; x += Tile)
            for (float z = MinZ + 4f; z < MaxZ; z += Tile)
            {
                bool sx = crossX.Contains(x), sz = crossZ.Contains(z);
                if (!sx && !sz) continue;
                string piece = sx && sz ? (z == -24f && (x == -24f || x == 24f) ? "road-crossroad-path" : "road-crossroad") : "road-straight";
                float yaw = sx && !sz ? 90f : 0f;                // el tramo recto de Kenney va a lo largo de X
                if (x == 0f && sz && z != -24f && z != -72f && z != 80f) piece = "road-crossing";  // paso de peatones de la plaza a la Asociación
                Road(piece, g, x, z, yaw);
            }

        // Callejones (5 m): oeste (x -48, manzana central oeste) y sur (z -48, manzana central sur)
        for (float z = -16f; z <= 16f; z += Tile) Road("road-straight", g, -48f, z, 90f, AlleyWidth / Tile);
        for (float x = -16f; x <= 16f; x += Tile) Road("road-straight", g, x, -48f, 0f, AlleyWidth / Tile);

        // Salidas del barrio, cortadas con vallas de obra
        foreach (float x in new[] { -24f, 24f })
        {
            Exit(g, new Vector3(x, 0f, MinZ - 4f), 90f, Vector3.back, 8f);
            Exit(g, new Vector3(x, 0f, MaxZ + 4f), 90f, Vector3.forward, 8f);
        }
        foreach (var (z, w) in new[] { (-24f, 8f), (AvenueZ - 4f, 8f), (AvenueZ + 4f, 8f) })
        {
            Exit(g, new Vector3(MinX - 4f, 0f, z), 0f, Vector3.left, w);
            Exit(g, new Vector3(MaxX + 4f, 0f, z), 0f, Vector3.right, w);
        }
    }

    static void Road(string piece, Transform g, float x, float z, float yaw, float widthScale = 1f)
    {
        var r = Kenney("CityKitRoads/" + piece, g, new Vector3(x, -0.08f, z), yaw, false);
        r.transform.localScale = new Vector3(RoadScale, RoadScale, RoadScale * widthScale);   // el ancho de la calle es el eje Z del tramo
    }

    static void Exit(Transform g, Vector3 center, float yaw, Vector3 outward, float width)
    {
        Road("road-straight", g, center.x, center.z, yaw);
        Vector3 across = Vector3.Cross(Vector3.up, outward);
        Vector3 end = center + outward * 3.2f;
        for (float t = -width * 0.5f + 1.5f; t < width * 0.5f; t += 3f)
            Kenney("CityKitRoads/construction-fence", g, end + across * t, Quaternion.LookRotation(across).eulerAngles.y, true, RoadScale);
        Kenney("CityKitRoads/construction-light", g, end + across * (width * 0.5f - 0.4f) - outward * 0.5f, 0f, false, RoadScale);
        Kenney("CityKitRoads/construction-barrier", g, end - outward * 1.5f + across * (width * 0.25f), Quaternion.LookRotation(across).eulerAngles.y, true, RoadScale);
    }

    // =====================================================================
    // Manzanas: acera elevada + filas de edificios de Kenney alrededor
    // =====================================================================

    static int BuildBlocks(Transform g)
    {
        int count = 0;
        for (int i = 0; i < 3; i++)
            for (int j = 0; j < 3; j++)
            {
                if (i == 1 && j == 1) continue;                          // la plaza va aparte
                var block = Rect.MinMaxRect(BlocksX[i].x, BlocksZ[j].x, BlocksX[i].y, BlocksZ[j].y);
                var t = Group($"Manzana_{i}{j}", g);
                if (i == 0 && j == 1)                                    // callejón N-S
                {
                    count += Lot(t, Rect.MinMaxRect(block.xMin, block.yMin, -48f - AlleyWidth * 0.5f, block.yMax), alleyE: true);
                    count += Lot(t, Rect.MinMaxRect(-48f + AlleyWidth * 0.5f, block.yMin, block.xMax, block.yMax), alleyW: true);
                }
                else if (i == 1 && j == 0)                               // callejón E-O
                {
                    count += Lot(t, Rect.MinMaxRect(block.xMin, block.yMin, block.xMax, -48f - AlleyWidth * 0.5f), alleyN: true);
                    count += Lot(t, Rect.MinMaxRect(block.xMin, -48f + AlleyWidth * 0.5f, block.xMax, block.yMax), alleyS: true);
                }
                else count += Lot(t, block);
            }
        return count;
    }

    static int Lot(Transform g, Rect r, bool alleyW = false, bool alleyE = false, bool alleyS = false, bool alleyN = false)
    {
        Box("Acera", g, new Vector3(r.center.x, (CurbHeight - 0.1f) * 0.5f, r.center.y), new Vector3(r.width, CurbHeight + 0.1f, r.height), Mat("Acera"));
        var f = Rect.MinMaxRect(r.xMin + (alleyW ? AlleySidewalk : Sidewalk), r.yMin + (alleyS ? AlleySidewalk : Sidewalk),
                                r.xMax - (alleyE ? AlleySidewalk : Sidewalk), r.yMax - (alleyN ? AlleySidewalk : Sidewalk));
        float d = Mathf.Min(Depth, f.width * 0.5f, f.height * 0.5f);
        int n = 0;
        n += Row(g, new Vector3(f.xMin, CurbHeight, f.yMax), Vector3.right, f.width, d, Vector3.forward);
        n += Row(g, new Vector3(f.xMin, CurbHeight, f.yMin), Vector3.right, f.width, d, Vector3.back);
        n += Row(g, new Vector3(f.xMax, CurbHeight, f.yMin + d), Vector3.forward, f.height - 2f * d, d, Vector3.right);
        n += Row(g, new Vector3(f.xMin, CurbHeight, f.yMin + d), Vector3.forward, f.height - 2f * d, d, Vector3.left);
        return n;
    }

    /// <summary>Fila de edificios con la fachada en la línea start→start+along*length, mirando a "outward". Salta los solares reservados.</summary>
    static int Row(Transform g, Vector3 start, Vector3 along, float length, float depth, Vector3 outward)
    {
        if (length < 4f) return 0;
        var gaps = new List<Vector2>();
        foreach (var res in reserved)
        {
            // Se proyecta el solar reservado sobre la fila si toca la franja de fachada
            Vector3 a = start, b = start + along * length - outward * depth;
            var band = Rect.MinMaxRect(Mathf.Min(a.x, b.x), Mathf.Min(a.z, b.z), Mathf.Max(a.x, b.x), Mathf.Max(a.z, b.z));
            if (!band.Overlaps(res)) continue;
            float t0 = Vector3.Dot(new Vector3(res.xMin, 0f, res.yMin) - start, along), t1 = Vector3.Dot(new Vector3(res.xMax, 0f, res.yMax) - start, along);
            gaps.Add(new Vector2(Mathf.Min(t0, t1), Mathf.Max(t0, t1)));
        }
        int n = 0;
        foreach (var s in Spans(0f, length, gaps)) n += RowSegment(g, start + along * s.x, along, s.y - s.x, depth, outward);
        return n;
    }

    static int RowSegment(Transform g, Vector3 start, Vector3 along, float length, float depth, Vector3 outward)
    {
        if (length < 3f) return 0;
        // Elige edificios hasta cubrir el tramo y luego los estira un poco para que encajen justos
        var picks = new List<(string name, Bounds b)>();
        float sum = 0f;
        string last = null;
        while (length - sum > 3f)
        {
            float remaining = length - sum;
            string pick = null; Bounds pb = default;
            for (int tries = 0; tries < 12; tries++)
            {
                string c = "building-" + Buildings[rng.Next(Buildings.Length)];
                var cb = MeshBounds("CityKitCommercial/" + c);
                if (c == last || cb.size.x * BuildingScale > remaining * 1.3f) continue;
                pick = c; pb = cb; break;
            }
            if (pick == null) { pick = "building-d"; pb = MeshBounds("CityKitCommercial/" + pick); }
            picks.Add((pick, pb));
            sum += pb.size.x * BuildingScale;
            last = pick;
        }
        float stretch = length / sum, t = 0f;
        var rot = Quaternion.LookRotation(-outward);                      // la fachada de Kenney mira a -Z
        foreach (var (name, b) in picks)
        {
            float w = b.size.x * BuildingScale * stretch;
            var scale = new Vector3(BuildingScale * stretch, BuildingScale, depth / b.size.z);
            Vector3 facadeMid = start + along * (t + w * 0.5f);
            Vector3 pos = facadeMid + outward * (b.min.z * scale.z) - (rot * Vector3.right) * (b.center.x * scale.x);
            var go = Kenney("CityKitCommercial/" + name, g, pos, rot.eulerAngles.y, true);
            go.transform.localScale = scale;
            if (rng.NextDouble() < 0.16) NeonBlade(g, facadeMid + along * (w * 0.5f - 1f), outward, along);
            t += w;
        }
        return picks.Count;
    }

    static List<Vector2> Spans(float from, float to, List<Vector2> gaps)
    {
        gaps.Sort((a, b) => a.x.CompareTo(b.x));
        var spans = new List<Vector2>();
        float cur = from;
        foreach (var gp in gaps)
        {
            if (gp.x > cur) spans.Add(new Vector2(cur, Mathf.Min(gp.x, to)));
            cur = Mathf.Max(cur, gp.y);
        }
        if (cur < to) spans.Add(new Vector2(cur, to));
        return spans;
    }

    /// <summary>Banderola de neón perpendicular a la fachada (bares, farmacia, estanco).</summary>
    static void NeonBlade(Transform g, Vector3 facadePoint, Vector3 outward, Vector3 along)
    {
        var (text, color) = NeonSigns[rng.Next(NeonSigns.Length)];
        var s = Group("Neon_" + text, g);
        s.SetPositionAndRotation(facadePoint + outward * 0.9f + Vector3.up * 3.6f, Quaternion.LookRotation(outward));
        LocalBox("Placa", s, Vector3.zero, new Vector3(0.12f, 0.7f, 1.6f), Mat("Oscuro"), false);
        Text3D("Texto_A", s, new Vector3(0.07f, 0f, 0f), Quaternion.Euler(0f, -90f, 0f), text, 1.5f, 0.6f, color);
        Text3D("Texto_B", s, new Vector3(-0.07f, 0f, 0f), Quaternion.Euler(0f, 90f, 0f), text, 1.5f, 0.6f, color);
    }

    // =====================================================================
    // Edificios especiales
    // =====================================================================

    /// <summary>Dragón Negro: fachada negra y dorada a la avenida, neón rojo. Interior hueco para la cocina (Dev B).</summary>
    static void BuildDragonNegro(Transform g)
    {
        const float x0 = -54f, x1 = -42f, wall = 0.3f, h = 4.5f, doorW = 1.6f, doorH = 2.4f, cx = -48f;
        float zFront = BlocksZ[2].x + Sidewalk, zBack = zFront + Depth, y = CurbHeight;
        reserved.Add(Rect.MinMaxRect(x0, zFront, x1, zBack));
        var black = Mat("DragonNegro");
        var gold = Mat("Oro");

        Box("Pared_Oeste", g, new Vector3(x0 + wall * 0.5f, y + h * 0.5f, (zFront + zBack) * 0.5f), new Vector3(wall, h, zBack - zFront), black);
        Box("Pared_Este", g, new Vector3(x1 - wall * 0.5f, y + h * 0.5f, (zFront + zBack) * 0.5f), new Vector3(wall, h, zBack - zFront), black);
        Box("Pared_Fondo", g, new Vector3(cx, y + h * 0.5f, zBack - wall * 0.5f), new Vector3(x1 - x0, h, wall), black);
        Box("Techo", g, new Vector3(cx, y + h + 0.1f, (zFront + zBack) * 0.5f), new Vector3(x1 - x0, 0.2f, zBack - zFront), black);

        // Fachada: puerta de 1.6 x 2.4 enfrente del callejón y dos escaparates
        float zf = zFront + wall * 0.5f, l = cx - doorW * 0.5f, r = cx + doorW * 0.5f;
        foreach (var (a, b) in new[] { (x0, l), (r, x1) })
        {
            Box("Zocalo", g, new Vector3((a + b) * 0.5f, y + 0.45f, zf), new Vector3(b - a, 0.9f, wall), black);
            Box("Escaparate", g, new Vector3((a + b) * 0.5f, y + 1.9f, zf), new Vector3(b - a - 1.6f, 2f, 0.06f), Mat("Cristal"));
            Box("Pilar", g, new Vector3(a + 0.4f, y + 1.9f, zf), new Vector3(0.8f, 2f, wall), black);
            Box("Pilar", g, new Vector3(b - 0.4f, y + 1.9f, zf), new Vector3(0.8f, 2f, wall), black);
            Box("Alto", g, new Vector3((a + b) * 0.5f, y + 3.7f, zf), new Vector3(b - a, 1.6f, wall), black);
        }
        Box("Dintel", g, new Vector3(cx, y + doorH + (h - doorH) * 0.5f, zf), new Vector3(doorW, h - doorH, wall), black);
        foreach (var (px, hh) in new[] { (l - 0.05f, doorH), (r + 0.05f, doorH), (x0 + 0.05f, h), (x1 - 0.05f, h) })
            Box("Oro", g, new Vector3(px, y + hh * 0.5f, zFront - 0.02f), new Vector3(0.1f, hh, 0.06f), gold, false);
        Box("Oro_Banda", g, new Vector3(cx, y + 3f, zFront - 0.02f), new Vector3(x1 - x0, 0.12f, 0.06f), gold, false);

        // Cartel: placa negra con marco dorado y letras de neón rojo (de noche, la única luz del local)
        float sy = y + 3.75f, sz = zFront - 0.15f;
        Box("Cartel", g, new Vector3(cx, sy, sz), new Vector3(7f, 1.1f, 0.15f), black, false);
        Box("Cartel_Marco", g, new Vector3(cx, sy, sz + 0.01f), new Vector3(7.2f, 1.3f, 0.12f), gold, false);
        Text3D("Neon", g, new Vector3(cx, sy, sz - 0.1f), Quaternion.identity, "DRAGÓN NEGRO", 6.6f, 0.9f, new Color(1f, 0.15f, 0.12f), true);

        var interior = new GameObject($"InteriorLibre_x{x0 + wall:0.#}a{x1 - wall:0.#}_z{zFront + wall:0.#}a{zBack - wall:0.#}_suelo{y:0.##}");
        interior.transform.SetParent(g, false);
        interior.transform.position = new Vector3(cx, y, (zFront + zBack) * 0.5f);
    }

    /// <summary>Asociación de Comerciantes (sede de Maicol, «El Paisa»), a la avenida enfrente de la plaza.</summary>
    static void BuildAssociation(Transform g)
    {
        float zFront = BlocksZ[2].x + Sidewalk;
        var b = MeshBounds("CityKitCommercial/building-n");
        float w = b.size.x * BuildingScale;
        reserved.Add(Rect.MinMaxRect(-w * 0.5f, zFront, w * 0.5f, zFront + Depth));
        var scale = new Vector3(BuildingScale, BuildingScale, Depth / b.size.z);
        var go = Kenney("CityKitCommercial/building-n", g, new Vector3(-b.center.x * scale.x, CurbHeight, zFront - b.min.z * scale.z), 0f, true);
        go.transform.localScale = scale;
        // Rótulo dorado y banderas en la fachada (mira a -Z, hacia la avenida)
        Box("Placa", g, new Vector3(0f, 4.9f, zFront - 0.12f), new Vector3(9.4f, 0.75f, 0.12f), Mat("Marmol"), false);
        Text3D("Rotulo", g, new Vector3(0f, 4.9f, zFront - 0.2f), Quaternion.identity, "ASOCIACIÓN DE COMERCIANTES", 9f, 0.55f, new Color(0.95f, 0.78f, 0.3f), true);
        for (int s = -1; s <= 1; s += 2)
        {
            Cyl("Mastil", g, new Vector3(s * 5.6f, 5.9f, zFront - 0.6f), 0.04f, 2.6f, Mat("Metal"), false);
            Box("Bandera", g, new Vector3(s * 5.6f + 0.6f, 6.8f, zFront - 0.6f), new Vector3(1.2f, 0.7f, 0.02f), Mat(s < 0 ? "BanderaAzul" : "BanderaAmarilla"), false);
        }
    }

    // =====================================================================
    // Perímetro (edificios fuera de la ronda), plaza, fondo y límites
    // =====================================================================

    static int BuildPerimeter(Transform g)
    {
        const float band = Sidewalk + Depth, o = 2.5f;   // o: ancho de acera
        var gapsX = new List<Vector2> { new(-28f, -20f), new(20f, 28f) };
        var gapsZ = new List<Vector2> { new(-28f, -20f), new(20f, 36f) };
        int n = 0;
        // Sur y norte (con las esquinas); oeste y este entre medias
        foreach (var (zLine, outward, z0, z1) in new[] { (MinZ - o, Vector3.forward, MinZ - band, MinZ), (MaxZ + o, Vector3.back, MaxZ, MaxZ + band) })
        {
            foreach (var s in Spans(MinX - band, MaxX + band, new List<Vector2>(gapsX)))
                Box("Acera", g, new Vector3((s.x + s.y) * 0.5f, (CurbHeight - 0.1f) * 0.5f, (z0 + z1) * 0.5f), new Vector3(s.y - s.x, CurbHeight + 0.1f, band), Mat("Acera"));
            foreach (var s in Spans(MinX - band, MaxX + band, new List<Vector2>(gapsX)))
                n += RowSegment(g, new Vector3(s.x, CurbHeight, zLine), Vector3.right, s.y - s.x, Depth, outward);
        }
        foreach (var (xLine, outward, x0, x1) in new[] { (MinX - o, Vector3.right, MinX - band, MinX), (MaxX + o, Vector3.left, MaxX, MaxX + band) })
        {
            foreach (var s in Spans(MinZ, MaxZ, new List<Vector2>(gapsZ)))
                Box("Acera", g, new Vector3((x0 + x1) * 0.5f, (CurbHeight - 0.1f) * 0.5f, (s.x + s.y) * 0.5f), new Vector3(band, CurbHeight + 0.1f, s.y - s.x), Mat("Acera"));
            foreach (var s in Spans(MinZ - o, MaxZ + o, new List<Vector2>(gapsZ)))
                n += RowSegment(g, new Vector3(xLine, CurbHeight, s.x), Vector3.forward, s.y - s.x, Depth, outward);
        }
        return n;
    }

    static void BuildPlaza(Transform g)
    {
        Box("Suelo", g, new Vector3(0f, (CurbHeight - 0.1f) * 0.5f, 0f), new Vector3(40f, CurbHeight + 0.1f, 40f), Mat("Plaza"));
        float y = CurbHeight;
        // Fuente central
        Cyl("Pila", g, new Vector3(0f, y + 0.3f, 0f), 3.2f, 0.6f, Mat("Piedra"), true);
        Cyl("Agua", g, new Vector3(0f, y + 0.58f, 0f), 2.9f, 0.05f, Mat("Agua"), false);
        Cyl("Columna", g, new Vector3(0f, y + 1.2f, 0f), 0.35f, 1.6f, Mat("Piedra"), true);
        Cyl("Taza", g, new Vector3(0f, y + 2f, 0f), 1.1f, 0.25f, Mat("Piedra"), false);
        foreach (var (dx, dz) in new[] { (1, 1), (-1, 1), (1, -1), (-1, -1) })
        {
            Tree(g, new Vector3(dx * 10f, y, dz * 10f));
            Tree(g, new Vector3(dx * 14f, y, dz * 6f));
            Bench(g, new Vector3(dx * 6.5f, y, dz * 10f), Quaternion.LookRotation(new Vector3(-dx, 0f, 0f)));
            Lamp(g, new Vector3(dx * 5f, y, dz * 5f), new Vector3(dx, 0f, dz));
        }
        // Terraza del bar de la plaza con sombrillas de Kenney
        for (int k = 0; k < 3; k++)
            Kenney("CityKitCommercial/detail-parasol-" + (k % 2 == 0 ? "a" : "b"), g, new Vector3(-14f + k * 4f, y, -15f), rng.Next(4) * 90f, false, BuildingScale);
    }

    static void BuildBackground(Transform g)
    {
        // Bloques lejanos sin collider para tapar el horizonte (la niebla los funde)
        for (float x = -150f; x <= 150f; x += 16f)
            for (float z = -146f; z <= 154f; z += 16f)
            {
                if (Mathf.Abs(x) < 98f && z > -98f && z < 106f) continue;
                if (rng.NextDouble() < 0.25) continue;
                var go = Kenney("CityKitCommercial/low-detail-building-" + (char)('a' + rng.Next(14)), g, new Vector3(x + Range(-2f, 2f), 0f, z + Range(-2f, 2f)), rng.Next(4) * 90f, false);
                go.transform.localScale = new Vector3(Range(18f, 26f), Range(7f, 14f), Range(18f, 26f));
            }
    }

    static void BuildLimits(Transform g)
    {
        foreach (var (c, s) in new[] { (new Vector3(-89f, 3f, 4f), new Vector3(1f, 6f, 200f)), (new Vector3(89f, 3f, 4f), new Vector3(1f, 6f, 200f)),
                                       (new Vector3(0f, 3f, -89f), new Vector3(200f, 6f, 1f)), (new Vector3(0f, 3f, 97f), new Vector3(200f, 6f, 1f)) })
            Box("Limite", g, c, s, null);
    }

    // =====================================================================
    // Mobiliario: farolas, coches aparcados, contenedores y semáforos
    // =====================================================================

    static void BuildStreetProps(Transform g)
    {
        // Farolas en cada lado de manzana que da a una calle (el brazo cuelga sobre la calzada)
        for (int i = 0; i < 3; i++)
            for (int j = 0; j < 3; j++)
            {
                if (i == 1 && j == 1) continue;   // la plaza tiene las suyas
                var r = Rect.MinMaxRect(BlocksX[i].x, BlocksZ[j].x, BlocksX[i].y, BlocksZ[j].y);
                for (float t = 6.7f; t < 40f; t += 13.3f)
                {
                    Lamp(g, new Vector3(r.xMin + t, CurbHeight, r.yMin + 0.5f), Vector3.back);
                    Lamp(g, new Vector3(r.xMin + t, CurbHeight, r.yMax - 0.5f), Vector3.forward);
                    Lamp(g, new Vector3(r.xMin + 0.5f, CurbHeight, r.yMin + t), Vector3.left);
                    Lamp(g, new Vector3(r.xMax - 0.5f, CurbHeight, r.yMin + t), Vector3.right);
                }
            }
        for (float t = -14f; t <= 14f; t += 14f)
        {
            Lamp(g, new Vector3(t + 7f, CurbHeight, -20f + 0.5f), Vector3.back);
            Lamp(g, new Vector3(t, CurbHeight, 20f - 0.5f), Vector3.forward);
            Lamp(g, new Vector3(-20f + 0.5f, CurbHeight, t), Vector3.left);
            Lamp(g, new Vector3(20f - 0.5f, CurbHeight, t + 7f), Vector3.right);
        }

        // Coches aparcados y contenedores junto al bordillo, un poco al azar por tramo de calle
        var segments = new List<(Vector3 a, Vector3 dir, Vector3 side, float halfWidth)>();
        foreach (float x in StreetX) foreach (var bz in BlocksZ) segments.Add((new Vector3(x, 0f, bz.x), Vector3.forward, Vector3.right, 4f));
        foreach (float z in StreetZ) foreach (var bx in BlocksX) segments.Add((new Vector3(bx.x, 0f, z), Vector3.right, Vector3.forward, 4f));
        foreach (var bx in BlocksX) segments.Add((new Vector3(bx.x, 0f, AvenueZ), Vector3.right, Vector3.forward, 8f));
        foreach (var (a, dir, side, half) in segments)
        {
            float sgn = rng.NextDouble() < 0.5 ? -1f : 1f;
            if (rng.NextDouble() < 0.75)
            {
                var p = a + dir * Range(7f, 33f) + side * sgn * (half - 1.3f);
                if (IsClear(p, 6f)) { Kenney("CarKit/" + Cars[rng.Next(Cars.Length)], g, p, Quaternion.LookRotation(dir * (rng.NextDouble() < 0.5 ? 1f : -1f)).eulerAngles.y, true, CarScale); keepClear.Add(p); }
            }
            if (rng.NextDouble() < 0.5)
            {
                var p = a + dir * Range(8f, 32f) - side * sgn * (half - 1f);
                if (IsClear(p, 4f)) { Kenney("CityKitRoads/dumpster", g, p, Quaternion.LookRotation(dir).eulerAngles.y, true, 6f); keepClear.Add(p); }
            }
        }

        // Semáforos en los cruces de la avenida
        foreach (float x in new[] { -24f, 24f })
            foreach (var (dx, dz) in new[] { (-1f, -1f), (1f, -1f), (-1f, 1f), (1f, 1f) })
                Kenney("CityKitRoads/traffic-light", g, new Vector3(x + dx * 4.6f, CurbHeight, AvenueZ + dz * 8.6f), dz < 0 ? 180f : 0f, true, RoadScale);
    }

    static void Lamp(Transform g, Vector3 pos, Vector3 toStreet)
    {
        if (!IsClear(pos, 2.5f)) return;
        var go = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Barrio/Farola.prefab"), g.gameObject.scene);
        go.transform.SetParent(g, false);
        go.transform.SetPositionAndRotation(pos, Quaternion.LookRotation(-toStreet));   // el brazo de la farola va hacia -Z
    }

    static bool IsClear(Vector3 p, float margin)
    {
        foreach (var c in keepClear) if (Vector2.Distance(new Vector2(p.x, p.z), new Vector2(c.x, c.z)) < margin) return false;
        return true;
    }

    // =====================================================================
    // Baches (para las caídas de T20)
    // =====================================================================

    static void BuildPotholes(Transform g)
    {
        // Sobre todo en la ruta lenta (callejones y ronda exterior): sin policía, pero con más baches
        var spots = new[]
        {
            new Vector3(-48.8f, 0f, -8f), new Vector3(-47.2f, 0f, 9f), new Vector3(-6f, 0f, -47.3f), new Vector3(9f, 0f, -48.7f),
            new Vector3(-70.5f, 0f, -40f), new Vector3(-30f, 0f, -73.5f), new Vector3(40f, 0f, -70.2f), new Vector3(70.5f, 0f, -10f),
            new Vector3(73.2f, 0f, 50f), new Vector3(12f, 0f, 78.4f), new Vector3(-60f, 0f, 81.4f), new Vector3(-25.4f, 0f, -52f),
        };
        int n = 0;
        foreach (var p in spots)
        {
            var b = Cyl("Bache_" + (++n), g, p + Vector3.up * 0.006f, Range(0.45f, 0.8f), 0.012f, Mat("Bache"), false);
            var trigger = b.AddComponent<BoxCollider>();   // T20 detectará la caída con este trigger
            trigger.isTrigger = true;
            trigger.size = new Vector3(1f, 80f, 1f);
        }
    }

    // =====================================================================
    // Juego: marcadores, entregas y controles
    // =====================================================================

    static void BuildMarkers(Transform g)
    {
        float zFront = BlocksZ[2].x + Sidewalk;
        Marker("SpawnJugador", g, new Vector3(-48f, CurbHeight + 0.05f, zFront + 7f), 180f);
        var sp = Marker("SpawnPatinete", g, new Vector3(-51.5f, CurbHeight + 1f, zFront - 1.2f), 90f);
        keepClear.Add(sp.position);
        keepClear.Add(new Vector3(-48f, 0f, zFront));                 // puerta del Dragón Negro
        keepClear.Add(new Vector3(-48f, 0f, 20f)); keepClear.Add(new Vector3(-48f, 0f, -20f));   // bocas del callejón oeste
        keepClear.Add(new Vector3(-20f, 0f, -48f)); keepClear.Add(new Vector3(20f, 0f, -48f));   // bocas del callejón sur
        keepClear.Add(new Vector3(0f, 0f, 20f)); keepClear.Add(new Vector3(0f, 0f, 36f));        // paso de peatones de la plaza
    }

    static void BuildDeliveries(Transform g)
    {
        float zFront = BlocksZ[2].x + Sidewalk;
        var recogida = Marker("Recogida", g, new Vector3(-48f, CurbHeight, zFront - 1.25f), 0f);
        TriggerBox(recogida, new Vector3(0f, 1f, 0f), new Vector3(3f, 2f, 2.5f));

        // Entregas en la acera delante de un portal. El nombre coincide con destinationId de NightConfig.
        var entregas = new (string id, Vector3 pos, Vector3 toStreet)[]
        {
            ("Entrega1", new Vector3(BlocksX[2].x + 1.25f, 0f, -48f), Vector3.left),       // SE, frente al callejón sur
            ("Entrega2", new Vector3(50f, 0f, BlocksZ[1].y - 1.25f), Vector3.forward),     // E, en la avenida
            ("Entrega3", new Vector3(-40f, 0f, BlocksZ[0].x + 1.25f), Vector3.back),       // SO, ronda sur
            ("Entrega4", new Vector3(40f, 0f, BlocksZ[2].y - 1.25f), Vector3.forward),     // NE, ronda norte
            ("Entrega5", new Vector3(BlocksX[0].x + 1.25f, 0f, -5f), Vector3.left),        // O, ronda oeste
        };
        int k = 0;
        foreach (var (id, pos, toStreet) in entregas)
        {
            var e = Marker(id, g, pos + Vector3.up * CurbHeight, Quaternion.LookRotation(toStreet).eulerAngles.y);
            keepClear.Add(e.position);
            TriggerBox(e, new Vector3(0f, 1f, 0f), new Vector3(3f, 2f, 2.5f));
            // Luz cálida sobre el portal y el cliente esperando
            var lightGo = new GameObject("LuzPortal");
            lightGo.transform.SetParent(e, false);
            lightGo.transform.localPosition = new Vector3(0f, 3f, -0.8f);
            var light = lightGo.AddComponent<Light>();
            light.type = LightType.Point; light.range = 7f; light.intensity = 3f; light.color = new Color(1f, 0.8f, 0.5f);
            Person(Neighbors[(k++ * 3) % Neighbors.Length], e, e.position + e.rotation * new Vector3(0.6f, 0f, -0.3f), e.rotation.eulerAngles.y);
        }
    }

    /// <summary>Controles policiales como zonas (T08/T21). Devuelve los puntos donde aparece el agente.</summary>
    static List<Transform> BuildControls(Transform g)
    {
        var defs = new (string name, Vector3 agent, float yaw, Vector3 car, float carYaw, bool active)[]
        {
            // 1: avenida delante de la Asociación, mirando al oeste (por donde llega la ruta rápida desde el Dragón Negro)
            ("Control_1_Avenida", new Vector3(8f, 0f, AvenueZ), 270f, new Vector3(13f, 0f, AvenueZ + 4.5f), 90f, true),
            // 2: calle este de la plaza, mirando al norte (bajada desde la avenida)
            ("Control_2_CalleEste", new Vector3(21.5f, 0f, -4f), 0f, new Vector3(26.2f, 0f, -9f), 0f, true),
            // 3: calle sur de la plaza, mirando al oeste. Solo se activa con Sospecha 40-69 (T21)
            ("Control_3_CalleSur", new Vector3(-4f, 0f, -21.5f), 270f, new Vector3(2f, 0f, -26.2f), 90f, false),
        };
        var points = new List<Transform>();
        foreach (var d in defs)
        {
            var c = Group(d.name, g);
            points.Add(Marker("PuntoAgente", c, d.agent + Vector3.up, d.yaw));
            keepClear.Add(d.agent); keepClear.Add(d.car);
            Kenney("CarKit/police", c, d.car, d.carYaw, true, CarScale);
            Vector3 fwd = Quaternion.Euler(0f, d.yaw, 0f) * Vector3.forward, right = Vector3.Cross(Vector3.up, fwd);
            // Conos que estrechan el paso delante del agente y una valla a su lado
            for (int k = 0; k < 5; k++)
                Kenney("CarKit/cone", c, d.agent + fwd * (3f + k * 1.5f) + right * (1.2f + k * 0.35f), 0f, false, 1.2f);
            Kenney("CityKitRoads/construction-barrier", c, d.agent - right * 1.5f, d.yaw, true, RoadScale);
            c.gameObject.SetActive(d.active);
        }
        return points;
    }

    static void SetupSpawner(List<Transform> points)
    {
        var spawner = Object.FindFirstObjectByType<CitySpawner>();
        if (spawner == null) return;
        var so = new SerializedObject(spawner);
        var prop = so.FindProperty("policeSpawnPoints");
        prop.arraySize = points.Count;
        for (int k = 0; k < points.Count; k++) prop.GetArrayElementAtIndex(k).objectReferenceValue = points[k];
        var police = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Barrio/PoliceMan_Kenney.prefab");
        if (police != null) so.FindProperty("policePrefab").objectReferenceValue = police;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    static void SetupEnvironment()
    {
        string skyPath = MaterialFolder + "/SKY_Noche.mat";
        var sky = AssetDatabase.LoadAssetAtPath<Material>(skyPath);
        if (sky == null) { sky = new Material(Shader.Find("Skybox/Procedural")); AssetDatabase.CreateAsset(sky, skyPath); }
        sky.SetFloat("_SunDisk", 0f);
        sky.SetFloat("_AtmosphereThickness", 0.35f);
        sky.SetColor("_SkyTint", new Color(0.18f, 0.2f, 0.35f));
        sky.SetColor("_GroundColor", new Color(0.05f, 0.05f, 0.08f));
        sky.SetFloat("_Exposure", 0.35f);
        EditorUtility.SetDirty(sky);

        RenderSettings.skybox = sky;
        RenderSettings.ambientMode = AmbientMode.Flat;
        RenderSettings.ambientLight = new Color(0.22f, 0.24f, 0.34f);
        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.Linear;
        RenderSettings.fogStartDistance = 40f;
        RenderSettings.fogEndDistance = 170f;
        RenderSettings.fogColor = new Color(0.07f, 0.08f, 0.13f);

        Light moon = null;
        foreach (var l in Object.FindObjectsByType<Light>(FindObjectsSortMode.None))
            if (l.type == LightType.Directional) { moon = l; break; }
        if (moon == null) moon = new GameObject("Luna").AddComponent<Light>();
        moon.type = LightType.Directional;
        moon.gameObject.name = "Luna";
        moon.color = new Color(0.65f, 0.72f, 1f);
        moon.intensity = 0.45f;
        moon.shadows = LightShadows.Soft;
        moon.transform.rotation = Quaternion.Euler(35f, 150f, 0f);
    }

    // =====================================================================
    // Piezas
    // =====================================================================

    /// <summary>Instancia un modelo de Kenney (enlazado al FBX) con un collider de caja ajustado a su malla.</summary>
    static GameObject Kenney(string model, Transform parent, Vector3 pos, float yaw, bool collider, float scale = 1f)
    {
        if (!models.TryGetValue(model, out var prefab))
        {
            prefab = AssetDatabase.LoadAssetAtPath<GameObject>(K + model.Replace("/", "/Models/") + ".fbx");
            if (prefab == null) throw new System.Exception("No existe el modelo de Kenney " + model);
            models[model] = prefab;
        }
        var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent.gameObject.scene);
        go.transform.SetParent(parent, false);
        go.transform.localScale = Vector3.one * scale;
        if (collider)
        {
            var b = new Bounds();
            bool first = true;
            foreach (var r in go.GetComponentsInChildren<Renderer>())
            {
                var lb = r.localBounds;
                var c = go.transform.InverseTransformPoint(r.transform.TransformPoint(lb.center));
                var size = Vector3.Scale(lb.size, Div(r.transform.lossyScale, go.transform.lossyScale));
                var rb = new Bounds(c, size);
                if (first) { b = rb; first = false; } else b.Encapsulate(rb);
            }
            var box = go.AddComponent<BoxCollider>();
            box.center = b.center; box.size = b.size;
        }
        go.transform.SetPositionAndRotation(pos, Quaternion.Euler(0f, yaw, 0f));
        return go;
    }

    static Vector3 Div(Vector3 a, Vector3 b) => new Vector3(a.x / b.x, a.y / b.y, a.z / b.z);

    static Bounds MeshBounds(string model)
    {
        var mf = AssetDatabase.LoadAssetAtPath<GameObject>(K + model.Replace("/", "/Models/") + ".fbx").GetComponentInChildren<MeshFilter>();
        return mf.sharedMesh.bounds;
    }

    /// <summary>Vecino de Kenney quieto (animación idle en bucle).</summary>
    static void Person(string model, Transform parent, Vector3 pos, float yaw)
    {
        var go = Kenney("MiniCharacters/character-" + model, parent, pos, yaw, false, PersonScale);
        var anim = go.GetComponent<Animator>() ?? go.AddComponent<Animator>();
        anim.runtimeAnimatorController = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>("Assets/Animations/AC_PersonajeQuieto.controller");
        foreach (var o in AssetDatabase.LoadAllAssetsAtPath(K + "MiniCharacters/Models/character-" + model + ".fbx"))
            if (o is AnimationClip clip && clip.name == "idle") clip.SampleAnimation(go, 0f);   // que no se vea en pose T en el editor
        var col = go.AddComponent<CapsuleCollider>();
        col.center = new Vector3(0f, 0.35f, 0f); col.radius = 0.15f; col.height = 0.7f;
    }

    static void Tree(Transform g, Vector3 basePos)
    {
        float h = Range(3.5f, 5f);
        var trunk = Cyl("Tronco", g, basePos + Vector3.up * h * 0.5f, 0.2f, h, Mat("Tronco"), true);
        Box("Copa", g, basePos + Vector3.up * (h + 0.8f), new Vector3(3.2f, 2.2f, 3.2f), Mat("Hoja"), false).transform.rotation = Quaternion.Euler(0f, Range(0f, 90f), 0f);
        Box("Copa", g, basePos + new Vector3(0.3f, h + 2f, -0.2f), new Vector3(2f, 1.4f, 2f), Mat("Hoja"), false).transform.rotation = Quaternion.Euler(0f, Range(0f, 90f), 0f);
    }

    static void Bench(Transform g, Vector3 pos, Quaternion rot)
    {
        var b = Group("Banco", g);
        b.SetPositionAndRotation(pos, rot);
        LocalBox("Asiento", b, new Vector3(0f, 0.45f, 0f), new Vector3(1.8f, 0.08f, 0.5f), Mat("Madera"), true);
        LocalBox("Respaldo", b, new Vector3(0f, 0.75f, -0.25f), new Vector3(1.8f, 0.5f, 0.06f), Mat("Madera"), false);
        LocalBox("Patas", b, new Vector3(0f, 0.22f, 0f), new Vector3(1.6f, 0.45f, 0.4f), Mat("Metal"), false);
    }

    static Transform Marker(string name, Transform parent, Vector3 pos, float yaw)
    {
        var t = new GameObject(name).transform;
        t.SetParent(parent, false);
        t.SetPositionAndRotation(pos, Quaternion.Euler(0f, yaw, 0f));
        return t;
    }

    static void TriggerBox(Transform t, Vector3 center, Vector3 size)
    {
        var c = t.gameObject.AddComponent<BoxCollider>();
        c.isTrigger = true; c.center = center; c.size = size;
    }

    /// <summary>Texto 3D de TextMeshPro. Se lee desde su -Z local.</summary>
    static void Text3D(string name, Transform parent, Vector3 pos, Quaternion rot, string text, float width, float height, Color color, bool world = false)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        if (world) go.transform.SetPositionAndRotation(pos, rot);
        else { go.transform.localPosition = pos; go.transform.localRotation = rot; }
        var tmp = go.AddComponent<TextMeshPro>();
        tmp.text = text;
        tmp.enableAutoSizing = true; tmp.fontSizeMin = 1f; tmp.fontSizeMax = 20f;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.fontStyle = FontStyles.Bold;
        tmp.color = color;
        tmp.rectTransform.sizeDelta = new Vector2(width, height);
    }

    static float Range(float a, float b) => a + (float)rng.NextDouble() * (b - a);

    static Transform Group(string name, Transform parent)
    {
        var t = new GameObject(name).transform;
        t.SetParent(parent, false);
        return t;
    }

    /// <summary>Caja en coordenadas del mundo. Sin material = solo collider invisible.</summary>
    static GameObject Box(string name, Transform parent, Vector3 center, Vector3 size, Material m, bool collider = true)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = name;
        if (!collider) Object.DestroyImmediate(go.GetComponent<Collider>());
        if (m == null) Object.DestroyImmediate(go.GetComponent<MeshRenderer>());
        else go.GetComponent<MeshRenderer>().sharedMaterial = m;
        go.transform.SetParent(parent, false);
        go.transform.position = center;
        go.transform.localScale = size;
        return go;
    }

    static GameObject Cyl(string name, Transform parent, Vector3 center, float radius, float height, Material m, bool collider)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        go.name = name;
        Object.DestroyImmediate(go.GetComponent<Collider>());
        go.transform.SetParent(parent, false);
        go.transform.position = center;
        go.transform.localScale = new Vector3(radius * 2f, height * 0.5f, radius * 2f);
        if (collider) { var c = go.AddComponent<CapsuleCollider>(); c.radius = 0.5f; c.height = 2f; }
        go.GetComponent<MeshRenderer>().sharedMaterial = m;
        return go;
    }

    static void LocalBox(string name, Transform parent, Vector3 localCenter, Vector3 size, Material m, bool collider)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = name;
        if (!collider) Object.DestroyImmediate(go.GetComponent<Collider>());
        go.transform.SetParent(parent, false);
        go.transform.localPosition = localCenter;
        go.transform.localScale = size;
        go.GetComponent<MeshRenderer>().sharedMaterial = m;
    }

    // ---------------- Materiales (solo para las piezas hechas con primitivas) ----------------

    static readonly Dictionary<string, (Color color, Color emission, float smooth)> Palette = new()
    {
        { "Asfalto", (new Color(0.22f, 0.22f, 0.27f), Color.black, 0.15f) },
        { "Acera", (new Color(0.62f, 0.6f, 0.66f), Color.black, 0.1f) },
        { "Plaza", (new Color(0.72f, 0.6f, 0.48f), Color.black, 0.1f) },
        { "Piedra", (new Color(0.78f, 0.75f, 0.68f), Color.black, 0.1f) },
        { "Agua", (new Color(0.2f, 0.45f, 0.6f), Color.black, 0.9f) },
        { "Hoja", (new Color(0.18f, 0.4f, 0.18f), Color.black, 0.05f) },
        { "Tronco", (new Color(0.35f, 0.25f, 0.17f), Color.black, 0.05f) },
        { "Madera", (new Color(0.55f, 0.36f, 0.2f), Color.black, 0.1f) },
        { "Metal", (new Color(0.25f, 0.27f, 0.3f), Color.black, 0.5f) },
        { "Oscuro", (new Color(0.1f, 0.1f, 0.12f), Color.black, 0.2f) },
        { "Cristal", (new Color(0.1f, 0.14f, 0.18f), Color.black, 0.9f) },
        { "Marmol", (new Color(0.92f, 0.9f, 0.86f), Color.black, 0.6f) },
        { "DragonNegro", (new Color(0.04f, 0.04f, 0.05f), Color.black, 0.6f) },
        { "Oro", (new Color(0.85f, 0.65f, 0.2f), new Color(0.25f, 0.18f, 0.04f), 0.8f) },
        { "BanderaAzul", (new Color(0.15f, 0.3f, 0.7f), Color.black, 0.1f) },
        { "BanderaAmarilla", (new Color(0.95f, 0.8f, 0.2f), Color.black, 0.1f) },
        { "Bache", (new Color(0.07f, 0.07f, 0.08f), Color.black, 0.3f) },
    };

    static Material Mat(string name)
    {
        if (materials.TryGetValue(name, out var cached)) return cached;
        if (!AssetDatabase.IsValidFolder(MaterialFolder)) AssetDatabase.CreateFolder("Assets/Materials", "Barrio");
        string path = $"{MaterialFolder}/M_{name}.mat";
        var m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (m == null) { m = new Material(Shader.Find("Universal Render Pipeline/Lit")); AssetDatabase.CreateAsset(m, path); }
        var (color, emission, smooth) = Palette[name];
        m.SetColor("_BaseColor", color);
        m.SetFloat("_Smoothness", smooth);
        bool emissive = emission.maxColorComponent > 0f;
        m.SetColor("_EmissionColor", emission);
        m.SetKeyword(new LocalKeyword(m.shader, "_EMISSION"), emissive);
        m.globalIlluminationFlags = emissive ? MaterialGlobalIlluminationFlags.RealtimeEmissive : MaterialGlobalIlluminationFlags.EmissiveIsBlack;
        EditorUtility.SetDirty(m);
        AssetDatabase.SaveAssetIfDirty(m);
        materials[name] = m;
        return m;
    }
}
