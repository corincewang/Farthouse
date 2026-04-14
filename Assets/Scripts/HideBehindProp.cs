using UnityEngine;

/// <summary>
/// Room items only: left-click during prep toggles hiding the player behind this prop (sorting order).
/// </summary>
public class HideBehindProp : MonoBehaviour
{
    [SerializeField] Camera targetCamera;
    [SerializeField] LayerMask interactLayers;

    [Tooltip("Character that moves visually behind this prop when hiding.")]
    [SerializeField] SpriteRenderer playerSprite;

    SpriteRenderer _plantSprite;
    Collider2D _collider;
    bool _hiding;
    int _savedPlayerSortingOrder;
    int _savedPlayerSortingLayerId;

    public bool IsHidingBehindThis => _hiding;

    void Awake()
    {
        _plantSprite = GetComponent<SpriteRenderer>();
        _collider = GetComponent<Collider2D>();
        if (_collider == null)
        {
            var box = gameObject.AddComponent<BoxCollider2D>();
            if (_plantSprite != null && _plantSprite.sprite != null)
            {
                box.size = _plantSprite.sprite.bounds.size;
                box.offset = _plantSprite.sprite.bounds.center;
            }
            _collider = box;
        }

        if (interactLayers.value == 0)
            interactLayers = LayerMask.GetMask("RoomItems");

        if (targetCamera == null) targetCamera = Camera.main;

        if (playerSprite != null)
            _savedPlayerSortingOrder = playerSprite.sortingOrder;
    }

    void Update()
    {
        if (FartGameSession.Instance == null || !FartGameSession.Instance.CanInteractDuringPrep) return;
        if (targetCamera == null || _collider == null) return;
        if (!Input.GetMouseButtonDown(0)) return;

        Vector2 world = FartMouseUtility.ScreenToWorld2D(targetCamera, Input.mousePosition, transform.position.z);
        Collider2D hit = Physics2D.OverlapPoint(world, interactLayers);
        if (hit == null || hit != _collider) return;

        _hiding = !_hiding;
        ApplyHideVisual();
        Debug.Log(_hiding ? "[Farthouse] Hiding behind plant." : "[Farthouse] Left the plant (no longer hiding).");
    }

    void ApplyHideVisual()
    {
        if (playerSprite == null || _plantSprite == null) return;

        if (_hiding)
        {
            _savedPlayerSortingOrder = playerSprite.sortingOrder;
            _savedPlayerSortingLayerId = playerSprite.sortingLayerID;
            playerSprite.sortingLayerID = _plantSprite.sortingLayerID;
            playerSprite.sortingOrder = _plantSprite.sortingOrder - 1;
        }
        else
        {
            playerSprite.sortingLayerID = _savedPlayerSortingLayerId;
            playerSprite.sortingOrder = _savedPlayerSortingOrder;
        }
    }
}
