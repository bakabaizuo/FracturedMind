using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;
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
public class AbilityManager
{
  AbilityManager(){}
  public static AbilityManager GetInstance()=>Instance ??= new();
  static AbilityManager Instance;
  public IAbility fallBack = new BadSkill();
  readonly Dictionary<AbilityFlags, IAbility> AbilityAtlas = new() {
    [AbilityFlags.None] = new NoSkill(),
    [AbilityFlags.Skill0] = new Flash()
  };
   public IAbility this[AbilityFlags flags]
    {
        get => AbilityAtlas.GetValueOrDefault(flags,fallBack);
    }
}
