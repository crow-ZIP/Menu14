using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

// Lives in the Persistent scene, which is never unloaded.
// Loads and unloads MainMenu / Restaurant / City on top of it.
public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    public const string PersistentScene = "Persistent";
    public const string SplashScene = "Splash";
    public const string MainMenuScene = "MainMenu";
    public const string RestaurantScene = "Restaurant";
    public const string CityScene = "City";
    public const string NightSummaryScene = "NightSummary";

    [SerializeField] private NightConfig[] nights;
    [Tooltip("Cifras de los encargos: ofertas a la vez, pago y tiempo según la distancia, nombres de clientes")]
    [SerializeField] private DeliveryConfig deliveryConfig;

    public int CurrentNightIndex { get; private set; }
    public NightConfig CurrentNight => nights[CurrentNightIndex];
    public NightConfig GetNight(int index) => nights[Mathf.Clamp(index, 0, nights.Length - 1)];
    public bool IsLoading { get; private set; }

    [Header("Partida")]
    [Tooltip("Deuda total. Se gana en cuanto el excedente acumulado de las noches llega a esto")]
    [SerializeField] private int totalMoneyGoal = 1000;
    [Tooltip("Si la sospecha llega a este valor, pierdes la partida")]
    [SerializeField] private int maxSuspicion = 100;

    public int TotalMoneyGoal => totalMoneyGoal;
    public int MaxSuspicion => maxSuspicion;
    public bool IsGameOver { get; private set; }

    // ---- Resultado de la última partida (lo enseña EndScreenUI en el menú) ----
    public enum GameResult { None, Victory, Defeat }
    public GameResult LastResult { get; private set; }
    public string LastReason { get; private set; }
    public void ClearLastResult() { LastResult = GameResult.None; LastReason = null; }

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

    // ---- Resumen de la última noche completada (lo enseña NightSummaryUI) ----
    public int LastNightNumber { get; private set; }
    public int LastNightEarned { get; private set; }
    public int LastNightInterest { get; private set; }
    public int LastNightSurplus { get; private set; }
    public int LastNightDelivered { get; private set; }
    public int LastNightLost { get; private set; }

    // ---- Pedido que lleva el jugador ahora mismo (null = ninguno) ----
    public DeliveryRequest ActiveOrder { get; private set; }
    public bool HasActiveOrder => ActiveOrder != null;
    public event System.Action<DeliveryRequest> OnOrderLost;
    public float OrderIntegrity { get; private set; } = 1f;

    // ---- Encargo aceptado en la pantalla del restaurante que aún no se ha recogido (null = ninguno) ----
    public DeliveryRequest AcceptedOrder { get; private set; }
    public bool HasAcceptedOrder => AcceptedOrder != null;

    // ---- Encargos de la noche: ofertas de la pantalla del PC y los que ya se han aceptado ----
    public enum OrderStatus { Pending, Accepted, InProgress, Delivered, Lost }
    private readonly List<DeliveryRequest> offers = new List<DeliveryRequest>();
    private readonly List<DeliveryRequest> takenOrders = new List<DeliveryRequest>();
    private readonly Dictionary<DeliveryRequest, OrderStatus> orderStatus = new Dictionary<DeliveryRequest, OrderStatus>();
    private readonly OrderGenerator generator = new OrderGenerator();
    public IReadOnlyList<DeliveryRequest> Offers => offers;
    public IReadOnlyList<DeliveryRequest> TakenOrders => takenOrders;
    public int DeliveredCount { get; private set; }
    public int LostCount { get; private set; }
    public int MaxDeliveries => CurrentNight.maxDeliveries;
    public int DeliveriesLeft => Mathf.Max(0, MaxDeliveries - DeliveredCount);
    public event System.Action OnOrdersChanged;

    // ---- Reloj de la noche ----
    public float NightTimeLeft { get; private set; }
    public bool NightRunning { get; private set; }
    public bool NightTimeUp => NightRunning && NightTimeLeft <= 0f;
    public bool InGracePeriod => NightTimeUp && HasActiveOrder;
    private bool nightClosed;
    private Coroutine endNightRoutine;

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

        if (deliveryConfig == null)
        {
            Debug.LogWarning("GameManager sin DeliveryConfig: se usan los valores por defecto");
            deliveryConfig = ScriptableObject.CreateInstance<DeliveryConfig>();
        }
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
                if (s.name == CityScene) BeginNight();
                return;
            }
        }

        // Normal start (Play from Persistent, or a real build): primero la pantalla de presentación
        LoadScene(SplashScene);
    }

    // ---- Called from buttons / gameplay ----
    public void StartNewGame()
    {
        CurrentNightIndex = 0;
        Suspicion = 0;
        NightMoney = 0;
        TotalMoney = 0;
        IsGameOver = false;
        ClearLastResult();
        ActiveOrder = null;
        AcceptedOrder = null;
        ClearNight();
        OnSuspicionChanged?.Invoke(Suspicion);
        OnMoneyChanged?.Invoke();
        LoadScene(CityScene);   // el restaurante está dentro de City
    }

    public void GoToCity() => LoadScene(CityScene);
    public void GoToRestaurant() => LoadScene(RestaurantScene);
    public void BackToMenu()
    {
        ReleaseCursor();   // si ibas en patinete, nadie más suelta el ratón
        StopNightClock();
        LoadScene(MainMenuScene);
    }

    // Se puede terminar la noche desde el PC, sin llevar un encargo encima, cuando ya están pagados
    // los intereses o cuando ya has entregado el máximo de la noche.
    public bool NightInterestPaid => NightMoney >= NightGoal;
    public bool NoOrdersLeft => DeliveriesLeft == 0;
    public bool CanEndNight => NightRunning && !nightClosed && endNightRoutine == null && !IsGameOver && !IsLoading
                               && !HasActiveOrder && (NightInterestPaid || NoOrdersLeft);

    private void Update()
    {
        if (!NightRunning || nightClosed || IsGameOver || IsLoading || NightTimeLeft <= 0f) return;

        NightTimeLeft = Mathf.Max(0f, NightTimeLeft - Time.deltaTime);
        if (NightTimeLeft <= 0f) OnNightTimeUp();
    }

    private void BeginNight()
    {
        ClearNight();
        ActiveOrder = null;
        AcceptedOrder = null;
        generator.Reset();
        if (generator.PointCount == 0) Debug.LogWarning("No hay ningún DeliveryPoint en la escena: no saldrán encargos");

        NightTimeLeft = CurrentNight.duration;
        NightRunning = true;
        RefillOffers();
        OnOrdersChanged?.Invoke();
    }

    private void ClearNight()
    {
        StopNightClock();
        offers.Clear();
        takenOrders.Clear();
        orderStatus.Clear();
        DeliveredCount = 0;
        LostCount = 0;
        NightTimeLeft = 0f;
        nightClosed = false;
        OnOrdersChanged?.Invoke();
    }

    private void StopNightClock()
    {
        NightRunning = false;
        if (endNightRoutine != null) StopCoroutine(endNightRoutine);
        endNightRoutine = null;
    }

    private void OnNightTimeUp()
    {
        offers.Clear();
        if (HasAcceptedOrder)
        {
            takenOrders.Remove(AcceptedOrder);
            orderStatus.Remove(AcceptedOrder);
            AcceptedOrder = null;
        }
        OnOrdersChanged?.Invoke();

        if (!HasActiveOrder) EndNightSoon();
    }

    private void EndNightSoon()
    {
        if (nightClosed || endNightRoutine != null) return;
        endNightRoutine = StartCoroutine(EndNightAfterDelay());
    }

    private IEnumerator EndNightAfterDelay()
    {
        yield return new WaitForSeconds(deliveryConfig.nightEndDelay);
        endNightRoutine = null;
        CompleteNight();
    }

    // Fin de la noche: pagar intereses, el excedente va a la deuda, y ver si se gana, se pierde o se sigue.
    public void CompleteNight()
    {
        if (IsGameOver || nightClosed) return;
        nightClosed = true;
        StopNightClock();

        int interest = NightGoal;
        if (NightMoney < interest)
        {
            GameOver($"No llegaste a los intereses de la noche ({NightMoney} / {interest} €)");
            return;
        }

        int surplus = NightMoney - interest;

        // Guardar el resumen antes de pasar a la noche siguiente
        LastNightNumber = CurrentNight.nightNumber;
        LastNightEarned = NightMoney;
        LastNightInterest = interest;
        LastNightSurplus = surplus;
        LastNightDelivered = DeliveredCount;
        LastNightLost = LostCount;

        TotalMoney += surplus;
        NightMoney = 0;
        ActiveOrder = null;
        AcceptedOrder = null;
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
            ReleaseCursor();
            AudioManager.Play(Sfx.NightComplete);
            LoadScene(NightSummaryScene);   // desde el resumen, "Siguiente noche" carga City de nuevo
        }
        else
        {
            GameOver("Se acabaron las noches sin pagar toda la deuda");
        }
    }

    // ---- Fin de partida ----
    // Guardan el resultado y vuelven al menú, donde EndScreenUI lo enseña.
    public void GameOver(string reason)
    {
        if (IsGameOver) return;
        IsGameOver = true;
        LastResult = GameResult.Defeat;
        AudioManager.Play(Sfx.Defeat);
        LastReason = reason;
        ActiveOrder = null;
        AcceptedOrder = null;
        Debug.Log($"PARTIDA PERDIDA: {reason}");
        ReleaseCursor();
        BackToMenu();
    }

    private void Victory()
    {
        IsGameOver = true;
        LastResult = GameResult.Victory;
        AudioManager.Play(Sfx.Victory);
        LastReason = $"Has pagado toda la deuda ({TotalMoney} / {totalMoneyGoal} €)";
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
    public void TakeOrder(DeliveryRequest order)
    {
        ActiveOrder = order;
        OrderIntegrity = 1f;
    }

    public void CompleteOrder()
    {
        if (!HasActiveOrder) return;
        int reward = Mathf.RoundToInt(ActiveOrder.reward * OrderIntegrity);
        SetActiveOrderStatus(OrderStatus.Delivered);
        ActiveOrder = null;
        AudioManager.Play(Sfx.Delivered);
        Notifications.Show(OrderIntegrity < 0.999f
            ? $"+{reward} €  ·  encargo al {Mathf.RoundToInt(OrderIntegrity * 100f)} %"
            : $"+{reward} €", NoticeKind.Good);
        AddNightMoney(reward);
        Debug.Log($"+{reward} €. Esta noche: {NightMoney} / {NightGoal} €");
        AfterOrderFinished();
    }

    // Suma (o resta, si es negativo) al dinero de esta noche. Nunca baja de 0.
    public void AddNightMoney(int amount)
    {
        bool wasPaid = NightInterestPaid;
        NightMoney = Mathf.Max(0, NightMoney + amount);
        OnMoneyChanged?.Invoke();

        if (!wasPaid && NightInterestPaid && NightGoal > 0)
        {
            AudioManager.Play(Sfx.InterestPaid);
            Notifications.Show("¡Intereses cubiertos! Ya puedes terminar la noche en el PC", NoticeKind.Good);
        }
    }

    // El cliente se ha cansado de esperar: pierdes el encargo sin cobrar (lo llama DeliveryTimer).
    public void ExpireOrder()
    {
        if (!HasActiveOrder) return;
        DeliveryRequest lost = ActiveOrder;
        SetActiveOrderStatus(OrderStatus.Lost);
        ActiveOrder = null;
        AudioManager.Play(Sfx.OrderLost);
        OnOrderLost?.Invoke(lost);
        Debug.Log($"{lost.customerName} se ha cansado de esperar: encargo perdido");
        AfterOrderFinished();
    }

    // Lo llama ScooterCrash en cada caída. Con la integridad a 0 el encargo se pierde.
    public void DamageActiveOrder(float amount)
    {
        if (!HasActiveOrder || IsGameOver) return;

        OrderIntegrity = InGracePeriod ? 0f : Mathf.Max(0f, OrderIntegrity - amount);
        if (OrderIntegrity > 0.001f) return;

        DeliveryRequest lost = ActiveOrder;
        SetActiveOrderStatus(OrderStatus.Lost);
        ActiveOrder = null;
        AudioManager.Play(Sfx.OrderLost);
        OnOrderLost?.Invoke(lost);
        Debug.Log($"El encargo de {lost.customerName} ha quedado destrozado");
        AfterOrderFinished();
    }

    private void AfterOrderFinished()
    {
        if (NightTimeUp) EndNightSoon();
        else RefillOffers();
        OnOrdersChanged?.Invoke();
    }

    // ---- Reparto: el jugador acepta un encargo en la pantalla y lo recoge en el mostrador ----
    public bool HasDeliveriesLeft => HasAcceptedOrder || offers.Count > 0;

    // Solo se puede aceptar uno a la vez, y no mientras llevas otro encima.
    public bool CanAcceptOrder => NightRunning && !NightTimeUp && !nightClosed && !IsGameOver
                                  && !HasActiveOrder && !HasAcceptedOrder;

    // Lo llama la interfaz de la pantalla de pedidos con el índice de la oferta en Offers.
    public bool AcceptOrder(int index)
    {
        if (!CanAcceptOrder || index < 0 || index >= offers.Count) return false;

        DeliveryRequest order = offers[index];
        offers.RemoveAt(index);
        AcceptedOrder = order;
        takenOrders.Add(order);
        orderStatus[order] = OrderStatus.Accepted;
        generator.MarkUsed(order);

        RefillOffers();
        OnOrdersChanged?.Invoke();
        return true;
    }

    // Lo llama OrderPickup: el encargo aceptado pasa a ser el que llevas y empieza a contar su tiempo.
    public bool PickUpAcceptedOrder()
    {
        if (!HasAcceptedOrder || HasActiveOrder || IsGameOver) return false;

        ActiveOrder = AcceptedOrder;
        AcceptedOrder = null;
        OrderIntegrity = 1f;
        SetActiveOrderStatus(OrderStatus.InProgress);
        return true;
    }

    private void RefillOffers()
    {
        if (!NightRunning || NightTimeUp) return;

        int inHand = HasActiveOrder || HasAcceptedOrder ? 1 : 0;
        int target = Mathf.Min(deliveryConfig.offersOnScreen, DeliveriesLeft - inHand);

        var busy = new List<DeliveryRequest>(offers);
        if (HasActiveOrder) busy.Add(ActiveOrder);
        if (HasAcceptedOrder) busy.Add(AcceptedOrder);

        while (offers.Count < target)
        {
            DeliveryRequest order = generator.Create(deliveryConfig, CurrentNight, busy);
            if (order == null) break;
            offers.Add(order);
            busy.Add(order);
        }
    }

    public OrderStatus GetOrderStatus(DeliveryRequest order) =>
        order != null && orderStatus.TryGetValue(order, out OrderStatus status) ? status : OrderStatus.Pending;

    private void SetActiveOrderStatus(OrderStatus status)
    {
        if (ActiveOrder == null) return;
        orderStatus[ActiveOrder] = status;
        if (status == OrderStatus.Delivered) DeliveredCount++;
        if (status == OrderStatus.Lost) LostCount++;
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

        bool lostOrder = HasActiveOrder;
        if (lostOrder)
        {
            DeliveryRequest lost = ActiveOrder;
            SetActiveOrderStatus(OrderStatus.Lost);
            ActiveOrder = null;
            OnOrderLost?.Invoke(lost);
            Debug.Log($"Pedido perdido: {lost.customerName}");
        }
        AudioManager.Play(Sfx.Caught);
        if (bailCost > 0) Notifications.Show($"-{bailCost} € de fianza", NoticeKind.Bad);
        if (suspicionPenalty > 0) Notifications.Show($"+{suspicionPenalty} de sospecha", NoticeKind.Bad);
        AddNightMoney(-bailCost);
        Debug.Log($"¡Pillado por la policía! Fianza: -{bailCost} €. Sospecha = {Suspicion + suspicionPenalty}");
        AddSuspicion(suspicionPenalty);
        if (lostOrder && !IsGameOver) AfterOrderFinished();
    }

    // ---- Scene switching ----
    public void LoadScene(string sceneName)
    {
        if (IsLoading) return;

        // Si la escena no está en Build Profiles > Scene List, Unity no puede cargarla
        if (!Application.CanStreamedLevelBeLoaded(sceneName))
        {
            Debug.LogError($"No se puede cargar la escena '{sceneName}': añádela en File > Build Profiles > Scene List.");
            return;
        }

        StartCoroutine(SwitchScene(sceneName));
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
        if (sceneName == CityScene) BeginNight();
    }
}
