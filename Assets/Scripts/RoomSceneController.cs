using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Put in <b>room_scene</b> only. Prep countdown uses <see cref="FartGameSession.GetRoomPrepRemainingSeconds"/> (wall-clock).
/// Left mouse in this scene is only for <see cref="ToggleInteractable"/> / <see cref="HideBehindProp"/> item hits — this script does not read mouse input.
/// </summary>
public class RoomSceneController : MonoBehaviour
{
    [SerializeField] bool createSessionIfMissing = true;
    [Tooltip("If null, a small overlay is created at runtime showing countdown until fart.")]
    [SerializeField] Text countdownLabel;
    [SerializeField] Text roundLabel;
    [SerializeField] Transform playerTransform;
    [SerializeField] FartGameSession.FartLocation defaultLocation = FartGameSession.FartLocation.None;

    [System.Serializable]
    struct LocationAnchor
    {
        public FartGameSession.FartLocation location;
        public Transform point;
        public float radius;
    }

    [SerializeField] LocationAnchor[] locationAnchors;

    bool _ended;
    Text _runtimeCountdown;
    Text _runtimeRound;

    void Awake()
    {
        if (countdownLabel == null)
            EnsureRoomCountdownOverlay();
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
        session.SetCurrentRoundFartLocation(ResolveCurrentLocation());
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

    void RefreshRoomHud(FartGameSession session)
    {
        int sec = Mathf.Max(0, Mathf.CeilToInt(session.GetRoomPrepRemainingSeconds()));
        string line = $"Fart in {sec}s";
        if (countdownLabel != null) countdownLabel.text = line;
        if (_runtimeCountdown != null) _runtimeCountdown.text = line;

        string roundLine = $"Round {session.CurrentRound}/{session.TotalRounds}";
        if (roundLabel != null) roundLabel.text = roundLine;
        if (_runtimeRound != null) _runtimeRound.text = roundLine;
    }

    void EnsureRoomCountdownOverlay()
    {
        if (EventSystem.current == null)
        {
            var es = new GameObject("EventSystem");
            es.AddComponent<EventSystem>();
            es.AddComponent<StandaloneInputModule>();
        }

        var canvasGo = new GameObject("RoomPrepCountdownUI");
        canvasGo.transform.SetParent(null, false);
        var canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 30000;
        canvas.overrideSorting = true;
        var scaler = canvasGo.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;
        canvasGo.AddComponent<GraphicRaycaster>();

        _runtimeCountdown = CreateHudLine(canvasGo.transform, "Countdown", 52,
            new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -72f),
            new Vector2(1400f, 80f));
        _runtimeRound = CreateHudLine(canvasGo.transform, "RoundLine", 34,
            new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -140f),
            new Vector2(1200f, 56f));
    }

    static Text CreateHudLine(Transform parent, string name, int fontSize,
        Vector2 anchorMin, Vector2 anchorMax, Vector2 anchoredPos, Vector2 sizeDelta)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = anchorMin;
        rt.anchorMax = anchorMax;
        rt.pivot = new Vector2(0.5f, 1f);
        rt.anchoredPosition = anchoredPos;
        rt.sizeDelta = sizeDelta;

        var text = go.AddComponent<Text>();
        var font = BuiltinUiFont();
        if (font != null) text.font = font;
        text.fontSize = fontSize;
        text.fontStyle = FontStyle.Bold;
        text.alignment = TextAnchor.MiddleCenter;
        text.color = Color.white;
        text.horizontalOverflow = HorizontalWrapMode.Wrap;
        text.verticalOverflow = VerticalWrapMode.Overflow;
        var outline = go.AddComponent<Outline>();
        outline.effectColor = new Color(0f, 0f, 0f, 0.9f);
        outline.effectDistance = new Vector2(2f, -2f);
        return text;
    }

    static Font BuiltinUiFont()
    {
        foreach (var path in new[] { "LegacyRuntime.ttf", "Arial.ttf" })
        {
            var f = Resources.GetBuiltinResource<Font>(path);
            if (f != null) return f;
        }

        foreach (var name in new[] { "Arial", "Helvetica", "PingFang SC", "Heiti SC", "Microsoft YaHei", "SimHei" })
        {
            try
            {
                var f = Font.CreateDynamicFontFromOSFont(name, 64);
                if (f != null) return f;
            }
            catch
            {
                // try next
            }
        }

        return null;
    }
}
