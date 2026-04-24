using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Put in ending_scene only. Shows one ending CG by key from <see cref="FartGameSession"/>.
/// Restart is keyboard-only: top-right prompt + Space.
/// </summary>
public class EndingSceneController : MonoBehaviour
{
    [System.Serializable]
    struct EndingCg
    {
        public string endingKey;
        public GameObject cgRoot;
    }

    [SerializeField] bool createSessionIfMissing = true;
    [SerializeField] EndingCg[] endingCgs;
    [SerializeField] string fallbackEndingKey = "EndingCleanQuiet";
    [SerializeField] string initialSceneName = "initial_scene";

    [Header("Restart prompt")]
    [SerializeField] string restartPromptText = "Space to restart";

    Text _runtimeRestartPrompt;

    void Awake()
    {
        EnsureRuntimeRestartPrompt();
    }

    void Start()
    {
        var session = FartGameSession.Instance;
        if (session == null && createSessionIfMissing)
        {
            var go = new GameObject("FartGameSession");
            session = go.AddComponent<FartGameSession>();
        }

        string key = fallbackEndingKey;
        if (session != null && !string.IsNullOrEmpty(session.FinalEndingKey))
            key = session.FinalEndingKey;

        ApplyEndingVisual(key);
    }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Space))
            BackToHome();
    }

    /// <summary>Restarts the run via <see cref="FartGameSession.StartNewGame"/> when session exists.</summary>
    public void BackToHome()
    {
        if (FartGameSession.Instance != null)
        {
            FartGameSession.Instance.StartNewGame();
            return;
        }

        SceneManager.LoadScene(initialSceneName);
    }

    /// <summary>Same as <see cref="BackToHome"/> (kept for older button wiring).</summary>
    public void BackToStart() => BackToHome();

    void ApplyEndingVisual(string key)
    {
        bool matched = false;
        if (endingCgs == null) return;

        for (int i = 0; i < endingCgs.Length; i++)
        {
            bool active = endingCgs[i].cgRoot != null && endingCgs[i].endingKey == key;
            if (endingCgs[i].cgRoot != null) endingCgs[i].cgRoot.SetActive(active);
            if (active) matched = true;
        }

        if (matched) return;

        for (int i = 0; i < endingCgs.Length; i++)
        {
            bool active = endingCgs[i].cgRoot != null && endingCgs[i].endingKey == fallbackEndingKey;
            if (endingCgs[i].cgRoot != null) endingCgs[i].cgRoot.SetActive(active);
        }
    }

    void EnsureRuntimeRestartPrompt()
    {
        if (EventSystem.current == null)
        {
            var es = new GameObject("EventSystem");
            es.AddComponent<EventSystem>();
            es.AddComponent<StandaloneInputModule>();
        }

        var canvasGo = new GameObject("EndingPromptUI");
        canvasGo.transform.SetParent(null, false);
        var canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 32000;
        canvas.overrideSorting = true;
        var scaler = canvasGo.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;
        canvasGo.AddComponent<GraphicRaycaster>();

        var labelGo = new GameObject("RestartPrompt", typeof(RectTransform));
        labelGo.transform.SetParent(canvasGo.transform, false);
        var lblRt = labelGo.GetComponent<RectTransform>();
        lblRt.anchorMin = new Vector2(1f, 1f);
        lblRt.anchorMax = new Vector2(1f, 1f);
        lblRt.pivot = new Vector2(1f, 1f);
        lblRt.anchoredPosition = new Vector2(-24f, -24f);
        lblRt.sizeDelta = new Vector2(520f, 90f);

        var txt = labelGo.AddComponent<Text>();
        var font = BuiltinUiFont();
        if (font != null) txt.font = font;
        txt.fontSize = 44;
        txt.fontStyle = FontStyle.Bold;
        txt.alignment = TextAnchor.UpperRight;
        txt.color = new Color(0.78f, 0.18f, 0.42f, 1f);
        txt.text = restartPromptText;
        txt.raycastTarget = false;
        _runtimeRestartPrompt = txt;
    }

    static Sprite _uiSprite;

    static Sprite UiWhiteSprite()
    {
        if (_uiSprite != null) return _uiSprite;
        var tex = Texture2D.whiteTexture;
        _uiSprite = Sprite.Create(tex, new Rect(0f, 0f, tex.width, tex.height), new Vector2(0.5f, 0.5f), 100f);
        return _uiSprite;
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
