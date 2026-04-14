using UnityEngine;

/// <summary>Screen → world XY for 2D room picking (item colliders only).</summary>
public static class FartMouseUtility
{
    public static Vector2 ScreenToWorld2D(Camera camera, Vector3 screenPosition, float worldZ)
    {
        if (camera == null) return Vector2.zero;
        screenPosition.z = camera.WorldToScreenPoint(new Vector3(0f, 0f, worldZ)).z;
        Vector3 w = camera.ScreenToWorldPoint(screenPosition);
        return new Vector2(w.x, w.y);
    }
}
