using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ScriptAnimatorPlayer : MonoBehaviour
{
    private Animator animator;
    private string currentAnimation = "";
    private Vector2 movement = Vector2.zero; // Declare the movement variable
    private int currentIdle = 0;

    // Start is called before the first frame update
    void Start()
    {
        animator = GetComponent<Animator>();

        // Start with the "Crouching_Absolute" animation
        ChangeAnimation("Crouching_Absolute");

        // Start the idle animation cycling coroutine
        StartCoroutine(ChangeIdle());
    }

    void Update()
    {
        // Get player input
        movement = new Vector2(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical"));
        
        // Check for animation changes
        CheckAnimation();
    }

    private void CheckAnimation()
    {
        if (movement.y == 1)
            ChangeAnimation("Run_N_Absolute");
        else if (movement.y == -1)
            ChangeAnimation("Run_S_Absolute");
        else if (movement.x == 1)
            ChangeAnimation("Dodge_Absolute");
        else if (movement.x == -1)
            ChangeAnimation("Dodge_Absolute");
        else
            CheckIdle(); // Default animation when no input
    }

    private void CheckIdle()
    {
        switch (currentIdle)
        {
            case 0:
                ChangeAnimation("idle_Absolute");
                break;
            case 1:
                ChangeAnimation("idle2_Absolute");
                break;
            case 2:
                ChangeAnimation("idle_Absolute");
                break;
            case 3:
                ChangeAnimation("idle2_Absolute");
                break;
            case 4:
                ChangeAnimation("idle_Absolute");
                break;
            case 5:
                ChangeAnimation("idle2_Absolute");
                break;
            case 6:
                ChangeAnimation("idle0_Absolute");
                break;
            default:
                ChangeAnimation("idle_Absolute");
                break;
        }
    }

    private void ChangeAnimation(string animation, float crossfade = 0.5f)
    {
        if (currentAnimation != animation)
        {
            currentAnimation = animation;
            animator.CrossFade(animation, crossfade);
        }
    }

    private IEnumerator ChangeIdle()
    {
        while (true)
        {
            yield return new WaitForSeconds(2f); // Wait for 2 seconds
            currentIdle++;
            if (currentIdle >= 7) // Reset the idle counter after 7
                currentIdle = 0;
        }
    }
}
