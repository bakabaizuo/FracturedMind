using System;
using System.Collections.Generic;
using UnityEngine;

namespace FracturedStudios.UI
{
    // Simple bridge for other systems to register commands/messages into the DebugDevConsoleUI
    public static class DevConsoleBridge
    {
        public static void AddMessage(string msg)
        {
            try { DebugDevConsoleUI.Instance?.AddMessage(msg); }
            catch { }
        }

        public static void RegisterCommand(string command, Action<string[]> action)
        {
            try { DebugDevConsoleUI.Instance?.RegisterCommand(command, action); }
            catch { }
        }
    }
}
