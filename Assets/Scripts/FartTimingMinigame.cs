using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Serialization;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/// <summary>
/// Fart meter UI: <b>SPACE or left-click</b> locks the fart. After result, use the on-screen <b>Return to room</b> button only (no Space/click to advance).
/// </summary>
[DisallowMultipleComponent]
public class FartTimingMinigame : MonoBehaviour
{
    [Header("Oscillation")]
    [SerializeField] float baseSpeed = 5f;
    [SerializeField] [Range(0f, 0.95f)] float speedWobbleAmount = 0.45f;
    [SerializeField] float speedWobbleFrequency = 0.4f;

    [Header("Layout")]
    [SerializeField] float barWidth = 76f;
    [SerializeField] float barHeight = 680f;
    [SerializeField] float meterInsetFromRight = 120f;
    [SerializeField] float iconBarGap = 16f;
    [SerializeField] float iconColumnWidth = 112f;
    [SerializeField] float animalIconSize = 96f;
    [Tooltip("Off = only the bar (use scene mouse/lion sprites). On = duplicate icons on the Canvas next to the bar.")]
    [SerializeField] bool buildAnimalIconsNextToBar;

    [Header("Left panel (optional UI duplicate)")]
    [SerializeField] bool useBuiltInButtPanel;
    [SerializeField] Sprite buttSprite;
    [SerializeField] [Range(0.2f, 0.55f)] float buttPanelWidthFraction = 0.44f;

    [Header("Animals beside bar (only if Build Animal Icons is on)")]
    [SerializeField] Sprite quietAnimalSprite;
    [SerializeField] Sprite loudAnimalSprite;

    [Header("Result text (binary vs meter)")]
    [Tooltip("Top 40% is quiet (0.0-0.4). Anything below that is loud.")]
    [SerializeField] [Range(0f, 1f)] float resultLoudQuietSplit = 0.4f;
    [SerializeField] string quietMessage = "Made a quiet fart.";
    [SerializeField] string loudMessage = "Made a loud fart.";
    [SerializeField] string returnToRoomButtonLabel = "Return to room";
    [SerializeField] string endRunButtonLabel = "Continue to ending";

    [Header("Aim phase prompt")]
    [FormerlySerializedAs("pressSpaceToFartMessage")]
    [SerializeField] string aimClickPromptMessage = "Press SPACE or click to fart.";

    [Header("Fart phase timer")]
    [Tooltip("If off, the meter never times out — you only commit with SPACE or left-click.")]
    [SerializeField] bool commitWhenFartPhaseTimesOut = false;

    [Header("Audio (after commit)")]
    [Tooltip("Played when meter is below resultLoudQuietSplit.")]
    [SerializeField] AudioClip quietFartClip;
    [Tooltip("Played when meter is at or above resultLoudQuietSplit.")]
    [SerializeField] AudioClip loudFartClip;
    [Tooltip("If a clip is longer, playback stops after this many seconds.")]
    [SerializeField] float fartSoundMaxSeconds = 3f;

    [Tooltip("After Space/click commits, wait this long (realtime) before playing the fart clip.")]
    [SerializeField] float fartSoundDelayAfterCommitSeconds = 0.5f;
    [SerializeField] float quietVolume = 0.35f;
    [SerializeField] float loudVolume = 1f;

    [Header("Cursor look")]
    [SerializeField] Color cursorColor = new Color(0.62f, 0.55f, 0.06f, 1f);

    public float CommittedLoudness01 { get; private set; }
    public float CurrentLoudness01 => _cursor01;

    FartGameSession _session;
    float _phaseRemaining;
    bool _committed;
    float _cursor01;
    float _oscillatorPhase;
    RectTransform _cursorRt;
    float _barHalfHeight;
    AudioSource _audio;
    Text _resultText;
    GameObject _resultBannerRoot;
    string _guiResultBackup;
    GameObject _aimPromptRoot;
    Text _aimPromptText;
    GameObject _returnButtonRoot;
    Button _returnButton;
    Text _returnButtonCaption;
    bool _leftFartScene;
    Coroutine _armReturnButtonCoroutine;
    Coroutine _stopFartAudioCoroutine;
    Coroutine _delayedFartSoundCoroutine;

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

    static bool SpaceOrPrimaryClickDown()
    {
        if (Input.GetKeyDown(KeyCode.Space) || Input.GetMouseButtonDown(0))
            return true;
#if ENABLE_INPUT_SYSTEM
        if (Keyboard.current != null && Keyboard.current.spaceKey.wasPressedThisFrame)
            return true;
        if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
            return true;
#endif
        return false;
    }

    static bool SpaceOrPrimaryHeld()
    {
        if (Input.GetKey(KeyCode.Space) || Input.GetMouseButton(0))
            return true;
#if ENABLE_INPUT_SYSTEM
        if (Keyboard.current != null && Keyboard.current.spaceKey.isPressed)
            return true;
        if (Mouse.current != null && Mouse.current.leftButton.isPressed)
            return true;
#endif
        return false;
    }

    void Awake()
    {
        EnsureUi();
    }

    void Start()
    {
        if (_session == null && FartGameSession.Instance != null)
            BeginPhase(FartGameSession.Instance);
    }

    public void BeginPhase(FartGameSession session)
    {
        if (_armReturnButtonCoroutine != null)
        {
            StopCoroutine(_armReturnButtonCoroutine);
            _armReturnButtonCoroutine = null;
        }

        if (_stopFartAudioCoroutine != null)
        {
            StopCoroutine(_stopFartAudioCoroutine);
            _stopFartAudioCoroutine = null;
        }

        if (_delayedFartSoundCoroutine != null)
        {
            StopCoroutine(_delayedFartSoundCoroutine);
            _delayedFartSoundCoroutine = null;
        }

        if (_audio != null)
            _audio.Stop();

        _session = session;
        if (_session == null) return;

        _phaseRemaining = _session.FartPhaseDurationSeconds;
        _committed = false;
        _oscillatorPhase = 0f;
        _guiResultBackup = null;
        _leftFartScene = false;
        if (_resultBannerRoot != null)
            _resultBannerRoot.SetActive(false);
        if (_resultText != null)
            _resultText.text = string.Empty;
        if (_returnButtonRoot != null)
        {
            _returnButtonRoot.SetActive(false);
            if (_returnButton != null)
                _returnButton.interactable = true;
            RefreshReturnButtonCaption();
        }

        if (_aimPromptRoot != null)
            _aimPromptRoot.SetActive(true);

        _session.NotifyFartSceneEntered();
        _session.SetFartHud(_phaseRemaining);
        _audio = GetComponent<AudioSource>();
        if (_audio == null) _audio = gameObject.AddComponent<AudioSource>();
        _audio.playOnAwake = false;
    }

    void Update()
    {
        if (_committed || _leftFartScene)
            return;

        float dt = Time.deltaTime;
        float wobble = 1f + speedWobbleAmount * Mathf.Sin(Time.time * speedWobbleFrequency * Mathf.PI * 2f);
        wobble = Mathf.Max(0.15f, wobble);
        _oscillatorPhase += dt * baseSpeed * wobble;
        _cursor01 = Mathf.PingPong(_oscillatorPhase, 1f);

        if (_cursorRt != null)
        {
            float y = Mathf.Lerp(_barHalfHeight, -_barHalfHeight, _cursor01);
            _cursorRt.anchoredPosition = new Vector2(0f, y);
        }

        bool timedOut = false;
        if (_session != null)
        {
            if (commitWhenFartPhaseTimesOut)
            {
                _phaseRemaining -= dt;
                _session.SetFartHud(Mathf.Max(0f, _phaseRemaining));
                timedOut = _phaseRemaining <= 0f;
            }
            else
                _session.SetFartHud(_phaseRemaining);
        }

        if (SpaceOrPrimaryClickDown() || timedOut)
        {
            if (_session != null)
                Commit();
        }
    }

    /// <summary>Wire this to a UI Button if you build your own result UI instead of the runtime banner.</summary>
    public void OnReturnToRoomClicked()
    {
        if (!_committed || _session == null || _leftFartScene)
            return;
        if (_returnButton != null && !_returnButton.interactable)
            return;

        if (_armReturnButtonCoroutine != null)
        {
            StopCoroutine(_armReturnButtonCoroutine);
            _armReturnButtonCoroutine = null;
        }

        _leftFartScene = true;
        if (_returnButton != null)
            _returnButton.interactable = false;
        enabled = false;
        _session.AcknowledgeFartResultAndContinue();
    }

    void Commit()
    {
        if (_committed) return;
        _committed = true;
        float smellReduction = _session != null ? _session.GetCurrentRoundSmellReduction01() : 0f;
        CommittedLoudness01 = Mathf.Clamp01(_cursor01 - smellReduction);
        if (_session != null)
            _session.SetLastFartLoudness(CommittedLoudness01);
        if (_aimPromptRoot != null)
            _aimPromptRoot.SetActive(false);

        if (_delayedFartSoundCoroutine != null)
            StopCoroutine(_delayedFartSoundCoroutine);
        _delayedFartSoundCoroutine = StartCoroutine(PlayCommittedFartSoundAfterDelay());

        ShowResultAfterCommit(CommittedLoudness01);
        RefreshReturnButtonCaption();
        if (_returnButtonRoot != null)
        {
            if (EventSystem.current != null)
                EventSystem.current.SetSelectedGameObject(null);

            _returnButtonRoot.SetActive(true);
            if (_returnButton != null)
            {
                _returnButton.interactable = false;
                if (_armReturnButtonCoroutine != null)
                    StopCoroutine(_armReturnButtonCoroutine);
                _armReturnButtonCoroutine = StartCoroutine(ArmReturnButtonAfterCommitInputReleased());
            }
        }
    }

    IEnumerator PlayCommittedFartSoundAfterDelay()
    {
        float wait = Mathf.Max(0f, fartSoundDelayAfterCommitSeconds);
        if (wait > 0f)
            yield return new WaitForSecondsRealtime(wait);
        _delayedFartSoundCoroutine = null;
        PlayCommittedFartSound();
    }

    void PlayCommittedFartSound()
    {
        if (_audio == null)
            return;

        bool loud = CommittedLoudness01 >= resultLoudQuietSplit;
        AudioClip clip = loud ? loudFartClip : quietFartClip;
        if (clip == null)
            return;

        float vol = loud ? loudVolume : quietVolume;
        _audio.Stop();
        _audio.clip = clip;
        _audio.volume = vol;
        _audio.time = 0f;
        _audio.Play();

        float cap = Mathf.Min(Mathf.Max(0.01f, fartSoundMaxSeconds), clip.length);
        if (_stopFartAudioCoroutine != null)
            StopCoroutine(_stopFartAudioCoroutine);
        _stopFartAudioCoroutine = StartCoroutine(StopFartAudioAfter(cap));
    }

    IEnumerator StopFartAudioAfter(float seconds)
    {
        yield return new WaitForSecondsRealtime(seconds);
        _stopFartAudioCoroutine = null;
        if (_audio != null && _audio.isPlaying)
            _audio.Stop();
    }

    IEnumerator ArmReturnButtonAfterCommitInputReleased()
    {
        // Same SPACE / click that committed must not immediately Submit / click this Button.
        yield return null;

        float waitUntil = Time.unscaledTime + 3f;
        while (Time.unscaledTime < waitUntil && SpaceOrPrimaryHeld())
            yield return null;

        yield return new WaitForSecondsRealtime(0.12f);

        _armReturnButtonCoroutine = null;
        if (_leftFartScene || _returnButton == null)
            yield break;

        _returnButton.interactable = true;
        if (EventSystem.current != null)
            EventSystem.current.SetSelectedGameObject(_returnButton.gameObject);
    }

    void RefreshReturnButtonCaption()
    {
        if (_returnButtonCaption == null || _session == null) return;
        _returnButtonCaption.text = endRunButtonLabel;
    }

    void ShowResultAfterCommit(float loudness01)
    {
        if (_resultText == null) return;

        string msg = loudness01 >= resultLoudQuietSplit ? loudMessage : quietMessage;

        _resultText.text = msg;
        if (_resultBannerRoot != null)
            _resultBannerRoot.SetActive(true);
        _guiResultBackup = _resultText.font == null ? msg : null;
    }

    void OnGUI()
    {
        if (string.IsNullOrEmpty(_guiResultBackup)) return;
        var style = new GUIStyle(GUI.skin.label)
        {
            fontSize = 34,
            alignment = TextAnchor.MiddleCenter,
            wordWrap = true,
            normal = { textColor = Color.white }
        };
        float h = Mathf.Min(220f, Screen.height * 0.28f);
        GUI.Box(new Rect(16f, Screen.height - h - 24f, Screen.width - 32f, h), GUIContent.none);
        GUI.Label(new Rect(32f, Screen.height - h - 16f, Screen.width - 64f, h - 8f), _guiResultBackup, style);
    }

    bool _uiBuilt;

    void EnsureUi()
    {
        if (_uiBuilt) return;
        _uiBuilt = true;

        if (EventSystem.current == null)
        {
            var es = new GameObject("EventSystem");
            es.AddComponent<EventSystem>();
            es.AddComponent<StandaloneInputModule>();
        }

        var canvasGo = new GameObject("FartTimingUI");
        canvasGo.transform.SetParent(null, false);
        var canvas = canvasGo.AddComponent<Canvas>();
        // Overlay always draws on top of world-space 2D; Screen Space Camera often ends up behind sprites.
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 32767;
        canvas.overrideSorting = true;
        canvas.pixelPerfect = false;
        var scaler = canvasGo.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;
        canvasGo.AddComponent<GraphicRaycaster>();

        BuildAimPrompt(canvasGo.transform);
        if (useBuiltInButtPanel)
            BuildButtPanel(canvasGo.transform);
        BuildMeterBlock(canvasGo.transform);
        BuildResultBanner(canvasGo.transform);
    }

    void BuildAimPrompt(Transform canvas)
    {
        var root = new GameObject("AimPrompt", typeof(RectTransform));
        root.transform.SetParent(canvas, false);
        _aimPromptRoot = root;
        var rt = root.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.08f, 0.88f);
        rt.anchorMax = new Vector2(0.92f, 0.98f);
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
        rt.pivot = new Vector2(0.5f, 0.5f);

        _aimPromptText = root.AddComponent<Text>();
        var font = BuiltinUiFont();
        if (font != null)
            _aimPromptText.font = font;
        _aimPromptText.fontSize = 44;
        _aimPromptText.fontStyle = FontStyle.Bold;
        _aimPromptText.alignment = TextAnchor.MiddleCenter;
        _aimPromptText.color = Color.white;
        _aimPromptText.text = aimClickPromptMessage;
        _aimPromptText.horizontalOverflow = HorizontalWrapMode.Wrap;
        _aimPromptText.verticalOverflow = VerticalWrapMode.Overflow;
        var outline = root.AddComponent<Outline>();
        outline.effectColor = new Color(0f, 0f, 0f, 0.88f);
        outline.effectDistance = new Vector2(2f, -2f);
    }

    void BuildButtPanel(Transform canvas)
    {
        if (buttSprite == null) return;

        var go = new GameObject("ButtPanel", typeof(RectTransform));
        go.transform.SetParent(canvas, false);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0f, 0.08f);
        rt.anchorMax = new Vector2(buttPanelWidthFraction, 0.92f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;

        var img = go.AddComponent<Image>();
        img.sprite = buttSprite;
        img.type = Image.Type.Simple;
        img.preserveAspect = true;
        img.color = Color.white;
    }

    void BuildMeterBlock(Transform canvas)
    {
        float iconCol = buildAnimalIconsNextToBar ? iconColumnWidth : 0f;
        float gap = buildAnimalIconsNextToBar ? iconBarGap : 0f;
        float clusterWidth = iconCol + gap + barWidth;

        float cursorHalf = 10f;
        _barHalfHeight = barHeight * 0.5f - cursorHalf;

        var root = new GameObject("MeterRoot", typeof(RectTransform));
        root.transform.SetParent(canvas, false);
        var rootRt = root.GetComponent<RectTransform>();
        rootRt.anchorMin = rootRt.anchorMax = new Vector2(1f, 0.5f);
        rootRt.pivot = new Vector2(1f, 0.5f);
        rootRt.sizeDelta = new Vector2(clusterWidth, barHeight);
        rootRt.anchoredPosition = new Vector2(-meterInsetFromRight, 0f);

        var barGo = new GameObject("Bar", typeof(RectTransform));
        barGo.transform.SetParent(root.transform, false);
        var barRt = barGo.GetComponent<RectTransform>();
        barRt.anchorMin = barRt.anchorMax = new Vector2(1f, 0.5f);
        barRt.pivot = new Vector2(1f, 0.5f);
        barRt.sizeDelta = new Vector2(barWidth, barHeight);
        barRt.anchoredPosition = Vector2.zero;

        var barImg = barGo.AddComponent<Image>();
        barImg.sprite = UiWhiteSprite();
        barImg.type = Image.Type.Simple;
        barImg.color = Color.white;
        barImg.raycastTarget = false;

        var cursorGo = new GameObject("Cursor", typeof(RectTransform));
        cursorGo.transform.SetParent(barGo.transform, false);
        _cursorRt = cursorGo.GetComponent<RectTransform>();
        _cursorRt.anchorMin = _cursorRt.anchorMax = new Vector2(0.5f, 0.5f);
        _cursorRt.pivot = new Vector2(0.5f, 0.5f);
        _cursorRt.sizeDelta = new Vector2(barWidth + 20f, cursorHalf * 2f);
        _cursorRt.anchoredPosition = new Vector2(0f, _barHalfHeight);

        var cursorImg = cursorGo.AddComponent<Image>();
        cursorImg.sprite = UiWhiteSprite();
        cursorImg.color = cursorColor;
        cursorImg.raycastTarget = false;

        if (buildAnimalIconsNextToBar)
        {
            float iconHalf = animalIconSize * 0.5f;
            float colCenterX = iconColumnWidth * 0.5f;
            BuildSideAnimal(root.transform, "QuietAnimal", quietAnimalSprite, animalIconSize,
                new Vector2(colCenterX, barHeight * 0.5f - iconHalf));
            BuildSideAnimal(root.transform, "LoudAnimal", loudAnimalSprite, animalIconSize,
                new Vector2(colCenterX, -barHeight * 0.5f + iconHalf));
        }
    }

    static void BuildSideAnimal(Transform parent, string name, Sprite sprite, float size, Vector2 anchoredPos)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = new Vector2(0f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(size, size);
        rt.anchoredPosition = anchoredPos;

        var img = go.AddComponent<Image>();
        img.sprite = sprite;
        img.type = Image.Type.Simple;
        img.preserveAspect = true;
        img.color = sprite != null ? Color.white : new Color(1f, 1f, 1f, 0f);
        img.raycastTarget = false;
    }

    void BuildResultBanner(Transform canvas)
    {
        var banner = new GameObject("ResultBanner", typeof(RectTransform));
        banner.transform.SetParent(canvas, false);
        _resultBannerRoot = banner;
        var bannerRt = banner.GetComponent<RectTransform>();
        bannerRt.anchorMin = new Vector2(0.04f, 0.05f);
        bannerRt.anchorMax = new Vector2(0.96f, 0.36f);
        bannerRt.offsetMin = Vector2.zero;
        bannerRt.offsetMax = Vector2.zero;
        bannerRt.pivot = new Vector2(0.5f, 0.5f);

        var backdrop = new GameObject("Backdrop", typeof(RectTransform));
        backdrop.transform.SetParent(banner.transform, false);
        var bdRt = backdrop.GetComponent<RectTransform>();
        bdRt.anchorMin = Vector2.zero;
        bdRt.anchorMax = Vector2.one;
        bdRt.offsetMin = Vector2.zero;
        bdRt.offsetMax = Vector2.zero;
        var bdImg = backdrop.AddComponent<Image>();
        bdImg.sprite = UiWhiteSprite();
        bdImg.color = new Color(0f, 0f, 0f, 0.82f);
        bdImg.raycastTarget = false;

        var textGo = new GameObject("ResultText", typeof(RectTransform));
        textGo.transform.SetParent(banner.transform, false);
        var trt = textGo.GetComponent<RectTransform>();
        trt.anchorMin = new Vector2(0.02f, 0.28f);
        trt.anchorMax = new Vector2(0.98f, 0.95f);
        trt.offsetMin = Vector2.zero;
        trt.offsetMax = Vector2.zero;

        _resultText = textGo.AddComponent<Text>();
        var font = BuiltinUiFont();
        if (font != null)
            _resultText.font = font;
        _resultText.fontSize = 42;
        _resultText.fontStyle = FontStyle.Bold;
        _resultText.alignment = TextAnchor.MiddleCenter;
        _resultText.color = Color.white;
        _resultText.text = string.Empty;
        _resultText.horizontalOverflow = HorizontalWrapMode.Wrap;
        _resultText.verticalOverflow = VerticalWrapMode.Overflow;
        _resultText.lineSpacing = 1.05f;

        var outline = textGo.AddComponent<Outline>();
        outline.effectColor = new Color(0f, 0f, 0f, 0.85f);
        outline.effectDistance = new Vector2(1.5f, -1.5f);

        var btnGo = new GameObject("ReturnToRoomButton", typeof(RectTransform));
        btnGo.transform.SetParent(banner.transform, false);
        _returnButtonRoot = btnGo;
        var btnRt = btnGo.GetComponent<RectTransform>();
        btnRt.anchorMin = new Vector2(0.2f, 0.06f);
        btnRt.anchorMax = new Vector2(0.8f, 0.24f);
        btnRt.offsetMin = Vector2.zero;
        btnRt.offsetMax = Vector2.zero;
        btnRt.pivot = new Vector2(0.5f, 0.5f);

        var btnImg = btnGo.AddComponent<Image>();
        btnImg.sprite = UiWhiteSprite();
        var yellow = new Color(0.98f, 0.78f, 0.15f, 1f);
        btnImg.color = yellow;
        btnImg.raycastTarget = true;
        _returnButton = btnGo.AddComponent<Button>();
        _returnButton.targetGraphic = btnImg;
        var colors = _returnButton.colors;
        colors.normalColor = yellow;
        colors.highlightedColor = new Color(1f, 0.9f, 0.35f, 1f);
        colors.pressedColor = new Color(0.85f, 0.65f, 0.1f, 1f);
        colors.disabledColor = new Color(0.6f, 0.55f, 0.45f, 0.55f);
        _returnButton.colors = colors;
        var nav = _returnButton.navigation;
        nav.mode = Navigation.Mode.None;
        _returnButton.navigation = nav;
        _returnButton.onClick.AddListener(OnReturnToRoomClicked);

        var btnLabelGo = new GameObject("Label", typeof(RectTransform));
        btnLabelGo.transform.SetParent(btnGo.transform, false);
        var lblRt = btnLabelGo.GetComponent<RectTransform>();
        lblRt.anchorMin = Vector2.zero;
        lblRt.anchorMax = Vector2.one;
        lblRt.offsetMin = Vector2.zero;
        lblRt.offsetMax = Vector2.zero;
        _returnButtonCaption = btnLabelGo.AddComponent<Text>();
        if (font != null)
            _returnButtonCaption.font = font;
        _returnButtonCaption.fontSize = 36;
        _returnButtonCaption.fontStyle = FontStyle.Bold;
        _returnButtonCaption.alignment = TextAnchor.MiddleCenter;
        _returnButtonCaption.color = Color.white;
        _returnButtonCaption.text = returnToRoomButtonLabel;
        _returnButtonCaption.raycastTarget = false;

        btnGo.SetActive(false);
        banner.transform.SetAsLastSibling();
        banner.SetActive(false);
    }
}
