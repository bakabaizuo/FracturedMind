using System;
using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityEngine.U2D;

namespace FracturedStudios.TAB
{
    /// <summary>
    /// Lightweight resource helpers for the TAB subsystem.
    /// - Fi: load by id/name (Resources.Load)
    /// - Pi: load by full path (Resources.Load)
    /// - FType: resolve a type by name across assemblies
    /// Also contains small helpers for sprites and prefab validation.
    /// </summary>
    public static class ResourceLoader
    {
        // Load a resource by id/name from Resources folder.
        public static T Fi<T>(string id) where T : UnityEngine.Object
        {
            if (string.IsNullOrEmpty(id)) return null;
            try
            {
                return Resources.Load<T>(id);
            }
            catch (Exception)
            {
                return null;
            }
        }

        // Load a resource by path (Resources.Load with path).
        public static T Pi<T>(string path) where T : UnityEngine.Object
        {
            if (string.IsNullOrEmpty(path)) return null;
            try
            {
                return Resources.Load<T>(path);
            }
            catch (Exception)
            {
                return null;
            }
        }

        // Resolve a Type by simple or full name across loaded assemblies.
        public static Type FType(string typeName)
        {
            if (string.IsNullOrEmpty(typeName)) return null;
            // Try GetType first (works for assembly-qualified names)
            var t = Type.GetType(typeName);
            if (t != null) return t;

            // Fallback: search loaded assemblies for matching type name
            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                try
                {
                    t = asm.GetTypes().FirstOrDefault(x => x.Name == typeName || x.FullName == typeName);
                    if (t != null) return t;
                }
                catch (Exception)
                {
                    // ignore reflection errors
                }
            }
            return null;
        }

        // Sprite helper for SpriteAtlas-backed sprites
        public static Sprite LoadSpriteFromAtlas(string spriteName, SpriteAtlas atlas)
        {
            if (atlas == null || string.IsNullOrEmpty(spriteName)) return null;
            try
            {
                return atlas.GetSprite(spriteName);
            }
            catch (Exception)
            {
                return null;
            }
        }
    }

    public static class PrefabValidator
    {
        // Runtime check to ensure an entry prefab contains an ItemPanel and its serialized fields.
        public static void ValidateEntryPrefab(GameObject prefab)
        {
            if (prefab == null)
            {
                Debug.LogWarning($"[PrefabValidator] Provided prefab is null");
                return;
            }

            var panel = prefab.GetComponentInChildren<FracturedStudios.UI.ItemPanel>(true);
            if (panel == null)
            {
                Debug.LogWarning($"[PrefabValidator] Prefab '{prefab.name}' does not contain an ItemPanel component (root or children). This will cause TryGetComponent to fail at runtime.");
                return;
            }

            // Inspect known serialized fields via exposed properties if available
            var labelText = panel.LabelText; // safe getter
            if (panel.SpriteHolder == null)
            {
                Debug.LogWarning($"[PrefabValidator] ItemPanel on prefab '{prefab.name}' has no Image assigned to 'spriteHolder'. Assign in inspector.");
            }
        }

        // Scan all MonoBehaviour components on the prefab (including children) and
        // return the prefab if any string field or readable string property contains
        // the provided bindingFlag (case-insensitive). Returns null otherwise.
        public static GameObject FindPrefabWithBinding(GameObject prefab, string bindingFlag)
        {
            if (prefab == null || string.IsNullOrEmpty(bindingFlag))
                return null;

            var comps = prefab.GetComponentsInChildren<MonoBehaviour>(true);
            foreach (var comp in comps)
            {
                if (comp == null) continue;
                var t = comp.GetType();
                var flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;
                foreach (var fi in t.GetFields(flags))
                {
                    if (fi.FieldType == typeof(string))
                    {
                        try
                        {
                            var val = fi.GetValue(comp) as string;
                            if (!string.IsNullOrEmpty(val) && val.IndexOf(bindingFlag, StringComparison.OrdinalIgnoreCase) >= 0)
                                return prefab;
                        }
                        catch { }
                    }
                }
                foreach (var pi in t.GetProperties(flags))
                {
                    if (pi.PropertyType == typeof(string) && pi.CanRead)
                    {
                        try
                        {
                            var val = pi.GetValue(comp) as string;
                            if (!string.IsNullOrEmpty(val) && val.IndexOf(bindingFlag, StringComparison.OrdinalIgnoreCase) >= 0)
                                return prefab;
                        }
                        catch { }
                    }
                }
            }

            return null;
        }
    }
}
