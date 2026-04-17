using UnityEngine;

/// <summary>
/// Put in <b>fart_scene</b> only. Starts <see cref="FartTimingMinigame"/>; player ack continues via <see cref="FartGameSession.AcknowledgeFartResultAndContinue"/>.
/// </summary>
[DefaultExecutionOrder(-200)]
[RequireComponent(typeof(FartTimingMinigame))]
public class FartSceneController : MonoBehaviour
{
    void Start()
    {
        var session = FartGameSession.Instance;
        if (session == null)
        {
            enabled = false;
            return;
        }

        GetComponent<FartTimingMinigame>().BeginPhase(session);
    }
}
