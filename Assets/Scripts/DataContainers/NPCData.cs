using System;
using UnityEngine.Events;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using EventKeys = FracturedStudios.Containers.EventKeys;
namespace FracturedStudios.Data
{
    /// <summary>
    /// Death state enum - represents lifecycle of NPC from alive to despawned.
    /// </summary>
 
    [Serializable]
    [DisallowMultipleComponent]
    public class NPCData: DataContainerBase
    {
        #region === Death State System ===
        
        [Header("Death State")]
        [SerializeField] private new DeathState _deathState = DeathState.Alive;

        public new DeathState CurrentDeathState
        {
            get => _deathState;
            private set
            {
                if (_deathState == value) return;
                _deathState = value;
                MarkDirty(DirtyReason.ValueChanged);
                OnDeathStateChanged?.Invoke(_deathState);  // 🔔 Event-driven notification
            }
        }

        /// <summary>
        /// Determines if NPC is not in Alive state.
        ///  Use this instead of isDead boolean
        /// </summary>
        public bool IsDead => _deathState != DeathState.Alive;

        /// <summary>
        /// Event fired whenever death state changes.
        /// Subscribe to this in behavior components (Combat, Ragdoll, AI, etc.)
        /// </summary>
        public new event System.Action<DeathState> OnDeathStateChanged;

        #endregion

       
       
        #region === Identity ===
        
        [Header("Identity")]
        public string npcId;
        public string displayName;
        
        #endregion

        #region === Stats ===
        
        [Header("Stats")]
       // public int currentHP = 100;
       // public int maxHP = 100;
        public int level = 1;
        
        #endregion

      

        [Header("Behavior")]
        [Tooltip("Overall disposition toward the player. -100 = Hostile, 0 = Neutral, +100 = Friendly.")]
        [Range(-100f, 100f)]
        public float disposition = 0f;

        public bool isEnemy;
        public bool isNeutral;
        public bool isFriendly;
        
        

        #region === Misc ===
        
        [Header("Misc")]
        public Vector3 lastKnownPosition;
        public string customTag;
        
        #endregion

        #region === Component References ===
        
      
        #endregion

        #region === Unity Lifecycle ===
        
        private new void Awake()
        {
            // Ensure npcId is set
            // Update combat state based on disposition and register to manager
            DataContainerBase.Instance?.Register(this);
        }

        private new void OnDestroy()
        {
            DataContainerBase.Instance?.Unregister(this);
        }
        
        #endregion

      

        #region === Health Management ===
        
        /// <summary>
        /// Writable current HP property (clamped to 0-maxHP).
        /// Use this to get/set NPC health
        /// </summary>
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
                    CurrentDeathState = DeathState.Dead;
                }
                MarkDirty(DirtyReason.ValueChanged);
            }
        }

        /// <summary>
        /// Writable max HP property.
        /// use this to get/set NPC max health
        /// </summary>
        public int maxHP
        {
            get => _maxHP;
            set
            {
                if (_maxHP == value) return;
                _maxHP = Mathf.Max(1, value);
                currentHP = Mathf.Min(currentHP, _maxHP);
                MarkDirty(DirtyReason.ValueChanged);
            }
        }

        /// <summary>
        /// Set HP directly (clamped to 0-maxHP).
        /// Triggers death if HP reaches 0.
        /// </summary>
        public void SetHP(int newHP)
        {
            currentHP = Mathf.Clamp(newHP, 0, maxHP);
            if (currentHP <= 0 && _deathState == DeathState.Alive)
            {
                CurrentDeathState = DeathState.Dead;
            }
            MarkDirty(DirtyReason.ValueChanged);
        }

        /// <summary>
        /// Apply damage to NPC. Triggers death sequence if HP reaches 0.
        /// IDamageable implementation.
        /// </summary>
        public override void TakeDamage(int amount)
        {
            if (amount <= 0 || IsDead) 
                return;

            currentHP -= amount;

            if (currentHP <= 0 && _deathState == DeathState.Alive)
            {
                CurrentDeathState = DeathState.Dead;
            }

            MarkDirty();
        }
        
        #endregion

        #region === Death / Ragdoll Sequence ===
        
     
        
        #endregion

      
        #region === Disposition System ===
        
        /// <summary>
        /// Set disposition to exact value (-100 to +100).
        /// </summary>
        public void SetDisposition(float newValue)
        {
            disposition = Mathf.Clamp(newValue, -100f, 100f);
            UpdateDispositionFlags();
            MarkDirty();
        }

        /// <summary>
        /// Modify disposition by delta amount.
        /// </summary>
        public void ModifyDisposition(float delta)
        {
            SetDisposition(disposition + delta);
        }

        /// <summary>
        /// Adjust disposition with custom dirty reason.
        /// </summary>
        public void AdjustDisposition(float amount, DirtyReason reason = DirtyReason.ValueChanged)
        {
            disposition = Mathf.Clamp(disposition + amount, -100f, 100f);
            MarkDirty(reason);
        }

        /// <summary>
        /// Update isEnemy/isNeutral/isFriendly flags based on disposition value and faction relationships.
        /// </summary>
        public virtual void UpdateDispositionFlags()
        {
            // Use raw disposition value for flags (no faction system yet)
            float effectiveDisposition = disposition;

            isEnemy = effectiveDisposition <= -35f;
            isNeutral = effectiveDisposition > -35f && effectiveDisposition < 35f;
            isFriendly = effectiveDisposition >= 35f;

            ///sync combat state
        }

   
        /// <summary>
        /// Check if this NPC is hostile toward the player based on current disposition and factions.
        /// </summary>
        public bool IsHostileToPlayer()
        {
            return disposition <= -35f;
        }

      
        #endregion

    

        #region === Serialization ===
        
        public override string SerializeData()
        {
            return string.Empty; // Placeholder: no HexSerialization yet
        }

        public override void DeserializeData(string data)
        {
            // Placeholder: no HexSerialization yet
        }
        
        #endregion

       

        #region === Clone / Copy ===

        /// <summary>
        /// Copy data from another NPC container.
        /// </summary>
        public void CopyFrom(NPCData source)
        {
            

            displayName = source.displayName;
            currentHP = source.currentHP;  
            maxHP = source.maxHP;
            level = source.level;

          

            disposition = source.disposition;
           

            lastKnownPosition = Vector3.zero;
            customTag = source.customTag;

            // Reset death state to alive
            CurrentDeathState = DeathState.Alive;

            DataContainerBase.Instance?.Register(this);
          
        }
        #endregion
    }
}
