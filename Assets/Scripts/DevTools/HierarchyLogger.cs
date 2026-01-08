using UnityEngine;
using System.Text;
using System.IO;

public static class HierarchyLogger
{
    /// <summary>
    /// Logs the hierarchy of the given root transform to the console and optionally to a file.
    /// </summary>
    /// <param name="root">The root transform to start logging from.</param>
    /// <param name="filePath">Optional file path to save the log.</param>
    private const string HLConsolePrefix = "[HL]:"; // Used to tag hierarchy logs
    public static void LogHierarchy(Transform root, string filePath = null)
    {
        StringBuilder sb = new StringBuilder();
        LogHierarchyRecursive(root, sb, 0);

        string log = sb.ToString();
        Debug.Log(HLConsolePrefix + log);

        if (!string.IsNullOrEmpty(filePath))
        {
            File.WriteAllText(filePath, log);
        }
    }
    public static void LogFullHierarchy(Transform root, string filePath = null)
    {
        StringBuilder sb = new StringBuilder();
        LogFullHierarchyRecursive(root, sb, 0);

        string log = sb.ToString();
        Debug.Log(HLConsolePrefix + log);

        if (!string.IsNullOrEmpty(filePath))
        {
            File.WriteAllText(filePath, log);
        }
    }
    public static Transform FindDeepChild(Transform parent, string name)
    {
        foreach (Transform child in parent)
        {
            if (child.name == name)
                return child;
            var result = FindDeepChild(child, name);
            if (result != null)
                return result;
        }
        return null;
    }
    private static void LogHierarchyRecursive(Transform t, StringBuilder sb, int indent)
    {
        string indentStr = new string(' ', indent * 2);
        string parentName = t.parent != null ? t.parent.name : "None";
        sb.AppendLine($"{indentStr}- {t.name} (Parent: {parentName})");

        foreach (Transform child in t)
        {
            LogHierarchyRecursive(child, sb, indent + 1);
        }
    }

        private static void LogFullHierarchyRecursive(Transform t, StringBuilder sb, int indent)
    {
        string indentStr = new string(' ', indent * 2);
        string parentName = t.parent != null ? t.parent.name : "None";
        sb.AppendLine($"{indentStr}- {t.name} (Parent: {parentName})");

        // List all components on this GameObject
        var components = t.GetComponents<Component>();
        foreach (var comp in components)
        {
            sb.AppendLine($"{indentStr}  [Component] {comp.GetType().Name}");
        }

        foreach (Transform child in t)
        {
            LogFullHierarchyRecursive(child, sb, indent + 1); // <-- Fix here
        }
    }
}

// Usage Example:
// HierarchyLogger.LogHierarchy(this.transform, "Assets/HierarchyLog.txt");
// or
// HierarchyLogger.LogHierarchy(this.transform);
// To find a deep child by name:
// Transform foundChild = HierarchyLogger.FindDeepChild(this.transform, "TargetChildName");
// Verbose example:
//public class PlayerReferences : MonoBehaviour
//{
//    public Transform aimTarget;
//    public Transform gunSocket;
//    public Transform rigLayer;
//
//    private void Awake()
//    {
//      aimTarget = HierarchyLogger.FindDeepChild(transform, "AimTarget_z");
//        gunSocket = HierarchyLogger.FindDeepChild(transform, "GunSocket");
//        rigLayer = HierarchyLogger.FindDeepChild(transform, "RigLayer");
//    }
//}