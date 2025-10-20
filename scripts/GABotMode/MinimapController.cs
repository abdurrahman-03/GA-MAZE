using UnityEngine;

/// <summary>
/// Attach this to an orthographic MinimapCamera that renders the maze top-down
/// into a RenderTexture. Assign your player and bot Transforms below.
/// The camera will follow the player's XZ position, and rotate around Y to match
/// the player's yaw, so the circular UI map always points in the direction the player is facing.
/// </summary>
public class MinimapController : MonoBehaviour
{
    [Tooltip("The player transform to follow / rotate with")]
    public Transform player;

    [Tooltip("The bot transform to follow (for centering)")]
    public Transform bot;

    [Tooltip("Height above the ground the minimap camera should sit at")]
    public float height = 50f;

    void LateUpdate()
    {
        if (player == null) return;

        // Position the camera directly above the player (or midpoint if desired)
        Vector3 targetPos = player.position;
        targetPos.y = height;
        transform.position = targetPos;

        // Rotate so 'up' on the map = player's forward direction
        Vector3 e = transform.eulerAngles;
        e.x = 90f;                    // look straight down
        e.y = player.eulerAngles.y;   // match player yaw
        e.z = 0f;
        transform.eulerAngles = e;
    }
}
