using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/// <summary>
/// When Space is pressed, activates the selected UI <see cref="Button"/>, or the only interactable
/// <see cref="Button"/> in the scene (common for single “Continue” / “Home” controls).
/// Put on the same DontDestroyOnLoad object as <see cref="FartGameSession"/> (created from Awake).
/// </summary>
[DefaultExecutionOrder(500)]
public class UiSpaceSubmitOnKey : MonoBehaviour
{
    static bool SpaceDownThisFrame()
    {
        if (Input.GetKeyDown(KeyCode.Space))
            return true;
#if ENABLE_INPUT_SYSTEM
        if (Keyboard.current != null && Keyboard.current.spaceKey.wasPressedThisFrame)
            return true;
#endif
        return false;
    }

    void Update()
    {
        if (!SpaceDownThisFrame())
            return;

        var es = EventSystem.current;
        if (es == null)
            return;

        if (es.currentSelectedGameObject != null)
        {
            var selBtn = es.currentSelectedGameObject.GetComponent<Button>();
            if (selBtn != null && selBtn.interactable && selBtn.isActiveAndEnabled)
            {
                selBtn.onClick.Invoke();
                return;
            }
        }

        Button only = null;
        int count = 0;
        var buttons = FindObjectsByType<Button>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        for (int i = 0; i < buttons.Length; i++)
        {
            var b = buttons[i];
            if (b == null || !b.isActiveAndEnabled || !b.interactable)
                continue;
            count++;
            only = b;
            if (count > 1)
            {
                only = null;
                break;
            }
        }

        if (count == 1 && only != null)
            only.onClick.Invoke();
    }
}
