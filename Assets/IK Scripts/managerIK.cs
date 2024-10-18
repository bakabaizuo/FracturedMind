using System.Collections;
using System.Collections.Generic;
using UnityEngine;
///This Script With the Usage of Kenetics IK will allow an NPC to look at the target "Player" Tutorial Info from Inverse Kinematics Unity Manual <summary>
/// This Script With the Usage of Kenetics IK will allow an NPC to look at the target "Player" Tutorial Info from Inverse Kinematics Unity Manual
/// </summary>
/// Make sure to set the IK in the BaseLayer and the script component to true 
public class managerIK : MonoBehaviour
{

   /// <summary>
   ///  This is the link to the animator where the animator on the far right is the animator in the assets
   ///  Animator animator;
   /// </summary>
   /// NPCAnimatorA is the Animators name not the component 
    Animator NPCAnimatorA;
    public bool ikActive = false;
    /// <summary>
    /// public Transform objTarget objTarget is the Player in this Case 
    /// objTarget can be renamed to fit project be sure to make this object and put it in the target you want
    /// the NPCAnimatorA's IK Bone/Head to look at IT will automatically work if you set it to true in the Animator Component of the NPC
    /// THE NPC's animator must be named NPCAnimatorA or can be changed!
    /// </summary>
    
    //dummy pivot CAN BE CHANGED!
    GameObject objPivot;
    public Transform objTarget;
    public float lookWeight;
    // Start is called before the first frame update
    void Start()
    {
        ///The animator component is essentail /// <animator>
        NPCAnimatorA = GetComponent<Animator>();
        ///Dummy Pivot Start \ Child of the Character
        objPivot = new GameObject("DummyPivot");
        objPivot.transform.parent = transform.parent;
        objPivot.transform.parent = transform.parent;
        objPivot.transform.localPosition = new Vector3(0, 1.40f, 0);
        ///NOTE 1.40 is the most it needs to go
    }
    void update()
    {
        objPivot.transform.LookAt(objTarget);
        float pivotRotY = objPivot.transform.localRotation.y;
        //Debug.Log(pivotRotY);
        if (pivotRotY > 0.64f && pivotRotY > - 0.64f)
        {
            //target tracking
            lookWeight =1f;
        }
        else
        {
            //target release
            lookWeight = 0f;
        }
    }
    private void OnAnimatorIK(int layerIndex)
    {
        if(NPCAnimatorA)
        {
            if(ikActive)
            {
                if(objTarget != null)
                {
                    NPCAnimatorA.SetLookAtWeight(lookWeight);
                    NPCAnimatorA.SetLookAtPosition(objTarget.position);
                }
            }
                else
                {
                    NPCAnimatorA.SetLookAtWeight(0);
                }
            }
        }
    }
    // Update is called once per frame
    ///WHEN YOU USE THIS SCRIPT MAKE SURE TO MAKE AN OBJECT CALLED objTarget!!!
