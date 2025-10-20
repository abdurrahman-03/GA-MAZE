using UnityEngine;

public class SceneBootstrap : MonoBehaviour
{
    public MazeGameManager mazeManager;

    void Start()
    {
        // Spawn player capsule
        var p = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        p.name = "Player";
        p.transform.position = mazeManager.GetNextPlayerSpawnPos();
        p.AddComponent<CharacterController>();
        p.AddComponent<DamageableEntity>();
        p.AddComponent<PlayerControllerLocal>();

        // Spawn bot capsule
        var b = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        b.name = "Bot";
        b.transform.position = mazeManager.GetNextPlayerSpawnPos();
        b.GetComponent<Renderer>().material.color = Color.red;
        b.AddComponent<CharacterController>();
        b.AddComponent<DamageableEntity>();

        var bot = b.AddComponent<BotController>();
        bot.mazeManager = mazeManager;
        bot.pathfinder = FindObjectOfType<AStarPathfinder>();
        bot.player = p.GetComponent<PlayerControllerLocal>();
    }
}
