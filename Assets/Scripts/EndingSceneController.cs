using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Put in ending_scene only. Shows one ending CG by key from <see cref="FartGameSession"/>,
/// Restart: assign <see cref="homeButton"/> in-scene, or <see cref="homeButtonPrefab"/>, or rely on runtime UI.
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

    [Header("Restart button (pick one)")]
    [Tooltip("Drag a Button from the scene hierarchy — position/size in the RectTransform as you like.")]
    [SerializeField] Button homeButton;

    [Tooltip("If Restart Button is empty, instantiate this prefab (root or child must have a Button).")]
    [SerializeField] GameObject homeButtonPrefab;

    [Tooltip("Parent for the prefab instance. If null, uses this object’s transform (prefer a Canvas under your UI).")]
    [SerializeField] Transform homeButtonParent;

    [Tooltip("Caption for scene/prefab/runtime button (TMP or legacy Text under the button).")]
    [SerializeField] string homeButtonLabel = "Restart";

    [Tooltip("If no Restart Button and no prefab: create the default yellow runtime button.")]
    [SerializeField] bool createHomeButtonIfMissing = true;

    void Awake()
    {
        SetupHomeButton();
    }

    void SetupHomeButton()
    {
        if (homeButton != null)
        {
            homeButton.onClick.RemoveListener(BackToHome);
            homeButton.onClick.AddListener(BackToHome);
            return;
        }

        if (homeButtonPrefab != null)
        {
            Transform parent = homeButtonParent != null ? homeButtonParent : transform;
            var instance = Instantiate(homeButtonPrefab, parent);
            instance.name = homeButtonPrefab.name;
            homeButton = instance.GetComponent<Button>();
            if (homeButton == null)
                homeButton = instance.GetComponentInChildren<Button>(true);
            if (homeButton != null)
            {
                homeButton.onClick.RemoveListener(BackToHome);
                homeButton.onClick.AddListener(BackToHome);
            }

            return;
        }

        if (createHomeButtonIfMissing)
            EnsureRuntimeHomeButton();
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
        ApplyHomeButtonCaption();

        if (homeButton != null && EventSystem.current != null)
            EventSystem.current.SetSelectedGameObject(homeButton.gameObject);
    }

    void ApplyHomeButtonCaption()
    {
        if (homeButton == null) return;

        var tmp = homeButton.GetComponentInChildren<TextMeshProUGUI>(true);
        if (tmp != null)
        {
            tmp.text = homeButtonLabel;
            tmp.fontSize = 52f;
            return;
        }

        var leg = homeButton.GetComponentInChildren<Text>(true);
        if (leg != null)
        {
            leg.text = homeButtonLabel;
            leg.fontSize = 52;
        }
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

    void EnsureRuntimeHomeButton()
    {
        if (EventSystem.current == null)
        {
            var es = new GameObject("EventSystem");
            es.AddComponent<EventSystem>();
            es.AddComponent<StandaloneInputModule>();
        }

        var canvasGo = new GameObject("EndingHomeUI");
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

        var btnGo = new GameObject("RestartButton", typeof(RectTransform));
        btnGo.transform.SetParent(canvasGo.transform, false);
        var btnRt = btnGo.GetComponent<RectTransform>();
        // Narrower band, shifted toward the right.
        btnRt.anchorMin = new Vector2(0.58f, 0.06f);
        btnRt.anchorMax = new Vector2(0.74f, 0.12f);
        btnRt.offsetMin = Vector2.zero;
        btnRt.offsetMax = Vector2.zero;
        btnRt.pivot = new Vector2(0.5f, 0.5f);

        var btnImg = btnGo.AddComponent<Image>();
        btnImg.sprite = UiWhiteSprite();
        var yellow = new Color(0.98f, 0.78f, 0.15f, 1f);
        btnImg.color = yellow;
        homeButton = btnGo.AddComponent<Button>();
        homeButton.targetGraphic = btnImg;
        var colors = homeButton.colors;
        colors.normalColor = yellow;
        colors.highlightedColor = new Color(1f, 0.9f, 0.35f, 1f);
        colors.pressedColor = new Color(0.85f, 0.65f, 0.1f, 1f);
        colors.disabledColor = new Color(0.6f, 0.55f, 0.45f, 0.55f);
        homeButton.colors = colors;
        var nav = homeButton.navigation;
        nav.mode = Navigation.Mode.None;
        homeButton.navigation = nav;
        homeButton.onClick.AddListener(BackToHome);

        var labelGo = new GameObject("Label", typeof(RectTransform));
        labelGo.transform.SetParent(btnGo.transform, false);
        var lblRt = labelGo.GetComponent<RectTransform>();
        lblRt.anchorMin = Vector2.zero;
        lblRt.anchorMax = Vector2.one;
        lblRt.offsetMin = Vector2.zero;
        lblRt.offsetMax = Vector2.zero;
        var txt = labelGo.AddComponent<Text>();
        var font = BuiltinUiFont();
        if (font != null) txt.font = font;
        txt.fontSize = 52;
        txt.fontStyle = FontStyle.Bold;
        txt.alignment = TextAnchor.MiddleCenter;
        txt.color = Color.white;
        txt.text = homeButtonLabel;
        txt.raycastTarget = false;
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
