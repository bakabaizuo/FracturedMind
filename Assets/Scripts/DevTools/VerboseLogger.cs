using System;
using System.IO;
using UnityEngine;
using System.Collections.Generic;
using System.Linq;
public class VerboseLogger : MonoBehaviour
{
    // Public singleton instance
    public static VerboseLogger? Instance { get; private set; }

    private const string LoggerVersion = "VerboseLogger v1.1";
    private const string InternalConsolePrefix = "[VL]:"; // used to tag console logs emitted by this logger
    private const string HLConsolePrefix = "[HL]:"; // used to tag hierarchy logs

    [Header("Debug Logger Settings")]
    public bool verboseLog = true;
    public string logFilePath = "Resources/Debug/DebugLog.txt";

    private StreamWriter? logWriter;
    private string? lastMessage = null;

    private static bool _bootstrapping;
  
    // Per-message suppression state: counts and first-seen timestamp
    private readonly Dictionary<string, (int count, DateTime firstSeen)> messageStats = new();
    // Time window in seconds to consider repeated messages for suppression
    private const double SuppressionWindowSeconds = 2.0;

    private void OnEnable()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(this);
            return;
        }

        Instance = this;

        if (verboseLog)
        {
            string fullPath = Path.Combine(Application.dataPath, logFilePath);
            var dir = Path.GetDirectoryName(fullPath);
            if (!string.IsNullOrEmpty(dir))
                Directory.CreateDirectory(dir);

            try
            {
                logWriter = new StreamWriter(fullPath, true);
                Application.logMessageReceived += HandleLog;
                // Write initial header
                WriteWithDuplicateSuppression($"=== VerboseLogger Enabled ({LoggerVersion}) ===");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[VerboseLogger] Failed to open log file '{fullPath}': {ex.Message}");
                logWriter = null;
            }
        }
    }

    private void OnDisable()
    {
        if (Instance == this)
            Instance = null;

        if (logWriter != null)
        {
            WriteWithDuplicateSuppression($"=== VerboseLogger Disabled ({LoggerVersion}) ===");
            try
            {
                logWriter.Close();
            }
            catch { }
            logWriter = null;
            Application.logMessageReceived -= HandleLog;
        }
    }

    // This receives Unity's normal console messages. We avoid re-writing messages that originate from
    // this logger's own Log() calls by checking for the InternalConsolePrefix.
    private void HandleLog(string logString, string stackTrace, LogType type)
    {
        if (!verboseLog || logWriter == null) return;

        if (!string.IsNullOrEmpty(logString))
        {
            if (logString.StartsWith(InternalConsolePrefix))
            {
                // Skip: this was emitted by VerboseLogger.Log to console and already written to file.
                return;
            }
            if (logString.StartsWith(HLConsolePrefix))
            {
                // Skip: this was emitted by HierarchyLogger and should not be captured in VerboseLogger.
                return;
            }
        }

        string entry = $"[{DateTime.Now:HH:mm:ss}] [{type}] {logString} [{LoggerVersion}]";
        if (type == LogType.Error || type == LogType.Exception)
            entry += $"\n{stackTrace}";

        WriteWithDuplicateSuppression(entry);
    }

    // Public API used by code: writes to file and also emits to the Unity console (tagged so HandleLog can ignore it).
    public void Log(string message)
    {
        if (string.IsNullOrEmpty(message)) return;

        // Write to file (custom tag) first
        if (verboseLog && logWriter != null)
        {
            string entry = $"[{DateTime.Now:HH:mm:ss}] [VerboseLogger] {message} [{LoggerVersion}]";
            WriteWithDuplicateSuppression(entry);
        }

        // Also write to Unity console for normal visibility, but tag it so we don't duplicate to file
        Debug.Log(InternalConsolePrefix + message);
    }

    // Safe static helper for callers so they don't need to check Instance everywhere.
    public static void SafeLog(string message)
    {
        try
        {
            if (Instance == null)
            {
                // When called from static constructors / early Awake, VerboseLogger may not exist yet.
                // Best-effort: locate an existing logger, or create one (play mode only) so logs hit the file.
                if (!_bootstrapping)
                {
                    _bootstrapping = true;
                    try
                    {
                        var existing = FindObjectOfType<VerboseLogger>();
                        if (existing != null)
                        {
                            Instance = existing;
                        }
                        else if (Application.isPlaying && Instance != existing)
                        {
                            var go = new GameObject(nameof(VerboseLogger));
                            DontDestroyOnLoad(go);
                            Instance = go.AddComponent<VerboseLogger>();
                            
                        }
                    }
                    catch { }
                    finally
                    {
                        _bootstrapping = false;
                    }
                }
            }

            if (Instance != null)
                Instance.Log(message);
            else
                Debug.Log("[VL-Fallback]: " + message);
        }
        catch (Exception)
        {
            // swallow to avoid logging causing further issues
        }
    }

    // Standardized system log helper
    public static void LogSystem(string systemName, string eventName)
    {
        SafeLog($"System:{systemName} {eventName}");
    }

    // Duplicate suppression: skip writing a message to file if it repeats 5 or more times within the suppression window.
    private void WriteWithDuplicateSuppression(string entry)
    {
        if (logWriter == null) return;

        var now = DateTime.Now;

        if (messageStats.TryGetValue(entry, out var stat))
        {
            // If outside the suppression window, reset
            if ((now - stat.firstSeen).TotalSeconds > SuppressionWindowSeconds)
            {
                stat = (1, now);
            }
            else
            {
                stat.count++;
            }

            // Update back
            messageStats[entry] = stat;

            if (stat.count >= 5)
            {
                // Suppress further identical messages within the window
                return;
            }
        }
        else
        {
            // First time seeing this exact message
            messageStats[entry] = (1, now);
        }

        try
        {
            logWriter.WriteLine(entry);
            logWriter.Flush();
        }
        catch (Exception ex)
        {
            Debug.LogError($"[VerboseLogger] Failed to write log entry: {ex.Message}");
        }

        // Optional: keep dictionary trimmed to avoid memory growth
        if (messageStats.Count > 3000)
        {
            // remove oldest entries
            var oldest = messageStats.OrderBy(kv => kv.Value.firstSeen).Take(200).Select(kv => kv.Key).ToList();
            foreach (var k in oldest) messageStats.Remove(k);
        }
    }
}
    