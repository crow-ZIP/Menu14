using UnityEngine;

public class ObstacleSpot : MonoBehaviour
{
    public ObstacleZone zone = ObstacleZone.Acera;

    [Tooltip("Zona libre en metros: X a lo largo de la calle y Z a lo ancho. En las aceras, +Z mira hacia la calzada")]
    public Vector2 area = new Vector2(7f, 2f);

    public Vector3 ClampToArea(Vector3 world)
    {
        Vector3 local = transform.InverseTransformPoint(world);
        local.x = Mathf.Clamp(local.x, -area.x * 0.5f, area.x * 0.5f);
        local.z = Mathf.Clamp(local.z, -area.y * 0.5f, area.y * 0.5f);
        local.y = 0f;
        return transform.TransformPoint(local);
    }

    public Vector3 RandomPointInArea()
    {
        var local = new Vector3(Random.Range(-0.5f, 0.5f) * area.x, 0f, Random.Range(-0.5f, 0.5f) * area.y);
        return transform.TransformPoint(local);
    }

    void OnDrawGizmos()
    {
        Gizmos.color = zone switch
        {
            ObstacleZone.Acera => new Color(1f, 0.8f, 0.2f),
            ObstacleZone.Callejon => new Color(0.6f, 0.4f, 1f),
            ObstacleZone.Plaza => new Color(0.3f, 1f, 0.5f),
            _ => new Color(1f, 0.3f, 0.3f),
        };
        Gizmos.matrix = transform.localToWorldMatrix;
        Gizmos.DrawWireCube(Vector3.up * 0.1f, new Vector3(area.x, 0.2f, area.y));
        Gizmos.DrawLine(Vector3.up * 0.1f, new Vector3(0f, 0.1f, area.y * 0.5f));
    }
}
