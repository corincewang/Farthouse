using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
/// <summary>
/// Persists across scenes (DontDestroyOnLoad). Flow is always
/// <b>initial_scene → room_scene → fart_scene → … → ending_scene</b> (see <see cref="totalRounds"/>).
/// Place one instance in <b>initial_scene</b>. Add all scenes to Build Settings.
/// </summary>
public class FartGameSession : MonoBehaviour
{
    public static FartGameSession Instance { get; private set; }

    [SerializeField] int totalRounds = 4;
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

    public int CurrentRound { get; private set; } = 1;
    public int TotalRounds => totalRounds;
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

    [Header("Smell reduction")]
    [Range(0f, 1f)]
    [SerializeField] float windowOpenSmellReduction = 0.12f;
    [Range(0f, 1f)]
    [SerializeField] float toiletSmellReduction = 0.25f;
    [Range(0f, 1f)]
    [SerializeField] float plantHideSmellReduction = 0.15f;

    [Header("Ending rules")]
    [Tooltip("How many rounds must be loud (meter) to treat the run as a \"loud\" ending branch.")]
    [SerializeField] int loudRoundsNeededForLoudRun = 3;

    [Tooltip("How many rounds with window open / at plant count as \"often\" for ending split.")]
    [SerializeField] int contextualRoundsNeeded = 2;

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

    /// <summary>True after a non-final fart: use for inter-round animation, then <see cref="ContinueToRoomPhase"/>.</summary>
    bool _awaitingContinueToRoom;

    /// <summary>True after the last round's fart: show end / replay on initial_scene.</summary>
    bool _completedRunOnInitial;
    bool _runCompleted;
    string _finalEndingKey = "EndingCleanQuiet";
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
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    public bool AwaitingContinueToRoom => _awaitingContinueToRoom;
    public bool CompletedRunAwaitingMenu => _completedRunOnInitial;
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

    public bool TryEnterToiletThisRound()
    {
        if (_currentRoundInToilet) return true;
        if (_forceToiletBlockedThisRound) return false;

        bool canEnter = UnityEngine.Random.value < 0.5f;
        if (!canEnter) return false;

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
        return Mathf.Clamp01(total);
    }

    /// <summary>New game from menu: round 1 → room_scene.</summary>
    public void StartNewGame()
    {
        _awaitingContinueToRoom = false;
        _completedRunOnInitial = false;
        _runCompleted = false;
        CurrentRound = 1;
        CurrentRoundLocation = FartLocation.None;
        LastFartLoudness01 = 0f;
        _roundRecords.Clear();
        _finalEndingKey = "EndingCleanQuiet";
        _advanceLocked = false;
        _currentRoundWindowOpen = false;
        _currentRoundInToilet = false;
        _currentRoundHidingBehindPlant = false;
        _forceToiletBlockedThisRound = false;
        _blockToiletNextRound = false;
        LoadRoomScene();
    }

    /// <summary>After inter-round animation on initial_scene, go to room for the current round.</summary>
    public void ContinueToRoomPhase()
    {
        if (!_awaitingContinueToRoom || _completedRunOnInitial) return;
        _awaitingContinueToRoom = false;
        LoadRoomScene();
    }

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

    /// <summary>Legacy path: after fart, go to initial_scene and wait for <see cref="ContinueToRoomPhase"/>.</summary>
    public void NotifyFartPhaseEnded()
    {
        CommitCurrentRoundResult();
        if (CurrentRound >= totalRounds)
        {
            CompleteRunAndShowEnding();
            return;
        }

        CurrentRound++;
        _awaitingContinueToRoom = true;
        _completedRunOnInitial = false;
        LoadInitialScene();
    }

    /// <summary>After the player reads the fart result: next round → <b>room_scene</b>; final round → <b>initial_scene</b> (run complete).</summary>
    public void AcknowledgeFartResultAndContinue()
    {
        if (_advanceLocked) return;
        _advanceLocked = true;

        CommitCurrentRoundResult();
        if (CurrentRound >= totalRounds)
        {
            CompleteRunAndShowEnding();
            return;
        }

        CurrentRound++;
        _awaitingContinueToRoom = false;
        _completedRunOnInitial = false;
        LoadRoomScene();
    }

    public void NotifyRoomSceneEntered()
    {
        _advanceLocked = false;
        _forceToiletBlockedThisRound = _blockToiletNextRound;
        _blockToiletNextRound = false;
        _currentRoundInToilet = false;
        _currentRoundHidingBehindPlant = false;
        _currentRoundWindowOpen = false;
        if (_scheduleRoomPrepAnchor)
        {
            _scheduleRoomPrepAnchor = false;
            _roomPrepEndTimeUnscaled = Time.unscaledTime + prepDurationSeconds;
        }

        RoomPrepStarted?.Invoke(CurrentRound);
    }

    /// <summary>Remaining prep time from session clock (not reset by room clicks).</summary>
    public float GetRoomPrepRemainingSeconds()
    {
        return Mathf.Max(0f, _roomPrepEndTimeUnscaled - Time.unscaledTime);
    }

    public void NotifyFartSceneEntered()
    {
        _advanceLocked = false;
        FartPhaseStarted?.Invoke(CurrentRound);
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
            HudMode.RoomPrep => $"Round {CurrentRound}/{totalRounds} — get in position before you fart.",
            HudMode.Fart => $"Round {CurrentRound}/{totalRounds} — FART!",
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
        if (_completedRunOnInitial)
            return "Run complete — left-click to play again.";
        if (_awaitingContinueToRoom)
            return $"Round {CurrentRound}/{totalRounds} — left-click to enter room.";
        return "Farthouse — left-click to start";
    }

    void CommitCurrentRoundResult()
    {
        if (_roundRecords.Count >= CurrentRound) return;
        _roundRecords.Add(new RoundRecord
        {
            roundIndex = CurrentRound,
            location = CurrentRoundLocation,
            loudness01 = LastFartLoudness01,
            windowOpen = _currentRoundWindowOpen,
            nearPlant = _currentRoundHidingBehindPlant || CurrentRoundLocation == FartLocation.Plant
        });
    }

    void CompleteRunAndShowEnding()
    {
        _awaitingContinueToRoom = false;
        _completedRunOnInitial = false;
        _runCompleted = true;
        _finalEndingKey = EvaluateEndingKey();
        GameCompleted?.Invoke();
        LoadEndingScene();
    }

    string EvaluateEndingKey()
    {
        if (_roundRecords.Count == 0)
            return "EndingCleanQuiet";

        int loudCount = 0;
        int quietCount = 0;
        int windowOpenRounds = 0;
        int plantRounds = 0;

        for (int i = 0; i < _roundRecords.Count; i++)
        {
            var r = _roundRecords[i];
            if (r.loudness01 >= 0.66f) loudCount++;
            else if (r.loudness01 <= 0.34f) quietCount++;
            if (r.windowOpen) windowOpenRounds++;
            if (r.nearPlant) plantRounds++;
        }

        int needLoud = Mathf.Clamp(loudRoundsNeededForLoudRun, 1, totalRounds);
        int needCtx = Mathf.Clamp(contextualRoundsNeeded, 1, totalRounds);
        bool loudRun = loudCount >= needLoud;
        bool quietRun = quietCount >= needLoud;

        if (loudRun)
        {
            if (plantRounds >= needCtx) return "EndingSmellLoud";
            if (windowOpenRounds >= needCtx) return "EndingFartendo";
            return "EndingSmellLoud";
        }

        if (quietRun)
        {
            if (plantRounds >= needCtx) return "EndingSmellQuiet";
            if (windowOpenRounds >= needCtx) return "EndingCleanQuiet";
            return "EndingSmellQuiet";
        }

        if (plantRounds >= needCtx) return "EndingSmellQuiet";
        if (windowOpenRounds >= needCtx) return "EndingCleanQuiet";
        if (loudCount > quietCount) return "EndingSmellLoud";
        if (quietCount > loudCount) return "EndingCleanQuiet";
        return "EndingSmellQuiet";
    }

#if UNITY_EDITOR
    [ContextMenu("Debug/Start New Game")]
    void DebugStartNewGame() => StartNewGame();
#endif
}
