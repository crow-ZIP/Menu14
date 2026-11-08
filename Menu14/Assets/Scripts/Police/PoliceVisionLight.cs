using UnityEngine;

[RequireComponent(typeof(Light))]
public class PoliceVisionLight : MonoBehaviour
{
    [SerializeField] private PoliceOfficer officer;
    [SerializeField] private Color calmColor = new Color(0.85f, 0.92f, 1f);
    [SerializeField] private Color alertColor = new Color(1f, 0.25f, 0.2f);
    [Tooltip("Grados que el foco apunta hacia abajo para iluminar el suelo")]
    [SerializeField] private float tiltDown = 20f;
    [Tooltip("Metros de más sobre la vista del policía: la luz se apaga poco a poco al final de su alcance")]
    [SerializeField] private float rangeMargin = 3f;

    private Light spot;

    private void Awake()
    {
        spot = GetComponent<Light>();
        if (officer == null) officer = GetComponentInParent<PoliceOfficer>();
    }

    private void Start()
    {
        if (officer == null) return;

        spot.type = LightType.Spot;
        spot.spotAngle = officer.viewAngle;
        spot.range = officer.viewDistance + rangeMargin;
        transform.localPosition = Vector3.up * officer.eyeHeight;
        transform.localRotation = Quaternion.Euler(tiltDown, 0f, 0f);
    }

    private void Update()
    {
        if (officer == null) return;
        spot.color = Color.Lerp(calmColor, alertColor, officer.DetectionProgress);
    }
}
