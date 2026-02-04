using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;
using FracturedStudios;

public class Dialogue : MonoBehaviour
{
    public TextMeshProUGUI textComponent;
    public string[] lines;
    public float textSpeed;
    [SerializeField] private ChapterState chapterState;
    private int index;

    void Awake()
    {
        chapterState = new ChapterState();
    }
    // Start is called before the first frame update
    void Start()
    {
        textComponent.text = string.Empty;
        StartDialogue();
    }
  
    // Update is called once per frame
    void Update()
    {
        if (Input.GetMouseButtonDown(0))
        {
            if (textComponent.text == lines[index])
            {
                NextLine();
            }
            else
            {
                StopAllCoroutines();
                textComponent.text = lines[index];
            }
        }
    }

    void StartDialogue()
    {
        index = 0;
        StartCoroutine(TypeLine());
    }

    IEnumerator TypeLine()
    {
        foreach (char c in lines[index].ToCharArray())
        {
            textComponent.text += c;
            yield return new WaitForSeconds(textSpeed);
        }
    }

    void NextLine()
    {
        if (index < lines.Length - 1)
        {
            index++;
            textComponent.text = string.Empty;
            StartCoroutine(TypeLine());
        }
        else
        {
            gameObject.SetActive(false);
       
        }
    }


  
///Dialogue Tracking Area   
enum IntroDialogueComplete
    {
          None = 0,
        BulliedInHallDone = 1,
        LadderFoundDone = 2,
        VentEnteredDone = 3,
        LibraryEnteredDone = 4,
        LampExplodedDone = 5,
        BookTakenDone = 6,
        PortalPulledDone = 7,
    }

    //Switch StateMachine for Chapters Area
    //we use public enum IntroStage for driving the state machine
    private IntroDialogueComplete IntroSwitch()
    {
      if (chapterState.CurrentChapter != ChapterId.Intro)
        return IntroDialogueComplete.None;
        //(Enum)int.introStage = return flag && dialogue;
    switch ((IntroStage)chapterState.ChapterStage)
    {       //Enum.int
          case IntroStage.None:
            return IntroDialogueComplete.None;
            //Enum.int
        case IntroStage.BulliedInHall:
            IntroBullyHallDialogueFlags();
            return IntroDialogueComplete.BulliedInHallDone;
            //Enum.int
        case IntroStage.LadderFound:
           // IntroLadderFoundDialogueFlags();
            return IntroDialogueComplete.LadderFoundDone;
            //Enum.int
        case IntroStage.VentEntered:
           // IntroVentEnteredDialogueFlags();
            return IntroDialogueComplete.VentEnteredDone;
            //debugging
        default:
            Debug.LogWarning("Unhandled IntroStage: " + chapterState.ChapterStage);
            return IntroDialogueComplete.None;
    }

    return IntroDialogueComplete.None;
}


    
    void IntroBullyHallDialogueFlags()
    {
        chapterState.CurrentChapter = ChapterId.Intro;
        chapterState.ChapterStage = (int)IntroStage.BulliedInHall;
        chapterState.SetFlag("Intro_BullyHall");
    }






}