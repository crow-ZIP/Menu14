using UnityEngine;

/// <summary>
/// Corta el tráfico en una calle. Ponlo en cada control policial (Control_1_Avenida, Control_2_CalleEste...).
/// La zona cortada es la caja que ocupan los modelos hijos (conos, barrera, coche patrulla) más 'margin'.
/// TrafficNetwork quita del grafo los tramos de calle que pasan por esa zona, así que los coches
/// ni aparecen ahí ni eligen ese camino. Se aplica al darle a Play (al construir la red).
/// Selecciona el objeto para ver la zona en naranja; los tramos cortados se ven en rojo al seleccionar 'Trafico'.
/// </summary>
public class TrafficBlocker : MonoBehaviour
{
    [Tooltip("Metros extra alrededor de los conos, la barrera y el coche patrulla")]
    public float margin = 1f;

    /// <summary>Zona ocupada por el control (solo X/Z importan).</summary>
    public Bounds Area
    {
        get
        {
            Bounds b = new Bounds(transform.position, Vector3.zero);
            bool first = true;
            foreach (Renderer r in GetComponentsInChildren<Renderer>())
            {
                if (first) { b = r.bounds; first = false; }
                else b.Encapsulate(r.bounds);
            }
            b.Expand(new Vector3(margin * 2f, 0f, margin * 2f));
            return b;
        }
    }

    /// <summary>¿La calle de 'a' a 'b' pasa por la zona? 'roadHalfWidth' ensancha la línea central hasta los carriles.</summary>
    public bool BlocksRoad(Vector3 a, Vector3 b, float roadHalfWidth)
    {
        Bounds area = Area;
        float minX = area.min.x - roadHalfWidth, maxX = area.max.x + roadHalfWidth;
        float minZ = area.min.z - roadHalfWidth, maxZ = area.max.z + roadHalfWidth;

        // Recorte de segmento contra rectángulo (Liang-Barsky) en el plano XZ
        float t0 = 0f, t1 = 1f;
        float dx = b.x - a.x, dz = b.z - a.z;
        return Clip(-dx, a.x - minX, ref t0, ref t1)
            && Clip(dx, maxX - a.x, ref t0, ref t1)
            && Clip(-dz, a.z - minZ, ref t0, ref t1)
            && Clip(dz, maxZ - a.z, ref t0, ref t1);
    }

    static bool Clip(float p, float q, ref float t0, ref float t1)
    {
        if (Mathf.Abs(p) < 0.0001f) return q >= 0f;   // paralelo al borde: dentro o fuera
        float r = q / p;
        if (p < 0f)
        {
            if (r > t1) return false;
            if (r > t0) t0 = r;
        }
        else
        {
            if (r < t0) return false;
            if (r < t1) t1 = r;
        }
        return true;
    }

    void OnDrawGizmosSelected()
    {
        Bounds b = Area;
        Gizmos.color = new Color(1f, 0.5f, 0f);
        Gizmos.DrawWireCube(new Vector3(b.center.x, 0.5f, b.center.z), new Vector3(b.size.x, 1f, b.size.z));
    }
}
