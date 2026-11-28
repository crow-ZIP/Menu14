using TMPro;
using UnityEngine;

public class NightClockUI : MonoBehaviour
{
    [SerializeField] private TMP_Text label;
    [Tooltip("Por debajo de estos segundos de noche, el reloj se pinta con el color de aviso")]
    [SerializeField] private float warningTime = 30f;
    [SerializeField] private Color normalColor = Color.white;
    [SerializeField] private Color warningColor = new Color(1f, 0.35f, 0.3f);

    private void Update()
    {
        GameManager gm = GameManager.Instance;
        bool show = label != null && gm != null && gm.NightRunning;
        if (label != null) label.enabled = show;
        if (!show) return;

        int seconds = Mathf.CeilToInt(gm.NightTimeLeft);
        label.text = $"{seconds / 60}:{seconds % 60:00}";
        label.color = gm.NightTimeLeft <= warningTime ? warningColor : normalColor;
    }
}
