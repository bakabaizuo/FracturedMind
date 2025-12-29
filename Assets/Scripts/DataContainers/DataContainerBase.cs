using System;
using UnityEngine;
using System.Collections.Generic;

namespace FracturedStudios.Data
{
    /// <summary>
    /// Basic lifecycle states for damageable entities.
    /// </summary>
    public enum DeathState
    {
        Alive = 0,
        Dead = 1,
        Ragdoll = 2,
        Despawned = 3
    }

    /// <summary>
    /// Minimal damageable contract.
    /// </summary>
    public interface IDamageable
    {
        void TakeDamage(int amount);
    }

    public interface IDataContainer
    {
        string SerializeData();
        void DeserializeData(string data);
    }

    /// <summary>
    /// Base class for all data containers in the project.
    /// Provides dirty tracking, serialization, and registration hooks.
    /// </summary>
    public abstract class DataContainerBase : MonoBehaviour, IDataContainer, IDamageable
    {
        public enum DirtyReason { ValueChanged, StructureChanged, Other }

        /// <summary>
        /// Global singleton-like instance for lightweight manager functionality.
        /// Provides registration of data containers without requiring a separate manager class.
        /// </summary>
        public static DataContainerBase Instance { get; private set; }

        /// <summary>
        /// Internal registry of all active data containers.
        /// </summary>
        private static readonly List<DataContainerBase> _registry = new List<DataContainerBase>();

        // ======== Minimal HP/Death placeholders so derived classes compile ========
        protected int _currentHP = 100;
        protected int _maxHP = 100;
        protected DeathState _deathState = DeathState.Alive;

        /// <summary>
        /// Current death state for containers that don't override with their own system.
        /// </summary>
        public virtual DeathState CurrentDeathState
        {
            get => _deathState;
            protected set
            {
                if (_deathState == value) return;
                _deathState = value;
                OnDeathStateChanged?.Invoke(_deathState);
                MarkDirty(DirtyReason.ValueChanged);
            }
        }

        /// <summary>
        /// Event fired when base death state changes.
        /// </summary>
        public event Action<DeathState> OnDeathStateChanged;

        /// <summary>
        /// If true, suppresses dirty marking and notifications.
        /// </summary>
        [NonSerialized]
        public bool SuppressDirty = false;

        /// <summary>
        /// Marks the data container as dirty (changed).
        /// </summary>
        protected void MarkDirty(DirtyReason reason = DirtyReason.Other)
        {
            if (SuppressDirty) return;
            // Implement dirty tracking logic here (e.g., notify manager, set flags)
        }

        /// <summary>
        /// Serialize the data container to a string.
        /// </summary>
        public virtual string SerializeData()
        {
            // Implement serialization logic here
            return string.Empty;
        }

        /// <summary>
        /// Deserialize the data container from a string.
        /// </summary>
        public virtual void DeserializeData(string data)
        {
            // Implement deserialization logic here
        }

        /// <summary>
        /// Minimal damage handling; can be overridden by derived classes.
        /// </summary>
        public virtual void TakeDamage(int amount)
        {
            if (amount <= 0 || _deathState != DeathState.Alive) return;
            _currentHP = Mathf.Max(0, _currentHP - amount);
            if (_currentHP <= 0)
            {
                OnHealthDepleted();
            }
            MarkDirty(DirtyReason.ValueChanged);
        }

        /// <summary>
        /// Minimal heal.
        /// </summary>
        public virtual void Heal(int amount)
        {
            if (amount <= 0) return;
            _currentHP = Mathf.Clamp(_currentHP + amount, 0, _maxHP);
            MarkDirty(DirtyReason.ValueChanged);
        }

        /// <summary>
        /// Minimal revive.
        /// </summary>
        public virtual void Revive()
        {
            if (CurrentDeathState != DeathState.Alive)
            {
                _currentHP = Math.Max(1, _currentHP);
                CurrentDeathState = DeathState.Alive;
            }
        }

        /// <summary>
        /// Called when HP reaches zero.
        /// </summary>
        protected virtual void OnHealthDepleted()
        {
            CurrentDeathState = DeathState.Dead;
            OnDeathSequenceTriggered();
        }

        /// <summary>
        /// Hook for derived classes to implement custom death behavior.
        /// </summary>
        protected virtual void OnDeathSequenceTriggered()
        {
            // Placeholder
        }

        #region === Manager Helpers ===
        /// <summary>
        /// Register a data container in the global registry.
        /// </summary>
        public virtual void Register(DataContainerBase container)
        {
            if (!_registry.Contains(container))
            {
                _registry.Add(container);
            }
        }

        /// <summary>
        /// Unregister a data container from the global registry.
        /// </summary>
        public virtual void Unregister(DataContainerBase container)
        {
            _registry.Remove(container);
        }

        /// <summary>
        /// Access current registry snapshot.
        /// </summary>
        public static IReadOnlyList<DataContainerBase> Registry => _registry;
        #endregion

        /// <summary>
        /// Awake assigns Instance if not set.
        /// </summary>
        protected virtual void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
            }
        }

        protected virtual void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }
        }
    }
}