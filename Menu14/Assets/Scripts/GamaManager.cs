using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

// Lives in the Persistent scene, which is never unloaded.
// Loads and unloads MainMenu / Restaurant / City on top of it.
public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    public const string PersistentScene = "Persistent";
    public const string MainMenuScene = "MainMenu";
    public const string RestaurantScene = "Restaurant";
    public const string CityScene = "City";

    [SerializeField] private NightConfig[] nights;

    public int CurrentNightIndex { get; private set; }
    public NightConfig CurrentNight => nights[CurrentNightIndex];
    public bool IsLoading { get; private set; }

    private string currentScene;

    // Editor helper: if you press Play while City or Restaurant is open,
    // this loads the Persistent scene automatically so the managers exist.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void EnsurePersistentLoaded()
    {
        if (!SceneManager.GetSceneByName(PersistentScene).isLoaded)
            SceneManager.LoadScene(PersistentScene, LoadSceneMode.Additive);
    }

    private void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;
    }

    private void Start()
    {
        // If another scene is already open (you pressed Play from it), keep it.
        for (int i = 0; i < SceneManager.sceneCount; i++)
        {
            Scene s = SceneManager.GetSceneAt(i);
            if (s.name != PersistentScene)
            {
                currentScene = s.name;
                SceneManager.SetActiveScene(s);
                return;
            }
        }

        // Normal start (Play from Persistent, or a real build).
        LoadScene(MainMenuScene);
    }

    // ---- Called from buttons / gameplay ----
    public void StartNewGame()
    {
        CurrentNightIndex = 0;
        LoadScene(RestaurantScene);
    }

    public void GoToCity() => LoadScene(CityScene);
    public void GoToRestaurant() => LoadScene(RestaurantScene);
    public void BackToMenu() => LoadScene(MainMenuScene);

    public void CompleteNight()
    {
        if (CurrentNightIndex < nights.Length - 1)
        {
            CurrentNightIndex++;
            LoadScene(RestaurantScene);
        }
        else
        {
            Debug.Log("All nights completed!");
            LoadScene(MainMenuScene);
        }
    }

    // ---- Scene switching ----
    public void LoadScene(string sceneName)
    {
        if (!IsLoading) StartCoroutine(SwitchScene(sceneName));
    }

    private IEnumerator SwitchScene(string sceneName)
    {
        IsLoading = true;

        if (!string.IsNullOrEmpty(currentScene))
            yield return SceneManager.UnloadSceneAsync(currentScene);

        yield return SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Additive);

        // The active scene decides lighting and where new objects are spawned.
        SceneManager.SetActiveScene(SceneManager.GetSceneByName(sceneName));
        currentScene = sceneName;

        IsLoading = false;
    }
}
