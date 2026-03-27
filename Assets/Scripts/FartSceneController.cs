using UnityEngine;

/// <summary>
/// Put in <b>fart_scene</b> only. After fart duration, either next round → room_scene or back to initial_scene.
/// </summary>
public class FartSceneController : MonoBehaviour
{
    float _remaining;
    bool _done;

    void Start()
    {
        var session = FartGameSession.Instance;
        if (session == null)
        {
            Debug.LogWarning("[Farthouse] FartSceneController: no FartGameSession. Load from initial_scene or add session.");
            enabled = false;
            return;
        }

        _remaining = session.FartPhaseDurationSeconds;
        session.NotifyFartSceneEntered();
        session.SetFartHud(_remaining);
    }

    void Update()
    {
        if (_done) return;
        var session = FartGameSession.Instance;
        if (session == null) return;

        _remaining -= Time.deltaTime;
        session.SetFartHud(Mathf.Max(0f, _remaining));

        if (_remaining > 0f) return;

        _done = true;
        session.NotifyFartPhaseEnded();
    }
}
