using UnityEngine.Events;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using FracturedStudios.Data;
using FracturedStudios.Invoker;
using FracturedStudios.Containers;
namespace FracturedStudios.Data
{
    [Serializable]
    public class PlayerData : DataContainerBase
    {
        #region === Identity ===
        
        [Header("Identity")]
        public string playerId;
        public string displayName;
      

   
        #endregion

        #region === Stats ===
        
        [Header("Stats")]
        [SerializeField] private int _xp = 0;
        [SerializeField] private int _level = 1;

        [Header("Attributes")]
        [SerializeField] private int _agility = 10;
        [SerializeField] private int _strength = 10;
        [SerializeField] private int _wepSkill = 10;
       
        
        #endregion

       
        #region === World State ===
        
        [Header("World Interaction")]
        public bool isActivePlayer = false;
        public virtual Vector3 lastKnownPosition { get; set; } = Vector3.zero;
        public virtual string lastScene { get; set; } = string.Empty;
        public virtual string lastCellId { get; set; } = "PlaceholderCell";
        
        #endregion

        

        #region === Components ===
        
       
        
        #endregion

        #region === Events ===
        public virtual string lastSpawnPointId { get; set; } = "player_default";
        public virtual string lastSpawnPointScene { get; set; } = "Overworld";
        public event EventHandler<PlayerData> OnStatsChanged;

        private PlayerEventData _eventDataContainer;

        protected void RaiseStatsChanged()
        {
            OnStatsChanged?.Invoke(this, this);
            MarkDirty(DirtyReason.ValueChanged);
        }

        /// <summary>Initialize event data container for stat broadcasting</summary>
        public void InitializeEventDataContainer(PlayerEventData eventContainer)
        {
            _eventDataContainer = eventContainer;
        }
        
        #endregion

        #region === Properties with Notify ===
        
        public int xp
        {
            get => _xp;
            set
            {
                if (_xp == value) return;
                _xp = Mathf.Max(0, value);
                RaiseStatsChanged();
            }
        }

        public int level
        {
            get => _level;
            set
            {
                if (_level == value) return;
                _level = Mathf.Max(1, value);
                RaiseStatsChanged();
            }
        }

        public int agility
        {
            get => _agility;
            set
            {
                if (_agility == value) return;
                _agility = Mathf.Max(0, value);
                RaiseStatsChanged();
            }
        }

        public int strength
        {
            get => _strength;
            set
            {
                if (_strength == value) return;
                _strength = Mathf.Max(0, value);
                RaiseStatsChanged();
            }
        }

        public int wepSkill
        {
            get => _wepSkill;
            set
            {
                if (_wepSkill == value) return;
                _wepSkill = Mathf.Clamp(value, 0, 100);
                RaiseStatsChanged();
            }
        }

        
        // ✅ Use property from base for health
        public int currentHP
        {
            get => _currentHP;
            set
            {
                int newHP = Mathf.Clamp(value, 0, _maxHP);
                if (_currentHP == newHP) return;

                _currentHP = newHP;
                if (_currentHP <= 0 && _deathState == DeathState.Alive)
                {
                    OnHealthDepleted();
                }
                RaiseStatsChanged();
            }
        }

        public int maxHP
        {
            get => _maxHP;
            set
            {
                if (_maxHP == value) return;
                _maxHP = Mathf.Max(1, value);
                currentHP = Mathf.Min(currentHP, _maxHP);
                RaiseStatsChanged();
            }
        }
        
        #endregion
private void OnEnable()
{
    OnDeathStateChanged += HandlePlayerDeathStateChange;
}

private void OnDisable()
{
    OnDeathStateChanged -= HandlePlayerDeathStateChange;
}

private void HandlePlayerDeathStateChange(DeathState newState)
{
    // Handle player-specific death UI, etc.
}

        #region === Prefab Registry ===
        
        public Dictionary<string, PlayerData> prefabLookup { get; private set; } = new();

        protected void InitializePrefabLookup(params PlayerData[] prefabs)
        {
            prefabLookup.Clear();
            foreach (var p in prefabs)
            {
                if (p == null) continue;
                prefabLookup[p.displayName] = p;
            }
        }
        
        #endregion

        #region === Unity Lifecycle ===
        
        protected override void Awake()
        {
            base.Awake();
            DataContainerBase.Instance?.Register(this);
        }

        protected override void OnDestroy()
        {
            DataContainerBase.Instance?.Unregister(this);
            base.OnDestroy();
        }
        
        #endregion

        #region === Methods ===
        
        // ✅ Override base TakeDamage to trigger events
        public override void TakeDamage(int amount)
        {
            base.TakeDamage(amount);
            RaiseStatsChanged();
        }

        public override void Heal(int amount)
        {
            base.Heal(amount);
            RaiseStatsChanged();
        }

        public override void Revive()
        {
            base.Revive();
            RaiseStatsChanged();
        }

        public virtual void LevelUp()
        {
            level += 1;
            maxHP += 10;
            currentHP = maxHP;

            // Broadcast level up event
            WorldBridgeSystem.Instance?.InvokeKey(EventKeys.Player.LEVEL_UP, level);
        }

        public virtual void AddXP(int amount)
        {
            if (amount <= 0) return;
            int oldXP = xp;
            xp += amount;

            // Broadcast XP gained event
            WorldBridgeSystem.Instance?.InvokeKey(EventKeys.Player.XP_GAINED, amount);
        }

        public virtual void IncreaseAttribute(string attribute, int amount)
        {
            if (amount <= 0) return;

            switch (attribute.ToLower())
            {
                case "agility":
                    agility += amount;
                    break;
                case "strength":
                    strength += amount;
                    break;
                case "wepskill":
                    wepSkill += amount;
                    break;
                default:
                    Debug.LogWarning($"[PlayerDataContainer] Unknown attribute '{attribute}'");
                    break;
            }
        }

       

        protected override void OnDeathSequenceTriggered()
        {
            base.OnDeathSequenceTriggered();
            Debug.Log($"[PlayerDataContainer] {displayName} death sequence triggered");
            // Add player-specific death logic here (UI, sound, etc.)
        }
        
        #endregion

        #region === Initialization ===
        
        public virtual void InitializeDefaults()
        {
            playerId = DefaultPlayerId;
            displayName = DefaultDisplayName;
           

            _maxHP = 100;
            _currentHP = 100;
            _xp = 0;
            _level = 1;
           CurrentDeathState = DeathState.Alive; 


            _agility = 10;
            _strength = 10;
            _wepSkill = 10;
           

            lastKnownPosition = DefaultPosition;
            lastCellId = DefaultCellId;
            isActivePlayer = true;

           

            DataContainerBase.Instance?.Register(this);
            WorldBridgeSystem.Instance?.RegisterID(playerId, gameObject);

            Debug.Log($"[{displayName}] Initialized with manual ID: {playerId}");
            RaiseStatsChanged();
        }

        public virtual void ResetCharacter()
        {
            InitializeDefaults();
            MarkDirty();
        }

        
        
        #endregion

        #region === Defaults ===
        
        public virtual string DefaultPlayerId => "Player_001";
        public virtual string DefaultDisplayName => "Player";
        public virtual Vector3 DefaultPosition => Vector3.zero;
        public virtual string DefaultCellId => "PlaceholderCell";
        
        #endregion
    }
}
   
