using System;
using System.Collections;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEngine;
using ColorUtility = UnityEngine.ColorUtility;

public class DialogueController : MonoBehaviour
{
    #region Singleton Creation
    //Create a singleton to call from anywhere (as long as prefab is in scene)
    public static DialogueController instance;
    private void Awake() {
        instance = this;
    }
    #endregion 

    [Header("Important References")]
    [SerializeField] private GameObject dialogueBoxPrefab;
    [SerializeField][Tooltip("Set this to the parent transform of where you want your dialogue boxes to spawn.")] private Transform dialogueBoxParent;
    
    [Header("Dialogue Settings")]
    [SerializeField][Tooltip("This dictates how long dialogue boxes stay on screen. Lower/Higher to make them last longer/shorter")] private int wpmReadingSpeed;
    [SerializeField] [Tooltip("Dictates how fast text appears in the box. Use 0 if you wish for it to appear immediately.")] private float textTypeSpeed = 0.1f;
    public SO_CharacterList characterList;
    public SO_TextStyleList textStyleList;

    [Header("Textbox Lerp Settings")]
    [SerializeField][Tooltip("Lerp the texbox into place once it is called. This can be configured on the TextBox prefabs UiLerpElement component.")] 
    private bool lerpDialogueBoxesIn = false;
    [SerializeField][Tooltip("True: Lerp effect is only applied to the first box showing up in the que. \n" +
                            "False: Lerp effect is applied to every box in the que.")] 
    private bool onlyLerpFirstBoxInQue = false;
    
    
    private List<GameObject> dialogueInstanceQue = new List<GameObject>();
    private Coroutine queIterationCoroutine;
    private bool firstQueIndex = false; //Lets us know if this is the first textbox in the current que (IMPROVE THIS PLEASE)

    //Base method that only utilizes dialogue
    public void NewDialogueInstance(string dialogue) {
        GameObject newDialogueBox = Instantiate(dialogueBoxPrefab, dialogueBoxParent);
        newDialogueBox.GetComponent<Textbox>().InitializeTextbox(ParseDialogueCustomStyle(dialogue));
        newDialogueBox.SetActive(false);
        
        dialogueInstanceQue.Add(newDialogueBox);
        if (queIterationCoroutine == null) {
            firstQueIndex = true;
            queIterationCoroutine = StartCoroutine(IterateQue());
        }
    }
    
    public void NewDialogueInstance(string dialogue, string characterID) {
        if (characterList == null) {
            Debug.Log("<color=cyan>Trying to reference a characterID, however there is no CharacterList referenced in your DialogueController.</color>");
            return;
        }
        
        CharacterProfile characterProfile = characterList.GetCharacter(characterID);

        if (characterProfile.characterName == "NULL") {
            Debug.Log("<color=cyan>GetCharacter returned NULL. Not creating new dialogue instance.. Sorry </color>");
            return;
        }
        
        GameObject newDialogueBox = Instantiate(dialogueBoxPrefab, dialogueBoxParent);
        newDialogueBox.GetComponent<Textbox>().InitializeTextbox(dialogue, characterProfile);
        newDialogueBox.SetActive(false);
        
        dialogueInstanceQue.Add(newDialogueBox);
        if (queIterationCoroutine == null) {
            firstQueIndex = true;
            queIterationCoroutine = StartCoroutine(IterateQue());
        }
    }
    

    private IEnumerator IterateQue() {
        dialogueInstanceQue[0].SetActive(true);

        //If user wants dialogue to lerp in, then only call it on the first textbox in the que
        if (lerpDialogueBoxesIn) {
            if (dialogueInstanceQue[0].TryGetComponent(out UILerpElement lerpElement)) {
                if (onlyLerpFirstBoxInQue && firstQueIndex) {
                    lerpElement.StartLerp();
                }

                if (!onlyLerpFirstBoxInQue) {
                    lerpElement.StartLerp();
                }
                
            } else {
                Debug.Log("<color=cyan>Trying to lerp dialogue box, however we could not find UILerpElement.cs on it!</color>");
            }
        }

        //Get how long the dialogue box should appear for
        Textbox currentTextBox = dialogueInstanceQue[0].GetComponent<Textbox>();
        float displayLength = currentTextBox.dialogue.Split(' ').Length / (wpmReadingSpeed / 60);

        if (currentTextBox.dialogue.Length * textTypeSpeed >= displayLength) {
            Debug.LogWarning("<color=cyan>Your textTypeSpeed is too slow in comparison to your wpmReadingSpeed. Dialogue box will disappear before all text is shown.</color>");
        }
        
        currentTextBox.DisplayText(textTypeSpeed);

        yield return new WaitForSeconds(displayLength);
        
        var toDestroy = dialogueInstanceQue[0];
        dialogueInstanceQue.Remove(toDestroy);
        Destroy(toDestroy);

        firstQueIndex = false;
        
        if (dialogueInstanceQue.Count > 0) {
            queIterationCoroutine = StartCoroutine(IterateQue());
        } else {
            queIterationCoroutine = null;
        }
    }
    
    //---------------------------------------------- Parsing Text For Custom Styles -----------------------------------------------------------------

    [ContextMenu("Test Parse")]
    public void TestParse() {
        Debug.Log(ParseDialogueCustomStyle("This is a test to find [TEST] and [/TEST] see [WHEENENNEN] how many times we can find it.[/WHEENENNEN]"));
    }

    public string ParseDialogueCustomStyle(string toParse) {
        string rawString = toParse;
        if (rawString.Contains("[")) {
            if (textStyleList == null) {
                Debug.Log("<color=cyan>Custom style tag detected in string, however DialogueController does not have a StyleSheetList referenced!</color>");
                return rawString;
            }
            string pattern = @"\[[A-Za-z]+\]"; //Regex pattern to find '[WORDSINHERE]' 
            string richtextString = toParse;

            foreach (Match match in Regex.Matches(rawString, pattern)) {
                //Get the starting tag index in our raw string
                string matchedTag = match.ToString();
                int tagStartIndex = rawString.IndexOf(matchedTag);
                int stringStartIndex = tagStartIndex + matchedTag.Length;
                
                //Get our ending tag index in our raw string
                string closeTag = matchedTag.Insert(1, @"/");
                int tagEndIndex = rawString.IndexOf(closeTag) + closeTag.Length;
                int stringEndIndex = tagEndIndex - closeTag.Length;

                string taggedString = rawString.Substring(tagStartIndex, tagEndIndex - tagStartIndex);
                string taglessString = rawString.Substring(stringStartIndex, stringEndIndex - stringStartIndex);
                // Debug.Log(taglessString); //String with the tags removed

                //Retrieve the textStyle class relative to this chunk of text
                CustomTextStyle textStyle = textStyleList.GetTextStyle(matchedTag.Replace("[", "").Replace("]", ""));
                if (textStyle == null) {
                    Debug.Log("<color=cyan>Could not find the custom text style [" + matchedTag + "] in your text: "+ rawString +"</color>");
                    return rawString;
                }
                
                //Apply RichText tags here!!!!!!!!!!!! 
                if (textStyle.isAllCaps) {
                    taglessString = "<allcaps>" + taglessString + "</allcaps>";
                }
                
                if (textStyle.overrideCharacterSpacing) {
                    taglessString = "<cspace=" + textStyle.spacingSize + ">" + taglessString + "</cspace>";
                }
                
                if (textStyle.isStrikeThrough) {
                    taglessString = "<s>" + taglessString + "</s>";
                }

                if (textStyle.isUnderLine) {
                    taglessString = "<u>" + taglessString + "</u>";
                }

                if (textStyle.isBold) {
                    taglessString = "<b>" + taglessString + "</b>";
                }
                
                if (textStyle.isItalic) {
                    taglessString = "<i>" + taglessString + "</i>";
                }

                if (textStyle.overrideColor) {
                    string colourHex = ColorUtility.ToHtmlStringRGB(textStyle.textColor);
                    taglessString = "<color=#" + colourHex + ">" + taglessString + "</color>";
                }

                if (textStyle.isHighlighted) {
                    string colourHex = ColorUtility.ToHtmlStringRGB(textStyle.highLightColor);
                    taglessString = "<mark=#" + colourHex + "aa>" + taglessString + "</mark>";
                }

                if (textStyle.overrideFontSize) {
                    string sizeValue = textStyle.sizeChangeAsPercent ? textStyle.fontSize + "%" : textStyle.fontSize.ToString();
                    taglessString = "<size=" + sizeValue + ">" + taglessString + "</size>";
                }

                if (textStyle.useTextAnimation) {
                    taglessString = "<animate=" + textStyle.textAnimationSettings.GetSettingsSeed() + ">" + taglessString + "</animate>";
                }      
                
                
                
                rawString = rawString.Replace(taggedString, taglessString);
            }
            
            return rawString;
        } else {
            return rawString;
        }
    }

    public string ParseDialogueCustomStyle(string toParse, bool removeAnimationTags) {
        string textToReturn = ParseDialogueCustomStyle(toParse);
        if (removeAnimationTags && textToReturn.Contains("<animate")) {
            var animStartIndex = textToReturn.IndexOf("<animate");
            var animEndIndex = 0;
            for (int i = 0; i < textToReturn.Length; i++) {
                if (textToReturn[i] == '>') {
                    animEndIndex = i+1;
                    break;
                }
            }
            textToReturn = textToReturn.Remove(animStartIndex, animEndIndex - animStartIndex);
            textToReturn = textToReturn.Replace("</animate>", "");
            return textToReturn;
        } else {
            return textToReturn;
        }
        
    }

}
