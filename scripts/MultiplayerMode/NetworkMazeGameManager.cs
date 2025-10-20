using UnityEngine;
using Mirror;

public class NetworkMazeGameManager : NetworkManager
{
    [Header("Maze Manager Reference")]
    // Reference to your MazeGameManager instance in the scene.
    public MazeGameManager mazeGameManager;

    public override void OnStartServer()
    {
        base.OnStartServer();
        // Generate the maze on the server so that all clients share the same maze.
        if (mazeGameManager != null)
        {
            mazeGameManager.GenerateMaze();
            mazeGameManager.AddExtraOpenings();
            mazeGameManager.DrawMaze();
            mazeGameManager.SpawnFruits();
        }
        else
        {
            Debug.LogError("MazeGameManager reference not set in NetworkMazeGameManager.");
        }
    }

    public override void OnServerAddPlayer(NetworkConnectionToClient conn)
    {
        // Determine a spawn position from the MazeGameManager.
        // For example, the first player spawns at top-left, the second at bottom-right.
        Vector3 spawnWorldPos = mazeGameManager.GetNextPlayerSpawnPos();

        // Instantiate the player prefab (it should have a NetworkIdentity and PlayerController).
        GameObject player = Instantiate(playerPrefab, spawnWorldPos, Quaternion.identity);

        NetworkServer.AddPlayerForConnection(conn, player);
    }
}
