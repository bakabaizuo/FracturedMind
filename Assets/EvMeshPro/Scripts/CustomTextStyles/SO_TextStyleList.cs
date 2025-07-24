using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "NewTextStyleList", menuName = "EvMeshPro/New Text Style List", order = 2)]
public class SO_TextStyleList : ScriptableObject
{
	// [TextArea]public string previewSampleText = "This is the sample text used when you are testing our your custom style in scene view!";

	public List<CustomTextStyle> customTextStyles = new List<CustomTextStyle>();
	
	public CustomTextStyle GetTextStyle(string textTagID) {
		foreach (CustomTextStyle textStyle in customTextStyles) {
			if (textStyle.tagID == textTagID) {
				return textStyle;
			}
		}
		
		Debug.Log("<color=cyan>Could not find text style (" + textTagID + ") in current custom style list... </color>");
		CustomTextStyle nullText = new CustomTextStyle();
		nullText.tagID = "NULL";
		return nullText;
	}
}

[Serializable]
public class CustomTextStyle
{
	[Header("Settings")]
	public string tagID;
	
	[Header("Text Color")]
	public bool overrideColor = false;
	public Color textColor;

	[Header("Text Style")]
	public bool overrideFontSize = false;
	public bool sizeChangeAsPercent = false;
	[Range(0, 200)] public float fontSize = 50;
	public bool overrideCharacterSpacing = false;
	[Range(0, 50)] public float spacingSize = 25;
	[Space]
	public bool isBold = false;
	public bool isItalic = false;
	public bool isAllCaps = false;
	public bool isStrikeThrough = false;
	public bool isUnderLine = false;
	[Space]
	public bool isHighlighted = false;
	public Color highLightColor;
	[Space]
	public bool useTextAnimation = false;
	public TextAnimationInfo textAnimationSettings;
}
