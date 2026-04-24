using UnityEngine;

/// <summary>
/// Room-only NPC dialogue: left-click the blue NPC during prep to show a dialogue box.
/// Interaction style mirrors other room interactables (mouse overlap + auto collider).
/// </summary>
public class BlueNpcDialogueInteractable : MonoBehaviour
{
    [SerializeField] Camera targetCamera;
    [SerializeField] LayerMask interactLayers;
    [SerializeField] SpriteRenderer npcSpriteRenderer;

    [Header("Dialogue")]
    [SerializeField] GameObject dialogueVisual;
    [SerializeField] bool autoFindDialogueVisual = true;
    [SerializeField] float autoHideSeconds = 2.8f;

    Collider2D _collider;
    float _hideAt;

    void Awake()
    {
        if (npcSpriteRenderer == null) npcSpriteRenderer = GetComponent<SpriteRenderer>();
        _collider = GetComponent<Collider2D>();
        if (_collider == null)
        {
            var box = gameObject.AddComponent<BoxCollider2D>();
            if (npcSpriteRenderer != null && npcSpriteRenderer.sprite != null)
            {
                box.size = npcSpriteRenderer.sprite.bounds.size;
                box.offset = npcSpriteRenderer.sprite.bounds.center;
            }
            _collider = box;
        }

        if (interactLayers.value == 0)
            interactLayers = LayerMask.GetMask("RoomItems");
        if (targetCamera == null) targetCamera = Camera.main;
        if (dialogueVisual == null && autoFindDialogueVisual)
            dialogueVisual = TryAutoFindDialogueVisual();

        if (dialogueVisual == null)
            Debug.LogWarning("BlueNpcDialogueInteractable: dialogueVisual is not assigned/found. Assign your dialouge image GameObject.");
        SetDialogueVisible(false);
    }

    void Update()
    {
        var session = FartGameSession.Instance;
        if (session == null || !session.CanInteractDuringPrep) return;
        if (targetCamera == null) return;

        if (Input.GetMouseButtonDown(0))
        {
            Vector2 world = FartMouseUtility.ScreenToWorld2D(targetCamera, Input.mousePosition, transform.position.z);
            Collider2D hit = Physics2D.OverlapPoint(world, interactLayers);
            bool clickedSelf = IsHitOnThis(hit);
            if (!clickedSelf && _collider != null)
                clickedSelf = _collider.OverlapPoint(world);

            if (clickedSelf)
                ShowDialogueNow();
        }

        if (dialogueVisual != null && dialogueVisual.activeSelf)
        {
            if (Time.time >= _hideAt)
                SetDialogueVisible(false);
        }
    }

    bool IsHitOnThis(Collider2D hit)
    {
        if (hit == null) return false;
        if (hit == _collider) return true;
        var t = hit.transform;
        return t == transform || t.IsChildOf(transform);
    }

    void ShowDialogueNow()
    {
        if (dialogueVisual == null) return;
        SetDialogueVisible(true);
        _hideAt = Time.time + Mathf.Max(0.1f, autoHideSeconds);
    }

    void SetDialogueVisible(bool show)
    {
        if (dialogueVisual != null) dialogueVisual.SetActive(show);
    }

    GameObject TryAutoFindDialogueVisual()
    {
        foreach (var tr in GetComponentsInChildren<Transform>(true))
        {
            if (tr == null || tr.gameObject == gameObject) continue;
            string n = tr.gameObject.name.ToLowerInvariant();
            if (n.Contains("dialog") || n.Contains("dialouge") || n.Contains("bubble"))
                return tr.gameObject;
        }

        return null;
    }
}

