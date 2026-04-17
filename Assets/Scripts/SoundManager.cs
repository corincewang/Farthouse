using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// DontDestroyOnLoad child of <see cref="FartGameSession"/>. Loops <see cref="whiteNoise"/> while the active scene
/// name matches <see cref="roomSceneName"/> (default: room_scene).
/// </summary>
public class SoundManager : MonoBehaviour
{
    [SerializeField] string roomSceneName = "room_scene";
    [SerializeField] AudioClip whiteNoise;
    [Range(0f, 1f)]
    [SerializeField] float volume = 0.35f;

    AudioSource _audio;

    void Awake()
    {
        _audio = GetComponent<AudioSource>();
        if (_audio == null)
            _audio = gameObject.AddComponent<AudioSource>();
        _audio.loop = true;
        _audio.playOnAwake = false;
        _audio.spatialBlend = 0f;
        _audio.volume = volume;
        _audio.clip = whiteNoise;
    }

    void OnEnable()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
        ApplyForScene(SceneManager.GetActiveScene().name);
    }

    void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        ApplyForScene(scene.name);
    }

    /// <summary>Called from <see cref="FartGameSession"/> after clip / scene names are known.</summary>
    public void Configure(string roomScene, AudioClip clip, float vol)
    {
        roomSceneName = roomScene;
        whiteNoise = clip;
        volume = Mathf.Clamp01(vol);
        if (_audio == null)
            _audio = GetComponent<AudioSource>();
        _audio.volume = volume;
        _audio.clip = whiteNoise;
        ApplyForScene(SceneManager.GetActiveScene().name);
    }

    void ApplyForScene(string sceneName)
    {
        if (_audio == null)
            return;

        if (whiteNoise != null && sceneName == roomSceneName)
        {
            _audio.clip = whiteNoise;
            _audio.volume = volume;
            if (!_audio.isPlaying)
                _audio.Play();
        }
        else if (_audio.isPlaying)
            _audio.Stop();
    }
}
