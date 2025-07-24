using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "NewCharacterList", menuName = "EvMeshPro/New Character List", order = 1)]
public class SO_CharacterList : ScriptableObject
{
	public List<CharacterProfile> characterProfiles = new List<CharacterProfile>();

	public CharacterProfile GetCharacter(string characterID) {
		foreach (CharacterProfile character in characterProfiles) {
			if (character.characterID == characterID) {
				return character;
			}
		}
		
		Debug.Log("<color=cyan>Could not find character (" + characterID + ") in current character list... </color>");
		CharacterProfile nullCharacter = new CharacterProfile();
		nullCharacter.characterName = "NULL";
		return nullCharacter;
	}
}

[Serializable]
public class CharacterProfile
{
	public string characterName;
	public string characterID;
	[TextArea] public string characterDescription;
	public Color characterColor;
	public Sprite characterSprite;
	public AudioClip[] speechSFXBlips;
}
