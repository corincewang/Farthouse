using UnityEngine;

/// <summary>
/// Put in <b>room_scene</b> only. 15s prep, then loads fart_scene. Props (plant, toilet) live only in this scene.
/// </summary>
public class RoomSceneController : MonoBehaviour
{
    [SerializeField] bool createSessionIfMissing = true;

    float _remaining;
    bool _ended;

    void Start()
    {
        var session = FartGameSession.Instance;
        if (session == null && createSessionIfMissing)
        {
            var go = new GameObject("FartGameSession");
            session = go.AddComponent<FartGameSession>();
        }

        if (session == null) return;

        _remaining = session.PrepDurationSeconds;
        session.CanInteractDuringPrep = true;
        session.NotifyRoomSceneEntered();
        session.SetRoomPrepHud(_remaining);
    }

    void Update()
    {
        var session = FartGameSession.Instance;
        if (session == null) return;

        _remaining -= Time.deltaTime;
        session.SetRoomPrepHud(Mathf.Max(0f, _remaining));

        if (_remaining > 0f || _ended) return;

        _ended = true;
        session.CanInteractDuringPrep = false;
        session.NotifyRoomPrepEnded();
        enabled = false;
    }
}
