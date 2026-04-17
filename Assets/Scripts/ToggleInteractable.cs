using UnityEngine;

/// <summary>
/// Room items only: left-click during prep toggles open/closed (window, toilet door, etc.).
/// </summary>
public class ToggleInteractable : MonoBehaviour
{
    enum InteractableKind
    {
        GenericToggle,
        Window,
        ToiletDoor
    }

    [SerializeField] Camera targetCamera;
    [SerializeField] LayerMask interactLayers;
    [SerializeField] InteractableKind interactableKind = InteractableKind.GenericToggle;

    [Header("Visual (pick one approach)")]
    [SerializeField] SpriteRenderer spriteRenderer;
    [SerializeField] Sprite openSprite;
    [SerializeField] Sprite closedSprite;

    [SerializeField] GameObject openVisual;
    [SerializeField] GameObject closedVisual;

    [Header("Toilet occupied UI (optional)")]
    [SerializeField] GameObject toiletOccupiedVisual;
    [SerializeField] bool autoFindToiletOccupiedVisual = true;
    [SerializeField] float toiletOccupiedHintSeconds = 1.25f;

    Collider2D _collider;
    bool _isOpen;
    Coroutine _occupiedHintRoutine;

    public bool IsOpen => _isOpen;

    void Awake()
    {
        if (spriteRenderer == null) spriteRenderer = GetComponent<SpriteRenderer>();

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

        if (interactLayers.value == 0)
            interactLayers = LayerMask.GetMask("RoomItems");

        if (targetCamera == null) targetCamera = Camera.main;

        if (interactableKind == InteractableKind.ToiletDoor && toiletOccupiedVisual == null && autoFindToiletOccupiedVisual)
            toiletOccupiedVisual = TryAutoFindToiletOccupiedVisual();

        ApplyVisual();
        if (toiletOccupiedVisual != null)
            toiletOccupiedVisual.SetActive(false);
    }

    void Update()
    {
        if (FartGameSession.Instance == null || !FartGameSession.Instance.CanInteractDuringPrep) return;
        if (targetCamera == null || _collider == null) return;
        if (!Input.GetMouseButtonDown(0)) return;

        Vector2 world = FartMouseUtility.ScreenToWorld2D(targetCamera, Input.mousePosition, transform.position.z);
        Collider2D hit = Physics2D.OverlapPoint(world, interactLayers);
        bool clickedSelf = hit != null && hit == _collider;
        if (!clickedSelf)
            clickedSelf = _collider.OverlapPoint(world);

        if (clickedSelf)
        {
            HandleInteract();
        }
    }

    void HandleInteract()
    {
        var session = FartGameSession.Instance;
        if (session == null) return;

        if (interactableKind == InteractableKind.ToiletDoor)
        {
            bool canEnter = session.TryEnterToiletThisRound();
            _isOpen = canEnter;
            ShowToiletOccupiedHint(!canEnter);
            ApplyVisual();
            return;
        }

        _isOpen = !_isOpen;
        if (interactableKind == InteractableKind.Window)
            session.SetCurrentRoundWindowState(_isOpen);
        ApplyVisual();
    }

    void ApplyVisual()
    {
        if (spriteRenderer != null && openSprite != null && closedSprite != null)
            spriteRenderer.sprite = _isOpen ? openSprite : closedSprite;

        if (openVisual != null) openVisual.SetActive(_isOpen);
        if (closedVisual != null) closedVisual.SetActive(!_isOpen);
    }

    void ShowToiletOccupiedHint(bool show)
    {
        if (toiletOccupiedVisual == null) return;

        if (_occupiedHintRoutine != null)
        {
            StopCoroutine(_occupiedHintRoutine);
            _occupiedHintRoutine = null;
        }

        toiletOccupiedVisual.SetActive(show);
        if (!show) return;

        float wait = Mathf.Max(0.1f, toiletOccupiedHintSeconds);
        _occupiedHintRoutine = StartCoroutine(HideToiletOccupiedHintAfter(wait));
    }

    System.Collections.IEnumerator HideToiletOccupiedHintAfter(float seconds)
    {
        yield return new WaitForSeconds(seconds);
        if (toiletOccupiedVisual != null)
            toiletOccupiedVisual.SetActive(false);
        _occupiedHintRoutine = null;
    }

    GameObject TryAutoFindToiletOccupiedVisual()
    {
        foreach (var sr in GetComponentsInChildren<SpriteRenderer>(true))
        {
            if (sr == null || sr.gameObject == gameObject) continue;
            string n = sr.gameObject.name.ToLowerInvariant();
            if (n.Contains("occupied") || n.Contains("effect") || n.Contains("toilet_effect"))
                return sr.gameObject;
        }

        foreach (var tr in GetComponentsInChildren<Transform>(true))
        {
            if (tr == null || tr.gameObject == gameObject) continue;
            string n = tr.gameObject.name.ToLowerInvariant();
            if (n.Contains("occupied") || n.Contains("effect") || n.Contains("toilet_effect"))
                return tr.gameObject;
        }

        return null;
    }
}
