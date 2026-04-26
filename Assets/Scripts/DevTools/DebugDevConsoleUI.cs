using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using  FracturedStudios.Invoker;
using FracturedStudios;

namespace FracturedStudios.UI
{
    [DisallowMultipleComponent]
    public class DebugDevConsoleUI : MonoBehaviour
    {
        public static DebugDevConsoleUI Instance { get; private set; }

        // Global toggle used by testing/cheat behaviors (exposed via dev console)
        public static bool TestingCheatsEnabled = false;

        [Header("UI")]
        public GameObject panel;

        [Tooltip("Legacy Unity UI InputField (optional). If using TextMeshPro, assign the TMP Input Field instead.)")]
        public InputField inputFieldLegacy;
        [Tooltip("TextMeshPro Input Field (optional). Preferred when available.")]
        public TMP_InputField inputFieldTMP;

        [Tooltip("Legacy Unity UI Text (optional). If using TextMeshPro, assign the TMP Text instead.")]
        public Text outputTextLegacy;
        [Tooltip("TextMeshPro Text (optional). Preferred when available.")]
        public TMP_Text outputTextTMP;

       
        [Tooltip("Max number of messages to retain in the UI")]
        public int maxMessages = 15;

        [Header("Console")]
        [Tooltip("Optional ScrollRect containing the output text; used to auto-scroll to bottom")]
        public ScrollRect outputScrollRect;
        [Tooltip("Maximum number of past commands stored in history")]
        public int maxHistory = 50;
        public int ACTIVEFLAGS = 0;
        public TMP_Text ActiveFLAGSTextTMP;
        private bool showActiveFlags = false;
        [Header("Backplane")]
        [Tooltip("Optional array of backplane Images - set active by index using SetBackplaneIndex")]
        public Image[] backplaneImages;

        [Header("Tracked Values")]
        [Tooltip("Optional TMP text used to display tracked values (falls back to ActiveFLAGSTextTMP)")]
        public TMP_Text trackedValuesTextTMP;

        [Header("Light Test Panel")]
        [Tooltip("Optional TMP text used for append-only light / darkness debug values")]
        public TMP_Text lightTestTextTMP;
        [Tooltip("Maximum number of light test lines to retain")]
        public int maxLightTestMessages = 12;

        private readonly Dictionary<string, Func<object>> _trackedValues = new Dictionary<string, Func<object>>(StringComparer.OrdinalIgnoreCase);
        public int DebugPanelIndex = 0; // ideally switching with < and > keys.
        [Header("Debug Panels")]
        [Tooltip("Optional explicit array of panels to switch between. If empty the children of `debugPanelsParent` or this GameObject will be used.")]
        public GameObject[] debugPanels;

        [Tooltip("Panel index used for the main console log view.")]
        public int consolePanelIndex = 0;

        [Tooltip("Panel index used for tracked values like AI light and controller state.")]
        public int trackedValuesPanelIndex = 1;

        [Tooltip("Panel index used for append-only light test messages.")]
        public int lightTestPanelIndex = 2;

        [Tooltip("Optional parent transform whose immediate children will be treated as panels when `debugPanels` is empty.")]
        public Transform debugPanelsParent;
        // Simple in-memory command history (newest at end)
        private readonly System.Collections.Generic.List<string> commandHistory = new System.Collections.Generic.List<string>();
        private int historyIndex = -1;

        private readonly Queue<string> _messages = new Queue<string>();
        private readonly Queue<string> _lightMessages = new Queue<string>();
        private readonly Dictionary<string, Action<string[]>> _commands = new Dictionary<string, Action<string[]>>(StringComparer.OrdinalIgnoreCase);
        private IDisposable _registrationToken;
       

        void Awake()
        {
            Instance = this;
            if (panel != null) panel.SetActive(true);
            RegisterDefaultCommands();
            DevConsoleBridge.ApplyBufferedRegistrations(this);
        }

        void OnEnable()
        {
            RegisterDefaultTrackedValues();
            DevConsoleBridge.ApplyBufferedRegistrations(this);

            try
            {
                if (WorldBridgeSystem.Instance != null)
                {
                    _registrationToken = WorldBridgeSystem.Instance.RegisterInvoker("debug.placement_failed", OnPlacementFailed);
                }
            }
            catch { }

            // Re-wire input handlers when the console is enabled (handles toggling the GameObject)
            try
            {
                if (inputFieldTMP != null)
                {
                    inputFieldTMP.onEndEdit.AddListener(OnInputSubmitTMP);
                }
                if (inputFieldLegacy != null)
                {
                    inputFieldLegacy.onEndEdit.AddListener(OnInputEndEditLegacy);
                }
            }
            catch { }

            // Refresh visible text and add a short enabled message so you can confirm it's active
            try { RefreshText(); } catch { }
            try { AddMessage("DevConsole enabled"); } catch { }

            // Auto-focus the input field so the player can type immediately
            try
            {
                if (inputFieldTMP != null)
                {
                    inputFieldTMP.ActivateInputField();
                    inputFieldTMP.Select();
                }
                else if (inputFieldLegacy != null)
                {
                    inputFieldLegacy.ActivateInputField();
                    inputFieldLegacy.Select();
                }
            }
            catch { }
        }

        void OnDisable()
        {
            // Clean up tracked value registration
            UnregisterTrackedValue("Ability_Flash");
            try { _registrationToken?.Dispose(); } catch { }

            try
            {
                if (inputFieldTMP != null) inputFieldTMP.onEndEdit.RemoveListener(OnInputSubmitTMP);
                if (inputFieldLegacy != null) inputFieldLegacy.onEndEdit.RemoveListener(OnInputEndEditLegacy);
            }
            catch { }
        }

        private void OnPlacementFailed(object[] args)
        {
            string text = "Placement failed";
            if (args != null && args.Length > 0 && args[0] != null) text = args[0].ToString();
            AddMessage(text);
        }

        public void AddMessage(string message)
        {
            string ts = DateTime.Now.ToString("HH:mm:ss");
            _messages.Enqueue($"[{ts}] {message}");
            while (_messages.Count > maxMessages) _messages.Dequeue();
            RefreshText();
        }

        /// <summary>
        /// Append a value line to the dedicated light test panel.
        /// This is separate from the main console message stream.
        /// </summary>
        public void AddLightMessage(string message)
        {
            if (string.IsNullOrWhiteSpace(message)) return;

            string ts = DateTime.Now.ToString("HH:mm:ss");
            _lightMessages.Enqueue($"[{ts}] {message}");
            while (_lightMessages.Count > maxLightTestMessages) _lightMessages.Dequeue();
            RefreshLightText();
        }

        public void ClearLightMessages()
        {
            _lightMessages.Clear();
            RefreshLightText();
        }

        private void RefreshText()
        {
            string text = string.Join("\n", _messages.ToArray());
            if (outputTextTMP != null)
            {
                outputTextTMP.text = text;
            }
            else if (outputTextLegacy != null)
            {
                outputTextLegacy.text = text;
            }

            // If output is in a ScrollRect, auto-scroll to bottom to show latest messages
            if (outputScrollRect != null)
            {
                Canvas.ForceUpdateCanvases();
                outputScrollRect.verticalNormalizedPosition = 0f;
            }
        }

        private void RefreshLightText()
        {
            if (lightTestTextTMP == null) return;
            lightTestTextTMP.text = string.Join("\n", _lightMessages.ToArray());
        }

        private void SetTrackedValuesVisible(bool visible)
        {
            showActiveFlags = visible;
            if (!showActiveFlags)
            {
                if (trackedValuesTextTMP != null) trackedValuesTextTMP.text = string.Empty;
                if (ActiveFLAGSTextTMP != null) ActiveFLAGSTextTMP.text = string.Empty;
                return;
            }

            SelectConfiguredPanel(trackedValuesPanelIndex);
            UpdateTrackedValuesDisplay();
        }

        private void SetLightPanelVisible()
        {
            SelectConfiguredPanel(lightTestPanelIndex);
            RefreshLightText();
        }

        private void SetConsolePanelVisible()
        {
            SelectConfiguredPanel(consolePanelIndex);
            RefreshText();
        }

        private void SelectConfiguredPanel(int configuredIndex)
        {
            EnsureBuiltPanels();
            if (debugPanels == null || debugPanels.Length == 0) return;

            int clampedIndex = Mathf.Clamp(configuredIndex, 0, debugPanels.Length - 1);
            SetDebugPanelIndex(clampedIndex);
        }

        // Simple command system (register commands via RegisterCommand)
        public void RegisterCommand(string command, Action<string[]> action)
        {
            _commands[command.ToLower()] = action;
        }

        // Convenience: read current input field value (TMP preferred) and submit it
        public void SubmitCurrentInput()
        {
            string line = null;
            if (inputFieldTMP != null) line = inputFieldTMP.text;
            else if (inputFieldLegacy != null) line = inputFieldLegacy.text;
            if (!string.IsNullOrWhiteSpace(line)) SubmitCommand(line);
        }

        // TMP submit handler — wired to onEndEdit; guard ensures only Enter triggers execution, not focus-loss clicks
        private void OnInputSubmitTMP(string s)
        {
            if (!Input.GetKeyDown(KeyCode.Return) && !Input.GetKeyDown(KeyCode.KeypadEnter)) return;
            if (!string.IsNullOrWhiteSpace(s)) SubmitCommand(s);
        }

        // Legacy InputField end-edit handler
        private void OnInputEndEditLegacy(string s)
        {
            if (!string.IsNullOrWhiteSpace(s)) SubmitCommand(s);
        }

        // Set the text of whichever input field is available and move caret to end
        private void SetInputText(string s)
        {
            if (inputFieldTMP != null)
            {
                inputFieldTMP.text = s ?? string.Empty;
                inputFieldTMP.caretPosition = s?.Length ?? 0;
            }
            else if (inputFieldLegacy != null)
            {
                inputFieldLegacy.text = s ?? string.Empty;
                inputFieldLegacy.caretPosition = s?.Length ?? 0;
            }
        }

        void Update()
        {
            // Keyboard navigation for command history when an input field is focused
            bool tmFocused = inputFieldTMP != null && inputFieldTMP.isFocused;
            bool legacyFocused = inputFieldLegacy != null && inputFieldLegacy.isFocused;
            if (tmFocused || legacyFocused)
            {
                if (Input.GetKeyDown(KeyCode.UpArrow))
                {
                    if (commandHistory.Count > 0)
                    {
                        historyIndex = Mathf.Clamp(historyIndex + 1, 0, commandHistory.Count - 1);
                        string cmd = commandHistory[commandHistory.Count - 1 - historyIndex];
                        SetInputText(cmd);
                    }
                }
                else if (Input.GetKeyDown(KeyCode.DownArrow))
                {
                    if (commandHistory.Count > 0)
                    {
                        historyIndex = Mathf.Clamp(historyIndex - 1, -1, commandHistory.Count - 1);
                        if (historyIndex == -1) SetInputText(string.Empty);
                        else SetInputText(commandHistory[commandHistory.Count - 1 - historyIndex]);
                    }
                }
                else if (Input.GetKeyDown(KeyCode.Escape))
                {
                    SetInputText(string.Empty);
                    if (inputFieldTMP != null) inputFieldTMP.DeactivateInputField();
                    if (inputFieldLegacy != null) inputFieldLegacy.DeactivateInputField();
                }
            }
            // Panel switching via < and > (comma / period keys). Only when an input is not focused.
            if (!tmFocused && !legacyFocused)
            {
                if (Input.GetKeyDown(KeyCode.Comma))
                {
                    PrevDebugPanel();
                }
                else if (Input.GetKeyDown(KeyCode.Period))
                {
                    NextDebugPanel();
                }
            }

            // Refresh tracked-values display if enabled
            UpdateTrackedValuesDisplay();
        }

        private void RegisterDefaultCommands()
        {
            RegisterCommand("clear", args => { _messages.Clear(); SetConsolePanelVisible(); });
            RegisterCommand("lightclear", args => { ClearLightMessages(); SetLightPanelVisible(); AddMessage("Light test panel cleared"); });
            RegisterCommand("help", args => { AddMessage("Available commands: clear, help, last,  (use 'help <cmd>' for details)"); });
            RegisterCommand("last", args => { if (_messages.Count>0) { SetConsolePanelVisible(); AddMessage(_messages.Peek()); } });
            RegisterCommand("flashflag", args => {
                // Usage: flashflag [on|off|toggle|status]
                if (args.Length == 0)
                {
                    AddMessage($"FlashAbility: {ChapterStateService.IsFlashAbilityUnlocked()}");
                    return;
                }
                var op = args[0].ToLower();
                switch (op)
                {
                    case "on":
                        ChapterStateService.Current.SetAbilityFlash(true);
                        AddMessage("FlashAbility set: ON");
                        break;
                    case "off":
                        ChapterStateService.Current.SetAbilityFlash(false);
                        AddMessage("FlashAbility set: OFF");
                        break;
                    case "toggle":
                        bool now = ChapterStateService.Current.ToggleAbilityFlash();
                        AddMessage($"FlashAbility toggled: {now}");
                        break;
                    case "status":
                        AddMessage($"FlashAbility: {ChapterStateService.IsFlashAbilityUnlocked()}");
                        break;
                    default:
                        AddMessage("Usage: flashflag [on|off|toggle|status]");
                        break;
                }
            });
            RegisterCommand("flags", args => { SetTrackedValuesVisible(!showActiveFlags); AddMessage("ShowFlags: " + showActiveFlags); });
            RegisterCommand("lightwatch", args => {
                if (args.Length == 0 || args[0].Equals("toggle", StringComparison.OrdinalIgnoreCase))
                {
                    SetTrackedValuesVisible(!showActiveFlags);
                }
                else if (args[0].Equals("on", StringComparison.OrdinalIgnoreCase))
                {
                    SetTrackedValuesVisible(true);
                }
                else if (args[0].Equals("off", StringComparison.OrdinalIgnoreCase))
                {
                    SetTrackedValuesVisible(false);
                }
                else if (args[0].Equals("status", StringComparison.OrdinalIgnoreCase))
                {
                }
                else
                {
                    AddMessage("Usage: lightwatch [on|off|toggle|status]");
                    return;
                }

                AddMessage("Light watch: " + showActiveFlags);
            });
            RegisterCommand("lightpanel", args => { SetLightPanelVisible(); AddMessage("Light test panel selected"); });
            RegisterCommand("backplane", args => { if (args.Length == 0) { AddMessage("Usage: backplane <index>"); return; } if (int.TryParse(args[0], out var idx)) { SetBackplaneIndex(idx); AddMessage($"Backplane set to {idx}"); } else AddMessage("Invalid index"); });
            RegisterCommand("panel", args => {
                if (args.Length == 0) { AddMessage($"Current panel: {DebugPanelIndex}"); return; }
                if (args[0].Equals("list", StringComparison.OrdinalIgnoreCase))
                {
                    EnsureBuiltPanels();
                    if (debugPanels == null || debugPanels.Length == 0) { AddMessage("No panels available"); return; }
                    for (int i = 0; i < debugPanels.Length; i++) AddMessage($"{i}: {debugPanels[i]?.name}");
                    return;
                }
                if (int.TryParse(args[0], out var pidx)) { SetDebugPanelIndex(pidx); AddMessage($"Panel set to {pidx}"); }
                else AddMessage("Usage: panel <index>|list");
            });
        }

        private void EnsureBuiltPanels()
        {
            if (debugPanels != null && debugPanels.Length > 0) return;
            var list = new List<GameObject>();
            if (debugPanelsParent != null)
            {
                foreach (Transform child in debugPanelsParent)
                {
                    if (child == null) continue;
                    list.Add(child.gameObject);
                }
            }
            else
            {
                foreach (Transform child in transform)
                {
                    if (child == null) continue;
                    list.Add(child.gameObject);
                }
            }
            debugPanels = list.ToArray();
        }

        public void SetDebugPanelIndex(int index)
        {
            EnsureBuiltPanels();
            if (debugPanels == null || debugPanels.Length == 0) return;
            if (index < 0) index = 0;
            if (index >= debugPanels.Length) index = debugPanels.Length - 1;
            for (int i = 0; i < debugPanels.Length; i++)
            {
                var go = debugPanels[i];
                if (go == null) continue;
                try { go.SetActive(i == index); } catch { }
            }
            DebugPanelIndex = index;
        }

        public void NextDebugPanel()
        {
            EnsureBuiltPanels();
            if (debugPanels == null || debugPanels.Length == 0) return;
            SetDebugPanelIndex((DebugPanelIndex + 1) % debugPanels.Length);
        }

        public void PrevDebugPanel()
        {
            EnsureBuiltPanels();
            if (debugPanels == null || debugPanels.Length == 0) return;
            int idx = (DebugPanelIndex - 1) % debugPanels.Length;
            if (idx < 0) idx += debugPanels.Length;
            SetDebugPanelIndex(idx);
        }

        /// <summary>
        /// Activate the backplane image at the given index (disables other backplanes).
        /// Safe if array is null or index out of range.
        /// </summary>
        public void SetBackplaneIndex(int index)
        {
            if (backplaneImages == null || backplaneImages.Length == 0) return;
            for (int i = 0; i < backplaneImages.Length; i++)
            {
                var img = backplaneImages[i];
                if (img == null) continue;
                try { img.gameObject.SetActive(i == index); } catch { }
            }
        }

        /// <summary>
        /// Register a runtime-tracked value that will be displayed when flags are shown.
        /// The getter may return any object; its type name and ToString() will be shown.
        /// </summary>
        public void RegisterTrackedValue(string name, Func<object> getter)
        {
            if (string.IsNullOrWhiteSpace(name) || getter == null) return;
            _trackedValues[name] = getter;
        }

        /// <summary>
        /// Unregister a previously registered tracked value.
        /// </summary>
        public bool UnregisterTrackedValue(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return false;
            return _trackedValues.Remove(name);
        }

        private void UpdateTrackedValuesDisplay()
        {
            if (!showActiveFlags) return;
            if ((trackedValuesTextTMP == null) && (ActiveFLAGSTextTMP == null)) return;
            if (_trackedValues.Count == 0)
            {
                const string emptyText = "<no tracked values registered>";
                if (trackedValuesTextTMP != null) trackedValuesTextTMP.text = emptyText;
                if (ActiveFLAGSTextTMP != null) ActiveFLAGSTextTMP.text = emptyText;
                return;
            }

            var lines = new List<string>();
            foreach (var kv in _trackedValues)
            {
                object val = null;
                try { val = kv.Value.Invoke(); } catch { val = "<error>"; }
                string typeName = val?.GetType().Name ?? "null";
                string str = val?.ToString() ?? "null";
                lines.Add($"{kv.Key}: {str} ({typeName})");
            }
            string text = string.Join("\n", lines);
            if (trackedValuesTextTMP != null) trackedValuesTextTMP.text = text;
            if (ActiveFLAGSTextTMP != null) ActiveFLAGSTextTMP.text = text;
        }

        private void RegisterDefaultTrackedValues()
        {
            RegisterTrackedValue("Ability_Flash", () => ChapterStateService.IsFlashAbilityUnlocked());
        }
          
        private string GetTransformPath(Transform transform, Transform root)
        {
            if (transform == null) return "null";
            if (transform == root) return root.name;

            var path = transform.name;
            var current = transform.parent;
            while (current != null && current != root)
            {
                path = current.name + "/" + path;
                current = current.parent;
            }
            return root.name + "/" + path;
        }

        // Hooked to UI submit button, InputField OnEndEdit, or called directly
        public void SubmitCommand(string line)
        {
            if (string.IsNullOrWhiteSpace(line)) return;

            // Add to history (keep newest at end)
            try
            {
                commandHistory.Add(line);
                if (commandHistory.Count > maxHistory) commandHistory.RemoveAt(0);
                historyIndex = -1;
            }
            catch { }

            string[] parts = line.Split(' ');
            string cmd = parts[0].ToLower();
            string[] args = new string[Math.Max(0, parts.Length - 1)];
            Array.Copy(parts, 1, args, 0, args.Length);
            AddMessage($"> {line}");
            if (_commands.TryGetValue(cmd, out var act))
            {
                try { act.Invoke(args); }
                catch (Exception ex) { AddMessage("Command error: " + ex.Message); }
            }
            else
            {
                AddMessage("Unknown command: " + cmd);
            }

            // Clear and refocus whichever input field is available
            if (inputFieldTMP != null)
            {
                inputFieldTMP.text = string.Empty;
                inputFieldTMP.ActivateInputField();
            }
            else if (inputFieldLegacy != null)
            {
                inputFieldLegacy.text = string.Empty;
                inputFieldLegacy.ActivateInputField();
            }
        }
    }
}
