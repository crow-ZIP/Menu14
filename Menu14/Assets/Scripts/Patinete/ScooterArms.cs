using UnityEngine;

/// <summary>
/// Estira cada brazo (una cápsula) desde el hombro hasta el puño del manillar en cada fotograma.
/// Así las manos siguen agarradas aunque el manillar y la cámara se inclinen distinto.
/// </summary>
public class ScooterArms : MonoBehaviour
{
    [System.Serializable]
    public struct Arm
    {
        public Transform shoulder;   // punto fijo respecto a la cámara
        public Transform grip;       // punto en el puño del manillar
        public Transform capsule;    // cápsula que hace de brazo (altura 2 a escala 1, como la primitiva de Unity)
    }

    public Arm[] arms;

    [Tooltip("Grosor del brazo (escala X y Z de la cápsula)")]
    public float thickness = 0.08f;

    void LateUpdate()
    {
        foreach (Arm arm in arms)
        {
            if (arm.shoulder == null || arm.grip == null || arm.capsule == null) continue;

            Vector3 from = arm.shoulder.position;
            Vector3 to = arm.grip.position;
            Vector3 dir = to - from;

            arm.capsule.SetPositionAndRotation((from + to) * 0.5f, Quaternion.FromToRotation(Vector3.up, dir));
            // La cápsula primitiva mide 2 de alto con escala 1
            arm.capsule.localScale = new Vector3(thickness, dir.magnitude * 0.5f, thickness);
        }
    }
}
