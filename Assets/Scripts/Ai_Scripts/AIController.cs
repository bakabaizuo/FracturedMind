using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

public enum AIState { Idle, Chase, Investigate }
public class AIController : MonoBehaviour
{
    [Header("References")]
    public NavMeshAgent agent;
    public AIVision vision;
    public EnemyAnimatorController enemyAnimator;
GameObject player;
    private AIState currentState = AIState.Idle;

    private void Start()
    {
      player = GameObject.FindWithTag("Player");

        // Ensure agent moves automatically
        if (agent != null)
        {
            agent.isStopped = false;
            agent.updateRotation = true;
        }
    }


    private void FixedUpdate()
    {
        currentState = vision.state;
        switch (currentState)
        {
            case AIState.Idle:
                Idle();
                break;

            case AIState.Chase:
                Chase();
                break;

            case AIState.Investigate:
                Investigate();
                break;
        }

        // Update movement animation every frame
        enemyAnimator.SetMovementAnimation(agent.velocity, currentState == AIState.Chase);
    }

    private void Idle()
    {
        if (!agent.pathPending && agent.remainingDistance < 0.1f)
            agent.ResetPath();

        enemyAnimator.PlayAnimation("Idle_Absolute");
    }

    private void Chase()
    {
        /*if (!vision.HasLastSeenPosition())
        {
            currentState = AIState.Idle;
            return;
        }*/

        Vector3 target = player.transform.position;
        target.y = 0;
        agent.SetDestination(target);
        //GetComponent<CharacterController>().Move(move* Time.deltaTime);
           // Update rotation
        if (agent.nextPosition - target != Vector3.zero)
          transform.LookAt(player.transform);
            //transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(move), 5f * Time.deltaTime);

        // Debug line to see target
        Debug.DrawLine(transform.position, target, Color.red);
        //Debug.Log($"[AI] Moving toward player at {target}");

        // Switch to investigate if reached last seen
        if (Vector3.Distance(transform.position, target) < 0.5f)
            vision.state = AIState.Investigate;
    }

    private void Investigate()
    {

        if (Vector3.Distance(transform.position, player.transform.position) < 0.5f)
          transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(Vector3.right), 5f * Time.deltaTime);

          //  vision.state = AIState.Idle;
    }
}
