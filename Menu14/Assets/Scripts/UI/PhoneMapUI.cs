using TMPro;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UI;

/// <summary>
/// Pestaña "Mapa" del móvil, estilo Google Maps. Una cámara mira la ciudad desde arriba siguiendo
/// al jugador, y encima se colocan dos iconos: el jugador (flecha) y el sitio al que tienes que ir:
///  - Si llevas un pedido: su DeliveryPoint.
///  - Si no llevas nada y quedan encargos: el restaurante (OrderPickup).
/// Si el destino queda fuera del mapa, su icono se queda pegado al borde señalando hacia dónde está.
/// La cámara la crea el propio script, así el prefab HUD no necesita nada de la escena.
/// Ponlo en el objeto PantallaMapa.
/// </summary>
public class PhoneMapUI : MonoBehaviour
{
    [Header("Interfaz")]
    [Tooltip("La RawImage donde se ve el mapa. Los dos iconos tienen que ser hijos suyos")]
    [SerializeField] private RawImage mapImage;
    [SerializeField] private RectTransform playerIcon;
    [SerializeField] private RectTransform targetIcon;
    [Tooltip("Opcional: texto con el destino")]
    [SerializeField] private TMP_Text infoLabel;
    [Tooltip("Grados extra para que el icono del jugador apunte hacia arriba. 180 si usas el sprite DropdownArrow")]
    [SerializeField] private float playerIconAngleOffset = 0f;
    [Tooltip("Margen (en píxeles del Canvas) para que el icono del destino no se salga por el borde")]
    [SerializeField] private float edgeMargin = 25f;

    [Header("Cámara del mapa")]
    [Tooltip("Metros que se ven del centro al borde de arriba. Más = más alejado")]
    [SerializeField] private float zoom = 60f;
    [SerializeField] private float cameraHeight = 100f;
    [Tooltip("Capas que salen en el mapa. Quita la de los policías si no quieres que se vean")]
    [SerializeField] private LayerMask mapLayers = ~0;
    [SerializeField] private Color backgroundColor = new Color(0.08f, 0.09f, 0.12f);
    [Tooltip("Luz ambiente SOLO para el mapa, para que de noche no salga todo negro. La niebla también se quita")]
    [SerializeField] private Color mapAmbient = new Color(0.6f, 0.6f, 0.65f);
    [Tooltip("Marcado: el mapa gira con el jugador (tú siempre miras hacia arriba). Sin marcar: el norte siempre arriba")]
    [SerializeField] private bool rotateWithPlayer = false;
    [SerializeField] private int textureSize = 512;

    private Camera mapCamera;
    private RenderTexture texture;
    private PhoneUI phone;

    private Transform target;            // a dónde hay que ir ahora
    private string targetName = "";
    private DeliveryRequest checkedOrder; // pedido con el que se buscó el destino por última vez
    private float nextSearch;

    // Lo que se cambia mientras se pinta el mapa, para dejarlo como estaba después
    private bool savedFog;
    private SphericalHarmonicsL2 savedAmbient;

    private void Awake()
    {
        phone = GetComponentInParent<PhoneUI>();

        texture = new RenderTexture(textureSize, textureSize, 16);
        mapImage.texture = texture;

        // La cámara es hija de esta página: si cambias a otra pestaña, se apaga sola
        var cameraObject = new GameObject("MapCamera");
        cameraObject.transform.SetParent(transform, false);
        mapCamera = cameraObject.AddComponent<Camera>();
        mapCamera.orthographic = true;
        mapCamera.clearFlags = CameraClearFlags.SolidColor;
        mapCamera.backgroundColor = backgroundColor;
        mapCamera.cullingMask = mapLayers;
        mapCamera.nearClipPlane = 1f;
        mapCamera.farClipPlane = cameraHeight + 50f;
        mapCamera.targetTexture = texture;
    }

    private void OnEnable()
    {
        RenderPipelineManager.beginCameraRendering += BeforeMapRender;
        RenderPipelineManager.endCameraRendering += AfterMapRender;
    }

    private void OnDisable()
    {
        RenderPipelineManager.beginCameraRendering -= BeforeMapRender;
        RenderPipelineManager.endCameraRendering -= AfterMapRender;
    }

    private void OnDestroy()
    {
        if (texture != null) texture.Release();
    }

    private void LateUpdate()
    {
        Collider playerCollider = PlayerLocator.Current;   // el patinete si vas montado, el Player si vas a pie

        // Sin jugador (por ejemplo en el restaurante) o con el móvil guardado, la cámara no pinta nada
        bool phoneOpen = phone == null || phone.IsOpen;
        mapCamera.enabled = phoneOpen && playerCollider != null;
        if (playerCollider == null)
        {
            playerIcon.gameObject.SetActive(false);
            targetIcon.gameObject.SetActive(false);
            if (infoLabel != null) infoLabel.text = "Sin señal de GPS";
            return;
        }
        if (!phoneOpen) return;

        Transform player = playerCollider.transform;

        // 1. La cámara, encima del jugador y mirando hacia abajo
        float mapYaw = rotateWithPlayer ? player.eulerAngles.y : 0f;
        mapCamera.transform.SetPositionAndRotation(player.position + Vector3.up * cameraHeight,
                                                   Quaternion.Euler(90f, mapYaw, 0f));
        mapCamera.orthographicSize = zoom;
        Rect area = mapImage.rectTransform.rect;
        if (area.height > 0f) mapCamera.aspect = area.width / area.height;   // que el mapa no salga estirado

        // 2. El jugador: en su sitio y girado hacia donde mira
        playerIcon.gameObject.SetActive(true);
        PlaceIcon(playerIcon, player.position);
        playerIcon.localEulerAngles = new Vector3(0f, 0f, mapYaw - player.eulerAngles.y + playerIconAngleOffset);

        // 3. El destino
        UpdateTarget();
        targetIcon.gameObject.SetActive(target != null);
        if (target != null) PlaceIcon(targetIcon, target.position);

        // 4. El texto: a dónde hay que ir
        if (infoLabel != null) infoLabel.text = targetName;
    }

    // Pasa una posición del mundo a un punto dentro de la imagen del mapa.
    // Si cae fuera, lo acerca al centro hasta tocar el borde, en la misma dirección.
    private void PlaceIcon(RectTransform icon, Vector3 worldPosition)
    {
        Vector3 viewport = mapCamera.WorldToViewportPoint(worldPosition);   // (0,0) abajo izq. (1,1) arriba der.
        Rect area = mapImage.rectTransform.rect;
        Vector2 point = new Vector2(Mathf.Lerp(area.xMin, area.xMax, viewport.x),
                                    Mathf.Lerp(area.yMin, area.yMax, viewport.y));

        Vector2 fromCenter = point - area.center;
        Vector2 half = area.size * 0.5f - Vector2.one * edgeMargin;
        float outside = Mathf.Max(Mathf.Abs(fromCenter.x) / half.x, Mathf.Abs(fromCenter.y) / half.y);
        if (outside > 1f) point = area.center + fromCenter / outside;

        icon.localPosition = point;
    }

    // Busca el destino solo cuando cambia el pedido, o cada segundo si todavía no lo ha encontrado
    private void UpdateTarget()
    {
        var gm = GameManager.Instance;
        DeliveryRequest order = gm != null ? gm.ActiveOrder : null;
        if (order == checkedOrder && (target != null || Time.time < nextSearch)) return;

        checkedOrder = order;
        nextSearch = Time.time + 1f;
        target = null;

        if (gm == null)
        {
            targetName = "";
        }
        else if (order != null)
        {
            targetName = $"Entrega a {order.customerName}";
            foreach (var point in FindObjectsByType<DeliveryPoint>(FindObjectsSortMode.None))
            {
                if (point.destinationId == order.destinationId) { target = point.transform; break; }
            }
        }
        else if (gm.HasDeliveriesLeft)
        {
            targetName = "Recoge el pedido en el restaurante";
            var pickup = FindFirstObjectByType<OrderPickup>();
            if (pickup != null) target = pickup.transform;
        }
        else
        {
            targetName = "No quedan encargos esta noche";
        }
    }

    // Justo antes de pintar el mapa: sin niebla y con más luz ambiente. Justo después, todo como estaba.
    private void BeforeMapRender(ScriptableRenderContext context, Camera cam)
    {
        if (cam != mapCamera) return;
        savedFog = RenderSettings.fog;
        savedAmbient = RenderSettings.ambientProbe;

        var ambient = new SphericalHarmonicsL2();
        ambient.AddAmbientLight(mapAmbient);
        RenderSettings.fog = false;
        RenderSettings.ambientProbe = ambient;
    }

    private void AfterMapRender(ScriptableRenderContext context, Camera cam)
    {
        if (cam != mapCamera) return;
        RenderSettings.fog = savedFog;
        RenderSettings.ambientProbe = savedAmbient;
    }
}
