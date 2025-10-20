using System;
using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(CharacterController))]
public class PlayerControllerLocal : MonoBehaviour
{
    [Header("Movement Settings")]
    public float moveSpeed = 5f;
    public float mouseSensitivity = 2f;
    public float entitySize = 1f;

    [Header("Laser Settings")]
    public float laserDamage = 25f;
    public float laserMaxRange = 50f;
    public Material laserMaterial;

    [Header("Teleport Settings")]
    public int minTeleportTileDistance = 1;
    private int teleportTileDistance = 3;
    private GameObject teleportCursor = null;
    private bool isTeleportModeActive = false;
    [Tooltip("Base chakra cost for full maze teleport (scaled by distance)")]
    public float teleportChakraConsumption = 15f;

    [Header("Wall Build Settings")]
    public float wallChakraConsumption = 10f;

    [Header("Player Stats")]
    public float maxChakra = 100f;
    public float chakraChargeRate = 20f;
    public float maxHealth = 100f;

    // HUD bars
    private const int barWidth = 200;
    private const int barHeight = 20;
    private const int margin = 10;

    // internals
    private CharacterController cc;
    private Camera mainCam;
    private LineRenderer laserLine;

    private MazeGameManager mazeManager;
    private Vector2Int playerGridPos;
    private float cellSize;
    private float currentChakra;
    private float currentHealth;

    // minimap
    private Camera minimapCam;
    private RawImage minimapImage;
    private RenderTexture minimapRT;

    // for drawing colored bars
    private static Texture2D _whiteTexture;
    private Texture2D WhiteTexture
    {
        get
        {
            if (_whiteTexture == null)
            {
                _whiteTexture = new Texture2D(1, 1);
                _whiteTexture.SetPixel(0, 0, Color.white);
                _whiteTexture.Apply();
            }
            return _whiteTexture;
        }
    }

    void Start()
    {
        // CharacterController setup
        cc = GetComponent<CharacterController>();
        cc.height = entitySize;
        cc.radius = entitySize * 0.5f;
        cc.center = new Vector3(0, cc.height / 2f, 0);

        // initial stats
        currentHealth = maxHealth;
        currentChakra = maxChakra;

        // snap above floor
        Vector3 p = transform.position;
        p.y = cc.height / 2f + 0.5f;
        transform.position = p;

        // main camera setup
        mainCam = Camera.main;
        if (mainCam != null)
        {
            mainCam.transform.SetParent(transform);
            mainCam.transform.localPosition = new Vector3(0, entitySize * 1.5f, -entitySize * 2f);
            mainCam.transform.localEulerAngles = new Vector3(15, 0, 0);
        }
        Cursor.lockState = CursorLockMode.Locked;

        // maze reference
        mazeManager = FindObjectOfType<MazeGameManager>();
        cellSize = mazeManager.cellSize;

        // minimap setup
        var camGO = new GameObject("MinimapCamera");
        camGO.transform.parent = transform;
        minimapCam = camGO.AddComponent<Camera>();
        minimapCam.orthographic = true;
        minimapCam.orthographicSize = Mathf.Max(mazeManager.rows, mazeManager.cols) * cellSize * 0.6f;
        minimapCam.clearFlags = CameraClearFlags.SolidColor;
        minimapCam.backgroundColor = Color.black;
        minimapCam.cullingMask = LayerMask.GetMask("Default");

        // render texture
        minimapRT = new RenderTexture(256, 256, 16);
        minimapRT.wrapMode = TextureWrapMode.Clamp;
        minimapCam.targetTexture = minimapRT;

        // attach to UI RawImage
        var imgGO = GameObject.Find("MinimapImage");
        if (imgGO != null)
        {
            minimapImage = imgGO.GetComponent<RawImage>();
            minimapImage.texture = minimapRT;
        }
        else
        {
            Debug.LogError("Could not find a GameObject named 'MinimapImage' in the scene.");
        }

        UpdatePlayerGridPos();
    }

    void Update()
    {
        // look & move
        float mx = Input.GetAxis("Mouse X") * mouseSensitivity;
        transform.Rotate(0, mx, 0);

        float f = Input.GetAxis("Vertical"), s = Input.GetAxis("Horizontal");
        Vector3 dir = (transform.forward * f + transform.right * s).normalized;
        cc.Move(dir * moveSpeed * Time.deltaTime);

        UpdatePlayerGridPos();
        HandleLaser();
        HandleTeleportAndBuild();
        // HandleChakraRegen(); // optional

        // minimap follow
        if (minimapCam != null)
        {
            minimapCam.transform.position = transform.position + Vector3.up * 20f;
            minimapCam.transform.rotation = Quaternion.Euler(90f, transform.eulerAngles.y, 0f);
        }
    }

    private void HandleLaser()
    {
        if (Input.GetMouseButton(0))
        {
            if (laserLine == null) CreateLaser();

            Vector3 start = cc.bounds.center;
            Vector3 forward = transform.forward;

            if (Physics.Raycast(start, forward, out RaycastHit hit, laserMaxRange))
            {
                laserLine.SetPosition(0, start);
                laserLine.SetPosition(1, hit.point);

                var dw = hit.collider.GetComponent<DamageableWall>();
                if (dw != null) dw.TakeDamage(laserDamage * Time.deltaTime);

                var bot = hit.collider.GetComponent<BotController>();
                if (bot != null)
                {
                    bot.TakeDamage(laserDamage * Time.deltaTime);
                    bot.RegisterPlayerHit();
                }
            }
            else
            {
                laserLine.SetPosition(0, start);
                laserLine.SetPosition(1, start + forward * laserMaxRange);
            }
        }
        else if (laserLine != null)
        {
            Destroy(laserLine.gameObject);
            laserLine = null;
        }
    }

    private void CreateLaser()
    {
        laserLine = new GameObject("Laser").AddComponent<LineRenderer>();
        laserLine.material = laserMaterial;
        laserLine.startWidth = laserLine.endWidth = 0.2f;
        laserLine.positionCount = 2;
        laserLine.useWorldSpace = true;
        laserLine.startColor = Color.red;
        laserLine.endColor = Color.red;
    }

    public void TakeDamage(float amount)
    {
        currentHealth = Mathf.Max(0f, currentHealth - amount);
    }

    private void HandleTeleportAndBuild()
    {
        if (Input.GetKeyDown(KeyCode.Q))
        {
            if (!isTeleportModeActive)
            {
                isTeleportModeActive = true;
                teleportTileDistance = Mathf.Max(minTeleportTileDistance, teleportTileDistance);
                CreateOrUpdateTeleportCursor();
                Debug.Log("Teleport mode activated. Scroll or press Q to confirm.");
            }
            else
            {
                if (teleportCursor != null && mazeManager != null)
                {
                    Vector3 targetPos = teleportCursor.transform.position;
                    float actualDist = Vector3.Distance(transform.position, targetPos);
                    float maxTilesDist = Mathf.Sqrt((mazeManager.rows - 1) * (mazeManager.rows - 1) +
                                                    (mazeManager.cols - 1) * (mazeManager.cols - 1));
                    float maxWorldDist = maxTilesDist * cellSize;

                    float cost = teleportChakraConsumption * (actualDist / maxWorldDist);
                    cost = Mathf.Min(cost, currentChakra);

                    if (currentChakra >= cost)
                    {
                        currentChakra -= cost;
                        TeleportPlayer();
                    }
                    else Debug.Log("Not enough chakra to teleport that far.");
                }
                CancelTeleportMode();
            }
        }

        if (isTeleportModeActive)
        {
            CreateOrUpdateTeleportCursor();
            float scroll = Input.GetAxis("Mouse ScrollWheel");
            if (Mathf.Abs(scroll) > 0.01f)
            {
                teleportTileDistance += scroll > 0 ? 1 : -1;
                teleportTileDistance = Mathf.Max(minTeleportTileDistance, teleportTileDistance);
                CreateOrUpdateTeleportCursor();
            }

            if (Input.GetMouseButton(0) || Input.GetMouseButtonDown(1))
                CancelTeleportMode();

            float mx2 = Input.GetAxis("Mouse X") * mouseSensitivity;
            transform.Rotate(0, mx2, 0);
            float f2 = Input.GetAxis("Vertical"), s2 = Input.GetAxis("Horizontal");
            cc.Move((transform.forward * f2 + transform.right * s2).normalized * moveSpeed * Time.deltaTime);
            UpdatePlayerGridPos();
            return;
        }

        if (Input.GetMouseButtonDown(1) && currentChakra >= wallChakraConsumption)
        {
            mazeManager.BuildOrRemoveWall(playerGridPos, transform.eulerAngles.y);
            currentChakra -= wallChakraConsumption;
        }
    }

    private void CreateOrUpdateTeleportCursor()
    {
        if (mazeManager == null) return;

        Vector3 cand = transform.position + transform.forward.normalized * teleportTileDistance * cellSize;
        int row = Mathf.Clamp(Mathf.FloorToInt(cand.z / cellSize), 0, mazeManager.rows - 1);
        int col = Mathf.Clamp(Mathf.FloorToInt(cand.x / cellSize), 0, mazeManager.cols - 1);
        Vector3 world = mazeManager.GridToWorld(new Vector2Int(row, col));

        if (teleportCursor == null)
        {
            teleportCursor = GameObject.CreatePrimitive(PrimitiveType.Cube);
            teleportCursor.GetComponent<Collider>().enabled = false;
            teleportCursor.transform.localScale = Vector3.one * entitySize;
        }
        teleportCursor.transform.position = world;
    }

    private void TeleportPlayer()
    {
        if (teleportCursor == null) return;
        cc.enabled = false;
        transform.position = teleportCursor.transform.position;
        cc.enabled = true;
        UpdatePlayerGridPos();
        Destroy(teleportCursor);
        teleportCursor = null;
        isTeleportModeActive = false;
    }

    private void CancelTeleportMode()
    {
        if (teleportCursor != null) Destroy(teleportCursor);
        teleportCursor = null;
        isTeleportModeActive = false;
    }

    private void HandleChakraRegen()
    {
        currentChakra = Mathf.Min(maxChakra, currentChakra + chakraChargeRate * Time.deltaTime);
    }

    private void UpdatePlayerGridPos()
    {
        int r = Mathf.FloorToInt(transform.position.z / cellSize);
        int c = Mathf.FloorToInt(transform.position.x / cellSize);
        playerGridPos = new Vector2Int(r, c);
    }

    void OnGUI()
    {
        float healthRatio = currentHealth / maxHealth;
        float chakraRatio = currentChakra / maxChakra;

        GUI.color = Color.green;
        GUI.DrawTexture(new Rect(margin, margin, barWidth * healthRatio, barHeight), WhiteTexture);

        GUI.color = Color.blue;
        GUI.DrawTexture(new Rect(margin, margin + barHeight + 5, barWidth * chakraRatio, barHeight), WhiteTexture);

        GUI.color = Color.white;
    }
}
