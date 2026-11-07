using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Muestra el dinero: lo ganado esta noche frente a los intereses, y la deuda pagada frente al total.
/// Las barras son Images con Image Type = Filled. Cualquier campo se puede dejar vacío.
/// </summary>
public class MoneyHUD : MonoBehaviour
{
    [Header("Intereses de esta noche")]
    [SerializeField] private TMP_Text nightLabel;
    [SerializeField] private Image nightBar;

    [Header("Deuda")]
    [SerializeField] private TMP_Text debtLabel;
    [SerializeField] private Image debtBar;

    [Header("Excedente (opcional)")]
    [Tooltip("Solo se ve cuando ya has cubierto los intereses: lo que irá a la deuda al acabar la noche")]
    [SerializeField] private TMP_Text surplusLabel;

    private IEnumerator Start()
    {
        // Igual que SuspicionHUD: esperar a que exista el GameManager
        while (GameManager.Instance == null) yield return null;

        GameManager.Instance.OnMoneyChanged += Refresh;
        Refresh();
    }

    private void OnDestroy()
    {
        if (GameManager.Instance != null)
            GameManager.Instance.OnMoneyChanged -= Refresh;
    }

    private void Refresh()
    {
        GameManager gm = GameManager.Instance;

        int nightGoal = gm.NightGoal;
        if (nightLabel != null) nightLabel.text = $"Intereses: {gm.NightMoney} / {nightGoal} €";
        if (nightBar != null) nightBar.fillAmount = Ratio(gm.NightMoney, nightGoal);

        if (debtLabel != null) debtLabel.text = $"Deuda pagada: {gm.TotalMoney} / {gm.TotalMoneyGoal} €";
        if (debtBar != null) debtBar.fillAmount = Ratio(gm.TotalMoney, gm.TotalMoneyGoal);

        if (surplusLabel != null)
        {
            int surplus = gm.NightSurplus;
            surplusLabel.gameObject.SetActive(surplus > 0);
            surplusLabel.text = $"+{surplus} € a la deuda al acabar la noche";
        }
    }

    // De 0 a 1 para el fillAmount. Si el objetivo es 0, la barra está llena.
    private static float Ratio(int value, int goal) => goal > 0 ? Mathf.Clamp01((float)value / goal) : 1f;
}
