using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using FracturedStudios.Abilities;
using FracturedStudios.Invoker;
//Description This class handles the flash effect. 
//Fields
//  Instance: the Singleton for this object
//  BlindScreen: GameObject that holds the visuals
//  Tint: Image of the visuals when flashing
//Methods
//  Dim:Returns IEnumerator. A Coroutine that dims the effect (then ends it).
//  Flash: Initiates the Flash.
public class FlashBang : MonoBehaviour
{
  //Cursed Singleton
  public static FlashBang Instance;
  void Awake(){
    if(Instance == null){
      Instance = this;
    }else{
      Destroy(this);
    }
  }
    [SerializeField]
    GameObject BlindScreen;
    [SerializeField]
    Image Tint;
    IEnumerator Dim(){
    
      for(float alpha = 1f; alpha > 0.001f; alpha -= 0.01f){
        Debug.Log(alpha);
        Tint.color = new Color(1f,1f,1f,alpha);
        yield return new WaitForFixedUpdate();
      }
      // BlindScreen.SetActive(false);
    }
    /// <summary>
    /// Initiates the flash visual effect.
    /// </summary>
    /// <remarks>
    /// This method is subscribed to the Skill0 ability data via
    /// <c>AbilityAtlas.GetInstance()[AbilityFlags.Skill0].Subscribe(Flash)</c>.
    /// Casting of Skill0 is guarded by <see cref="FracturedStudios.Abilities.AbilityCaster.Cast(FracturedStudios.Abilities.AbilityFlags)"/>,
    /// which checks <see cref="FracturedStudios.ChapterStateService.IsFlashAbilityUnlocked()"/> and will block the cast
    /// when the <c>Ability_Flash</c> chapter flag is not set. This ensures the visual effect cannot be triggered by the
    /// normal ability path until the player has unlocked the flash ability.
    /// </remarks>
    public void Flash()
    {
      VerboseLogger.SafeLog("[FlashBang] Flash() called");
      if (BlindScreen == null)
        BlindScreen = GameObject.FindWithTag("Flash");
      BlindScreen.SetActive(true);
      StartCoroutine(Dim());
    }
    // Start is called before the first frame update
    void Start()
    {
      AbilityAtlas.GetInstance()[AbilityFlags.Skill0].Subscribe(Flash);
   
    }
    void OnDestroy(){

     AbilityAtlas.GetInstance()[AbilityFlags.Skill0].Unsubscribe(Flash);
    }

    // Update is called once per frame
    // void Update()
    // {
    //   Debug.Log(this);
    // }
}
