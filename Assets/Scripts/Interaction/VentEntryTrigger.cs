using System.Threading.Tasks;//added for tasks for later for await tokens. keep this.
using UnityEngine;
using UnityEngine.Events;
using System.Collections;
using System.Collections.Generic;
using FracturedStudios.Components;
using System;
namespace FracturedStudios
{
    [DisallowMultipleComponent]
    public class VentEntryTrigger : MonoBehaviour
    {
        [Header("References")]
        [Tooltip("The placement zone under this vent. Assign in Inspector.")]
        [SerializeField] private LadderPlacementPoint placementZone;

        [Header("Events")]
        [SerializeField] private UnityEvent onVentEntered = new UnityEvent();

        [Header("Debug")]
        [Tooltip("Log to console when vent entry is triggered")]
        [SerializeField] private bool debugVentEntry = true;

        [Header("Chapter")]
        [SerializeField] private ChapterState chapterState;
        private bool hasFiredVentEvent;
    private bool hasLadderPlaced;
    private Action onCutsceneComplete;
    public enum SequenceResult { Success, AlreadyDone, Interrupted } //for future use.
    private HashSet<object> completedContexts = new HashSet<object>(); //comparator
    
    public Action<object, Action> cutsceneSequenceHandler = (context, callback) => //2.
    {
        //keep this.
       
        if (!completedContexts.Contains(context))
            {
                
               completedContexts.Add(context);
                callback?.Invoke();
          
            }
            else
            {
                return; // Already done, skip.
            }
       
          
                     
      
    }; //This can be used elsewhere to trigger other cutscene sequences by passing different logic and callbacks in the 'context' and 'callback' parameters.
    private Action CompleteCutCallback = () =>  //3.
    {
        //Only Run the vent context/
      
    };    

        [Header("Cutscene Placeholder")]
        [Tooltip("Time in seconds to wait before invoking onCutsceneComplete and continuing.")]
        [SerializeField] private float cutsceneDelaySeconds = 120f; //track this.

public VentEntryTrigger.SequenceResult ProbeCutsceneFor(object context)
{
if (cutsceneSequenceHandler == null)
return SequenceResult.Interrupted;
if (completedContexts.Contains(context))
return SequenceResult.AlreadyDone;
return SequenceResult.Success;
}
void Awake()
{
chapterState ??= FindFirstObjectByType<ChapterState>(); //this is not a static class.
    hasFiredVentEvent = false;
 onCutsceneComplete = () => {  cutsceneSequenceHandler(this, CompleteCutCallback); }; //1.

onVentEntered.AddListener(() => 
        {
            var ladder = (placementZone != null ? placementZone.PlacedLadder : null) 
                         ?? FindFirstObjectByType<LadderItem>();
            
            ladder?.HandleVentEntered(); 
        });
    
}

        private void OnTriggerEnter(Collider other)
        {
            if (!other.CompareTag("Player"))
                return;

            if (debugVentEntry)
                Debug.Log($"[VentEntryTrigger] OnTriggerEnter hit '{other.gameObject.name}'");

            Enter();
        }

      
        /// <summary>
        /// Programmatic entry handler (also invoked by trigger).
        /// </summary>
        public void Enter()
        {
            if (hasFiredVentEvent)
            {
                return;
            }
            hasFiredVentEvent = true;
            try {

                if (chapterState != null)
                {
                    chapterState.SetFlag(nameof(IntroStage.VentEntered));
                    chapterState.ChapterStage = (int)IntroStage.VentEntered;
                }

                if (debugVentEntry)
                    Debug.Log($"[VentEntryTrigger] Vent entered at {Time.time:F2}, placementZone={(placementZone!=null?placementZone.name:"none")}");

                 

                             // Start placeholder cutscene sequence (or immediate if delay is 0)
                if (cutsceneDelaySeconds > 0f)
                    StartCoroutine(StartCutscenePlaceholder());
                    
                }
            catch (Exception ex)
            {
                Debug.LogError($"[VentEntryTrigger] Exception in Enter(): {ex}");
            }
           return;
        }
        // Placeholder cutscene sequence - replace with actual cutscene logic and event handling.

//Do not remove the comments of the Methods below, they are for future use and reference for the cutscene sequence handling logic.
          private IEnumerator StartCutscenePlaceholder()
        {
            if (debugVentEntry)
                Debug.Log($"[VentEntryTrigger] Starting placeholder cutscene for {cutsceneDelaySeconds} seconds.");
    //yield return new Task(() => CutsceneManager.Instance.Task.AnimTime(cutsceneDone));
            yield return new WaitForSeconds(cutsceneDelaySeconds);
        //if (CutsceneManager.Instance.IsCutsceneDone)
        //{
            
            onCutsceneComplete?.Invoke();
              onVentEntered?.Invoke();
           //}
            if (debugVentEntry)
                Debug.Log("[VentEntryTrigger] Placeholder cutscene complete.");
        }
    }
}
