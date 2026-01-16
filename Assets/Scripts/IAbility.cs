using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Threading.Tasks;
using System;
//TODO: make into a namespace

namespace FracturedStudios.Abilities{

[Flags]
public enum AbilityFlags:byte{
  None = 0,
  Skill0 =1,
  Skill1 = 2,
  Skill2 = 4,
  Skill3 = 8,
  Skill4 = 16,
  Skill5 = 32,
  Skill6 =64,
  Skill7 = 128,
  /*
  Skill8 = 256,
  Skill9 = 512,
  Skill10 = 1024,
  Skill11 = 2048,
  Skill12 =4096,
  Skill13 = 8192,
  Skill14 = 16384,
  Skill15 = 32768,
  */
}


//TODO: Remove IAbility or change it into a flatter structure. Use function composition to do that
public abstract class IAbility
{
  protected int delay;
  protected bool waiting = true;
  protected event Action Casting;
  // protected abstract void skill();
  public async void CoolDown(){
      await Task.Delay(delay);
      waiting = false;
  }
  public void cast(){
    if(waiting)
      return;
    
    Casting?.Invoke();
    waiting = true;
    Task.Run(CoolDown);
  }
}
public sealed class BadSkill:IAbility{
  public BadSkill()=>delay=300;
  protected event Action Casting = ()=>Debug.Log("????");
}
public sealed class Flash:IAbility{
  //TODO:make a FlashBang GameObject and other objects that need the the flash
  
  // protected override void skill(){
  //   Debug.Log("Flashing!");
  //   Casting?.Invoke();
  // }
  public Flash(){
    delay = 1000;
    GameObject flash = GameObject.FindWithTag("Flash");
    if(flash.TryGetComponent(out FlashBang flashBang))
      Casting += flashBang.Flash;
  }
}
public sealed class NoSkill:IAbility{
  protected event Action Casting = ()=>Debug.Log("What are you doing?");

   public NoSkill(){
    delay =300;
  }
}
}
