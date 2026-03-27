using UnityEngine;

/// <summary>
/// Fixed object: click during prep to toggle open/closed (window, toilet door, etc.).
/// </summary>
public class ToggleInteractable : MonoBehaviour
{
    [SerializeField] Camera targetCamera;
    [SerializeField] LayerMask interactLayers;

    [Header("Visual (pick one approach)")]
    [SerializeField] SpriteRenderer spriteRenderer;
    [SerializeField] Sprite openSprite;
    [SerializeField] Sprite closedSprite;

    [SerializeField] GameObject openVisual;
    [SerializeField] GameObject closedVisual;

    Collider2D _collider;
    bool _isOpen;

    void Awake()
    {
        _collider = GetComponent<Collider2D>();
        if (_collider == null)
        {
            var box = gameObject.AddComponent<BoxCollider2D>();
            if (spriteRenderer != null && spriteRenderer.sprite != null)
            {
                box.size = spriteRenderer.sprite.bounds.size;
                box.offset = spriteRenderer.sprite.bounds.center;
            }
            _collider = box;
        }

        if (spriteRenderer == null) spriteRenderer = GetComponent<SpriteRenderer>();

        if (interactLayers.value == 0)
            interactLayers = LayerMask.GetMask("RoomItems");

        if (targetCamera == null) targetCamera = Camera.main;

        ApplyVisual();
    }

    void Update()
    {
        if (FartGameSession.Instance == null || !FartGameSession.Instance.CanInteractDuringPrep) return;
        if (targetCamera == null || _collider == null) return;
        if (!Input.GetMouseButtonDown(0)) return;

        Vector2 world = FartMouseUtility.ScreenToWorld2D(targetCamera, Input.mousePosition, transform.position.z);
        Collider2D hit = Physics2D.OverlapPoint(world, interactLayers);
        if (hit != null && hit == _collider)
        {
            _isOpen = !_isOpen;
            ApplyVisual();
        }
    }

    void ApplyVisual()
    {
        if (spriteRenderer != null && openSprite != null && closedSprite != null)
            spriteRenderer.sprite = _isOpen ? openSprite : closedSprite;

        if (openVisual != null) openVisual.SetActive(_isOpen);
        if (closedVisual != null) closedVisual.SetActive(!_isOpen);
    }
}
