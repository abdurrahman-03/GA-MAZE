using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(CharacterController))]
public class BotController : MonoBehaviour
{
    [Header("References")]
    public MazeGameManager mazeManager;
    public AStarPathfinder pathfinder;
    public PlayerControllerLocal player;

    [Header("Stats & Materials")]
    public float playerSpeed = 4f;
    public float entitySize = 1f;
    public float maxChakra = 100f;
    public float chakraChargeRate = 20f;
    public float laserDamage = 10f;
    public float laserMaxRange = 50f;
    public Material laserMaterial;

    // HUD
    private const int barWidth = 200;
    private const int barHeight = 20;
    private const int margin = 10;

    private CharacterController cc;
    private LineRenderer laserLine;

    private Vector2Int gridPos;
    private float lastThinkTime;
    private bool isBusy;

    private float currentChakra;
    private float currentHealth;

    private float lastBotHitPlayerTime = -Mathf.Infinity;
    private float lastPlayerHitBotTime = -Mathf.Infinity;
    private const float hitWindow = 0.2f;

    private float[] weights => GAOptimizer.Instance.bestIndividual;
    private readonly string[] actions = { "Laser", "blocking", "hindering", "escaping", "showing up", "Teleport", "charging" };
    private const int numFeat = 5;

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
        cc = GetComponent<CharacterController>();
        cc.height = entitySize;
        cc.radius = entitySize * 0.5f;
        cc.center = new Vector3(0, cc.height / 2f, 0);

        Vector3 p = transform.position;
        p.y = cc.height / 2f + 0.5f;
        transform.position = p;

        currentChakra = maxChakra;
        currentHealth = maxChakra; // reuse same scale for health
    }

    void Update()
    {
        if (Time.time - lastThinkTime >= 1f && !isBusy)
        {
            lastThinkTime = Time.time;
            StartCoroutine(PerformBestAction());
        }
    }

    private IEnumerator PerformBestAction()
    {
        isBusy = true;
        UpdateGridPos();

        // Build state vector
        float[] state = new float[numFeat];
        Vector2Int pp = new Vector2Int(
            Mathf.FloorToInt(player.transform.position.z / mazeManager.cellSize),
            Mathf.FloorToInt(player.transform.position.x / mazeManager.cellSize)
        );
        state[0] = Vector2Int.Distance(gridPos, pp) /
                   Mathf.Sqrt(mazeManager.rows * mazeManager.rows + mazeManager.cols * mazeManager.cols);
        state[1] = ComputeVisibility() ? 1f : 0f;
        state[2] = (Time.time - lastBotHitPlayerTime) <= hitWindow ? 1f : 0f;
        state[3] = (Time.time - lastPlayerHitBotTime) <= hitWindow ? 1f : 0f;
        state[4] = currentChakra / maxChakra;

        Debug.Log($"[Bot] State: [{state[0]:F2}, {state[1]:F2}, {state[2]:F2}, {state[3]:F2}, {state[4]:F2}]");

        // Pick best action
        float bestFit = float.NegativeInfinity;
        int bestIdx = 0;
        for (int i = 0; i < actions.Length; i++)
        {
            float dot = 0f;
            for (int f = 0; f < numFeat; f++)
                dot += weights[i * numFeat + f] * state[f];
            if (dot > bestFit)
            {
                bestFit = dot;
                bestIdx = i;
            }
        }

        Debug.Log($"[Bot] Chose action: {actions[bestIdx]} (fitness={bestFit:F2})");

        yield return ExecuteAction(actions[bestIdx]);
        isBusy = false;
    }

    private bool ComputeVisibility()
    {
        Vector3 me = transform.position + Vector3.up * (entitySize * 0.5f);
        Vector3 ot = player.transform.position + Vector3.up * (entitySize * 0.5f);
        Vector3 dir = (ot - me).normalized;
        float dist = Vector3.Distance(me, ot);
        var hits = Physics.RaycastAll(me, dir, dist);
        Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
        foreach (var h in hits)
        {
            if (h.collider.gameObject == gameObject) continue;
            return h.collider.gameObject == player.gameObject;
        }
        return true;
    }

    private void UpdateGridPos()
    {
        int r = Mathf.FloorToInt(transform.position.z / mazeManager.cellSize);
        int c = Mathf.FloorToInt(transform.position.x / mazeManager.cellSize);
        gridPos = new Vector2Int(r, c);
    }

    private IEnumerator ExecuteAction(string act)
    {
        switch (act)
        {
            case "Laser": yield return LaserAction(); break;
            case "Teleport": yield return TeleportAction(); break;
            case "blocking": yield return BlockingAction(); break;
            case "hindering": yield return HinderingAction(); break;
            case "escaping": yield return EscapingAction(); break;
            case "showing up": yield return ShowingUpAction(); break;
            case "charging": yield return ChargingAction(); break;
        }
    }

    private IEnumerator LaserAction()
    {
        if (laserLine == null)
        {
            laserLine = new GameObject("BotLaser").AddComponent<LineRenderer>();
            laserLine.material = laserMaterial;
            laserLine.startWidth = laserLine.endWidth = 0.1f;
            laserLine.positionCount = 2;
            laserLine.useWorldSpace = true;
            laserLine.startColor = Color.yellow;
            laserLine.endColor = Color.yellow;
        }

        float end = Time.time + 2f;
        while (Time.time < end)
        {
            Vector3 dir = (player.transform.position - transform.position);
            dir.y = 0;
            transform.rotation = Quaternion.LookRotation(dir);

            Vector3 start = transform.position + Vector3.up * (entitySize * 0.5f);
            if (Physics.Raycast(start, dir, out var hit, laserMaxRange))
            {
                laserLine.SetPosition(0, start);
                laserLine.SetPosition(1, hit.point);

                hit.collider.GetComponent<DamageableWall>()?.TakeDamage(laserDamage * Time.deltaTime);

                var plc = hit.collider.GetComponent<PlayerControllerLocal>();
                if (plc != null)
                {
                    plc.TakeDamage(laserDamage * Time.deltaTime);
                    lastBotHitPlayerTime = Time.time;
                }
            }
            else
            {
                laserLine.SetPosition(0, start);
                laserLine.SetPosition(1, start + dir * laserMaxRange);
            }

            currentChakra = Mathf.Max(0f, currentChakra - laserDamage * Time.deltaTime);
            yield return null;
        }

        if (laserLine)
        {
            Destroy(laserLine.gameObject);
            laserLine = null;
        }
    }

    private IEnumerator TeleportAction()
    {
        var all = new List<Vector2Int>();
        for (int r = 0; r < mazeManager.rows; r++)
            for (int c = 0; c < mazeManager.cols; c++)
                all.Add(new Vector2Int(r, c));

        Vector2Int pp = new Vector2Int(
            Mathf.FloorToInt(player.transform.position.z / mazeManager.cellSize),
            Mathf.FloorToInt(player.transform.position.x / mazeManager.cellSize)
        );

        bool vis = ComputeVisibility();

        var ideal = all.FindAll(tp =>
            tp != gridPos && tp != pp &&
            ((!vis && IsVisibleAt(tp) && Vector2Int.Distance(tp, gridPos) >= 2f) ||
             (vis && !IsVisibleAt(tp) && Vector2Int.Distance(tp, gridPos) >= 5f)));

        var candidates = ideal.Count > 0 ? ideal : all.FindAll(tp => tp != gridPos && tp != pp);
        Vector2Int dest = candidates[UnityEngine.Random.Range(0, candidates.Count)];

        transform.position = mazeManager.GridToWorld(dest);
        yield return null;
    }

    private IEnumerator BlockingAction()
    {
        Vector3 dir = (player.transform.position - transform.position);
        dir.y = 0;
        transform.rotation = Quaternion.LookRotation(dir);
        mazeManager.BuildOrRemoveWall(gridPos, transform.eulerAngles.y);
        yield return new WaitForSeconds(1f);
    }

    private IEnumerator HinderingAction()
    {
        Vector2Int pp = new Vector2Int(
            Mathf.FloorToInt(player.transform.position.z / mazeManager.cellSize),
            Mathf.FloorToInt(player.transform.position.x / mazeManager.cellSize)
        );
        var path = pathfinder.FindPath(pp, gridPos);
        if (path.Count >= 2)
            mazeManager.BuildOrRemoveWall(path[1], 0f);
        yield return new WaitForSeconds(1f);
    }

    private IEnumerator EscapingAction()
    {
        List<Vector2Int> invis = new List<Vector2Int>();
        for (int r = 0; r < mazeManager.rows; r++)
            for (int c = 0; c < mazeManager.cols; c++)
                if (!(new Vector2Int(r, c) == gridPos) && !IsVisibleAt(new Vector2Int(r, c)))
                    invis.Add(new Vector2Int(r, c));

        List<Vector2Int> bestPath = null;
        int bestLen = int.MaxValue;
        foreach (var cand in invis)
        {
            var path = pathfinder.FindPath(gridPos, cand);
            if (path != null && path.Count > 0 && path.Count < bestLen)
            {
                bestLen = path.Count;
                bestPath = path;
            }
        }

        if (bestPath != null)
        {
            foreach (var step in bestPath)
            {
                Vector3 w = mazeManager.GridToWorld(step);
                while (Vector3.Distance(transform.position, w) > 0.1f)
                {
                    Vector3 d = (w - transform.position).normalized;
                    transform.rotation = Quaternion.LookRotation(d);
                    cc.Move(d * playerSpeed * Time.deltaTime);
                    yield return null;
                }
            }
        }
    }

    private IEnumerator ShowingUpAction()
    {
        List<Vector2Int> visList = new List<Vector2Int>();
        for (int r = 0; r < mazeManager.rows; r++)
            for (int c = 0; c < mazeManager.cols; c++)
                if (!(new Vector2Int(r, c) == gridPos) && IsVisibleAt(new Vector2Int(r, c)))
                    visList.Add(new Vector2Int(r, c));

        List<Vector2Int> bestPath = null;
        int bestLen = int.MaxValue;
        foreach (var cand in visList)
        {
            var path = pathfinder.FindPath(gridPos, cand);
            if (path != null && path.Count > 0 && path.Count < bestLen)
            {
                bestLen = path.Count;
                bestPath = path;
            }
        }

        if (bestPath != null)
        {
            foreach (var step in bestPath)
            {
                Vector3 w = mazeManager.GridToWorld(step);
                while (Vector3.Distance(transform.position, w) > 0.1f)
                {
                    Vector3 d = (w - transform.position).normalized;
                    transform.rotation = Quaternion.LookRotation(d);
                    cc.Move(d * playerSpeed * Time.deltaTime);
                    yield return null;
                }
            }
        }
    }

    private IEnumerator ChargingAction()
    {
        float end = Time.time + 1f;
        while (Time.time < end)
        {
            currentChakra = Mathf.Min(maxChakra, currentChakra + chakraChargeRate * Time.deltaTime);
            yield return null;
        }
    }

    private bool IsVisibleAt(Vector2Int gp)
    {
        Vector3 from = new Vector3(gp.y * mazeManager.cellSize, entitySize * 0.5f, gp.x * mazeManager.cellSize);
        Vector3 to = player.transform.position + Vector3.up * (entitySize * 0.5f);
        Vector3 dir = (to - from).normalized;
        float dist = Vector3.Distance(from, to);
        var hits = Physics.RaycastAll(from, dir, dist);
        Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
        foreach (var h in hits)
        {
            if (h.collider.gameObject == player.gameObject) return true;
            if (h.collider.GetComponent<DamageableWall>() != null) return false;
        }
        return true;
    }

    public void TakeDamage(float amt)
    {
        currentHealth = Mathf.Max(0f, currentHealth - amt);
        lastPlayerHitBotTime = Time.time;
    }

    public void RegisterPlayerHit()
    {
        lastBotHitPlayerTime = Time.time;
    }

    void OnGUI()
    {
        int x = Screen.width - barWidth - margin;

        float healthRatio = currentHealth / maxChakra;
        float chakraRatio = currentChakra / maxChakra;

        GUI.color = Color.green;
        GUI.DrawTexture(new Rect(x, margin, barWidth * healthRatio, barHeight), WhiteTexture);

        GUI.color = Color.blue;
        GUI.DrawTexture(new Rect(x, margin + barHeight + 5, barWidth * chakraRatio, barHeight), WhiteTexture);

        GUI.color = Color.white;
    }
}
