using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using System;
using UnityEngine.U2D;
using FracturedStudios.UI;
using FracturedStudios.TAB;

public class ItemEntry : MonoBehaviour
{
  [SerializeField]
  float radius;
  [SerializeField]
  GameObject EntryPrefab;
  [SerializeField]
  SpriteAtlas atlas;
  List<FracturedStudios.UI.ItemPanel> panels;
  List<RadialItem> items;
  float slice;
  public Dictionary<string, Transform> EntryRadialLookup = new Dictionary<string, Transform>(StringComparer.Ordinal);
    // Start is called before the first frame update
    void Start()
    {
    
    }
  

    private void ItemEntryEnable()
    {
         panels = new();
       RadialMenu parent;
       if(transform.parent.gameObject.TryGetComponent(out parent)){
         slice = parent.slice;
         items = parent.items;
       }
      // Validate EntryPrefab if provided
      if (EntryPrefab != null)
      {
        PrefabValidator.ValidateEntryPrefab(EntryPrefab);
      }
      else
      {
        // Attempt to find an entry prefab under Resources/UI/RadialMenu
        StartCoroutine(SearchForEntryPrefabCoroutine());
      }

      if((items?.Count ?? 0) < 1)
        return;

      // If there is an ItemMenu in parent hierarchy, hand off initialization to it.
      var menu = GetComponentInParent<ItemMenu>();
      if (menu != null)
      {
        menu.Initialize(EntryPrefab, atlas, items);
        return;
      }

      // Otherwise, create entries locally
      foreach(var it in items)
      {
        var p = CreateEntry(it);
        if (p != null) panels.Add(p);
      }

      Display();
    }
     /// <summary>
     /// 
     /// 

    private System.Collections.IEnumerator SearchForEntryPrefabCoroutine()
    {
      // Load all prefabs under Resources/UI/RadialMenu (Resources.LoadAll is synchronous)
      var candidates = Resources.LoadAll<GameObject>("UI/RadialMenu");
      // Yield one frame to keep the coroutine behaviour and allow other startup work to run
      yield return null;
      if (candidates == null || candidates.Length == 0)
      {
        Debug.LogWarning("[ItemEntry] No prefabs found in Resources/UI/RadialMenu");
        yield break;
      }

      foreach (var prefab in candidates)
      {
        if (prefab == null) continue;

        // First try heuristic binding search (string fields/properties)
        var match = PrefabValidator.FindPrefabWithBinding(prefab, "Entry");
        if (match != null)
        {
          EntryPrefab = match;
          Debug.Log($"[ItemEntry] Auto-selected EntryPrefab: {EntryPrefab.name}");
          PrefabValidator.ValidateEntryPrefab(EntryPrefab);
          yield break;
        }

        // Fallback: check for ItemPanel component
        var panel = prefab.GetComponentInChildren<ItemPanel>(true);
        if (panel != null)
        {
          EntryPrefab = prefab;
          Debug.Log($"[ItemEntry] Auto-selected EntryPrefab by ItemPanel presence: {EntryPrefab.name}");
          PrefabValidator.ValidateEntryPrefab(EntryPrefab);
          yield break;
        }
      }

      Debug.LogWarning("[ItemEntry] No suitable EntryPrefab found in Resources/UI/RadialMenu");
    }
    FracturedStudios.UI.ItemPanel CreateEntry(RadialItem item)
    {
      if (EntryPrefab == null) return null;
      GameObject entry = Instantiate(EntryPrefab, transform);
      // Try to find ItemPanel on the instantiated prefab (root or children)
      FracturedStudios.UI.ItemPanel panel = entry.GetComponentInChildren<FracturedStudios.UI.ItemPanel>(true);
      if (panel == null)
      {
        Debug.LogWarning($"[ItemEntry] Instantiated entry prefab '{EntryPrefab.name}' has no ItemPanel component.");
        return null;
      }

      // Populate panel from RadialItem
      if (item != null)
      {
        panel.Populate(item, atlas);
      }

      return panel;
    }
    void Display(){
      int max = items.Count;
      RectTransform m_RectTransform;
      for(int i =0; i < max; i++){
        if(panels[i] == null) continue;
        if(panels[i].TryGetComponent(out m_RectTransform))
        {
          m_RectTransform.anchoredPosition = new(MathF.Sin(slice * i),MathF.Cos(slice*i));
          m_RectTransform.anchoredPosition *= radius;
        }
      }

    }
    

    // Update is called once per frame
    void Update()
    {
        
    }

    // Placeholder mapping method for future extension.
    // Attempts to find a cached radial Transform by matching the source.name
    // against keys in the provided context's EntryRadialLookup. If found,
    // caches the result under the source.name and returns it; otherwise null.
    private static Transform MapRad(RadContext context, Transform source)
    {
      if (context == null || source == null) return null;

      var kv = context.EntryRadialLookup.FirstOrDefault(kv0 => kv0.Key.IndexOf(source.name, StringComparison.OrdinalIgnoreCase) >= 0);
      var val = kv.Value;
      if (val != null)
      {
        // cache by exact source name for faster future lookup
        context.EntryRadialLookup[source.name] = val;
        return val;
      }
      return null;
    }
}

// Minimal RadContext used by MapRad; can be extended later.
public class RadContext
{
  public Dictionary<string, Transform> EntryRadialLookup = new Dictionary<string, Transform>(StringComparer.Ordinal);
}
