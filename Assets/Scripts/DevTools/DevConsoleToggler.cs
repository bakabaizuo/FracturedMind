using UnityEngine;

namespace FracturedStudios.UI
{
    /// <summary>
    /// Tiny helper attached to the root UI GameObject to toggle a child GameObject named "DevConsole".
    /// - Press the backquote key (`) to toggle at runtime.
    /// - Public API: Toggle(), SetVisible(bool)
    /// - Context menu: "`toggle" (right-click on component in Inspector)
    /// </summary>
    [DisallowMultipleComponent]
    public class DevConsoleToggler : MonoBehaviour
    {
        [Tooltip("Name of the console GameObject to toggle (searched as a child of this GameObject if DevConsole is not assigned)")]
        public string devConsoleName = "DevConsole";

        [Tooltip("Key used to toggle the console (default: backquote / `)")]
        public KeyCode toggleKey = KeyCode.BackQuote;

        [Tooltip("Optional explicit reference to the DevConsole GameObject. If null, the script will search for a child with name 'devConsoleName' in Start().")]
        public GameObject devConsole;

        [Tooltip("Components to disable while the console is open (e.g. ThirdPersonBasic, PlayerInteract). Drag player components here.")]
        public MonoBehaviour[] disableWhileOpen;

        void Start()
        {
            if (devConsole == null)
            {
                // Search immediate children and deep children for convenience
                var t = transform.Find(devConsoleName);
                if (t != null) devConsole = t.gameObject;
                else
                {
                    // deep search
                    Transform found = null;
                    foreach (Transform child in transform.GetComponentsInChildren<Transform>(true))
                    {
                        if (child.name == devConsoleName) { found = child; break; }
                    }
                    if (found != null) devConsole = found.gameObject;
                }
            }
        }

        void Update()
        {
            if (Input.GetKeyDown(toggleKey)) Toggle();
        }

        /// <summary>
        /// Toggle the current enabled state of the DevConsole GameObject.
        /// </summary>
        public void Toggle()
        {
            if (devConsole == null)
            {
                Debug.LogWarning($"DevConsoleToggler: DevConsole not found (expected name '{devConsoleName}'). Attach or set the reference.");
                return;
            }
            SetVisible(!devConsole.activeSelf);
        }

        /// <summary>
        /// Explicitly show or hide the DevConsole GameObject.
        /// </summary>
        public void SetVisible(bool visible)
        {
            if (devConsole == null)
            {
                Debug.LogWarning($"DevConsoleToggler: DevConsole not found (expected name '{devConsoleName}'). Attach or set the reference.");
                return;
            }
            devConsole.SetActive(visible);

            // Disable/enable player-side components so input doesn't bleed through
            if (disableWhileOpen != null)
            {
                foreach (var c in disableWhileOpen)
                {
                    if (c != null) c.enabled = !visible;
                }
            }
        }

        // Expose a context menu command that matches your requested label "`toggle" so you can toggle from the inspector.
        [ContextMenu("`toggle")]
        private void ContextMenuToggle()
        {
            Toggle();
        }
    }
}