using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Threading.Tasks;
using UnityEngine.UI;
//TODO: make into a namespace

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

public readonly struct AbilityCaster{
  // readonly AbilityManager manager = AbilityManager.GetInstance();
  public void cast(AbilityFlags inventory, AbilityFlags skill){
    cast(inventory & skill);
  }
  public void cast(AbilityFlags flags){
    IAbility skill = AbilityManager.GetInstance()[flags];
  }
}
public abstract class IAbility
{
  protected int delay;
  protected bool ready = true;
  protected abstract void skill();
  public void cast(){
    if(ready){
      skill();
      ready = false;
      Task.Delay(delay);
      ready = true;
    }
  }
}
public sealed class BadSkill:IAbility{
  protected override void skill()=>Debug.Log("????");
}
public sealed class Flash:IAbility{
  GameObject flashBang;
  Image tint;
  async void FlashScreen(){
    flashBang.SetActive(true);
    for(float alpha = 1f;alpha > 0.1f; alpha -= 0.1f ){
      tint.color = new Color(1,1,1,alpha);
      await Task.Yield();
    }
    flashBang.SetActive(false);
  }
  protected override void skill(){
    Debug.Log("Flashing!");
  }
  public Flash(){
    delay = 1000;
    flashBang = GameObject.FindWithTag("Flash");
    tint = flashBang.GetComponentInChildren<Image>(false);
  }
}
public sealed class NoSkill:IAbility{
  protected override void skill(){
    Debug.Log("What am I thinking?");
  }
   public NoSkill(){
    delay =300;
  }
}
