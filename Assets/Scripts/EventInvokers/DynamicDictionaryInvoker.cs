using System;
using System.Collections.Generic;
using UnityEngine;
using FracturedStudios.Data;
using System.Linq;

namespace FracturedStudios.Invoker
{
    /// <summary>
    /// Universal dictionary-driven invoker system. Merged, thread-safe version.
    /// Supports args, metadata, safe invokes, returnable handlers, and transactional "Pay" calls.
    /// Handles registration, invocation, and safe execution with optional retries.
    /// </summary>
    public class DynamicDictionaryInvoker : MonoBehaviour
    {
        public enum Layer { Func, Overlay, Blocking }

        private readonly Dictionary<string, Dictionary<Layer, List<InvocationEntry>>> _methodDict = new();
        private readonly object _sync = new object();

        public static DynamicDictionaryInvoker Instance { get; private set; }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(this);
                return;
            }
            Instance = this;
        }

        #region === Registering ===
        public IDisposable Register(
            string key,
            Action<object[]> method,
            Layer layer = Layer.Func,
            string id = null,
            object metadata = null)
        {
            if (string.IsNullOrWhiteSpace(key) || method == null)
                return null;

            lock (_sync)
            {
                if (!_methodDict.TryGetValue(key, out var layerDict))
                    _methodDict[key] = layerDict = new Dictionary<Layer, List<InvocationEntry>>();

                if (!layerDict.TryGetValue(layer, out var list))
                    layerDict[layer] = list = new List<InvocationEntry>();

                var entryId = string.Intern((id ?? Guid.NewGuid().ToString()));
                var entry = new InvocationEntry(method, entryId, metadata);
                list.Add(entry);
                return new Token(this, key, layer, entry);
            }
        }

        /// <summary>
        /// Register a returnable handler that returns an object result when invoked.
        /// </summary>
        public IDisposable RegisterReturn(string key, Func<object[], object> method, Layer layer = Layer.Func, string id = null, object metadata = null)
        {
            if (string.IsNullOrWhiteSpace(key) || method == null)
                return null;

            lock (_sync)
            {
                if (!_methodDict.TryGetValue(key, out var layerDict))
                    _methodDict[key] = layerDict = new Dictionary<Layer, List<InvocationEntry>>();

                if (!layerDict.TryGetValue(layer, out var list))
                    layerDict[layer] = list = new List<InvocationEntry>();

                var entryId = string.Intern((id ?? Guid.NewGuid().ToString()));
                var entry = new InvocationEntry(method, entryId, metadata);
                list.Add(entry);
                return new Token(this, key, layer, entry);
            }
        }

        /// <summary>
        /// Convenience RegisterReturn with int serial id
        /// </summary>
        public IDisposable RegisterReturn(string key, Func<object[], object> method, Layer layer, int serialId, object metadata = null)
        {
            return RegisterReturn(key, method, layer, serialId.ToString(), metadata);
        }

        // Convenience overload: register with an integer serial id (converted to interned string)
        public IDisposable Register(string key, Action<object[]> method, Layer layer, int serialId, object metadata = null)
        {
            return Register(key, method, layer, serialId.ToString(), metadata);
        }

        /// <summary>
        /// Register a handler that will automatically be removed after its first invocation.
        /// Returns a token for manual disposal as well.
        /// </summary>
        public IDisposable RegisterOnce(string key, Action<object[]> method, Layer layer = Layer.Func, string id = null, object metadata = null)
        {
            if (string.IsNullOrWhiteSpace(key) || method == null) return null;
            var entryId = string.Intern((id ?? Guid.NewGuid().ToString()));

            Action<object[]> wrapper = null;
            wrapper = (args) =>
            {
                try { method(args); }
                finally { RemoveEntry(key, entryId); }
            };

            return Register(key, wrapper, layer, entryId, metadata);
        }

        // RegisterOnce overload with int serial id
        public IDisposable RegisterOnce(string key, Action<object[]> method, Layer layer, int serialId, object metadata = null)
        {
            return RegisterOnce(key, method, layer, serialId.ToString(), metadata);
        }
        #endregion

        #region === Invoking ===
        public void Invoke(string key, params object[] args)
        {
            if (!_methodDict.TryGetValue(key, out var layerDict))
                return;

            foreach (Layer layer in Enum.GetValues(typeof(Layer)))
            {
                if (!layerDict.TryGetValue(layer, out var actions) || actions.Count == 0) 
                    continue;

                // Copy to avoid modification during iteration
                var actionBuffer = actions.ToArray();

                foreach (var entry in actionBuffer)
                {
                    if (entry == null) continue;

                    bool success = false;

                    // First attempt
                    try
                    {
                        entry.Method?.Invoke(args);
                        success = true;
                    }
                    catch (Exception ex)
                    {
                        Debug.LogWarning($"[DynamicDictionaryInvoker] First attempt failed in Invoke('{key}') at layer {layer}: {ex.Message}");

                        // Retry once
                        try
                        {
                            entry.Method?.Invoke(args);
                            success = true;
                            Debug.Log($"[DynamicDictionaryInvoker] Retry succeeded in Invoke('{key}') at layer {layer}");
                        }
                        catch (Exception retryEx)
                        {
                            Debug.LogError($"[DynamicDictionaryInvoker] Retry failed in Invoke('{key}') at layer {layer}: {retryEx.Message}");
                        }
                    }

                    // Optional: remove faulty entry if it keeps failing
                    if (!success)
                        actions.Remove(entry);
                }

                // Stop execution if Blocking layer encountered
                if (layer == Layer.Blocking)
                    break;
            }
        }

        public bool TryInvoke(string key, params object[] args)
        {
            try
            {
                Invoke(key, args);
                return true;
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[DynamicDictionaryInvoker] TryInvoke failed for '{key}': {ex.Message}");
                return false;
            }
        }
        #endregion

        #region === Enum Loop Buffer ===


        private static readonly List<InvocationEntry> _loopBufferDynamic = new List<InvocationEntry>(16);

        /// <summary>
        /// Loops through the enum (Layer) dictionary buffer.
        /// Executes all actions safely in Func, Overlay, Blocking order.
        /// Automatically retries once on exception.
        /// Faulty entries can optionally be removed to prevent repeated failures.
        /// </summary>
        public void LoopBufferDynamic(params object[] args)
        {
            if (_methodDict.Count == 0)
                return;

            try
            {
                // Pre-copy keys to avoid collection modification
                var keysSnapshot = new List<string>(_methodDict.Keys);

                foreach (var key in keysSnapshot)
                {
                    if (!_methodDict.TryGetValue(key, out var layerDict) || layerDict.Count == 0)
                        continue;

                    // Loop through Layer enum order
                    foreach (Layer layer in Enum.GetValues(typeof(Layer)))
                    {
                        if (!layerDict.TryGetValue(layer, out var entries) || entries.Count == 0)
                            continue;

                        _loopBufferDynamic.Clear();
                        _loopBufferDynamic.AddRange(entries);

                        foreach (var entry in _loopBufferDynamic)
                        {
                            if (entry == null) continue;

                            bool success = false;

                            // First attempt
                            try
                            {
                                entry.Method?.Invoke(args);
                                success = true;
                            }
                            catch (Exception ex)
                            {
                                Debug.LogWarning($"[DynamicDictionaryInvoker] First attempt failed for '{key}' layer {layer}: {ex.Message}");

                                // Retry once
                                try
                                {
                                    entry.Method?.Invoke(args);
                                    success = true;
                                    Debug.Log($"[DynamicDictionaryInvoker] Retry succeeded for '{key}' layer {layer}");
                                }
                                catch (Exception retryEx)
                                {
                                    Debug.LogError($"[DynamicDictionaryInvoker] Retry failed for '{key}' layer {layer}: {retryEx.Message}");
                                }
                            }

                            // Optionally remove faulty entry if it keeps failing
                            if (!success)
                                entries.Remove(entry);
                        }

                        // Stop execution if Blocking layer encountered
                        if (layer == Layer.Blocking)
                            break;
                    }
                }
            }
            catch (Exception exOuter)
            {
                Debug.LogError($"[DynamicDictionaryInvoker] LoopBufferDynamic outer exception: {exOuter}");
            }
            finally
            {
                _loopBufferDynamic.Clear(); // manual cleanup
            }
        }
        /// <summary>
        /// "Pay" calls are conditional or transactional invokes.
        /// Automatically retries once on exception and logs failures.
        /// </summary>
        public bool Pay(string key, object token = null, params object[] args)
        {
            if (!_methodDict.TryGetValue(key, out var layerDict))
                return false;

            bool executed = false;

            foreach (var kv in layerDict)
            {
                foreach (var entry in kv.Value.ToArray())
                {
                    if (entry == null) continue;

                    bool success = false;

                    try
                    {
                        if (token == null || Equals(entry.Metadata, token))
                        {
                            entry.Method?.Invoke(args);
                            success = true;
                            executed = true;
                        }
                    }
                    catch (Exception ex)
                    {
                        Debug.LogWarning($"[DynamicDictionaryInvoker] Pay() first attempt failed for '{key}': {ex.Message}");

                        // Retry once
                        try
                        {
                            if (token == null || Equals(entry.Metadata, token))
                            {
                                entry.Method?.Invoke(args);
                                success = true;
                                executed = true;
                                Debug.Log($"[DynamicDictionaryInvoker] Pay() retry succeeded for '{key}'");
                            }
                        }
                        catch (Exception retryEx)
                        {
                            Debug.LogError($"[DynamicDictionaryInvoker] Pay() retry failed for '{key}': {retryEx.Message}");
                        }
                    }

                    // Optionally remove faulty entry if still failing
                    if (!success)
                        kv.Value.Remove(entry);
                }
            }

            return executed;
        }

        /// <summary>
        /// Invoke all returnable handlers registered under a key and collect their results.
        /// Returns an array of results in invocation order (may contain nulls).
        /// </summary>
        public object[] InvokeReturn(string key, params object[] args)
        {
            if (!_methodDict.TryGetValue(key, out var layerDict)) return Array.Empty<object>();

            var results = new List<object>();

            foreach (Layer layer in Enum.GetValues(typeof(Layer)))
            {
                if (!layerDict.TryGetValue(layer, out var actions) || actions.Count == 0) continue;

                var actionBuffer = actions.ToArray();
                foreach (var entry in actionBuffer)
                {
                    if (entry == null) continue;
                    try
                    {
                        if (entry.ReturnMethod != null)
                        {
                            var r = entry.ReturnMethod(args);
                            results.Add(r);
                        }
                    }
                    catch (Exception ex)
                    {
                        Debug.LogWarning($"[DynamicDictionaryInvoker] InvokeReturn handler failed for '{key}' layer {layer}: {ex.Message}");
                        // keep going; add null to preserve ordering
                        results.Add(null);
                    }
                }

                if (layer == Layer.Blocking) break;
            }

            return results.ToArray();
        }

        /// <summary>
        /// Invoke returnable handlers and return the first non-null result (or null if none).
        /// </summary>
        public object InvokeReturnFirst(string key, params object[] args)
        {
            var res = InvokeReturn(key, args);
            foreach (var r in res) if (r != null) return r;
            return null;
        }
        /// <summary>
        /// Checks if an invoker key exists (has registered handlers).
        /// Fast O(1) lookup using dictionary key check.
        /// </summary>
        public bool HasInvoker(string key)
        {
            if (string.IsNullOrWhiteSpace(key))
                return false;
            return _methodDict.ContainsKey(key);
        }

        /// <summary>
        /// Emulates `?.Invoke()` behavior — does nothing if key not found.
        /// </summary>
        public void InvokeSafe(string key, params object[] args)
        {
            if (!_methodDict.ContainsKey(key)) return;
            TryInvoke(key, args);
        }
        #endregion

        #region === Internal Classes ===
        private class InvocationEntry
        {
            // Either Method (void) or ReturnMethod (returns object) may be set. Both can be null when disabled.
            public Action<object[]> Method;
            public Func<object[], object> ReturnMethod;
            public readonly string Id;
            public readonly object Metadata;

            public InvocationEntry(Action<object[]> method, string id, object metadata)
            {
                Method = method;
                ReturnMethod = null;
                Id = id;
                Metadata = metadata;
            }

            public InvocationEntry(Func<object[], object> returnMethod, string id, object metadata)
            {
                Method = null;
                ReturnMethod = returnMethod;
                Id = id;
                Metadata = metadata;
            }
        }

        private class Token : IDisposable
        {
            private readonly DynamicDictionaryInvoker _owner;
            private readonly string _key;
            private readonly Layer _layer;
            private readonly InvocationEntry _entry;
            private bool _disposed;

            public Token(DynamicDictionaryInvoker owner, string key, Layer layer, InvocationEntry entry)
            {
                _owner = owner;
                _key = key;
                _layer = layer;
                _entry = entry;
            }

            public void Dispose()
            {
                if (_disposed) return;

                lock (_owner._sync)
                {
                    if (_owner._methodDict.TryGetValue(_key, out var dict) &&
                        dict.TryGetValue(_layer, out var list))
                    {
                        list.Remove(_entry);
                        if (list.Count == 0) dict.Remove(_layer);
                        if (dict.Count == 0) _owner._methodDict.Remove(_key);
                    }
                }

                _disposed = true;
            }
        }
        #endregion

        /// <summary>
        /// Invoke a key once and then remove all handlers registered under that key.
        /// Useful for one-shot events.
        /// </summary>
        public void InvokeOnce(string key, params object[] args)
        {
            // Invoke first (keeps same semantics as Invoke)
            Invoke(key, args);

            // Then remove the key atomically
            lock (_sync)
            {
                _methodDict.Remove(key);
            }
        }

        /// <summary>
        /// Remove all handlers for a given key. Returns true if removed.
        /// </summary>
        public bool RemoveKey(string key)
        {
            lock (_sync)
            {
                return _methodDict.Remove(key);
            }
        }

        /// <summary>
        /// Remove a single registered invocation by id (the id passed to Register or generated GUID).
        /// Returns true if found and removed.
        /// </summary>
        public bool RemoveEntry(string key, string entryId)
        {
            if (string.IsNullOrEmpty(key) || string.IsNullOrEmpty(entryId)) return false;
            lock (_sync)
            {
                if (!_methodDict.TryGetValue(key, out var layerDict)) return false;
                bool removed = false;
                var layers = new List<Layer>(layerDict.Keys);
                foreach (var layer in layers)
                {
                    if (!layerDict.TryGetValue(layer, out var list)) continue;
                    var toRemove = list.FindAll(e => e != null && e.Id == entryId);
                    foreach (var e in toRemove) list.Remove(e);
                    if (toRemove.Count > 0) removed = true;
                    if (list.Count == 0) layerDict.Remove(layer);
                }
                if (layerDict.Count == 0) _methodDict.Remove(key);
                return removed;
            }
        }

        /// <summary>
        /// Remove all registered invocation entries that match the given entry id across all keys.
        /// Returns the number of removed entries.
        /// </summary>
        public int RemoveAllEntriesForId(string entryId)
        {
            if (string.IsNullOrEmpty(entryId)) return 0;
            int removedTotal = 0;
            lock (_sync)
            {
                var keys = new List<string>(_methodDict.Keys);
                foreach (var key in keys)
                {
                    if (!_methodDict.TryGetValue(key, out var layerDict) || layerDict.Count == 0) continue;
                    var layers = new List<Layer>(layerDict.Keys);
                    foreach (var layer in layers)
                    {
                        if (!layerDict.TryGetValue(layer, out var list)) continue;
                        var toRemove = list.FindAll(e => e != null && e.Id == entryId);
                        foreach (var e in toRemove) list.Remove(e);
                        removedTotal += toRemove.Count;
                        if (list.Count == 0) layerDict.Remove(layer);
                    }
                    if (layerDict.Count == 0) _methodDict.Remove(key);
                }
            }
            return removedTotal;
        }

        /// <summary>
        /// Disable a registered invocation by id by setting its Method to null.
        /// Returns true if an entry was found and disabled.
        /// </summary>
        public bool SetNull(string key, string entryId)
        {
            if (string.IsNullOrEmpty(key) || string.IsNullOrEmpty(entryId)) return false;
            lock (_sync)
            {
                if (!_methodDict.TryGetValue(key, out var layerDict)) return false;
                bool found = false;
                foreach (var kv in layerDict)
                {
                    var list = kv.Value;
                    for (int i = 0; i < list.Count; i++)
                    {
                        var e = list[i];
                        if (e != null && e.Id == entryId)
                        {
                            e.Method = null;
                            found = true;
                        }
                    }
                }
                return found;
            }
        }

        #region === Debug & Introspection ===
        public void PrintRegistry()
        {
            foreach (var key in _methodDict)
            {
                Debug.Log($"Key: {key.Key}");
                foreach (var layer in key.Value)
                {
                    Debug.Log($"  Layer: {layer.Key}");
                    foreach (var entry in layer.Value)
                        Debug.Log($"     ID: {entry.Id}, Metadata: {entry.Metadata}");
                }
            }
        }
        #endregion
    }
}

