using UnityEngine;

/// <summary>
/// WASD: A/D strafe, W/S move along Y as pseudo-depth. Uniform scale by Y: smaller when farther, larger when closer.
/// Set the sprite pivot at the feet so scaling reads as grounded on the horizon line.
/// </summary>
[RequireComponent(typeof(SpriteRenderer))]
public class RoomCharacterWalk : MonoBehaviour
{
    [Header("Movement")]
    [SerializeField] float moveSpeed = 3f;

    [Tooltip("Horizontal bounds (world units).")]
    [SerializeField] float minX = -8f;
    [SerializeField] float maxX = 8f;

    [Tooltip("Depth bounds on Y (world space), e.g. -5 (far) … -0.9 (near).")]
    [SerializeField] float minY = -5f;
    [SerializeField] float maxY = -0.9f;

    [Header("Depth = scale (far = small, near = big)")]
    [SerializeField] float scaleAtMinY = 0.55f;
    [SerializeField] float scaleAtMaxY = 1.15f;

    [Tooltip("If true, higher world Y = farther (smaller). If false, invert that mapping.")]
    [SerializeField] bool largerTowardMaxY = true;

    [Header("Optional")]
    [SerializeField] bool flipSpriteOnTurn = true;
    [SerializeField] bool updateSortingOrderByDepth = false;
    [SerializeField] int sortingOrderBase = 0;
    [SerializeField] int sortingOrderPerY = 10;

    SpriteRenderer _sr;

    void Awake()
    {
        _sr = GetComponent<SpriteRenderer>();
    }

    void Update()
    {
        if (!FartGameSession.Instance || !FartGameSession.Instance.CanInteractDuringPrep)
            return;

        float x = Input.GetAxisRaw("Horizontal");
        float y = Input.GetAxisRaw("Vertical");

        Vector3 p = transform.position;
        p.x += x * moveSpeed * Time.deltaTime;
        p.y += y * moveSpeed * Time.deltaTime;
        p.x = Mathf.Clamp(p.x, minX, maxX);
        p.y = Mathf.Clamp(p.y, minY, maxY);
        transform.position = p;

        float depthT = Mathf.InverseLerp(minY, maxY, p.y);
        if (!largerTowardMaxY) depthT = 1f - depthT;
        // Invert Y vs scale: walking toward camera (usually lower Y) grows, away shrinks.
        depthT = 1f - depthT;

        float s = Mathf.Lerp(scaleAtMinY, scaleAtMaxY, depthT);
        transform.localScale = new Vector3(s, s, 1f);

        if (flipSpriteOnTurn && _sr != null && Mathf.Abs(x) > 0.01f)
            _sr.flipX = x < 0f;

        if (updateSortingOrderByDepth && _sr != null)
            _sr.sortingOrder = sortingOrderBase + Mathf.RoundToInt(p.y * sortingOrderPerY);
    }

#if UNITY_EDITOR
    void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(0f, 1f, 0.5f, 0.35f);
        float cx = (minX + maxX) * 0.5f;
        float cy = (minY + maxY) * 0.5f;
        Vector3 c = transform.position;
        c.x = cx;
        c.y = cy;
        Vector3 size = new Vector3(maxX - minX, maxY - minY, 0.1f);
        Gizmos.DrawWireCube(c, size);
    }
#endif
}
