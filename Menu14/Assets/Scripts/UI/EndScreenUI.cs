using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Pantalla de fin de partida (victoria o derrota). Va en el MainMenu, en un panel que tapa el menú.
/// Al volver al menú tras acabar una partida se enseña con el resultado; si no, se queda oculta.
/// </summary>
public class EndScreenUI : MonoBehaviour
{
    [Tooltip("Panel que se enseña u oculta. Si se deja vacío, este mismo objeto")]
    [SerializeField] private GameObject panel;
    [SerializeField] private TMP_Text title;
    [SerializeField] private TMP_Text reason;
    [Tooltip("Opcional: deuda pagada, noche a la que llegaste y sospecha")]
    [SerializeField] private TMP_Text summary;
    [SerializeField] private Button playAgainButton;
    [SerializeField] private Button menuButton;
    [Tooltip("Opcional: lo que se oculta mientras se ve esta pantalla (la pantalla de inicio del menú)")]
    [SerializeField] private GameObject hideWhileShown;

    [Header("Textos y colores")]
    [SerializeField] private string victoryTitle = "¡Deuda pagada!";
    [SerializeField] private string defeatTitle = "Fin de la partida";
    [SerializeField] private Color victoryColor = new Color(0.45f, 0.85f, 0.45f);  // verde
    [SerializeField] private Color defeatColor = new Color(1f, 0.35f, 0.3f);       // rojo

    private void Awake()
    {
        if (panel == null) panel = gameObject;
        if (playAgainButton != null) playAgainButton.onClick.AddListener(PlayAgain);
        if (menuButton != null) menuButton.onClick.AddListener(Hide);
    }

    private void Start()
    {
        GameManager gm = GameManager.Instance;
        if (gm == null || gm.LastResult == GameManager.GameResult.None)
        {
            panel.SetActive(false);
            return;
        }

        bool won = gm.LastResult == GameManager.GameResult.Victory;
        if (title != null)
        {
            title.text = won ? victoryTitle : defeatTitle;
            title.color = won ? victoryColor : defeatColor;
        }
        if (reason != null) reason.text = gm.LastReason;
        if (summary != null)
            summary.text = $"Deuda pagada: {gm.TotalMoney} / {gm.TotalMoneyGoal} €\n" +
                           $"Noche {gm.CurrentNight.nightNumber} · Sospecha {gm.Suspicion} / {gm.MaxSuspicion}";

        panel.SetActive(true);
        if (hideWhileShown != null) hideWhileShown.SetActive(false);
    }

    private void PlayAgain()
    {
        if (GameManager.Instance != null) GameManager.Instance.StartNewGame();
    }

    // Vuelve al menú normal. El resultado se borra para que no salga otra vez.
    private void Hide()
    {
        if (GameManager.Instance != null) GameManager.Instance.ClearLastResult();
        panel.SetActive(false);
        if (hideWhileShown != null) hideWhileShown.SetActive(true);
    }
}
