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

    [Header("Partida")]
    [Tooltip("Deuda total. Se gana en cuanto el excedente acumulado de las noches llega a esto")]
    [SerializeField] private int totalMoneyGoal = 1000;
    [Tooltip("Si la sospecha llega a este valor, pierdes la partida")]
    [SerializeField] private int maxSuspicion = 100;

    public int TotalMoneyGoal => totalMoneyGoal;
    public int MaxSuspicion => maxSuspicion;
    public bool IsGameOver { get; private set; }

    // ---- Sospecha (se mantiene entre noches; solo se reinicia en partida nueva) ----
    public int Suspicion { get; private set; }
    public event System.Action<int> OnSuspicionChanged;   // nuevo valor

    // ---- Dinero ----
    // Lo ganado esta noche. Al acabar la noche se pagan los intereses (moneyGoal)
    // y solo el excedente va a la deuda.
    public int NightMoney { get; private set; }
    // Deuda pagada hasta ahora (suma de los excedentes de las noches).
    public int TotalMoney { get; private set; }
    public int NightGoal => CurrentNight.moneyGoal;
    public int NightSurplus => Mathf.Max(0, NightMoney - NightGoal);
    public event System.Action OnMoneyChanged;

    // ---- Pedido que lleva el jugador ahora mismo (null = ninguno) ----
    public DeliveryRequest ActiveOrder { get; private set; }
    public bool HasActiveOrder => ActiveOrder != null;
    public event System.Action<DeliveryRequest> OnOrderLost;

    // ---- Estado de cada pedido de la noche (para la lista de encargos del móvil) ----
    public enum OrderStatus { Pending, InProgress, Delivered, Lost }
    private OrderStatus[] orderStatus;   // uno por pedido de CurrentNight.deliveries
    public event System.Action OnOrdersChanged;

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
        Suspicion = 0;
        NightMoney = 0;
        TotalMoney = 0;
        IsGameOver = false;
        ActiveOrder = null;
        nextDeliveryIndex = 0;
        ResetOrderStatus();
        OnSuspicionChanged?.Invoke(Suspicion);
        OnMoneyChanged?.Invoke();
        LoadScene(RestaurantScene);
    }

    public void GoToCity() => LoadScene(CityScene);
    public void GoToRestaurant() => LoadScene(RestaurantScene);
    public void BackToMenu() => LoadScene(MainMenuScene);

    // Fin de la noche: pagar intereses, el excedente va a la deuda, y ver si se gana, se pierde o se sigue.
    public void CompleteNight()
    {
        if (IsGameOver) return;

        int interest = NightGoal;
        if (NightMoney < interest)
        {
            GameOver($"No llegaste a los intereses de la noche ({NightMoney} / {interest} €)");
            return;
        }

        int surplus = NightMoney - interest;
        TotalMoney += surplus;
        NightMoney = 0;
        ActiveOrder = null;
        OnMoneyChanged?.Invoke();
        Debug.Log($"Noche {CurrentNight.nightNumber} completada: intereses {interest} €, +{surplus} € a la deuda. " +
                  $"Deuda pagada: {TotalMoney} / {totalMoneyGoal} €");

        if (TotalMoney >= totalMoneyGoal)
        {
            Victory();
            return;
        }

        if (CurrentNightIndex < nights.Length - 1)
        {
            CurrentNightIndex++;
            nextDeliveryIndex = 0;
            ResetOrderStatus();
            LoadScene(RestaurantScene);
        }
        else
        {
            GameOver("Se acabaron las noches sin pagar toda la deuda");
        }
    }

    // ---- Fin de partida ----
    // De momento solo escriben en consola y vuelven al menú. Más adelante, una pantalla propia.
    public void GameOver(string reason)
    {
        if (IsGameOver) return;
        IsGameOver = true;
        ActiveOrder = null;
        Debug.Log($"PARTIDA PERDIDA: {reason}");
        ReleaseCursor();
        BackToMenu();
    }

    private void Victory()
    {
        IsGameOver = true;
        Debug.Log($"¡HAS GANADO! Deuda pagada: {TotalMoney} / {totalMoneyGoal} €");
        ReleaseCursor();
        BackToMenu();
    }

    // Si acabas montado en el patinete, nadie desbloquea el ratón al ir al menú.
    private void ReleaseCursor()
    {
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    // ---- Pedidos y sospecha ----
    public void TakeOrder(DeliveryRequest order) => ActiveOrder = order;
    public void CompleteOrder()
    {
        if (!HasActiveOrder) return;
        int reward = ActiveOrder.reward;
        SetActiveOrderStatus(OrderStatus.Delivered);
        ActiveOrder = null;
        AddNightMoney(reward);
        Debug.Log($"+{reward} €. Esta noche: {NightMoney} / {NightGoal} €");
    }

    // Suma (o resta, si es negativo) al dinero de esta noche. Nunca baja de 0.
    public void AddNightMoney(int amount)
    {
        NightMoney = Mathf.Max(0, NightMoney + amount);
        OnMoneyChanged?.Invoke();
    }

    // El cliente se ha cansado de esperar: pierdes el encargo sin cobrar (lo llama DeliveryTimer).
    public void ExpireOrder()
    {
        if (!HasActiveOrder) return;
        DeliveryRequest lost = ActiveOrder;
        SetActiveOrderStatus(OrderStatus.Lost);
        ActiveOrder = null;
        OnOrderLost?.Invoke(lost);
        Debug.Log($"{lost.customerName} se ha cansado de esperar: encargo perdido");
    }

    // ---- Reparto: qué pedido de la noche toca ahora ----
    private int nextDeliveryIndex;
    public bool HasDeliveriesLeft => nextDeliveryIndex < CurrentNight.deliveries.Count;

    // Te da el siguiente pedido de la noche. Devuelve false si ya llevas uno o no quedan.
    public bool TakeNextOrder()
    {
        if (HasActiveOrder || !HasDeliveriesLeft) return false;

        ActiveOrder = CurrentNight.deliveries[nextDeliveryIndex];
        nextDeliveryIndex++;
        SetActiveOrderStatus(OrderStatus.InProgress);
        return true;
    }

    // Estado del pedido número 'index' de esta noche.
    public OrderStatus GetOrderStatus(int index)
    {
        EnsureOrderStatus();
        return orderStatus[index];
    }

    // Se crea al primer uso, así también funciona si pulsas Play directamente en una escena de prueba.
    private void EnsureOrderStatus()
    {
        int count = CurrentNight.deliveries.Count;
        if (orderStatus == null || orderStatus.Length != count)
            orderStatus = new OrderStatus[count];   // todos empiezan en Pending
    }

    private void ResetOrderStatus()
    {
        orderStatus = null;
        OnOrdersChanged?.Invoke();
    }

    private void SetActiveOrderStatus(OrderStatus status)
    {
        int index = CurrentNight.deliveries.IndexOf(ActiveOrder);
        if (index < 0) return;
        EnsureOrderStatus();
        orderStatus[index] = status;
        OnOrdersChanged?.Invoke();
    }

    public void AddSuspicion(int amount)
    {
        Suspicion = Mathf.Min(Suspicion + amount, maxSuspicion);
        OnSuspicionChanged?.Invoke(Suspicion);

        if (Suspicion >= maxSuspicion)
            GameOver("La sospecha llegó al máximo");
    }

    // Lo llama un policía cuando detecta al jugador: pierdes el pedido, pagas la fianza y sube la sospecha.
    public void PlayerCaught(int suspicionPenalty, int bailCost)
    {
        if (IsGameOver) return;

        if (HasActiveOrder)
        {
            DeliveryRequest lost = ActiveOrder;
            SetActiveOrderStatus(OrderStatus.Lost);
            ActiveOrder = null;
            OnOrderLost?.Invoke(lost);
            Debug.Log($"Pedido perdido: {lost.customerName}");
        }
        AddNightMoney(-bailCost);
        Debug.Log($"¡Pillado por la policía! Fianza: -{bailCost} €. Sospecha = {Suspicion + suspicionPenalty}");
        AddSuspicion(suspicionPenalty);
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
