using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
/// <summary>
/// Persists across scenes (DontDestroyOnLoad). Single play:
/// <b>initial_scene → room_scene → fart_scene → ending_scene</b>; replay from ending or menu.
/// Place one instance in <b>initial_scene</b>. Add all scenes to Build Settings.
/// </summary>
public class FartGameSession : MonoBehaviour
{
    public static FartGameSession Instance { get; private set; }

    [SerializeField] float prepDurationSeconds = 15f;
    [SerializeField] float fartPhaseDurationSeconds = 6f;

    [Header("Scene asset names (must match Build Settings, no .unity)")]
    [SerializeField] string initialSceneName = "initial_scene";
    [SerializeField] string roomSceneName = "room_scene";
    [SerializeField] string fartSceneName = "fart_scene";
    [SerializeField] string endingSceneName = "ending_scene";

    [Header("Optional UI (assign on DDOL object)")]
    [SerializeField] UnityEngine.UI.Text statusLabel;
    [SerializeField] UnityEngine.UI.Text timerLabel;

    [Header("Room ambience (SoundManager)")]
    [Tooltip("Looped while room_scene is loaded. Assign your white noise clip here.")]
    [SerializeField] AudioClip roomWhiteNoiseClip;
    [SerializeField] [Range(0f, 1f)] float roomWhiteNoiseVolume = 0.35f;

    public float PrepDurationSeconds => prepDurationSeconds;
    public float FartPhaseDurationSeconds => fartPhaseDurationSeconds;

    /// <summary>After last fart commit: 0 = quiet (top of bar), 1 = loud (bottom).</summary>
    public float LastFartLoudness01 { get; private set; }
    public FartLocation CurrentRoundLocation { get; private set; } = FartLocation.None;

    /// <summary>Window open at end of prep (set by <see cref="RoomSceneController"/> from a <see cref="ToggleInteractable"/>).</summary>
    bool _currentRoundWindowOpen;
    bool _currentRoundInToilet;
    bool _currentRoundHidingBehindPlant;
    bool _forceToiletBlockedThisRound;
    bool _blockToiletNextRound;
    /// <summary>Rolled once when room prep starts: if false, toilet stays “occupied” for the whole 15s.</summary>
    bool _toiletRollAllowsEntry;
    bool _liveOpenPlantProximitySmell;
    bool _snapOpenPlantProximitySmellForFart;

    [Header("Smell reduction")]
    [Range(0f, 1f)]
    [SerializeField] float windowOpenSmellReduction = 0.12f;
    [Range(0f, 1f)]
    [SerializeField] float toiletSmellReduction = 0.25f;
    [Range(0f, 1f)]
    [SerializeField] float plantHideSmellReduction = 0.15f;

    [Tooltip("Prep ended while plant was open and player was inside proximity hide zone (see PlantOpenProximityHide).")]
    [Range(0f, 1f)]
    [SerializeField] float plantOpenProximitySmellReduction = 0.15f;

    [Header("Ending rules (binary fart, keys must match ending_scene)")]
    [Tooltip("Meter value ≥ this counts as loud; below counts as quiet.")]
    [SerializeField] [Range(0f, 1f)] float loudQuietSplitThreshold = 0.5f;

    [Tooltip("Loud fart without smell → this ending key (e.g. fartendo).")]
    [SerializeField] string loudEndingKey = "fartendo";

    [Tooltip("Loud fart with smell → this ending key (e.g. smell_loud).")]
    [SerializeField] string loudSmellEndingKey = "smell_loud";

    [Tooltip("Quiet fart with window closed → this ending key (e.g. smell_quiet).")]
    [SerializeField] string quietWindowClosedEndingKey = "smell_quiet";

    [Tooltip("Quiet fart with window open → not-smelly quiet branch (e.g. quietnotsmell).")]
    [SerializeField] string quietWindowOpenEndingKey = "quietnotsmell";

    /// <summary>True only during room_scene prep countdown.</summary>
    public bool CanInteractDuringPrep { get; internal set; }

    public event Action<int> RoomPrepStarted;
    public event Action<int> FartPhaseStarted;
    public event Action GameCompleted;

    public enum HudMode
    {
        None,
        Initial,
        RoomPrep,
        Fart
    }

    HudMode _hudMode = HudMode.None;
    float _hudRoomTime;
    float _hudFartTime;

    /// <summary>Seconds left in room prep (updated each frame in room_scene).</summary>
    public float RoomPrepRemainingSeconds { get; private set; }

    /// <summary>Wall-clock prep end (unscaled). Anchored only when <see cref="_scheduleRoomPrepAnchor"/> is set (see <see cref="LoadRoomScene"/>).</summary>
    float _roomPrepEndTimeUnscaled;

    /// <summary>When true, the next <see cref="NotifyRoomSceneEntered"/> sets <see cref="_roomPrepEndTimeUnscaled"/>.
    /// Prevents a second <c>RoomSceneController</c> (or delayed <c>Start</c>) from resetting the timer on click.</summary>
    bool _scheduleRoomPrepAnchor = true;
    bool _runCompleted;
    string _finalEndingKey = "smell_quiet";
    readonly List<RoundRecord> _roundRecords = new List<RoundRecord>(8);
    bool _advanceLocked;

    [Serializable]
    public struct RoundRecord
    {
        public int roundIndex;
        public FartLocation location;
        public float loudness01;
        public bool windowOpen;
        public bool nearPlant;
    }

    public enum FartLocation
    {
        None = 0,
        RestroomSafe = 1,
        Restroom = 2,
        Dog = 3,
        Window = 4,
        Fan = 5,
        Plant = 6,
        Music = 7
    }

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
        EnsureUiSpaceSubmit();
        EnsureSoundManager();
    }

    void EnsureUiSpaceSubmit()
    {
        if (GetComponent<UiSpaceSubmitOnKey>() == null)
            gameObject.AddComponent<UiSpaceSubmitOnKey>();
    }

    void EnsureSoundManager()
    {
        var existing = GetComponentInChildren<SoundManager>(true);
        if (existing != null)
        {
            existing.Configure(roomSceneName, roomWhiteNoiseClip, roomWhiteNoiseVolume);
            return;
        }

        var go = new GameObject("SoundManager");
        go.transform.SetParent(transform, false);
        var sm = go.AddComponent<SoundManager>();
        sm.Configure(roomSceneName, roomWhiteNoiseClip, roomWhiteNoiseVolume);
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    public bool AwaitingContinueToRoom => false;
    public bool CompletedRunAwaitingMenu => false;
    public bool RunCompleted => _runCompleted;
    public string FinalEndingKey => _finalEndingKey;
    public IReadOnlyList<RoundRecord> RoundRecords => _roundRecords;
    public bool CurrentRoundInToilet => _currentRoundInToilet;
    public bool CurrentRoundHidingBehindPlant => _currentRoundHidingBehindPlant;
    public bool IsToiletBlockedThisRound => _forceToiletBlockedThisRound;

    public void SetLastFartLoudness(float t)
    {
        LastFartLoudness01 = Mathf.Clamp01(t);
    }

    public void SetCurrentRoundFartLocation(FartLocation location)
    {
        CurrentRoundLocation = location;
    }

    /// <summary>Call when room prep ends (before fart): whether the tracked window was open.</summary>
    public void SetCurrentRoundWindowOpen(bool open)
    {
        _currentRoundWindowOpen = open;
    }

    public void SetCurrentRoundWindowState(bool open)
    {
        _currentRoundWindowOpen = open;
    }

    public void SetCurrentRoundHidingBehindPlant(bool hiding)
    {
        _currentRoundHidingBehindPlant = hiding;
    }

    /// <summary>Live prep flag: plant open + player in proximity (see <see cref="PlantOpenProximityHide"/>).</summary>
    public void SetLiveOpenPlantProximitySmell(bool eligible)
    {
        _liveOpenPlantProximitySmell = eligible;
    }

    /// <summary>Call once when room prep ends, before fart scene, so smell reduction matches last prep geometry.</summary>
    public void SnapshotOpenPlantProximitySmellFromPrepEnd()
    {
        _snapOpenPlantProximitySmellForFart = _liveOpenPlantProximitySmell;
    }

    public bool TryEnterToiletThisRound()
    {
        if (_currentRoundInToilet) return true;
        if (!_toiletRollAllowsEntry)
            return false;

        _currentRoundInToilet = true;
        _blockToiletNextRound = true;
        CurrentRoundLocation = FartLocation.RestroomSafe;
        return true;
    }

    public float GetCurrentRoundSmellReduction01()
    {
        float total = 0f;
        if (_currentRoundWindowOpen) total += windowOpenSmellReduction;
        if (_currentRoundInToilet) total += toiletSmellReduction;
        if (_currentRoundHidingBehindPlant) total += plantHideSmellReduction;
        if (_snapOpenPlantProximitySmellForFart) total += plantOpenProximitySmellReduction;
        return Mathf.Clamp01(total);
    }

    /// <summary>New game from menu or ending: reset run → room_scene.</summary>
    public void StartNewGame()
    {
        _runCompleted = false;
        CurrentRoundLocation = FartLocation.None;
        LastFartLoudness01 = 0f;
        _roundRecords.Clear();
        _finalEndingKey = "smell_quiet";
        _advanceLocked = false;
        _currentRoundWindowOpen = false;
        _currentRoundInToilet = false;
        _currentRoundHidingBehindPlant = false;
        _forceToiletBlockedThisRound = false;
        _blockToiletNextRound = false;
        _toiletRollAllowsEntry = false;
        _liveOpenPlantProximitySmell = false;
        _snapOpenPlantProximitySmellForFart = false;
        LoadRoomScene();
    }

    /// <summary>Legacy no-op (multi-round flow removed).</summary>
    public void ContinueToRoomPhase() { }

    public void LoadInitialScene()
    {
        CanInteractDuringPrep = false;
        _advanceLocked = false;
        _hudMode = HudMode.Initial;
        SceneManager.LoadScene(initialSceneName);
    }

    public void LoadRoomScene()
    {
        CanInteractDuringPrep = true;
        _scheduleRoomPrepAnchor = true;
        SceneManager.LoadScene(roomSceneName);
    }

    public void LoadFartScene()
    {
        CanInteractDuringPrep = false;
        SceneManager.LoadScene(fartSceneName);
    }

    public void LoadEndingScene()
    {
        CanInteractDuringPrep = false;
        SceneManager.LoadScene(endingSceneName);
    }

    /// <summary>Called by <see cref="RoomSceneController"/> when prep hits 0.</summary>
    public void NotifyRoomPrepEnded()
    {
        LoadFartScene();
    }

    /// <summary>Legacy path: after fart, go straight to ending like <see cref="AcknowledgeFartResultAndContinue"/>.</summary>
    public void NotifyFartPhaseEnded()
    {
        CommitCurrentRoundResult();
        CompleteRunAndShowEnding();
    }

    /// <summary>After the player reads the fart result → <b>ending_scene</b>.</summary>
    public void AcknowledgeFartResultAndContinue()
    {
        if (_advanceLocked) return;
        _advanceLocked = true;

        CommitCurrentRoundResult();
        CompleteRunAndShowEnding();
    }

    public void NotifyRoomSceneEntered()
    {
        _advanceLocked = false;
        _forceToiletBlockedThisRound = _blockToiletNextRound;
        _blockToiletNextRound = false;
        _currentRoundInToilet = false;
        _currentRoundHidingBehindPlant = false;
        _currentRoundWindowOpen = false;
        if (_forceToiletBlockedThisRound)
            _toiletRollAllowsEntry = false;
        else
            _toiletRollAllowsEntry = UnityEngine.Random.value < 0.5f;
        _liveOpenPlantProximitySmell = false;
        _snapOpenPlantProximitySmellForFart = false;
        if (_scheduleRoomPrepAnchor)
        {
            _scheduleRoomPrepAnchor = false;
            _roomPrepEndTimeUnscaled = Time.unscaledTime + prepDurationSeconds;
        }

        RoomPrepStarted?.Invoke(1);
    }

    /// <summary>Remaining prep time from session clock (not reset by room clicks).</summary>
    public float GetRoomPrepRemainingSeconds()
    {
        return Mathf.Max(0f, _roomPrepEndTimeUnscaled - Time.unscaledTime);
    }

    public void NotifyFartSceneEntered()
    {
        _advanceLocked = false;
        FartPhaseStarted?.Invoke(1);
    }

    internal void SetRoomPrepHud(float prepRemaining)
    {
        _hudMode = HudMode.RoomPrep;
        _hudRoomTime = prepRemaining;
        RoomPrepRemainingSeconds = Mathf.Max(0f, prepRemaining);
        RefreshUi();
    }

    internal void SetFartHud(float fartRemaining)
    {
        _hudMode = HudMode.Fart;
        _hudFartTime = fartRemaining;
        RefreshUi();
    }

    public void SetInitialHud()
    {
        _hudMode = HudMode.Initial;
        RefreshUi();
    }

    void RefreshUi()
    {
        string status = _hudMode switch
        {
            HudMode.Initial => InitialSceneStatusText(),
            HudMode.RoomPrep => "Get in position before you fart.",
            HudMode.Fart => "FART!",
            _ => ""
        };

        if (statusLabel != null) statusLabel.text = status;

        if (timerLabel != null)
        {
            timerLabel.text = _hudMode switch
            {
                HudMode.RoomPrep => $"Fart in {Mathf.CeilToInt(_hudRoomTime)}s",
                HudMode.Fart => $"{_hudFartTime:0.0}s",
                _ => ""
            };
        }
    }

    string InitialSceneStatusText()
    {
        return "Farthouse — click / SPACE / ENTER to start";
    }

    void CommitCurrentRoundResult()
    {
        if (_roundRecords.Count > 0) return;
        _roundRecords.Add(new RoundRecord
        {
            roundIndex = 1,
            location = CurrentRoundLocation,
            loudness01 = LastFartLoudness01,
            windowOpen = _currentRoundWindowOpen,
            nearPlant = _currentRoundHidingBehindPlant || CurrentRoundLocation == FartLocation.Plant ||
                      _snapOpenPlantProximitySmellForFart
        });
    }

    void CompleteRunAndShowEnding()
    {
        if (_runCompleted) return;
        _runCompleted = true;
        _finalEndingKey = EvaluateEndingKey();
        GameCompleted?.Invoke();
        LoadEndingScene();
    }

    string EvaluateEndingKey()
    {
        if (_roundRecords.Count == 0)
            return quietWindowClosedEndingKey;

        var r = _roundRecords[0];
        bool loud = r.loudness01 >= loudQuietSplitThreshold;
        bool smell = !r.windowOpen || r.nearPlant;
        if (loud)
            return smell ? loudSmellEndingKey : loudEndingKey;
        if (r.windowOpen)
            return quietWindowOpenEndingKey;
        return quietWindowClosedEndingKey;
    }

#if UNITY_EDITOR
    [ContextMenu("Debug/Start New Game")]
    void DebugStartNewGame() => StartNewGame();
#endif
}
