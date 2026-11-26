using UnityEngine;

[RequireComponent(typeof(Collider))]
public class Pothole : MonoBehaviour
{
    [Tooltip("Resalto: se puede pasar más rápido que un bache sin caerse (CrashConfig.speedBumpFallSpeed)")]
    public bool speedBump;
}
