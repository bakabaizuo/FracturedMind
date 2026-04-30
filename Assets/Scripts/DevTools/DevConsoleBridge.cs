using System;
using System.Collections.Generic;
using UnityEngine;

namespace FracturedStudios.UI
{
    // Simple bridge for other systems to register commands/messages into the DebugDevConsoleUI
    public static class DevConsoleBridge
    {
        private static readonly Dictionary<string, Action<string[]>> RegisteredCommands = new Dictionary<string, Action<string[]>>(StringComparer.OrdinalIgnoreCase);
        private static readonly Dictionary<string, Func<object>> RegisteredTrackedValues = new Dictionary<string, Func<object>>(StringComparer.OrdinalIgnoreCase);
        private static readonly Dictionary<string, Func<string>> RegisteredActiveFlagsDetails = new Dictionary<string, Func<string>>(StringComparer.OrdinalIgnoreCase);

        public static void AddMessage(string msg)
        {
            try { DebugDevConsoleUI.Instance?.AddMessage(msg); }
            catch { }
        }

        public static void AddLightMessage(string msg)
        {
            try { DebugDevConsoleUI.Instance?.AddLightMessage(msg); }
            catch { }
        }

        public static void RegisterCommand(string command, Action<string[]> action)
        {
            if (string.IsNullOrWhiteSpace(command) || action == null)
                return;

            RegisteredCommands[command] = action;

            try { DebugDevConsoleUI.Instance?.RegisterCommand(command, action); }
            catch { }
        }

        public static void RegisterTrackedValue(string name, Func<object> getter)
        {
            if (string.IsNullOrWhiteSpace(name) || getter == null)
                return;

            RegisteredTrackedValues[name] = getter;

            try { DebugDevConsoleUI.Instance?.RegisterTrackedValue(name, getter); }
            catch { }
        }

        public static void UnregisterTrackedValue(string name)
        {
            if (!string.IsNullOrWhiteSpace(name))
                RegisteredTrackedValues.Remove(name);

            try { DebugDevConsoleUI.Instance?.UnregisterTrackedValue(name); }
            catch { }
        }

        public static void RegisterActiveFlagsDetail(string name, Func<string> getter)
        {
            if (string.IsNullOrWhiteSpace(name) || getter == null)
                return;

            RegisteredActiveFlagsDetails[name] = getter;

            try { DebugDevConsoleUI.Instance?.RegisterActiveFlagsDetail(name, getter); }
            catch { }
        }

        public static void UnregisterActiveFlagsDetail(string name)
        {
            if (!string.IsNullOrWhiteSpace(name))
                RegisteredActiveFlagsDetails.Remove(name);

            try { DebugDevConsoleUI.Instance?.UnregisterActiveFlagsDetail(name); }
            catch { }
        }

        public static void ApplyBufferedRegistrations(DebugDevConsoleUI console)
        {
            if (console == null)
                return;

            foreach (var entry in RegisteredCommands)
            {
                try { console.RegisterCommand(entry.Key, entry.Value); }
                catch { }
            }

            foreach (var entry in RegisteredTrackedValues)
            {
                try { console.RegisterTrackedValue(entry.Key, entry.Value); }
                catch { }
            }

            foreach (var entry in RegisteredActiveFlagsDetails)
            {
                try { console.RegisterActiveFlagsDetail(entry.Key, entry.Value); }
                catch { }
            }
        }
    }
}
