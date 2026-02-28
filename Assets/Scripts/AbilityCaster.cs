using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;
using System.Threading;
using System.Threading.Tasks;
using FracturedStudios.Invoker;
using FracturedStudios;
namespace FracturedStudios.Abilities{

  //this enum contains flags for each ability. each flag is equivalent to a bitmask.
  //doing a bitwise & to an int is how you determine if the ability is castable.
  //
  [Flags]
  public enum AbilityFlags:byte{
    None = 0, Skill0 =1, Skill1 = 2, Skill2 = 4, Skill3 = 8,
    Skill4 = 16, Skill5 = 32, Skill6 =64, Skill7 = 128,
    /*
    Skill8 = 256, Skill9 = 512, Skill10 = 1024,
    Skill11 = 2048, Skill12 =4096, Skill13 = 8192,
    Skill14 = 16384, Skill15 = 32768,
    */
  }

//Holds the data needed by Ability
  public class AbilityData{
    public int Delay; //    the cooldown for ability;
    public bool Waiting = false;//  is the ability in cooldown;
    public event Action Subscribers;// event to be triggered when cast
    public void Casting(){ Subscribers?.Invoke();} // cast the ability
    public AbilityData(int delay, Action[] subscribers){
      foreach (var subscriber in subscribers)
        Subscribers += subscriber;
      Delay = delay;
    }
    //Subscribe a handler to event
    public void Subscribe(Action handler){
      Subscribers += handler;
    }
    //Unsubscribe handler in event
    public void Unsubscribe(Action handler){
      Subscribers -= handler;
    }
    // public void Clear(){
    //   Delegate[] handlers = Subscribers?.GetInvocationList();
    //   if(handlers == null || handlers.Length< 1){
    //     return;
    //   }
    //   foreach (var handler in handlers)
    //   {
    //     Unsubscribe((Action) handler);
    //   }
    // }
  }

  public readonly struct AbilityCaster{
    static readonly AbilityAtlas atlas = AbilityAtlas.GetInstance();//Atlas of abilities
    //Cast Ability
    //Params:
    //      inventory: the skills something can cast
    //      skill: the flag of the skill to cast
    public void Cast(AbilityFlags inventory, AbilityFlags skill){
      //do a bitwise & to get the skill to cast
      AbilityFlags ability = inventory & skill; 
      Cast(ability);
    }
    //Cast Ability
    //Params:
    //  flags:flag of skill to cast
    public void Cast(AbilityFlags flags){
      //Get data of skill to cast
      var skill = atlas[flags];
      if(skill == null){
          Debug.LogWarning($"[AbilityCaster] no data for flags {flags}");
          return;
      }
      // Heuristic guard: prevent flash (Skill0) until ChapterState grants the Ability_Flash flag
      if (flags == AbilityFlags.Skill0 && !ChapterStateService.IsFlashAbilityUnlocked())
      {
        VerboseLogger.SafeLog("[AbilityCaster] Skill0 blocked by ChapterState (Ability_Flash not unlocked)");
        return;
      }
      VerboseLogger.SafeLog($"[AbilityCaster] request cast {flags} waiting={skill.Waiting} delay={skill.Delay}ms");
      //Trigger the skill especially if not in cooldown
      if(skill.Waiting)
        return;
      skill?.Casting();
      VerboseLogger.SafeLog($"[AbilityCaster] {flags} invoked subscribers");
      skill.Waiting = true;
      // Asynchronously run the cooldown timer but cancel if the WorldBridgeSystem signals shutdown
      var shutdownToken = CancellationToken.None;
      try { shutdownToken = WorldBridgeSystem.Instance?.GetShutdownToken() ?? CancellationToken.None; } catch { shutdownToken = CancellationToken.None; }
      Task.Run(async ()=> {
        try
        {
          await Task.Delay(skill.Delay, shutdownToken);
          skill.Waiting = false;
          VerboseLogger.SafeLog($"[AbilityCaster] {flags} cooldown expired");
        }
        catch (OperationCanceledException)
        {
          // If we're shutting down, clear waiting so state doesn't remain stuck.
          skill.Waiting = false;
         
        }
        catch (Exception ex)
        {
          // Ensure we clear waiting on unexpected errors
          skill.Waiting = false;
          Debug.LogWarning($"[AbilityCaster] cooldown task error for {flags}: {ex.Message}");
        }
      });
    }
  }
//Ability Atlas is where you find the abilities's data
  public class AbilityAtlas{
    AbilityAtlas(){}//private constructor stopping users from initializing it without GetInstance
    public static AbilityAtlas GetInstance()=>Instance ??= new(); // Return the singleton of this or create if none
    static AbilityAtlas Instance;// the singleton instance
    public AbilityData fallBack = null;//default value if nothing can be cast
    readonly Dictionary<AbilityFlags, AbilityData> atlas = new() {
      [AbilityFlags.None] = new (100,new Action[]{()=>Debug.Log("What am I doing?")}),
      [AbilityFlags.Skill0] = new (10000, new Action[]{})
    };
    public AbilityData this[AbilityFlags flags] //Indexer for this class
    {
        get => atlas.GetValueOrDefault(flags,fallBack);
    }
// public static void Clear(){
    //   foreach (var item in Instance.atlas.Values)
    //   {
    //       item.Clear();
    //   }
    //   Instance = null;
    //
    //
    // }
  }
}
