using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// SOLO PARA ESCENAS DE PRUEBA. Atajos de teclado para probar el dinero y la sospecha sin jugar entera la noche.
///   N = terminar la noche      M = +100 € esta noche      K = +1 de sospecha
/// No lo pongas en City ni en Restaurant.
/// </summary>
public class DebugKeys : MonoBehaviour
{
    void Update()
    {
        Keyboard kb = Keyboard.current;
        GameManager gm = GameManager.Instance;
        if (kb == null || gm == null || gm.IsGameOver) return;

        if (kb.nKey.wasPressedThisFrame) gm.CompleteNight();
        if (kb.mKey.wasPressedThisFrame) gm.AddNightMoney(100);
        if (kb.kKey.wasPressedThisFrame) gm.AddSuspicion(1);
    }
}
