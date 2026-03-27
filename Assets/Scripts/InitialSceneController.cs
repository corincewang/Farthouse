using UnityEngine;

/// <summary>
/// Put in <b>initial_scene</b> (menu). <b>Space</b>: start run, continue to room after a fart, or restart after all rounds. UI buttons can still call <see cref="StartGame"/> / <see cref="ContinueToRoom"/>.
/// </summary>
public class InitialSceneController : MonoBehaviour
{
    [SerializeField] bool createSessionIfMissing = true;

    void Update()
    {
        if (!Input.GetKeyDown(KeyCode.Space)) return;
        var s = FartGameSession.Instance;
        if (s == null)
        {
            StartGame();
            return;
        }

        if (s.CompletedRunAwaitingMenu)
        {
            StartGame();
            return;
        }

        if (s.AwaitingContinueToRoom)
        {
            ContinueToRoom();
            return;
        }

        StartGame();
    }

    void Start()
    {
        if (FartGameSession.Instance == null && createSessionIfMissing)
        {
            var go = new GameObject("FartGameSession");
            go.AddComponent<FartGameSession>();
        }

        FartGameSession.Instance?.SetInitialHud();
    }

    public void StartGame()
    {
        if (FartGameSession.Instance == null)
        {
            var go = new GameObject("FartGameSession");
            go.AddComponent<FartGameSession>();
        }

        FartGameSession.Instance.StartNewGame();
    }

    /// <summary>Call from animation event or button after fart, when <see cref="FartGameSession.AwaitingContinueToRoom"/> is true.</summary>
    public void ContinueToRoom()
    {
        if (FartGameSession.Instance == null) return;
        FartGameSession.Instance.ContinueToRoomPhase();
    }
}
