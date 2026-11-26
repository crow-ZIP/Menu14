using System.Collections.Generic;
using UnityEngine;

public class LaneBlock : MonoBehaviour
{
    public static readonly List<LaneBlock> Active = new List<LaneBlock>();

    [Tooltip("Metros de carril que ocupa el obstáculo, a lo largo de la calle")]
    public float length = 7f;
    [Tooltip("Metros de ancho que ocupa, centrados en el carril")]
    public float width = 2.2f;
    [Tooltip("Si está desmarcado, la calle queda cortada: los coches se paran detrás y hacen cola en vez de adelantar")]
    public bool canPass = true;

    public float HalfLength => length * 0.5f;

    void OnEnable() => Active.Add(this);
    void OnDisable() => Active.Remove(this);

    public static bool Covers(Vector3 point, float margin)
    {
        foreach (LaneBlock block in Active)
        {
            Vector3 local = block.transform.InverseTransformPoint(point);
            if (Mathf.Abs(local.x) <= block.HalfLength + margin && Mathf.Abs(local.z) <= block.width * 0.5f + margin)
                return true;
        }
        return false;
    }

    void OnDrawGizmos()
    {
        Gizmos.color = new Color(1f, 0.4f, 0f);
        Gizmos.matrix = transform.localToWorldMatrix;
        Gizmos.DrawWireCube(Vector3.up * 0.5f, new Vector3(length, 1f, width));
    }
}
