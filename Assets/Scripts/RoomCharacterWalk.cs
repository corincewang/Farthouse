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
    [SerializeField] bool enableBandSpecificLeftBound = true;
    [SerializeField] float bandMinY = -1.2f;
    [SerializeField] float bandMaxY = -0.9f;
    [SerializeField] float bandMinX = -5.3f;

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
    Rigidbody2D _rb;
    Vector2 _moveInput;

    void Awake()
    {
        _sr = GetComponent<SpriteRenderer>();
        _rb = GetComponent<Rigidbody2D>();
    }

    void Update()
    {
        if (!FartGameSession.Instance || !FartGameSession.Instance.CanInteractDuringPrep)
            return;

        _moveInput = ReadMoveInput();
        if (_rb == null)
            ApplyMove(Time.deltaTime);
    }

    void FixedUpdate()
    {
        if (!FartGameSession.Instance || !FartGameSession.Instance.CanInteractDuringPrep)
            return;
        if (_rb == null) return;
        ApplyMove(Time.fixedDeltaTime);
    }

    Vector2 ReadMoveInput()
    {
        float x = 0f;
        float y = 0f;

        if (Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.LeftArrow)) x -= 1f;
        if (Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow)) x += 1f;
        if (Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.DownArrow)) y -= 1f;
        if (Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.UpArrow)) y += 1f;

        return new Vector2(x, y).normalized;
    }

    void ApplyMove(float dt)
    {
        float x = _moveInput.x;
        float y = _moveInput.y;

        Vector3 p = _rb != null ? _rb.position : (Vector2)transform.position;
        float proposedX = p.x + x * moveSpeed * dt;
        p.y += y * moveSpeed * dt;
        p.y = Mathf.Clamp(p.y, minY, maxY);
        float activeMinX = ResolveActiveMinX(p.y);

        // Block outward movement at boundaries instead of snapping to the edge.
        if (proposedX < activeMinX && x < 0f)
            proposedX = p.x;
        else if (proposedX > maxX && x > 0f)
            proposedX = p.x;

        p.x = proposedX;
        if (_rb != null) _rb.MovePosition(p);
        else transform.position = p;

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

    float ResolveActiveMinX(float y)
    {
        if (!enableBandSpecificLeftBound) return minX;
        if (y >= bandMinY && y <= bandMaxY)
            return Mathf.Min(minX, bandMinX);
        return minX;
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
