using System.Collections;
using System.Collections.Generic;
using UnityEngine;
//[RequireComponent(typeof(BoxCollider))]
//[RequireComponent(typeof(LayerMask))]
public class CursorLockerAndUnlocker : MonoBehaviour
{
 
     private bool isCursorLocked = true; 

   private void Start()
    {
        LockCursor();
 
        //cursor lock is in here !!!!!!!!!!!!!!!!!!!!!!!!!!!CURSOR LOCK!!!!!!!!!!!!!!!
                      ///Cursor.lockState = CursorLockMode.Locked;
                      ///Cursor.visible = false;
                      ///Cursor Lock states


    }

    // Update
    void Update()
    {
        HandleCursorToggle();
    }
    /// <summary>
    /// Line Bellow Handels CursorToggles and Locks For Tab and Esc
    /// </summary>
        private void HandleCursorToggle()
    {
        // Check if the player presses Tab or Escape
        if (Input.GetKeyDown(KeyCode.Tab) || Input.GetKeyDown(KeyCode.Escape))
        {
            // Toggle cursor lock state
            if (isCursorLocked)
            {
                UnlockCursor();
            }
            else
            {
                LockCursor();
            }
        }
    }

    private void LockCursor()
    {
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
        isCursorLocked = true;
    }

    private void UnlockCursor()
    {
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        isCursorLocked = false;
    }
}

