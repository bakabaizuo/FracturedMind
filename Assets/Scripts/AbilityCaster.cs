using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;
namespace FracturedStudios.Abilities
{

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
    
}
