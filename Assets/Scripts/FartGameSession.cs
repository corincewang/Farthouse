using System;
using UnityEngine;
using UnityEngine.SceneManagement;
/// <summary>
/// Persists across scenes (DontDestroyOnLoad). Flow is always
/// <b>initial_scene → room_scene → fart_scene → initial_scene → …</b>.
/// After each fart, the minigame can send the player straight to <b>room_scene</b> (see <see cref="AcknowledgeFartResultAndContinue"/>), or you can still use <b>initial_scene</b> + <see cref="ContinueToRoomPhase"/> for an interstitial.
/// Place one instance in <b>initial_scene</b>. Add all three scenes to Build Settings.
/// </summary>
public class FartGameSession : MonoBehaviour
{
    public static FartGameSession Instance { get; private set; }

    [SerializeField] int totalRounds = 5;
    [SerializeField] float prepDurationSeconds = 15f;
    [SerializeField] float fartPhaseDurationSeconds = 6f;

    [Header("Scene asset names (must match Build Settings, no .unity)")]
    [SerializeField] string initialSceneName = "initial_scene";
    [SerializeField] string roomSceneName = "room_scene";
    [SerializeField] string fartSceneName = "fart_scene";

    [Header("Optional UI (assign on DDOL object)")]
    [SerializeField] UnityEngine.UI.Text statusLabel;
    [SerializeField] UnityEngine.UI.Text timerLabel;

    public int CurrentRound { get; private set; } = 1;
    public int TotalRounds => totalRounds;
    public float PrepDurationSeconds => prepDurationSeconds;
    public float FartPhaseDurationSeconds => fartPhaseDurationSeconds;

    /// <summary>After last fart commit: 0 = quiet (top of bar), 1 = loud (bottom).</summary>
    public float LastFartLoudness01 { get; private set; }

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

    /// <summary>True after a non-final fart: use for inter-round animation, then <see cref="ContinueToRoomPhase"/>.</summary>
    bool _awaitingContinueToRoom;

    /// <summary>True after the last round's fart: show end / replay on initial_scene.</summary>
    bool _completedRunOnInitial;

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

    public void SetLastFartLoudness(float t)
    {
        LastFartLoudness01 = Mathf.Clamp01(t);
    }

    /// <summary>New game from menu: round 1 → room_scene.</summary>
    public void StartNewGame()
    {
        _awaitingContinueToRoom = false;
        _completedRunOnInitial = false;
        CurrentRound = 1;
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
        _hudMode = HudMode.Initial;
        SceneManager.LoadScene(initialSceneName);
    }

    public void LoadRoomScene()
    {
        CanInteractDuringPrep = true;
        SceneManager.LoadScene(roomSceneName);
    }

    public void LoadFartScene()
    {
        CanInteractDuringPrep = false;
        SceneManager.LoadScene(fartSceneName);
    }

    /// <summary>Called by <see cref="RoomSceneController"/> when prep hits 0.</summary>
    public void NotifyRoomPrepEnded()
    {
        LoadFartScene();
    }

    /// <summary>Legacy path: after fart, go to initial_scene and wait for <see cref="ContinueToRoomPhase"/>.</summary>
    public void NotifyFartPhaseEnded()
    {
        if (CurrentRound >= totalRounds)
        {
            GameCompleted?.Invoke();
            _awaitingContinueToRoom = false;
            _completedRunOnInitial = true;
            LoadInitialScene();
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
        if (CurrentRound >= totalRounds)
        {
            GameCompleted?.Invoke();
            _awaitingContinueToRoom = false;
            _completedRunOnInitial = true;
            LoadInitialScene();
            return;
        }

        CurrentRound++;
        _awaitingContinueToRoom = false;
        _completedRunOnInitial = false;
        LoadRoomScene();
    }

    public void NotifyRoomSceneEntered()
    {
        RoomPrepStarted?.Invoke(CurrentRound);
    }

    public void NotifyFartSceneEntered()
    {
        FartPhaseStarted?.Invoke(CurrentRound);
    }

    internal void SetRoomPrepHud(float prepRemaining)
    {
        _hudMode = HudMode.RoomPrep;
        _hudRoomTime = prepRemaining;
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
            HudMode.RoomPrep => $"Round {CurrentRound}/{totalRounds} — Get ready!",
            HudMode.Fart => $"Round {CurrentRound}/{totalRounds} — FART!",
            _ => ""
        };

        if (statusLabel != null) statusLabel.text = status;

        if (timerLabel != null)
        {
            timerLabel.text = _hudMode switch
            {
                HudMode.RoomPrep => $"{Mathf.CeilToInt(_hudRoomTime)}s",
                HudMode.Fart => $"{_hudFartTime:0.0}s",
                _ => ""
            };
        }
    }

    string InitialSceneStatusText()
    {
        if (_completedRunOnInitial)
            return "Run complete — press SPACE to play again.";
        if (_awaitingContinueToRoom)
            return $"Round {CurrentRound}/{totalRounds} — press SPACE to enter room.";
        return "Farthouse — press SPACE to start";
    }

#if UNITY_EDITOR
    [ContextMenu("Debug/Start New Game")]
    void DebugStartNewGame() => StartNewGame();
#endif
}
