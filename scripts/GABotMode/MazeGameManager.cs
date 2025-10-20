using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class MazeGameManager : MonoBehaviour
{
    [Header("Maze Settings")]
    public int rows = 20;
    public int cols = 20;
    public float cellSize = 4f;

    [Header("Prefabs (assign in Inspector)")]
    public GameObject wallPrefab;
    public GameObject floorPrefab;
    // fruitPrefab removed

    [Header("Extra Openings")]
    [Range(0f, 1f)] public float openingsProbability = 0.3f;

    [Header("Wall & Floor Dimensions")]
    public float wallThickness = 0.3f;
    public float wallHeight = 3f;

    [Header("Wall Health Settings")]
    public float wallMaxHealth = 50f;
    public float wallHealthBarDisappearDelay = 3f;

    // runtime built walls
    public float builtWallDuration = 10f;
    public int maxBuiltWalls = 10;

    [Header("FX")]
    public AudioClip buildClip; // assign sound effect

    // internals
    private bool[,] verticalWalls;
    private bool[,] horizontalWalls;

    private Dictionary<string, GameObject> builtWalls = new Dictionary<string, GameObject>();
    private Dictionary<string, float> builtWallExpiry = new Dictionary<string, float>();

    // changed so walls sit flush on the floor
    private float wallYOffset = 0f;

    // spawn positions
    private List<Vector3> playerSpawnPositions = new List<Vector3>();
    private int nextSpawnIndex = 0;

    void Awake()
    {
        GenerateMaze();
        AddExtraOpenings();
        DrawMaze();
    }

    void Start()
    {
        playerSpawnPositions.Add(GridToWorld(new Vector2Int(1, 1)));
        playerSpawnPositions.Add(GridToWorld(new Vector2Int(rows - 2, cols - 2)));
    }

    void Update()
    {
        float now = Time.time;
        var expired = new List<string>();

        foreach (var kv in builtWallExpiry)
            if (now >= kv.Value)
                expired.Add(kv.Key);

        foreach (var key in expired)
        {
            Destroy(builtWalls[key]);
            builtWalls.Remove(key);
            builtWallExpiry.Remove(key);
        }
    }

    #region Maze Generation & Drawing
    public void GenerateMaze()
    {
        verticalWalls = new bool[rows, cols - 1];
        horizontalWalls = new bool[rows - 1, cols];
        bool[,] visited = new bool[rows, cols];

        for (int r = 0; r < rows; r++)
            for (int c = 0; c < cols - 1; c++)
                verticalWalls[r, c] = true;

        for (int r = 0; r < rows - 1; r++)
            for (int c = 0; c < cols; c++)
                horizontalWalls[r, c] = true;

        var stack = new Stack<Vector2Int>();
        visited[0, 0] = true;
        stack.Push(new Vector2Int(0, 0));
        var rnd = new System.Random();

        while (stack.Count > 0)
        {
            var cur = stack.Peek();
            var neigh = new List<Vector2Int>();

            if (cur.x > 0 && !visited[cur.x - 1, cur.y]) neigh.Add(new Vector2Int(cur.x - 1, cur.y));
            if (cur.x < rows - 1 && !visited[cur.x + 1, cur.y]) neigh.Add(new Vector2Int(cur.x + 1, cur.y));
            if (cur.y > 0 && !visited[cur.x, cur.y - 1]) neigh.Add(new Vector2Int(cur.x, cur.y - 1));
            if (cur.y < cols - 1 && !visited[cur.x, cur.y + 1]) neigh.Add(new Vector2Int(cur.x, cur.y + 1));

            if (neigh.Count > 0)
            {
                var pick = neigh[rnd.Next(neigh.Count)];

                if (pick.x == cur.x + 1) horizontalWalls[cur.x, cur.y] = false;
                else if (pick.x == cur.x - 1) horizontalWalls[pick.x, cur.y] = false;
                else if (pick.y == cur.y + 1) verticalWalls[cur.x, pick.y - 1] = false;
                else verticalWalls[cur.x, pick.y] = false;

                visited[pick.x, pick.y] = true;
                stack.Push(pick);
            }
            else stack.Pop();
        }
    }

    public void AddExtraOpenings()
    {
        var rnd = new System.Random();
        for (int r = 0; r < rows; r++)
            for (int c = 0; c < cols - 1; c++)
                if (verticalWalls[r, c] && rnd.NextDouble() < openingsProbability)
                    verticalWalls[r, c] = false;

        for (int r = 0; r < rows - 1; r++)
            for (int c = 0; c < cols; c++)
                if (horizontalWalls[r, c] && rnd.NextDouble() < openingsProbability)
                    horizontalWalls[r, c] = false;
    }

    public void DrawMaze()
    {
        // floor
        if (floorPrefab)
        {
            Vector3 fp = new Vector3((cols * cellSize) / 2f, 0, (rows * cellSize) / 2f);
            var fl = Instantiate(floorPrefab, fp, Quaternion.identity, transform);
            fl.transform.localScale = new Vector3((cols * cellSize) / 10f, 1, (rows * cellSize) / 10f);
        }

        // vertical walls
        for (int r = 0; r < rows; r++)
            for (int c = 0; c < cols - 1; c++)
                if (verticalWalls[r, c])
                    SpawnWallAt(
                        new Vector3((c + 1) * cellSize, wallHeight / 2f, r * cellSize + cellSize / 2f),
                        new Vector3(wallThickness, wallHeight, cellSize)
                    );

        // horizontal walls
        for (int r = 0; r < rows - 1; r++)
            for (int c = 0; c < cols; c++)
                if (horizontalWalls[r, c])
                    SpawnWallAt(
                        new Vector3(c * cellSize + cellSize / 2f, wallHeight / 2f, (r + 1) * cellSize),
                        new Vector3(cellSize, wallHeight, wallThickness)
                    );

        // border walls
        for (int r = 0; r < rows; r++)
        {
            SpawnWallAt(new Vector3(0, wallHeight / 2f, r * cellSize + cellSize / 2f),
                        new Vector3(wallThickness, wallHeight, cellSize));
            SpawnWallAt(new Vector3(cols * cellSize, wallHeight / 2f, r * cellSize + cellSize / 2f),
                        new Vector3(wallThickness, wallHeight, cellSize));
        }

        for (int c = 0; c < cols; c++)
        {
            SpawnWallAt(new Vector3(c * cellSize + cellSize / 2f, wallHeight / 2f, 0),
                        new Vector3(cellSize, wallHeight, wallThickness));
            SpawnWallAt(new Vector3(c * cellSize + cellSize / 2f, wallHeight / 2f, rows * cellSize),
                        new Vector3(cellSize, wallHeight, wallThickness));
        }
    }

    private void SpawnWallAt(Vector3 pos, Vector3 scale)
    {
        var w = Instantiate(wallPrefab, pos + Vector3.up * wallYOffset, Quaternion.identity, transform);
        w.transform.localScale = scale;

        w.AddComponent<BoxCollider>();

        var dw = w.AddComponent<DamageableWall>();
        dw.maxHealth = wallMaxHealth;
        dw.currentHealth = wallMaxHealth;
        dw.healthBarDisappearDelay = wallHealthBarDisappearDelay;
    }
    #endregion

    #region Dynamic Wall Build/Remove
    public void BuildOrRemoveWall(Vector2Int playerGridPos, float playerAngle)
    {
        var offset = GetFacingGridOffset(playerAngle);
        bool horiz = offset.x != 0;
        int wr = horiz ? (offset.x == 1 ? playerGridPos.x : playerGridPos.x - 1) : playerGridPos.x;
        int wc = horiz ? playerGridPos.y : (offset.y == 1 ? playerGridPos.y : playerGridPos.y - 1);

        string key = (horiz ? "H_" : "V_") + wr + "_" + wc;

        // If already built, remove it
        if (builtWalls.ContainsKey(key))
        {
            Destroy(builtWalls[key]);
            builtWalls.Remove(key);
            builtWallExpiry.Remove(key);
            return;
        }

        if (wr < 0 || wc < 0 || wr >= rows || wc >= cols) return;

        Vector3 posWorld, scale;
        if (horiz)
        {
            posWorld = new Vector3(wc * cellSize + cellSize / 2f, wallHeight / 2f, (wr + 1) * cellSize)
                       + Vector3.up * wallYOffset;
            scale = new Vector3(cellSize, wallHeight, wallThickness);
        }
        else
        {
            posWorld = new Vector3((wc + 1) * cellSize, wallHeight / 2f, wr * cellSize + cellSize / 2f)
                       + Vector3.up * wallYOffset;
            scale = new Vector3(wallThickness, wallHeight, cellSize);
        }

        Collider[] hits = Physics.OverlapBox(posWorld, scale * 0.5f, Quaternion.identity);
        foreach (var hit in hits)
            if (hit.GetComponent<MeshRenderer>() != null && Vector3.Distance(hit.transform.position, posWorld) < 0.1f)
                return;

        if (builtWalls.Count >= maxBuiltWalls) return;

        var nw = Instantiate(wallPrefab);
        nw.transform.parent = transform;
        nw.transform.position = posWorld;
        nw.transform.localScale = scale;

        if (buildClip != null)
            AudioSource.PlayClipAtPoint(buildClip, nw.transform.position);

        builtWalls[key] = nw;
        builtWallExpiry[key] = Time.time + builtWallDuration;

        nw.AddComponent<BoxCollider>();
        var dw2 = nw.AddComponent<DamageableWall>();
        dw2.maxHealth = wallMaxHealth;
        dw2.currentHealth = wallMaxHealth;
        dw2.healthBarDisappearDelay = wallHealthBarDisappearDelay;
    }
    #endregion

    #region Helpers
    public Vector3 GridToWorld(Vector2Int gp)
        => new Vector3(gp.y * cellSize + cellSize / 2f, 0.5f, gp.x * cellSize + cellSize / 2f);

    public Vector3 GetNextPlayerSpawnPos()
    {
        if (playerSpawnPositions.Count == 0)
            return GridToWorld(new Vector2Int(1, 1));

        var pos = playerSpawnPositions[nextSpawnIndex];
        nextSpawnIndex = (nextSpawnIndex + 1) % playerSpawnPositions.Count;
        return pos;
    }

    private Vector2Int GetFacingGridOffset(float angle)
    {
        angle %= 360;
        if (angle < 0) angle += 360;

        if (angle >= 315 || angle < 45) return new Vector2Int(1, 0);
        if (angle < 135) return new Vector2Int(0, 1);
        if (angle < 225) return new Vector2Int(-1, 0);
        return new Vector2Int(0, -1);
    }
    #endregion
}
