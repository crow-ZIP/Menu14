using UnityEngine;

public class ScooterArms : MonoBehaviour
{
    [System.Serializable]
    public struct Arm
    {
        public Transform shoulder;
        public Transform grip;
        public Transform capsule;
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
