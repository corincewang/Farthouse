using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/// <summary>
/// Put in <b>initial_scene</b> (menu). <b>Left mouse click</b> (not on UI): start run, continue to room, or restart. UI buttons can still call <see cref="StartGame"/> / <see cref="ContinueToRoom"/>.
/// If this component lives on a DontDestroyOnLoad object, click handling is limited to <see cref="menuSceneName"/> so room_scene clicks do not reload the room.
/// </summary>
public class InitialSceneController : MonoBehaviour
{
    [SerializeField] bool createSessionIfMissing = true;
    [SerializeField] string menuSceneName = "initial_scene";

    static bool LeftMouseClickedThisFrame()
    {
        if (Input.GetMouseButtonDown(0))
            return true;
#if ENABLE_INPUT_SYSTEM
        if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
            return true;
#endif
        return false;
    }

    void Update()
    {
        if (SceneManager.GetActiveScene().name != menuSceneName)
            return;

        if (!LeftMouseClickedThisFrame()) return;
        if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
            return;

        if (FartGameSession.Instance == null)
        {
            StartGame();
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
