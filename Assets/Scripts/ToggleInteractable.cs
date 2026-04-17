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

    [Header("Toilet entered (optional)")]
    [SerializeField] GameObject toiletEnterMessageVisual;
    [SerializeField] bool autoFindToiletEnterMessageVisual = true;
    [SerializeField] float toiletEnterHintSeconds = 1.4f;
    [Tooltip("If null, uses GameObject with tag Player.")]
    [SerializeField] GameObject characterRootToHide;
    [SerializeField] string toiletEnterLogMessage = "Entered toilet.";

    Collider2D _collider;
    bool _isOpen;
    Coroutine _occupiedHintRoutine;
    Coroutine _enterHintRoutine;

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
        if (interactableKind == InteractableKind.ToiletDoor && toiletEnterMessageVisual == null && autoFindToiletEnterMessageVisual)
            toiletEnterMessageVisual = TryAutoFindToiletEnterMessageVisual();

        ApplyVisual();
        if (toiletOccupiedVisual != null)
            toiletOccupiedVisual.SetActive(false);
        if (toiletEnterMessageVisual != null)
            toiletEnterMessageVisual.SetActive(false);
    }

    void Update()
    {
        if (FartGameSession.Instance == null || !FartGameSession.Instance.CanInteractDuringPrep) return;
        if (targetCamera == null) return;
        if (!Input.GetMouseButtonDown(0)) return;

        Vector2 world = FartMouseUtility.ScreenToWorld2D(targetCamera, Input.mousePosition, transform.position.z);
        Collider2D hit = Physics2D.OverlapPoint(world, interactLayers);
        bool clickedSelf = IsHitOnThisInteractable(hit);
        if (!clickedSelf && _collider != null)
            clickedSelf = _collider.OverlapPoint(world);

        if (clickedSelf)
        {
            HandleInteract();
        }
    }

    bool IsHitOnThisInteractable(Collider2D hit)
    {
        if (hit == null) return false;
        if (hit == _collider) return true;
        Transform t = hit.transform;
        return t == transform || t.IsChildOf(transform);
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
            if (canEnter)
            {
                ShowToiletEnterHint(true);
                Debug.Log("[Farthouse] " + toiletEnterLogMessage);
                HideCharacterForToilet();
            }

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

    void ShowToiletEnterHint(bool show)
    {
        if (toiletEnterMessageVisual == null) return;

        if (_enterHintRoutine != null)
        {
            StopCoroutine(_enterHintRoutine);
            _enterHintRoutine = null;
        }

        toiletEnterMessageVisual.SetActive(show);
        if (!show) return;

        float wait = Mathf.Max(0.1f, toiletEnterHintSeconds);
        _enterHintRoutine = StartCoroutine(HideToiletEnterHintAfter(wait));
    }

    System.Collections.IEnumerator HideToiletEnterHintAfter(float seconds)
    {
        yield return new WaitForSeconds(seconds);
        if (toiletEnterMessageVisual != null)
            toiletEnterMessageVisual.SetActive(false);
        _enterHintRoutine = null;
    }

    void HideCharacterForToilet()
    {
        GameObject root = characterRootToHide;
        if (root == null)
        {
            var tagged = GameObject.FindGameObjectWithTag("Player");
            if (tagged != null) root = tagged;
        }

        if (root != null)
            root.SetActive(false);
    }

    GameObject TryAutoFindToiletEnterMessageVisual()
    {
        foreach (var tr in GetComponentsInChildren<Transform>(true))
        {
            if (tr == null || tr.gameObject == gameObject) continue;
            string n = tr.gameObject.name.ToLowerInvariant();
            if (n.Contains("enter") || n.Contains("toilet_in") || n.Contains("inside") || n.Contains("success"))
                return tr.gameObject;
        }

        return null;
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
