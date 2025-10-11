using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

public class AIController : MonoBehaviour
{
    [Header("References")]
    public NavMeshAgent agent;
    public AIVision vision;
    public EnemyAnimatorController enemyAnimator;

    private enum AIState { Idle, Chase, Investigate }
    private AIState currentState = AIState.Idle;

    private void Start()
    {
        if (vision != null)
            vision.OnPlayerDetected += OnPlayerDetected;

        // Ensure agent moves automatically
        if (agent != null)
        {
            agent.isStopped = false;
            agent.updateRotation = true;
        }
    }

    void OnTriggerEnter(Collider other){
      Debug.Log(other.name);
    }
    private void OnPlayerDetected(Transform player, bool isCrouching)
    {
        currentState = AIState.Chase;
        agent.SetDestination(player.position);

        // Set animation: walk if crouching, run otherwise
        enemyAnimator.PlayAnimation(isCrouching ? "Walk_N_Absolute" : "Run_N_Absolute");
    }

    private void Update()
    {
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
        if (!vision.HasLastSeenPosition())
        {
            currentState = AIState.Idle;
            return;
        }

        Vector3 target = vision.GetLastSeenPosition();
        agent.SetDestination(target);
          Vector3 move = agent.nextPosition - transform.position;
        move.y = 0; // prevent lifting
        GetComponent<CharacterController>().Move(move* Time.deltaTime);
           // Update rotation
        if (move != Vector3.zero)
            transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(move), 5f * Time.deltaTime);

        // Debug line to see target
        Debug.DrawLine(transform.position, target, Color.red);
        //Debug.Log($"[AI] Moving toward player at {target}");

        // Switch to investigate if reached last seen
        if (Vector3.Distance(transform.position, target) < 0.5f)
            currentState = AIState.Investigate;
    }

    private void Investigate()
    {
        if (!vision.HasLastSeenPosition())
        {
            currentState = AIState.Idle;
            return;
        }

        Vector3 target = vision.GetLastSeenPosition();
        agent.SetDestination(target);

        if (Vector3.Distance(transform.position, target) < 0.5f)
            currentState = AIState.Idle;
    }
}
