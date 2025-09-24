using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Animator))]
public class SimpleIKTest : MonoBehaviour
{
    public Transform lookTarget;  // assign an object in front of the player
    private Animator animator;

    void Start()
    {
        animator = GetComponent<Animator>();
    }

    void OnAnimatorIK(int layerIndex)
    {
        if (!animator) return;

        // Always force IK LookAt, no conditions
        animator.SetLookAtWeight(1f, 0.5f, 1f, 0.3f, 0.5f);
        animator.SetLookAtPosition(lookTarget.position);
    }
}
