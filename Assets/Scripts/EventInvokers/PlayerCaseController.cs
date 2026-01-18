using System.Collections;
using System.Collections.Generic;
using System;
using UnityEngine;
using FracturedStudios.Data;
// Then reference it as DynamicObjectManager.Instance
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace FracturedStudios.Invoker
{
    /// <summary>
    /// Lambda based array pusher/poper for player cases.
    /// Manages player state cases (modes) such as Combat, Dialogue, UI, Movement, etc.
    /// Uses a stack to allow layered cases with priorities.
    /// Each case can have multiple update actions registered.
    /// Invoke actions and manage cases via PushCase/PopCase.
    /// quick anonymous action registration with IDisposable token.
    /// disposable token removes single action from case when disposed.
    /// </summary>

public class PlayerCaseController : MonoBehaviour

{
    public enum PlayerCase
    {
        None = 0, // no case active
        Custom = 1, // custom case for anything
        Combat = 2, // in combat mode
        Quest = 3, // quest-related actions
        Interaction = 4, // switch case for interacting with objects
        UI = 5,     // menus, inventory
        Dialogue = 6,            // player in conversation
        Movement = 7,         // standard walking/running
        Dead = 8,             // player death state
        Respawn = 9,          // transitioning from death
        Cinematic = 10,        // camera-controlled scene
        Mounted = 11,          // on mount or vehicle
        Stealth = 12,          // stealth mode
        SecondaryAction = 13,   // using secondary 
        PrimaryAction = 14,     // using primary 
        Method = 15,             // lightweight one-off / lambda case (used by InvokeMethod/InvokeUpdate)
        Aim = 16,
        Pause = 17,
        Standby = 18            // neutral state: weapon shop, menus, dialogue standby (no input allowed)
    }
    ///More for token based use and string based case popping or default layer assignment
    public enum CaseLayer
{
    Func,       // Functions for lambdas in updates quick calls
    Overlay,    // Add-ons 
    Blocking,   // Disables 
    Custom,     // Custom user-defined cases
    SecondaryAction, // Custom user-defined cases
    PrimaryAction // Custom user-defined cases
}
 

#if UNITY_EDITOR
[SerializeField, Tooltip("Currently active player case (read-only)")]
private PlayerCase debugActiveCase;
#endif
    public static PlayerCaseController Instance;
    public CaseLayer caseLayer = CaseLayer.Custom;
    private PlayerCase activeCase = PlayerCase.None;
    private readonly Stack<PlayerCase> caseStack = new Stack<PlayerCase>();
    // quick membership test to avoid O(n) Stack.Contains
    private readonly HashSet<PlayerCase> caseSet = new HashSet<PlayerCase>();
      // expose the case
    //private readonly Action action;
    // Each case can have multiple Actions assigned
    private readonly Dictionary<PlayerCase, List<Action>> caseActions = new Dictionary<PlayerCase, List<Action>>();

    // Optional timed auto-pop
    private readonly Dictionary<PlayerCase, float> caseTimers = new Dictionary<PlayerCase, float>();

    [Header("LoopBuffer")]
    [Tooltip("Enable/disable LoopBuffer execution at runtime.")]
    [SerializeField] private bool enableLoopBuffer = true;

    //public event Action<PlayerCase> OnCasePushed;
    //public event Action<PlayerCase> OnCasePopped;
    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(this);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(this);
    }
    private void Update()
    {
        // tick timers first so expired cases are removed before running update callbacks
        TickCaseTimers();
        
        //LoopBuffer();
        // Only invoke the top-most case
        if (caseStack.Count > 0)
        {
            activeCase = caseStack.Peek();
            RunCase(activeCase);
        }
        else
        {
            activeCase = PlayerCase.None;
        }
        #if UNITY_EDITOR
        debugActiveCase = activeCase;
        #endif
    }

    private void RunCase(PlayerCase currentCase)
    {
        if (caseActions.TryGetValue(currentCase, out var list) && list != null && list.Count > 0)
        {
            // iterate over a copy to avoid collection-modified exceptions
            var actions = list.ToArray();
            foreach (var action in actions)
                action?.Invoke();
        }
    }
    /// <summary>
    /// Push a one-shot method/lambda that will run on the next Update and then remove itself.
    /// Returns IDisposable token (you can Dispose early to cancel).
    /// </summary>
    public IDisposable InvokeMethod(Action onUpdate)
    {
        if (onUpdate == null) return null;

        Action wrapper = null;
        wrapper = () =>
        {
            try { onUpdate(); }
            finally
            {
                // remove wrapper and pop the Method case if no more actions remain
                UnregisterCaseAction(PlayerCase.Method, wrapper);
                // safe to call PopCase even if other Method actions are present;
                // PopCase will only remove the case if it's present in the stack.
                PopCase(PlayerCase.Method);
            }
        };

        // Ensure the case exists and register wrapper; caller can still Dispose the returned token.
        return PushCase(PlayerCase.Method, wrapper);
    }

    /// <summary>
    /// Push a method that will run repeatedly for 'duration' seconds (0 = until explicitly popped).
    /// </summary>
    public IDisposable InvokeUpdate(Action onUpdate, float durationSeconds)
    {
        if (onUpdate == null) return null;
        return PushCase(PlayerCase.Method, onUpdate, durationSeconds);
    }

    /// <summary>
    /// Push a new case with optional actions and optional duration
    /// Returns IDisposable token you can Dispose() to remove the added action (if any).
    /// </summary>
    public IDisposable PushCase(PlayerCase newCase, Action onUpdate = null, float duration = 0f)
    {
        if (caseSet.Add(newCase))
        {
            caseStack.Push(newCase);
        }

        if (!caseActions.TryGetValue(newCase, out var list))
        {
            list = new List<Action>();
            caseActions[newCase] = list;
        }

        if (onUpdate != null)
            list.Add(onUpdate);

        if (duration > 0f)
            caseTimers[newCase] = duration;

        // return a simple token to remove the single action if caller provided one
        if (onUpdate != null)
            return new ActionToken(this, newCase, onUpdate);

        return null;
    }

    // helper to register/unregister single actions without affecting other actions or the case stack
    public void RegisterCaseAction(PlayerCase c, Action action)
    {
        if (!caseActions.TryGetValue(c, out var list))
        {
            list = new List<Action>();
            caseActions[c] = list;
        }
        list.Add(action);
    }

    public void UnregisterCaseAction(PlayerCase c, Action action)
    {
        if (caseActions.TryGetValue(c, out var list))
        {
            list.RemoveAll(a => a == action);
            if (list.Count == 0)
                caseActions.Remove(c);
        }
    }

    /// <summary>
/// Iterates through all active cases and safely invokes their actions.
/// Automatically cleans up disposed or invalid actions.
/// </summary>

private static readonly List<PlayerCase> _loopCaseBuffer = new List<PlayerCase>(16);
private static readonly List<Action> _loopActionBuffer = new List<Action>(16);

private bool LoopBuffer()
{
    // Short-hand for looping active case stack without allocations.
    if (!enableLoopBuffer)
        return false;

    if (caseStack.Count == 0)
        return false;

    // Copy current stack snapshot (avoid modifying while iterating)
    _loopCaseBuffer.Clear();
    _loopCaseBuffer.AddRange(caseStack);

    bool poppedAny = false;

    foreach (var currentCase in _loopCaseBuffer)
    {
        if (caseActions.TryGetValue(currentCase, out var actions) && actions != null && actions.Count > 0)
        {
            // Copy actions to buffer
            _loopActionBuffer.Clear();
            _loopActionBuffer.AddRange(actions);

            // Safe execution loop
            foreach (var action in _loopActionBuffer)
            {
                try
                {
                    action?.Invoke();
                }
                catch (Exception ex)
                {
                    Debug.LogError($"[PlayerCaseController] Exception in {currentCase} action: {ex}");
                    // remove failing action to avoid spamming
                    UnregisterCaseAction(currentCase, action);
                }
            }
        }

        // safety cleanup: if all actions for this case got removed mid-run
        if (!caseActions.TryGetValue(currentCase, out var list) || list.Count == 0)
        {
            PopCase(currentCase);
            poppedAny = true;
        }
    }

    return poppedAny;
}
    // Buffers are reused every frame — no GC allocation, no recursion


    // Ticks all timers and pops expired cases (safe: operates on a snapshot)
    private void TickCaseTimers()
    {
        if (caseTimers.Count == 0) return;

        var keys = new List<PlayerCase>(caseTimers.Keys); // snapshot of keys
        var expired = new List<PlayerCase>();

        foreach (var k in keys)
        {
            float remaining = caseTimers[k] - Time.deltaTime;
            if (remaining <= 0f)
                expired.Add(k);
            else
                caseTimers[k] = remaining;
        }

        foreach (var k in expired)
            PopCase(k);
    }

        public bool PopCaseOfType(string caseName)
    {
        if (string.IsNullOrEmpty(caseName)) return false;
        if (Enum.TryParse<PlayerCase>(caseName, true, out var parsed))
        {
            PopCase(parsed);
            return true;
        }
        return false;
    }

    /// <summary>
    /// Remove a case from the stack
    /// </summary>
    public void PopCase(PlayerCase caseToRemove)
    {
        if (!caseSet.Contains(caseToRemove)) return;

        var tempStack = new Stack<PlayerCase>();
        // Pop until we remove the target
        while (caseStack.Count > 0)
        {
            var top = caseStack.Pop();
            if (top == caseToRemove) break;
            tempStack.Push(top);
        }
        // Put back the remaining
        while (tempStack.Count > 0)
            caseStack.Push(tempStack.Pop());

        // Cleanup
        caseSet.Remove(caseToRemove);
        caseActions.Remove(caseToRemove);
        caseTimers.Remove(caseToRemove);

        // update activeCase to reflect new top
        activeCase = caseStack.Count > 0 ? caseStack.Peek() : PlayerCase.None;
    }

    /// <summary>
    /// Utility to remove all common combat-related cases.
    /// This is intended to be used by systems that need to force-exit combat/aim states
    /// (for example when unequipping weapons or entering a locked context).
    /// </summary>
    public void CleanupAllCombatCases()
    {
        // Define the set of cases we consider "combat-related".
        var combatCases = new[]
        {
            PlayerCase.Combat,
            PlayerCase.Aim,
            PlayerCase.PrimaryAction,
            PlayerCase.SecondaryAction
        };

        foreach (var c in combatCases)
        {
            if (caseSet.Contains(c))
                PopCase(c);
        }
    }

    public void ResetCases()
    {
        caseStack.Clear();
        caseSet.Clear();
        caseActions.Clear();
        caseTimers.Clear();
        activeCase = PlayerCase.None;
    }

    public PlayerCase GetCurrentCase() => activeCase;
    public PlayerCase PeekCase() => caseStack.Count > 0 ? caseStack.Peek() : PlayerCase.None;
    public bool HasCase(PlayerCase c) => caseSet.Contains(c);

        /// <summary>
        /// Check if a given case blocks weapon input (firing/reloading).
        /// Returns true if the case is a blocking state.
        /// </summary>
        public static bool IsBlockingCase(PlayerCase c)
        {
            return c switch
            {
                           
                PlayerCase.Dead => true,            // Dead = can't use weapon
                PlayerCase.Dialogue => true,        // Dialogue = conversation
                PlayerCase.UI => true,              // UI = menu/inventory
                PlayerCase.Cinematic => true,       // Cinematic = scene control
                PlayerCase.Pause => true,           // Pause = paused
                PlayerCase.Standby => true,         // Standby = no input (shop, lockout)
                _ => false                          // All other cases allow weapon use
            };
        }
        /// <summary>
        /// helper method for isblockingcase method
        /// </summary>
        /// <returns></returns>
        public static bool IsAnyActiveCaseBlocking()
        {
            foreach (var c in Instance.caseStack) // Access the private stack via reflection or expose it
            {
                if (IsBlockingCase(c)) return true;
            }
            return false;
        }

        /// <summary>
        ///  EXAMPLE Check if any of Combat, Quest, or Interaction are active before pushing Cinematic
        /// 
        //  if (!PlayerCaseController.AreAnyCasesActive(PlayerCase.Combat, PlayerCase.Quest, PlayerCase.Interaction))
        //  {
        //    PlayerCaseController.Instance.PushCase(PlayerCase.Cinematic);
        //  }
        ///  EXAMPLE Second if statement is a guard clause that exits early if any cases are active bail
        /// 
        //  if (PlayerCaseController.AreAnyCasesActive(PlayerCase.Combat, PlayerCase.Quest, PlayerCase.Interaction))
        //  {
        //    return; 
        //  }
        /// </summary>
        /// <param name="cases"></param>
        /// <returns></returns>
        public static bool AreAnyCasesActive(params PlayerCase[] cases)
        {
            foreach (var c in cases)
            {
                if (Instance.HasCase(c)) return true;
            }
            return false;
        }
    
    /// <summary>
    /// Check if a given case blocks camera input/switching.
    /// Returns true if the case locks camera to current mode.
    /// </summary>
    public static bool IsBlockingCameraSwitch(PlayerCase c)
    {
        return c switch
        {
            PlayerCase.Standby => true,         // Standby = locked to target socket
            PlayerCase.Cinematic => true,       // Cinematic = fixed camera control
            PlayerCase.UI => true,              // UI = fixed view
            _ => false                          // All other cases allow camera switching
        };
    }

    // small disposable token to remove a single action added via PushCase
    public class ActionToken : IDisposable
    {
        private readonly PlayerCaseController owner;
        //public for GetComponent cases public PlayerCase c;
       
        private readonly Action action;
        private bool disposed;
        public PlayerCase Case { get; }  
        public ActionToken(PlayerCaseController owner, PlayerCase c, Action action)
        {
            if (owner == null)
            throw new ArgumentNullException(nameof(owner));
          
            if (!Enum.IsDefined(typeof(PlayerCaseController.PlayerCase), c))
            throw new ArgumentException("Invalid PlayerCase provided.", nameof(c));
            

            this.owner = owner ?? throw new ArgumentNullException(nameof(owner));
            this.Case = c; 
    
            this.action = action;
        }

        public void Dispose()
        {
            if (disposed) return;
            owner.UnregisterCaseAction(Case, action);
            ///remove the case if no more actions remain
                        if (!owner.caseActions.TryGetValue(Case, out var list) || list.Count == 0)
                owner.PopCase(Case);
            disposed = true;
        }

        public override string ToString() => Case.ToString(); 
    }

    public class ReadOnlyAttribute : PropertyAttribute { }
#region GUIEditor
#if UNITY_EDITOR
[CustomPropertyDrawer(typeof(ReadOnlyAttribute))]
public class ReadOnlyDrawer : PropertyDrawer
{
    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        GUI.enabled = false;
        EditorGUI.PropertyField(position, property, label);
        GUI.enabled = true;
    }
}
#endif
}
#if UNITY_EDITOR
    [CustomEditor(typeof(PlayerCaseController))]
    public class PlayerCaseControllerEditor : Editor
    {
        private GUIStyle headerStyle;
        private GUIStyle caseStyle;
        private GUIStyle faded;

        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();
            var ctrl = (PlayerCaseController)target;

            EditorGUILayout.Space(10);
            EditorGUILayout.LabelField("Runtime Debug View", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("Shows current player cases and timers during Play Mode.", MessageType.Info);

            if (!Application.isPlaying)
            {
                EditorGUILayout.LabelField("▶ Enter Play Mode to view active cases.", EditorStyles.miniLabel);
                return;
            }

            headerStyle ??= new GUIStyle(EditorStyles.boldLabel) { fontSize = 11 };
            caseStyle ??= new GUIStyle(EditorStyles.label)
            {
                fontSize = 10,
                normal = { textColor = Color.cyan }
            };
            faded ??= new GUIStyle(EditorStyles.miniLabel)
            {
                fontSize = 9,
                normal = { textColor = Color.gray }
            };

            var stack = typeof(PlayerCaseController)
                .GetField("caseStack", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                ?.GetValue(ctrl) as Stack<PlayerCaseController.PlayerCase>;

            var timers = typeof(PlayerCaseController)
                .GetField("caseTimers", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                ?.GetValue(ctrl) as Dictionary<PlayerCaseController.PlayerCase, float>;

            var actions = typeof(PlayerCaseController)
                .GetField("caseActions", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                ?.GetValue(ctrl) as Dictionary<PlayerCaseController.PlayerCase, List<Action>>;

            if (stack == null || stack.Count == 0)
            {
                EditorGUILayout.LabelField("No active cases.", faded);
                return;
            }

            EditorGUILayout.Space(5);
            EditorGUILayout.LabelField("Active Stack (Top → Bottom)", headerStyle);

            foreach (var c in stack)
            {
                float timer = timers != null && timers.TryGetValue(c, out var t) ? t : -1f;
                string timerText = timer > 0f ? $"⏱ {timer:F1}s" : "";

                var list = (actions != null && actions.TryGetValue(c, out var aList)) ? aList : null;
                int count = list?.Count ?? 0;

                Color prev = GUI.color;
                if (timer > 0f && timer < 2f)
                    GUI.color = Color.Lerp(Color.yellow, Color.red, (2f - timer) / 2f);

                EditorGUILayout.BeginVertical("box");
                EditorGUILayout.LabelField($"• {c} ({count} actions) {timerText}", caseStyle);

                if (list != null && list.Count > 0)
                {
                    EditorGUI.indentLevel++;
                    foreach (var a in list)
                        EditorGUILayout.LabelField($"↳ {a.Method.Name}", faded);
                    EditorGUI.indentLevel--;
                }
                EditorGUILayout.EndVertical();

                GUI.color = prev;
            }
        }
    }
    [UnityEditor.InitializeOnLoad]
    public static class PlayerCaseSceneGUI
    {
        static PlayerCaseSceneGUI()
        {
            SceneView.duringSceneGui += OnSceneGUI;
        }

        private static void OnSceneGUI(SceneView sceneView)
        {
            if (!Application.isPlaying) return;
            var ctrl = GameObject.FindObjectOfType<PlayerCaseController>();
            if (ctrl == null) return;

            Handles.BeginGUI();
            GUILayout.BeginArea(new Rect(15, 15, 280, 300), "Player Cases", GUI.skin.window);

            GUILayout.Label($"Active Case: {ctrl.GetCurrentCase()}", EditorStyles.boldLabel);
            GUILayout.Space(5);

            foreach (var c in Enum.GetValues(typeof(PlayerCaseController.PlayerCase)))
            {
                var caseEnum = (PlayerCaseController.PlayerCase)c;
                if (ctrl.HasCase(caseEnum))
                    GUILayout.Label($"• {caseEnum}", EditorStyles.label);
            }

            GUILayout.EndArea();
            Handles.EndGUI();
        }
    }
#endif

#endregion
}