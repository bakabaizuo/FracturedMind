using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Linq;
using System.Runtime.CompilerServices;
//using FracturedStudios.Invoker;
using FracturedStudios;
namespace FracturedStudios.Components
{
    public static class ComponentExtensions
    {

        /// <summary>
        /// Get a component "no matter what" in the local prefab hierarchy.
        /// Search order: self -> parents -> children -> transform.root (siblings + whole prefab).
        /// </summary>
        public static T GetAny<T>(this Component comp, bool includeInactive = true) where T : Component
        {
            if (comp == null) return null;

            var found = comp.GetComponent<T>();
            if (found != null) return found;

            found = comp.GetComponentInParent<T>(includeInactive);
            if (found != null) return found;

            found = comp.GetComponentInChildren<T>(includeInactive);
            if (found != null) return found;

            var root = comp.transform != null ? comp.transform.root : null;
            if (root == null) return null;

            return root.GetComponentInChildren<T>(includeInactive);
        }

        /// <summary>
        /// GameObject overload for GetAny.
        /// </summary>
        public static T GetAny<T>(this GameObject obj, bool includeInactive = true) where T : Component
        {
            if (obj == null) return null;
            return obj.transform.GetAny<T>(includeInactive);
        }

        /// <summary>
        /// TryGet version for GetAny.
        /// </summary>
        public static bool TryGetAny<T>(this Component comp, out T result, bool includeInactive = true) where T : Component
        {
            result = comp.GetAny<T>(includeInactive);
            return result != null;
        }

        /// <summary>
        /// TryGet version for GetAny (GameObject overload).
        /// </summary>
        public static bool TryGetAny<T>(this GameObject obj, out T result, bool includeInactive = true) where T : Component
        {
            result = obj.GetAny<T>(includeInactive);
            return result != null;
        }

        /// <summary>
        /// Get an interface implementation "no matter what" in the local prefab hierarchy.
        /// Works for things like IPlayerAimController implemented by a MonoBehaviour.
        /// Search order: self -> parents -> children -> transform.root (siblings + whole prefab).
        /// </summary>
        public static T GetAnyInterface<T>(this Component comp, bool includeInactive = true) where T : class
        {
            if (comp == null) return null;

            // Self
            foreach (var mb in comp.GetComponents<MonoBehaviour>())
                if (mb is T tSelf) return tSelf;

            // Parents (includes self too, but self already checked)
            foreach (var mb in comp.GetComponentsInParent<MonoBehaviour>(includeInactive))
                if (mb is T tParent) return tParent;

            // Children
            foreach (var mb in comp.GetComponentsInChildren<MonoBehaviour>(includeInactive))
                if (mb is T tChild) return tChild;

            // Root (siblings + whole prefab)
            var root = comp.transform != null ? comp.transform.root : null;
            if (root == null) return null;
            foreach (var mb in root.GetComponentsInChildren<MonoBehaviour>(includeInactive))
                if (mb is T tRoot) return tRoot;

            return null;
        }

        /// <summary>
        /// GameObject overload for GetAnyInterface.
        /// </summary>
        public static T GetAnyInterface<T>(this GameObject obj, bool includeInactive = true) where T : class
        {
            if (obj == null) return null;
            return obj.transform.GetAnyInterface<T>(includeInactive);
        }

        /// <summary>
        /// TryGet version for GetAnyInterface.
        /// </summary>
        public static bool TryGetAnyInterface<T>(this Component comp, out T result, bool includeInactive = true) where T : class
        {
            result = comp.GetAnyInterface<T>(includeInactive);
            return result != null;
        }

        /// <summary>
        /// TryGet version for GetAnyInterface (GameObject overload).
        /// </summary>
        public static bool TryGetAnyInterface<T>(this GameObject obj, out T result, bool includeInactive = true) where T : class
        {
            result = obj.GetAnyInterface<T>(includeInactive);
            return result != null;
        }
  
       /// <summary>
/// Shorthand version that returns the component or null
/// </summary>
public static T TryGetComponent<T>(this Component comp, string name) where T : Component
{
    if (comp == null) return null;
    return comp.GetComponentsInChildren<T>(true).FirstOrDefault(c => c.name == name)
           ?? (comp.GetComponentInParent<T>(true)?.name == name ? comp.GetComponentInParent<T>(true) : null)
           ?? comp.GetComponent<T>()
           ?? comp.GetComponentInParent<T>()
           ?? comp.GetComponentInChildren<T>();
}
/// <summary>
/// TryGetComponent with out parameter for compatibility
/// </summary>
public static bool TryGetComponent<T>(this Component comp, out T result, string name) where T : Component
{
    result = null;
    if (comp == null) return false; // ← missing semicolon added
    result = comp.GetComponentsInChildren<T>(true).FirstOrDefault(c => c.name == name)
             ?? (comp.GetComponentInParent<T>(true)?.name == name ? comp.GetComponentInParent<T>(true) : null)
             ?? comp.GetComponent<T>()
             ?? comp.GetComponentInParent<T>()
             ?? comp.GetComponentInChildren<T>();
    return result != null;
}
        /// <summary>
        /// Try to get component in children only
        /// </summary>
        public static bool TryGetComponentInChildren<T>(this Component comp, out T result, string name) where T : Component
        {
            result = null;
            if (comp == null) return false;
            result = comp.GetComponentsInChildren<T>(true).FirstOrDefault(c => c.name == name);
            return result != null;
        }
        /// <summary>
        /// Shorthand for children only
        /// </summary>
        public static T TryGetComponentInChildren<T>(this Component comp, string name) where T : Component
        {
            if (comp == null) return null;
            return comp.GetComponentsInChildren<T>(true).FirstOrDefault(c => c.name == name);
        }
        /// <summary>
        /// Try to get component in parent hierarchy
        /// </summary>
        public static bool TryGetComponentInParent<T>(this Component comp, out T result, string name) where T : Component
        {
            result = null;
            if (comp == null || comp.transform == null) return false;
            result = comp.GetComponentInParent<T>(true);
            if (result != null && result.name == name) return true;
            // Search up the hierarchy
            Transform parent = comp.transform.parent;
            while (parent != null)
            {
                result = parent.GetComponent<T>();
                if (result != null && result.name == name) return true;
                parent = parent.parent;
            }
            result = null;
            return false;
        }
        /// <summary>
        /// Shorthand for parent hierarchy
        /// </summary>
        public static T TryGetComponentInParent<T>(this Component comp, string name) where T : Component
        {
            if (comp == null) return null;
            T result = comp.GetComponentInParent<T>(true);
            if (result != null && result.name == name) return result;
            // Search up the hierarchy
            Transform parent = comp.transform.parent;
            while (parent != null)
            {
                result = parent.GetComponent<T>();
                if (result != null && result.name == name) return result;
                parent = parent.parent;
            }
            return null;
        }
        /// <summary>
        /// Get all components matching name in hierarchy
        /// </summary>
        public static IEnumerable<T> TryGetComponents<T>(this Component comp, string name) where T : Component
        {
            if (comp == null) return Enumerable.Empty<T>();
            return comp.GetComponentsInChildren<T>(true).Where(c => c.name == name)
                   .Concat(comp.GetComponentsInParent<T>(true).Where(c => c.name == name))
                   .Distinct();
        }
        /// <summary>
        /// Check if component exists without getting it
        /// </summary>
        public static bool HasComponent<T>(this Component comp, string name = null) where T : Component
        {
            if (comp == null) return false;
            if (string.IsNullOrEmpty(name))
            {
                return comp.GetComponent<T>() != null ||
                       comp.GetComponentInParent<T>() != null ||
                       comp.GetComponentInChildren<T>() != null;
            }
            else
            {
                return comp.TryGetComponent<T>(name) != null;
            }
        }
        /// <summary>
        /// Ultra-short static method for component finding
        /// Usage: Component.Find<MyComponent>(gameObject, "name")
        /// </summary>
        public static T Find<T>(GameObject obj, string name = null) where T : Component
        {
            if (obj == null) return null;
            if (string.IsNullOrEmpty(name)) return obj.GetComponent<T>();
            return obj.GetComponentsInChildren<T>(true).FirstOrDefault(c => c.name == name)
                   ?? obj.GetComponentInParent<T>(true);
        }
        /// <summary>
        /// Even shorter static method - direct component access
        /// Usage: Component.Get<MyComponent>(transform, "name")
        /// </summary>
        public static T Get<T>(Component comp, string name = null) where T : Component
        {
            if (comp == null) return null;
            return string.IsNullOrEmpty(name) ? comp.GetComponent<T>() : comp.TryGetComponent<T>(name);
        }
        /// <summary>
        /// TryGetComponent with out parameter (Unity-style)
        /// Usage: if (transform.TryGet(out Rigidbody rb, "PhysicsBody")) { ... }
        /// </summary>
        public static bool TryGet<T>(this Component comp, out T result, string name = null) where T : Component
        {
            result = null;
            if (comp == null) return false;
            if (string.IsNullOrEmpty(name))
            {
                result = comp.GetComponent<T>() ?? comp.GetComponentInParent<T>() ?? comp.GetComponentInChildren<T>();
            }
            else
            {
                result = comp.GetComponentsInChildren<T>(true).FirstOrDefault(c => c.name == name)
                         ?? (comp.GetComponentInParent<T>(true)?.name == name ? comp.GetComponentInParent<T>(true) : null)
                         ?? comp.GetComponent<T>()
                         ?? comp.GetComponentInParent<T>()
                         ?? comp.GetComponentInChildren<T>();
            }
            return result != null;
        }
        /// <summary>
        /// Ultra-short method for component access with caching
       
        /// Usage: Component.G<MyComponent>(transform, "name")
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static T G<T>(Component comp, string name = null) where T : Component =>
            comp != null
                ? (string.IsNullOrEmpty(name)
                    ? comp.GetComponent<T>() ?? comp.GetComponentInParent<T>() ?? comp.GetComponentInChildren<T>()
                    : comp.TryGetComponent<T>(name))
                : null;
        /// <summary>
        /// Find deep child - searches recursively through all descendants
        /// Usage: Component.FindDeep<MyComponent>(transform, "DeepChildName")
        /// </summary>
        /// <summary>
        /// Get component by type in the order: self -> children -> parents.
        /// Use this for wiring systems where "local wins" and you only want to climb to parents if needed.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool TryGetSCP<T>(this Component comp, out T result, bool includeInactive = true)
            where T : Component
        {
            result = null;
            if (comp == null) return false;

            result = comp.GetComponent<T>();
            if (result != null) return true;

            result = comp.GetComponentInChildren<T>(includeInactive);
            if (result != null) return true;

            result = comp.GetComponentInParent<T>(includeInactive);
            return result != null;
        }

        /// <summary>
        /// Shorthand: returns component by type in the order: self -> children -> parents.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static T GetSCP<T>(this Component comp, bool includeInactive = true)
            where T : Component
        {
            if (comp == null) return null;
            return comp.GetComponent<T>()
                ?? comp.GetComponentInChildren<T>(includeInactive)
                ?? comp.GetComponentInParent<T>(includeInactive);
        }
        public static T FindDeep<T>(this Component comp, string name) where T : Component
        {
            if (comp == null || string.IsNullOrEmpty(name)) return null;
            return comp.GetComponentsInChildren<T>(true).FirstOrDefault(c => c.name == name);
        }
        /// <summary>
        /// Find component by path (like "Parent/Child/GrandChild")
        /// Usage: Component.FindByPath<MyComponent>(transform, "UI/Canvas/Button")
        /// </summary>
        public static T FindByPath<T>(this Component comp, string path) where T : Component
        {
            if (comp == null || string.IsNullOrEmpty(path) || comp.transform == null) return null;
            Transform target = comp.transform.Find(path);
            return target != null ? target.GetComponent<T>() : null;
        }
        public static T FindByPathDeep<T>(this Component comp, string path) where T : Component
{
            if (comp == null || string.IsNullOrEmpty(path) || comp.transform == null) return null;
            Transform target = comp.transform.Find(path);
            if (target != null) return target.GetComponent<T>();
            // fallback: recursive search by name at any depth
            return comp.GetComponentsInChildren<T>(true).FirstOrDefault(c => c.name == path);
}
        /// <summary>
        /// Get or add component if it doesn't exist
        /// Usage: Component.GetOrAdd<MyComponent>(gameObject)
        /// </summary>
        public static T GetOrAdd<T>(GameObject obj) where T : Component
        {
            if (obj == null) return null;
            T comp = obj.GetComponent<T>();
            return comp != null ? comp : obj.AddComponent<T>();
        }
        /// <summary>
        /// Find component by tag in hierarchy
        /// Usage: Component.FindByTag<MyComponent>(transform, "Enemy")
        /// </summary>
        public static T FindByTag<T>(Component comp, string tag) where T : Component
        {
            if (comp == null || string.IsNullOrEmpty(tag)) return null;
            return comp.GetComponentsInChildren<T>(true).FirstOrDefault(c => c.CompareTag(tag))
                   ?? comp.GetComponentInParent<T>(true);
        }
        /// <summary>
        /// Get all components of type in hierarchy (cached version)
        /// Usage: Component.GetAllInHierarchy<MyComponent>(transform)
        /// </summary>
        public static T[] GetAllInHierarchy<T>(Component comp) where T : Component
        {
            if (comp == null) return new T[0];
            return comp.GetComponentsInChildren<T>(true);
        }
        /// <summary>
        /// Safe component destruction with null check
        /// Usage: Component.SafeDestroy(myComponent)
        /// </summary>
        public static void SafeDestroy<T>(T component) where T : Component
        {
            if (component != null)
            {
                if (Application.isPlaying)
                    Object.Destroy(component);
                else
                    Object.DestroyImmediate(component);
            }
        }
        /// <summary>
        /// Enable/disable multiple components at once
        /// Usage: Component.SetEnabled(transform, false, typeof(Renderer), typeof(Collider))
        /// </summary>
        public static void SetEnabled(Component comp, bool enabled, params System.Type[] componentTypes)
        {
            if (comp == null || componentTypes == null) return;
            foreach (var type in componentTypes)
            {
                var component = comp.GetComponent(type);
                if (component is Behaviour behaviour)
                    behaviour.enabled = enabled;
                else if (component is Renderer renderer)
                    renderer.enabled = enabled;
                else if (component is Collider collider)
                    collider.enabled = enabled;
            }
        }
        /// <summary>
        /// Copy component values from one to another
        /// Usage: Component.CopyValues(sourceComponent, targetComponent)
        /// </summary>
        public static void CopyValues<T>(T source, T target) where T : Component
        {
            if (source == null || target == null) return;
            try
            {
                var sourceType = source.GetType();
                var fields = sourceType.GetFields(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
                foreach (var field in fields)
                {
                    try
                    {
                        field.SetValue(target, field.GetValue(source));
                    }
                    catch (System.Exception ex)
                    {
                        Debug.LogWarning($"Failed to copy field {field.Name}: {ex.Message}");
                    }
                }
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"Failed to copy component values: {ex.Message}");
            }
        }
        /// <summary>
        /// Struct to hold invoked components for easy access
        /// </summary>

    }
}


