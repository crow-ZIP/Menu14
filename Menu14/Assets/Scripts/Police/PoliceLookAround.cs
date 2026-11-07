using UnityEngine;

[RequireComponent(typeof(PoliceOfficer))]
public class PoliceLookAround : MonoBehaviour
{
    [Tooltip("Grados que gira a cada lado de su dirección inicial")]
    [SerializeField] private float sweepAngle = 45f;
    [Tooltip("Velocidad de giro, en grados por segundo")]
    [SerializeField] private float turnSpeed = 35f;
    [Tooltip("Segundos que se queda mirando en cada extremo")]
    [SerializeField] private float pauseTime = 1.5f;

    private PoliceOfficer officer;
    private float baseYaw;
    private float offset;
    private int direction = 1;
    private float pauseTimer;

    private void Start()
    {
        officer = GetComponent<PoliceOfficer>();
        baseYaw = transform.eulerAngles.y;
        offset = Random.Range(-sweepAngle, sweepAngle);
        direction = Random.value < 0.5f ? -1 : 1;
    }

    private void Update()
    {
        if (officer.SeesPlayer) return;

        if (pauseTimer > 0f)
        {
            pauseTimer -= Time.deltaTime;
            return;
        }

        offset += direction * turnSpeed * Time.deltaTime;
        if (Mathf.Abs(offset) >= sweepAngle)
        {
            offset = Mathf.Clamp(offset, -sweepAngle, sweepAngle);
            direction = -direction;
            pauseTimer = pauseTime;
        }

        transform.rotation = Quaternion.Euler(0f, baseYaw + offset, 0f);
    }
}
