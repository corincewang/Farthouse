using UnityEngine;

/// <summary>
/// Put on the <b>plant</b> root (same object as <see cref="ToggleInteractable"/> with open/close visuals).
/// When the plant is <see cref="ToggleInteractable.IsOpen"/> and the player is within <see cref="proximityRadius"/>,
/// hides the character until they leave the radius or the plant closes. Smell reduction for the fart uses a
/// prep-end snapshot via <see cref="FartGameSession.SnapshotOpenPlantProximitySmellFromPrepEnd"/>.
/// </summary>
public class PlantOpenProximityHide : MonoBehaviour
{
    [SerializeField] ToggleInteractable plantToggle;
    [Tooltip("Distance from measureFrom to player (2D) for hide + smell eligibility.")]
    [SerializeField] float proximityRadius = 2.2f;
    [Tooltip("Defaults to this transform. Use a child empty for the bush “center” if needed.")]
    [SerializeField] Transform measureFrom;
    [SerializeField] Transform playerTransform;
    [Tooltip("If null, uses Player-tagged object.")]
    [SerializeField] GameObject characterRootToHide;

    void Awake()
    {
        if (plantToggle == null)
            plantToggle = GetComponent<ToggleInteractable>();
        if (measureFrom == null)
            measureFrom = transform;
    }

    void Update()
    {
        var session = FartGameSession.Instance;
        if (session == null || !session.CanInteractDuringPrep)
            return;
        RefreshCore();
    }

    /// <summary>Called by <see cref="RoomSceneController"/> on the prep-end frame so smell snapshot matches geometry.</summary>
    public void RefreshSmellEligibilityNow()
    {
        RefreshCore();
    }

    void RefreshCore()
    {
        var session = FartGameSession.Instance;
        if (session == null)
            return;

        if (session.CurrentRoundInToilet)
        {
            session.SetLiveOpenPlantProximitySmell(false);
            return;
        }

        bool plantOpen = plantToggle != null && plantToggle.IsOpen;
        Transform player = playerTransform;
        if (player == null)
        {
            var tagged = GameObject.FindGameObjectWithTag("Player");
            if (tagged != null)
                player = tagged.transform;
        }

        bool inRange = player != null &&
                       Vector2.Distance((Vector2)measureFrom.position, (Vector2)player.position) <= proximityRadius;
        bool hide = plantOpen && inRange;
        session.SetLiveOpenPlantProximitySmell(hide);

        GameObject root = characterRootToHide;
        if (root == null && player != null)
            root = player.gameObject;
        if (root != null)
            root.SetActive(!hide);
    }
}
