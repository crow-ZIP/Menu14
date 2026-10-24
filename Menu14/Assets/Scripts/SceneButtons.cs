using UnityEngine;

// Put this on a Canvas in any scene, then hook its methods to Button OnClick.
// (Buttons can't reference the GameManager directly because it lives in another scene.)
public class SceneButtons : MonoBehaviour
{
    public void StartNewGame() => GameManager.Instance.StartNewGame();
    public void GoToCity() => GameManager.Instance.GoToCity();
    public void GoToRestaurant() => GameManager.Instance.GoToRestaurant();
    public void CompleteNight() => GameManager.Instance.CompleteNight();
    public void BackToMenu() => GameManager.Instance.BackToMenu();
}
