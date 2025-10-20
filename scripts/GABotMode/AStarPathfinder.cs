using System;
using System.Collections.Generic;
using UnityEngine;

public class AStarPathfinder : MonoBehaviour
{
    public MazeGameManager mazeManager;

    private int rows, cols;
    private float cellSize;
    private bool[,] vWalls, hWalls;

    void Awake()
    {
        rows = mazeManager.rows;
        cols = mazeManager.cols;
        cellSize = mazeManager.cellSize;

        // Reflect into private arrays
        vWalls = (bool[,])typeof(MazeGameManager)
            .GetField("verticalWalls", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
            .GetValue(mazeManager);

        hWalls = (bool[,])typeof(MazeGameManager)
            .GetField("horizontalWalls", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
            .GetValue(mazeManager);
    }

    public List<Vector2Int> FindPath(Vector2Int start, Vector2Int goal)
    {
        var closed = new HashSet<Vector2Int>();
        var open = new PriorityQueue<Node>();
        var cameFrom = new Dictionary<Vector2Int, Vector2Int>();
        var gScore = new Dictionary<Vector2Int, float>();
        gScore[start] = 0f;
        open.Enqueue(new Node(start, Heuristic(start, goal)));

        while (open.Count > 0)
        {
            var current = open.Dequeue().pos;
            if (current == goal)
                return ReconstructPath(cameFrom, current);

            closed.Add(current);

            foreach (var dir in new[] { Vector2Int.up, Vector2Int.down, Vector2Int.left, Vector2Int.right })
            {
                var neigh = current + dir;
                if (neigh.x < 0 || neigh.y < 0 || neigh.x >= rows || neigh.y >= cols)
                    continue;
                if (IsBlocked(current, dir)) continue;
                if (closed.Contains(neigh)) continue;

                float tentative = gScore[current] + 1f;
                if (!gScore.ContainsKey(neigh) || tentative < gScore[neigh])
                {
                    cameFrom[neigh] = current;
                    gScore[neigh] = tentative;
                    float f = tentative + Heuristic(neigh, goal);
                    open.Enqueue(new Node(neigh, f));
                }
            }
        }

        return new List<Vector2Int>();
    }

    private bool IsBlocked(Vector2Int cell, Vector2Int dir)
    {
        if (dir.x != 0)
        {
            int r = cell.x;
            int c = dir.x == 1 ? cell.y : cell.y - 1;
            if (c < 0 || c >= cols - 1) return true;
            return vWalls[r, c];
        }
        else
        {
            int r = dir.y == 1 ? cell.x : cell.x - 1;
            int c = cell.y;
            if (r < 0 || r >= rows - 1) return true;
            return hWalls[r, c];
        }
    }

    private float Heuristic(Vector2Int a, Vector2Int b)
        => Mathf.Abs(a.x - b.x) + Mathf.Abs(a.y - b.y);

    private List<Vector2Int> ReconstructPath(Dictionary<Vector2Int, Vector2Int> cameFrom, Vector2Int current)
    {
        var path = new List<Vector2Int> { current };
        while (cameFrom.ContainsKey(current))
        {
            current = cameFrom[current];
            path.Insert(0, current);
        }
        return path;
    }

    private class Node : IComparable<Node>
    {
        public Vector2Int pos;
        public float f;
        public Node(Vector2Int p, float f)
        {
            pos = p;
            this.f = f;
        }
        public int CompareTo(Node other) => f < other.f ? -1 : (f > other.f ? 1 : 0);
    }

    private class PriorityQueue<T> where T : IComparable<T>
    {
        private List<T> data = new List<T>();
        public int Count => data.Count;

        public void Enqueue(T item)
        {
            data.Add(item);
            int ci = data.Count - 1;
            while (ci > 0)
            {
                int pi = (ci - 1) / 2;
                if (data[ci].CompareTo(data[pi]) >= 0) break;
                (data[ci], data[pi]) = (data[pi], data[ci]);
                ci = pi;
            }
        }

        public T Dequeue()
        {
            int li = data.Count - 1;
            var front = data[0];
            data[0] = data[li];
            data.RemoveAt(li);
            li--;

            int pi = 0;
            while (true)
            {
                int ci = pi * 2 + 1;
                if (ci > li) break;
                int rc = ci + 1;
                if (rc <= li && data[rc].CompareTo(data[ci]) < 0) ci = rc;
                if (data[pi].CompareTo(data[ci]) <= 0) break;
                (data[pi], data[ci]) = (data[ci], data[pi]);
                pi = ci;
            }
            return front;
        }
    }
}
