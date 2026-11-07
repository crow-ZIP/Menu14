using TMPro;
using UnityEngine;

public class DetectionIndicator : MonoBehaviour
{
    [SerializeField] private PoliceOfficer officer;
    [SerializeField] private TMP_Text label;

    [SerializeField] private Color startColor = new Color(1f, 0.85f, 0.2f);
    [SerializeField] private Color caughtColor = new Color(1f, 0.25f, 0.2f);
    [SerializeField] private float minScale = 0.5f;
    [SerializeField] private float maxScale = 1f;

    private Vector3 baseScale;

    private void Awake()
    {
        if (officer == null) officer = GetComponentInParent<PoliceOfficer>();
        if (label == null) label = GetComponent<TMP_Text>();
        baseScale = transform.localScale;
    }

    private void LateUpdate()
    {
        if (officer == null || label == null) return;

        float progress = officer.DetectionProgress;
        label.enabled = progress > 0f;
        if (!label.enabled) return;

        label.color = Color.Lerp(startColor, caughtColor, progress);
        transform.localScale = baseScale * Mathf.Lerp(minScale, maxScale, progress);

        Camera cam = Camera.main;
        if (cam != null) transform.rotation = cam.transform.rotation;
    }
}
