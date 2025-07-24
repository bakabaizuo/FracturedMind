using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEditor;

[CustomEditor(typeof(SO_TextStyleList))]
public class StyleSheetCustomEditor : Editor
{
   //Custom Editor script for previewing changes to custom style tags
   private GameObject previewGameObject; //References the currently instantiated preview canvas
   private int selected = 0; //Keeps track of the selected ID index
   private string previewText = "Custom text style preview text...";
   
   public override void OnInspectorGUI() {
      SO_TextStyleList previewList = (SO_TextStyleList)target;
      
      GUILayout.Box("Use the dropdown below to select what custom text style you wish to preview, then hit the \"Preview In Scene\" button.");
      
      //Create a popup of ID's on the current custom style list
      List<string> popupOptions = new List<string>();
      foreach (CustomTextStyle style in previewList.customTextStyles) {
         popupOptions.Add(style.tagID);
      }
      string[] options = popupOptions.ToArray();
      selected = EditorGUILayout.Popup("Text Style To Test:", selected, options); 
      
      
      if (GUILayout.Button("Preview In Scene")) {
        PreviewInScene(options[selected]);
      }

      previewText = GUILayout.TextField(previewText, 50);
      
      GUILayout.Space(10f);
      
      base.OnInspectorGUI();
   }

   private void OnDisable() {
      if (previewGameObject) {
         DestroyImmediate(previewGameObject);
      }
   }

   public void PreviewInScene(string id) {
      SO_TextStyleList previewList = (SO_TextStyleList)target;

      //Destroy our current preview object in the scene (cleaner than replacing shit)
      if (previewGameObject) { DestroyImmediate(previewGameObject); }

      //Instantiate our preview canvas from resources and locate its text asset
      GameObject previewObj = Resources.Load<GameObject>("Canvas_PreviewExample");
      previewGameObject = Instantiate(previewObj, Vector3.zero, Quaternion.identity);
      TMP_Text textAsset = previewGameObject.GetComponentInChildren<TMP_Text>();

      //Create the preview text and add our ID tags to both ends
      string textToPreview = "[" + id + "]" + previewText + "[/" + id + "]";

      //Create a dialogue controller on our preview canvas and use that to generate our rich text string
      DialogueController previewDialogueController = previewGameObject.AddComponent<DialogueController>();
      previewDialogueController.textStyleList = previewList;
      string parsedText = previewDialogueController.ParseDialogueCustomStyle(textToPreview, true);

      //Assign our new rich text to the text asset on our preview canvas
      textAsset.text = parsedText;
   }
}
