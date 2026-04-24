using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Put in <b>room_scene</b> only. Prep countdown uses <see cref="FartGameSession.GetRoomPrepRemainingSeconds"/> (wall-clock).
/// Left mouse in this scene is only for <see cref="ToggleInteractable"/> / <see cref="HideBehindProp"/> / <see cref="PlantOpenProximityHide"/> item hits — this script does not read mouse input.
/// </summary>
public class RoomSceneController : MonoBehaviour
{
    [SerializeField] bool createSessionIfMissing = true;
    [Tooltip("Legacy scene label. When runtime overlay is enabled, this is ignored.")]
    [SerializeField] TextMeshProUGUI countdownLabel;
    [SerializeField] TextMeshProUGUI roundLabel;
    [SerializeField] Transform playerTransform;
    [SerializeField] FartGameSession.FartLocation defaultLocation = FartGameSession.FartLocation.None;
    [Header("Countdown UI")]
    [Tooltip("Always generate a top-center runtime countdown so build/editor positions stay consistent.")]
    [SerializeField] bool useRuntimeTopCenterCountdown = true;

    [System.Serializable]
    struct LocationAnchor
    {
        public FartGameSession.FartLocation location;
        public Transform point;
        public float radius;
    }

    [SerializeField] LocationAnchor[] locationAnchors;

    [Header("Ending state (optional)")]
    [Tooltip("Usually the window’s ToggleInteractable. Snapshotted when prep ends for that round’s ending record.")]
    [SerializeField] ToggleInteractable windowForEndingSnapshot;

    bool _ended;
    TextMeshProUGUI _runtimeFartInPrefix;
    TextMeshProUGUI _runtimeSecondsDigits;
    TextMeshProUGUI _runtimeSecondsSuffix;
    const float CountdownFartInFontSize = 38f;
    const float CountdownSecondsBaseFontSize = 38f;
    const float CountdownSecondsRampStep = 12f;
    static readonly Color MutedRed = new Color(0.82f, 0.36f, 0.36f, 1f);
    /// <summary>Non-breaking spaces so "Fart in" never wraps across lines in a narrow RectTransform.</summary>
    const string CountdownPrefixNoBreak = "Fart\u00A0in\u00A0";

    void Awake()
    {
        if (useRuntimeTopCenterCountdown)
        {
            EnsureRoomCountdownOverlay();
            countdownLabel = null;
        }
        else if (countdownLabel == null)
        {
            EnsureRoomCountdownOverlay();
        }
    }

    void Start()
    {
        var session = FartGameSession.Instance;
        if (session == null && createSessionIfMissing)
        {
            var go = new GameObject("FartGameSession");
            session = go.AddComponent<FartGameSession>();
        }

        if (session == null) return;

        session.CanInteractDuringPrep = true;
        session.SetCurrentRoundFartLocation(defaultLocation);
        session.NotifyRoomSceneEntered();
        float remaining = session.GetRoomPrepRemainingSeconds();
        session.SetRoomPrepHud(remaining);

        if (playerTransform == null)
        {
            var tagged = GameObject.FindGameObjectWithTag("Player");
            if (tagged != null) playerTransform = tagged.transform;
        }

        EnsurePlayerVisible();
        PrepareCountdownStyle();
    }

    void Update()
    {
        var session = FartGameSession.Instance;
        if (session == null) return;

        float remaining = session.GetRoomPrepRemainingSeconds();
        session.SetRoomPrepHud(remaining);
        RefreshRoomHud(session);

        if (remaining > 0f || _ended) return;

        _ended = true;
        foreach (var plant in Object.FindObjectsByType<PlantOpenProximityHide>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
            plant.RefreshSmellEligibilityNow();
        session.SnapshotOpenPlantProximitySmellFromPrepEnd();

        if (session.CurrentRoundInToilet)
            session.SetCurrentRoundFartLocation(FartGameSession.FartLocation.RestroomSafe);
        else
            session.SetCurrentRoundFartLocation(ResolveCurrentLocation());
        if (windowForEndingSnapshot != null)
            session.SetCurrentRoundWindowOpen(windowForEndingSnapshot.IsOpen);
        if (!session.CurrentRoundInToilet)
            EnsurePlayerVisible();
        session.CanInteractDuringPrep = false;
        session.NotifyRoomPrepEnded();
        enabled = false;
    }

    FartGameSession.FartLocation ResolveCurrentLocation()
    {
        if (playerTransform == null || locationAnchors == null || locationAnchors.Length == 0)
            return defaultLocation;

        Vector2 p = playerTransform.position;
        float bestDist = float.MaxValue;
        FartGameSession.FartLocation best = defaultLocation;

        for (int i = 0; i < locationAnchors.Length; i++)
        {
            var anchor = locationAnchors[i];
            if (anchor.point == null || anchor.radius <= 0f) continue;
            float d = Vector2.Distance(p, anchor.point.position);
            if (d > anchor.radius || d >= bestDist) continue;
            bestDist = d;
            best = anchor.location;
        }

        return best;
    }

    void EnsurePlayerVisible()
    {
        Transform t = playerTransform;
        if (t == null)
        {
            var go = GameObject.FindGameObjectWithTag("Player");
            if (go != null) t = go.transform;
        }

        if (t != null)
            t.gameObject.SetActive(true);
    }

    void RefreshRoomHud(FartGameSession session)
    {
        float remaining = session.GetRoomPrepRemainingSeconds();
        int sec = Mathf.Max(0, Mathf.CeilToInt(remaining));
        if (roundLabel != null) roundLabel.text = string.Empty;

        if (_runtimeFartInPrefix != null && _runtimeSecondsDigits != null && _runtimeSecondsSuffix != null)
        {
            _runtimeFartInPrefix.text = CountdownPrefixNoBreak;
            _runtimeSecondsDigits.text = sec.ToString();
            _runtimeSecondsSuffix.text = "s";
            _runtimeFartInPrefix.fontSize = CountdownFartInFontSize;
            _runtimeSecondsSuffix.fontSize = CountdownFartInFontSize;
            float numSize = CountdownSecondsBaseFontSize;
            if (sec <= 3 && sec > 0)
                numSize = CountdownSecondsBaseFontSize + (4 - sec) * CountdownSecondsRampStep;
            _runtimeSecondsDigits.fontSize = numSize;
        }
        else if (countdownLabel != null)
        {
            countdownLabel.text = $"{CountdownPrefixNoBreak}{sec}s";
            countdownLabel.fontSize = CountdownFartInFontSize;
        }
    }

    void EnsureRoomCountdownOverlay()
    {
        if (EventSystem.current == null)
        {
            var es = new GameObject("EventSystem");
            es.AddComponent<EventSystem>();
            es.AddComponent<StandaloneInputModule>();
        }

        for (int i = transform.childCount - 1; i >= 0; i--)
        {
            var c = transform.GetChild(i);
            if (c.name == "RoomPrepCountdownUI")
                Destroy(c.gameObject);
        }

        var orphan = GameObject.Find("RoomPrepCountdownUI");
        if (orphan != null && orphan.transform.parent == null)
            Destroy(orphan);

        var canvasGo = new GameObject("RoomPrepCountdownUI");
        canvasGo.transform.SetParent(transform, false);
        var canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 30000;
        canvas.overrideSorting = true;
        var scaler = canvasGo.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;
        canvasGo.AddComponent<GraphicRaycaster>();

        var rowGo = new GameObject("CountdownRow", typeof(RectTransform));
        rowGo.transform.SetParent(canvasGo.transform, false);
        var rowRt = rowGo.GetComponent<RectTransform>();
        rowRt.anchorMin = new Vector2(0.5f, 1f);
        rowRt.anchorMax = new Vector2(0.5f, 1f);
        rowRt.pivot = new Vector2(0.5f, 1f);
        rowRt.anchoredPosition = new Vector2(0f, -10f);
        rowRt.sizeDelta = new Vector2(900f, 90f);
        var hlg = rowGo.AddComponent<HorizontalLayoutGroup>();
        hlg.childAlignment = TextAnchor.MiddleCenter;
        hlg.spacing = 6f;
        hlg.childControlWidth = false;
        hlg.childControlHeight = false;
        hlg.childForceExpandWidth = false;
        hlg.childForceExpandHeight = false;

        _runtimeFartInPrefix = CreateHudTmpInRow(rowGo.transform, "FartInPrefix", CountdownPrefixNoBreak, CountdownFartInFontSize, 150f);
        _runtimeSecondsDigits = CreateHudTmpInRow(rowGo.transform, "SecondsDigits", "0", CountdownSecondsBaseFontSize, 96f);
        _runtimeSecondsSuffix = CreateHudTmpInRow(rowGo.transform, "SecondsSuffix", "s", CountdownFartInFontSize, 22f);

        Canvas.ForceUpdateCanvases();
        LayoutRebuilder.ForceRebuildLayoutImmediate(rowRt);
    }

    static TextMeshProUGUI CreateHudTmpInRow(Transform rowParent, string name, string initial, float fontSize, float layoutMinWidth = 0f)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(rowParent, false);
        var rt = go.GetComponent<RectTransform>();
        rt.sizeDelta = new Vector2(0f, 72f);

        var tmp = go.AddComponent<TextMeshProUGUI>();
        ApplyDefaultTmpFont(tmp);
        tmp.fontSize = fontSize;
        tmp.fontStyle = FontStyles.Bold;
        tmp.text = initial;
        tmp.color = Color.white;
        tmp.raycastTarget = false;
        tmp.enableWordWrapping = false;
        tmp.overflowMode = TextOverflowModes.Overflow;
#if UNITY_2023_1_OR_NEWER
        tmp.textWrappingMode = TextWrappingModes.NoWrap;
#endif
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.margin = Vector4.zero;
        tmp.outlineWidth = 0f;

        var le = go.AddComponent<LayoutElement>();
        le.minHeight = 56f;
        le.preferredHeight = 72f;
        le.flexibleWidth = 0f;
        if (layoutMinWidth > 0f)
            le.minWidth = layoutMinWidth;
        var csf = go.AddComponent<ContentSizeFitter>();
        csf.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
        csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        return tmp;
    }

    static void ApplyDefaultTmpFont(TextMeshProUGUI tmp)
    {
        if (TMP_Settings.defaultFontAsset != null)
        {
            tmp.font = TMP_Settings.defaultFontAsset;
            return;
        }

        var fallback = Resources.Load<TMP_FontAsset>("Fonts & Materials/LiberationSans SDF");
        if (fallback != null)
            tmp.font = fallback;
    }

    void PrepareCountdownStyle()
    {
        ApplyHudTmpStyle(countdownLabel);
        ApplyHudTmpStyle(_runtimeFartInPrefix);
        ApplyHudTmpStyle(_runtimeSecondsDigits);
        ApplyHudTmpStyle(_runtimeSecondsSuffix);
        if (roundLabel != null)
            roundLabel.text = string.Empty;
    }

    static void ApplyHudTmpStyle(TextMeshProUGUI label)
    {
        if (label == null) return;
        label.color = MutedRed;
        label.alignment = TextAlignmentOptions.Center;
        label.outlineWidth = 0f;
        label.enableWordWrapping = false;
        label.overflowMode = TextOverflowModes.Overflow;
#if UNITY_2023_1_OR_NEWER
        label.textWrappingMode = TextWrappingModes.NoWrap;
#endif
    }
}
