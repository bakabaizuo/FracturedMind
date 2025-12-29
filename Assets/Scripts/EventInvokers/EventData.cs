using UnityEngine;
using FracturedStudios.Data;
using FracturedStudios.Invoker;
using System;
using System.Collections.Generic;
using UnityEngine.Events;

namespace FracturedStudios.Containers
{
    /// <summary>
    /// Central repository for all event key constants used throughout the event system.
    /// This eliminates magic strings and makes refactoring easier.
    /// 
    /// Usage:
    ///     EventData.SubscribeTo(EventKeys.Player.SPAWNED, args => {...});
    ///     string healthKey = EventKeys.GetHealthKey("npc_bandit_01");
    /// </summary>
    public static class EventKeys
    {
        #region === Generic Health/Death Events (Prefix-based) ===
        public const string HEALTH_CHANGED = "_hp_changed";
        public const string DAMAGED = "_damaged";
        public const string HEALED = "_healed";
        public const string DEATH_STATE_CHANGED = "_death_state_changed";
        public const string DIED = "_died";
        public const string REVIVED = "_revived";
        #endregion

        #region === Player Events (Global) ===
        public static class Player
        {
            public const string SPAWNED = "player_spawned";
            public const string RESTORED = "player_restored";
            public const string LEVEL_UP = "player_levelup";
            public const string SCENE_CHANGED = "player_scene_changed";
            public const string XP_GAINED = "player_xp_gained";


        }
        #endregion

        #region === NPC Events (Prefix-based) ===
        public static class NPC
        {
            public const string BEHAVIOR_CHANGED = "_behavior_changed";

        }
        #endregion

        #region === Weapon Events (Prefix-based) ===

        #endregion

        #region === Scene/World Events (Global) ===
        public static class Scene
        {
            public const string CELL_CHANGED = "scene_cell_changed";
            public const string LOADED = "scene_loaded";
            public const string UNLOADED = "scene_unloaded";
        }
        #endregion

        #region === Helper Methods for Prefix-Based Keys ===
       
        /// <summary>Get health changed key for a prefixed entity (e.g., "npc_bandit_01")</summary>
       
        public static string GetHealthKey(string prefix) => $"{prefix}{HEALTH_CHANGED}";
        
         /// <summary>Get damaged key for a prefixed entity</summary>
        
        public static string GetDamagedKey(string prefix) => $"{prefix}{DAMAGED}";
       
       
       /// <summary>Get healed key for a prefixed entity</summary>
        public static string GetHealedKey(string prefix) => $"{prefix}{HEALED}";
       
       
       /// <summary>Get death state changed key for a prefixed entity</summary>
        public static string GetDeathStateKey(string prefix) => $"{prefix}{DEATH_STATE_CHANGED}";
      
      
      /// <summary>Get died key for a prefixed entity</summary>
        public static string GetDiedKey(string prefix) => $"{prefix}{DIED}";
       
       
       /// <summary>Get revived key for a prefixed entity</summary>
        public static string GetRevivedKey(string prefix) => $"{prefix}{REVIVED}";
       
       
       /// <summary>Get behavior changed key for NPC</summary>
        public static string GetBehaviorChangedKey(string npcPrefix) => $"{npcPrefix}{NPC.BEHAVIOR_CHANGED}";
       

        #endregion
    }

    public class EventData
    {
        public enum EventBroadcastMode
        {
            None,           // No broadcasting
            LocalOnly,      // Local events only (C# events)
            WorldBridge,    // Via WorldBridgeSystem only
            Both            // Local + WorldBridge (recommended)
        }

    private readonly DataContainerBase _container;
        private readonly string _eventPrefix;
        private readonly EventBroadcastMode _broadcastMode;

        private int _lastHP;
        private int _lastMaxHP;
        private DeathState _lastDeathState;

        // =========== Local C# Events ===========
        public event Action<int, int> OnHealthChanged;
        public event Action<int, int, int> OnTakeDamage;
        public event Action<int, int, int> OnHeal;
        public event Action<DeathState, DeathState> OnDeathStateChanged;
        public event Action<int> OnDeath;
        public event Action OnRevived;
        public event Action<string> OnPlayerSpawned;
        public event Action<string> OnPlayerRestored;
        public event Action<string> OnSceneChanged;
        public event Action<int> OnLevelUp;
        public event Action<int> OnXPGained;

        // Placeholder invocations to prevent CS0067 'event never used' warnings.
        // This method is intentionally not called during normal execution.
        // It references the events so the compiler treats them as used.
        private void InvokePlaceholder()
        {
            // Use safe null-conditional invokes with simple placeholder values.
            OnPlayerSpawned?.Invoke(string.Empty);
            OnPlayerRestored?.Invoke(string.Empty);
            OnSceneChanged?.Invoke(string.Empty);
            OnLevelUp?.Invoke(0);
            OnXPGained?.Invoke(0);
        }

        // =========== Stat Change Events ===========
        public event Action<string, int, int> OnStatChanged; // statName, oldValue, newValue
        public event Action<string, float, float> OnDerivedStatChanged; // statName, oldValue, newValue

        // =========== Constructor ===========
    public EventData(
        DataContainerBase container,
                string eventPrefix = null,
                EventBroadcastMode broadcastMode = EventBroadcastMode.Both)
        {
            if (container == null)
                throw new ArgumentNullException(nameof(container));

            _container = container;

            // Generate unique prefix if not provided
            if (string.IsNullOrEmpty(eventPrefix))
            {
                var npc = container as NPCData;
                var player = container as PlayerData;

                if (npc != null)
                    eventPrefix = $"npc_{npc.displayName.ToLower().Replace(' ', '_')}";
                else if (player != null)
                    eventPrefix = $"player_{player.displayName.ToLower().Replace(' ', '_')}";
                else
                    eventPrefix = $"{container.GetType().Name.ToLower()}";
            }

            _eventPrefix = eventPrefix;
            _broadcastMode = broadcastMode;

            // Cache initial state
            _lastHP = GetCurrentHP();
            _lastMaxHP = GetMaxHP();
            _lastDeathState = GetCurrentDeathState();

            Debug.Log($"[EventData] Created wrapper for '{_eventPrefix}' (mode: {broadcastMode})");
        }

        /// <summary>
        /// Call this every frame or when you need to check for changes.
        /// Automatically fires events if values changed since last call.
        /// </summary>
        public void UpdateEvents()
        {
            if (_container == null) return;

            CheckHealthChanged();
            CheckDeathStateChanged();
        }

        /// <summary>
        /// Manually trigger a health change event (useful for forced updates)
        /// </summary>
        public void ForceHealthUpdate()
        {
            _lastHP = -1; // Force mismatch on next UpdateEvents()
            UpdateEvents();
        }

        /// <summary>
        /// Manually trigger a death state change event
        /// </summary>
        public void ForceDeathStateUpdate()
        {
            _lastDeathState = (DeathState)(-1); // Force mismatch
            UpdateEvents();
        }

        private void CheckHealthChanged()
        {
            int currentHP = GetCurrentHP();
            int maxHP = GetMaxHP();

            if (currentHP != _lastHP || maxHP != _lastMaxHP)
            {
                int delta = currentHP - _lastHP;

                if (delta > 0)
                {
                    // Healing
                    OnHeal?.Invoke(delta, currentHP, maxHP);
                    BroadcastEvent($"{_eventPrefix}_healed", new object[] { delta, currentHP, maxHP });
                }
                else if (delta < 0)
                {
                    // Taking damage
                    OnTakeDamage?.Invoke(-delta, currentHP, maxHP);
                    BroadcastEvent($"{_eventPrefix}_damaged", new object[] { -delta, currentHP, maxHP });
                }

                OnHealthChanged?.Invoke(currentHP, maxHP);
                BroadcastEvent($"{_eventPrefix}_hp_changed", new object[] { currentHP, maxHP });

                _lastHP = currentHP;
                _lastMaxHP = maxHP;
            }
        }



        private void CheckDeathStateChanged()
        {
            DeathState currentState = GetCurrentDeathState();

            if (currentState != _lastDeathState)
            {
                OnDeathStateChanged?.Invoke(currentState, _lastDeathState);
                BroadcastEvent(
                    EventKeys.GetDeathStateKey(_eventPrefix),
                    new object[] { currentState.ToString(), _lastDeathState.ToString() }
                );

                if (currentState != DeathState.Alive && _lastDeathState == DeathState.Alive)
                {
                    OnDeath?.Invoke(GetCurrentHP());
                    BroadcastEvent(EventKeys.GetDiedKey(_eventPrefix), new object[] { GetCurrentHP(), currentState.ToString() });
                }
                else if (currentState == DeathState.Alive && _lastDeathState != DeathState.Alive)
                {
                    OnRevived?.Invoke();
                    BroadcastEvent(EventKeys.GetRevivedKey(_eventPrefix), new object[] { GetCurrentHP() });
                }

                _lastDeathState = currentState;

            }
        }

        /// <summary>Register all event keys this container will broadcast</summary>
        public virtual void RegisterWithWorldBridge()
        {
            if (WorldBridgeSystem.Instance == null)
            {
                Debug.LogWarning($"[EventData] WorldBridgeSystem not initialized!");
                return;
            }

            var bridge = WorldBridgeSystem.Instance;
            var invoker = DynamicDictionaryInvoker.Instance;

            if (invoker == null)
            {
                Debug.LogWarning($"[EventData] DynamicDictionaryInvoker not found!");
                return;
            }

            // Register all event keys using constants
            string[] eventKeys = new[]
            {
        EventKeys.GetHealthKey(_eventPrefix),
        EventKeys.GetDamagedKey(_eventPrefix),
        EventKeys.GetHealedKey(_eventPrefix),
        EventKeys.GetDeathStateKey(_eventPrefix),
        EventKeys.GetDiedKey(_eventPrefix),
        EventKeys.GetRevivedKey(_eventPrefix)
    };

            foreach (var key in eventKeys)
            {
                if (!bridge.HasInvoker(key))
                {
                    bridge.RegisterInvoker(key, (args) => { }, DynamicDictionaryInvoker.Layer.Overlay);
                }
            }

            Debug.Log($"[EventData] Registered '{_eventPrefix}' with {eventKeys.Length} broadcast events");
        }

        /// <summary>Broadcast a stat change event</summary>
        public void BroadcastStatChanged(string statName, int oldValue, int newValue)
        {
            OnStatChanged?.Invoke(statName, oldValue, newValue);
            BroadcastEvent($"{_eventPrefix}_stat_changed", new object[] { statName, oldValue, newValue });
        }

        /// <summary>Broadcast a derived stat change event</summary>
        public void BroadcastDerivedStatChanged(string statName, float oldValue, float newValue)
        {
            OnDerivedStatChanged?.Invoke(statName, oldValue, newValue);
            BroadcastEvent($"{_eventPrefix}_derived_stat_changed", new object[] { statName, oldValue, newValue });
        }


        /// <summary>
        /// Subscribe to this container's events via WorldBridgeSystem.
        /// Example: EventDataContainer.SubscribeTo("npc_bandit_01_hp_changed", (args) => {...})
        /// </summary>
        public static IDisposable SubscribeTo(
            string eventKey,
            Action<object[]> callback,
            DynamicDictionaryInvoker.Layer layer = DynamicDictionaryInvoker.Layer.Overlay)
        {
            var bridge = WorldBridgeSystem.Instance;
            if (bridge != null)
            {
                return bridge.RegisterInvoker(eventKey, callback, layer);
            }
            return null;
        }
/// <summary>
/// Broadcast an event through the WorldBridge system based on the current broadcast mode
/// </summary>
protected void BroadcastEvent(string eventKey, object[] args)
{
    if (_broadcastMode == EventBroadcastMode.None)
        return;

    if ((_broadcastMode == EventBroadcastMode.WorldBridge || _broadcastMode == EventBroadcastMode.Both))
    {
        var bridge = WorldBridgeSystem.Instance;
        if (bridge != null)
        {
            bridge.InvokeKey(eventKey, args);
        }
    }
}

        // ======== Minimal accessors to adapt to current project containers ========
        private int GetCurrentHP()
        {
            if (_container is NPCData npc) return npc.currentHP;
            if (_container is PlayerData player) return player.currentHP;
            return 0;
        }

        private int GetMaxHP()
        {
            if (_container is NPCData npc) return npc.maxHP;
            if (_container is PlayerData player) return player.maxHP;
            return 0;
        }

        private DeathState GetCurrentDeathState()
        {
            if (_container is NPCData npc) return npc.CurrentDeathState;
            if (_container is PlayerData player) return player.CurrentDeathState;
            return DeathState.Alive;
        }


        // =========== Getters ===========
        public string EventPrefix => _eventPrefix;
        public DataContainerBase Container => _container;
        public int LastHP => _lastHP;
        public int LastMaxHP => _lastMaxHP;
        public DeathState LastDeathState => _lastDeathState;
    }
    public class PlayerEventData : EventData
    {
        private PlayerData _playerData;

        public PlayerEventData(PlayerData player, string prefix = null)
            : base(player, prefix, EventBroadcastMode.Both)
        {
            _playerData = player;
        }

        public void BroadcastSpawned(string spawnId)
        {
            BroadcastEvent(EventKeys.Player.SPAWNED, new object[] { spawnId });
        }

        public void BroadcastLevelUp(int newLevel)
        {
            BroadcastEvent(EventKeys.Player.LEVEL_UP, new object[] { newLevel });
        }

        public void BroadcastRestored(string scene)
        {
            BroadcastEvent(EventKeys.Player.RESTORED, new object[] { scene });
        }

        public void BroadcastSceneChanged(string newScene)
        {
            BroadcastEvent(EventKeys.Player.SCENE_CHANGED, new object[] { newScene });
        }

        public void BroadcastXPGained(int xpAmount)
        {
            BroadcastEvent(EventKeys.Player.XP_GAINED, new object[] { xpAmount });
        }

        public override void RegisterWithWorldBridge()
        {
            base.RegisterWithWorldBridge();

            if (WorldBridgeSystem.Instance == null) return;

            var bridge = WorldBridgeSystem.Instance;
            string[] playerEventKeys = new[]
            {
            EventKeys.Player.SPAWNED,
            EventKeys.Player.LEVEL_UP,
            EventKeys.Player.RESTORED,
            EventKeys.Player.SCENE_CHANGED,
            EventKeys.Player.XP_GAINED,

        };

            foreach (var key in playerEventKeys)
            {
                if (!bridge.HasInvoker(key))
                {
                    bridge.RegisterInvoker(key, (args) => { }, DynamicDictionaryInvoker.Layer.Overlay);
                }
            }
        }
    }

    public class NPCEventData : EventData
    {
        private NPCData _npcData;

        public NPCEventData(NPCData npc, string prefix = null)
            : base(npc, prefix, EventBroadcastMode.Both)
        {
            _npcData = npc;
        }

        public void BroadcastBehaviorChanged(string newBehavior)
        {
            BroadcastEvent(EventKeys.GetBehaviorChangedKey(EventPrefix), new object[] { newBehavior });
        }


        public override void RegisterWithWorldBridge()
        {
            base.RegisterWithWorldBridge();

            if (WorldBridgeSystem.Instance == null) return;

            var bridge = WorldBridgeSystem.Instance;
            string[] npcEventKeys = new[]
            {
                EventKeys.GetBehaviorChangedKey(EventPrefix)
            };

            foreach (var key in npcEventKeys)
            {
                if (!bridge.HasInvoker(key))
                {
                    bridge.RegisterInvoker(key, (args) => { }, DynamicDictionaryInvoker.Layer.Overlay);
                }
            }
        }
    }

    }
