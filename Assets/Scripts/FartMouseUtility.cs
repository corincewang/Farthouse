using UnityEngine;

/// <summary>Screen → world (XY) for an orthographic camera; keeps Z from the target transform.</summary>
public static class FartMouseUtility
{
    public static Vector2 ScreenToWorld2D(Camera cam, Vector3 screenPosition, float targetWorldZ)
    {
        if (cam == null) return Vector2.zero;
        screenPosition.z = Mathf.Abs(cam.transform.position.z - targetWorldZ);
        Vector3 w = cam.ScreenToWorldPoint(screenPosition);
        return new Vector2(w.x, w.y);
    }
}
