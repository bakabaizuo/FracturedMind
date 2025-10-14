using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// by @kurtdekker - to make a Unity singleton that has some
// prefab-stored, data associated with it, eg a music manager
//
// To use: access with PlayerPrefabLoader.Instance
//
// To set up:
//	- Copy this file (duplicate it)
//	- rename class PlayerPrefabLoader to your own classname
//	- rename CS file too
//	- create the prefab asset associated with this singleton
//		NOTE: read docs on Resources.Load() for where it must exist!!
//
// DO NOT DRAG THE PREFAB INTO A SCENE! THIS CODE AUTO-INSTANTIATES IT!
//
// I do not recommend subclassing unless you really know what you're doing.

public class PlayerPrefabLoader: MonoBehaviour
{
	// This is really the only blurb of code you need to implement a Unity singleton
	private static PlayerPrefabLoader _Instance;
	public static PlayerPrefabLoader Instance
	{
		get
		{
			if (!_Instance)
			{
				// NOTE: read docs to see directory requirements for Resources.Load!
				var prefab = Resources.Load<GameObject>("PathToYourPlayerPrefabLoader");
				// create the prefab in your scene
				var inScene = Instantiate<GameObject>(prefab);
				// try find the instance inside the prefab
				_Instance = inScene.GetComponentInChildren<PlayerPrefabLoader>();
				// guess there isn't one, add one
				if (!_Instance) _Instance = inScene.AddComponent<PlayerPrefabLoader>();
				// mark root as DontDestroyOnLoad();
				DontDestroyOnLoad(_Instance.transform.root.gameObject);
			}
			return _Instance;
		}
	}

	// implement your Awake, Start, Update, or other methods here...
}
