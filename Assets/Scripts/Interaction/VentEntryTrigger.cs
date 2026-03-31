using System.Threading.Tasks;//added for tasks for later for await tokens. keep this.
using UnityEngine;
using UnityEngine.Events;
using System.Collections;
using System.Collections.Generic;
using FracturedStudios.Components;
using System;
using System.Dynamic;
using Microsoft.VisualBasic;
using System.Linq;
namespace FracturedStudios
{
    [DisallowMultipleComponent]
    public class VentEntryTrigger : MonoBehaviour
    {
        [Header("References")]
        [Tooltip("The placement zone under this vent. Assign in Inspector.")]
        [SerializeField] private LadderPlacementPoint placementZone;

        [Tooltip("The exit teleporter for this vent. Assign an empty GameObject with a Teleporter component.")]
        [SerializeField] private Teleporter exitTeleporter;

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
    
    private Action<object, Action> cutsceneSequenceHandler;

    private Action CompleteCutCallback;

        public bool HasReceivedVentEntry { get; private set; }
        public bool IsCutsceneDone { get; private set; }
        public bool Done { get; private set; }
        public Teleporter ExitTeleporter => exitTeleporter;
        public Vector3 ToPosition => exitTeleporter != null ? exitTeleporter.ToPosition : transform.position;
        public Vector3 Position => transform.position;
        public Vector3 EntryPosition => transform.position;
        public bool TeleportSignal => Done;

        [Header("Cutscene Placeholder")]
        [Tooltip("Time in seconds to wait before invoking onCutsceneComplete and continuing.")]
        [SerializeField] private float cutsceneDelaySeconds = 120f; //track this.

public VentEntryTrigger.SequenceResult ProbeCutsceneFor(object context)
{
if (cutsceneSequenceHandler == null)
return SequenceResult.Interrupted;
return IsCutsceneDone ? SequenceResult.Success : SequenceResult.Interrupted;
}
void Awake()
{
chapterState ??= ChapterStateService.Current;
if (chapterState == null)
{
    #if UNITY_EDITOR
    Debug.LogWarning($"[VentEntryTrigger] No ChapterState found  or in service. Chapter progression will not be tracked.");
#endif
}
   RegisterSequencers();

  

}


        private void OnTriggerEnter(Collider other)
        {if (other.CompareTag("Player"))
        {
             if (debugVentEntry)
                Debug.Log($"[VentEntryTrigger] OnTriggerEnter hit '{other.gameObject.name}'");
            Enter();
        
        }
        else{return;}
        }

      private void RegisterSequencers()
      {
///cutsceneSequenceHandler is a delegate that handles cutscene sequences with duplicate suppression based on context. 
/// It ensures that the cutscene logic for a given context only runs once, even if triggered multiple times. 
/// The context can be any identifier (e.g., string, enum, object) that represents the specific cutscene 
/// or event being handled. The callback is invoked only if the context has not been completed before, 
/// allowing for safe and idempotent cutscene triggering.
  cutsceneSequenceHandler = (context, callback) =>
    {
        if (!completedContexts.Contains(context))
        {
            completedContexts.Add(context);
            callback?.Invoke();
        }
        else
        {
            return; // Already done, skip.
        }
    };
///onCutsceneComplete is an event that gets invoked when the cutscene sequence is complete. 
/// It can be subscribed to by other parts of the code to perform actions that should occur after the cutscene finishes. 
/// In this implementation, it is set to invoke the cutsceneSequenceHandler with the current context 
/// and a callback for when the cutscene is complete. This allows for centralized management of cutscene completion 
/// logic and ensures that any necessary follow-up actions are executed in a controlled manner.
    onCutsceneComplete = () => { cutsceneSequenceHandler(this, CompleteCutCallback); };

    onVentEntered.AddListener(() =>
    {
        var ladder = (placementZone != null ? placementZone.PlacedLadder : null)
                     ?? FindFirstObjectByType<LadderItem>();

        ladder?.HandleVentEntered();
    });
///CompleteCutCallback is a callback function that is intended to be called when the cutscene sequence is complete. 
/// It is defined as an Action delegate and can be assigned any method that matches its signature 
/// (i.e., takes no parameters and returns void). In this implementation,
///  it is set to invoke the cutsceneSequenceHandler
///  with the current context and a callback for when the cutscene is complete. 
/// This allows for centralized management of cutscene completion logic and ensures 
/// that any necessary follow-up actions are executed in a controlled manner once the cutscene finishes.
    CompleteCutCallback = () =>
    {
            IsCutsceneDone = true;
            Done = IsCutsceneDone;
            hasFiredVentEvent = true; //called later in the flow when the cutscene is complete, ensuring the vent event is marked as fired to prevent re-entry.
        // Only run the vent context when invoked.
        return; //last call/Return. in the flow first frame.
    };



      }
        /// <summary>
        /// Programmatic entry handler (also invoked by trigger).
        /// </summary>
        void OnDisable() //reset the trigger when disabled, allowing for re-entry if needed.
        {
            hasFiredVentEvent = false; //third frame pass. if needed.
            HasReceivedVentEntry = false;
            IsCutsceneDone = false;
            Done = false;
            //call static class for ressurection. probably won't be needed but just in case for future use and testing.
            completedContexts.Clear(); //reset completed contexts to allow for re-triggering cutscenes if the trigger is re-enabled.
        }
        public void Enter()
        {
            if (hasFiredVentEvent)
            {
                      Debug.LogWarning($"[VentEntryTrigger] Enter() called but vent event has already fired. Ignoring subsequent entry.");    
                this.gameObject.SetActive(false); //disable the trigger to prevent further entries.
                return; //last call/Return. in the flow on the if re-entry / next frame.
          

            }
            HasReceivedVentEntry = true;
          
            try {

                if (chapterState != null)
                {
                    chapterState.SetFlag(nameof(IntroStage.VentEntered));
                    chapterState.ChapterStage = (int)IntroStage.VentEntered;
                }

                if (debugVentEntry)
                    Debug.Log($"[VentEntryTrigger] Vent entered at {Time.time:F2}, entryPosition={EntryPosition}, placementZone={(placementZone!=null?placementZone.name:"none")}");

                 

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
