using UnityEngine;

public class FaceCamera : MonoBehaviour
{
    [Tooltip("Si es mayor que 0, solo se ve cuando la cámara está a menos de estos metros")]
    public float showDistance;

    Renderer view;

    void Awake() => view = GetComponent<Renderer>();

    void LateUpdate()
    {
        Camera cam = Camera.main;
        if (cam == null) return;

        if (view != null && showDistance > 0f)
        {
            bool near = Vector3.Distance(cam.transform.position, transform.position) <= showDistance;
            if (view.enabled != near) view.enabled = near;
            if (!near) return;
        }
        transform.rotation = Quaternion.LookRotation(transform.position - cam.transform.position);
    }
}
