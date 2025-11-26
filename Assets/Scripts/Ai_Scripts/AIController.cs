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

    private void Start()
    {
      player = GameObject.FindWithTag("PlayerCollider");

        // Ensure agent moves automatically
        if (agent != null)
        {
            agent.isStopped = false;
            agent.updateRotation = true;
        }
    }


    private void FixedUpdate()
    {
        switch (vision.state)
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
        // enemyAnimator.SetMovementAnimation(agent.velocity, currentState == AIState.Chase);
    }

    private void Idle()
    {
        if (agent.pathPending)
            agent.ResetPath();

        enemyAnimator.PlayAnimation("Idle_Absolute");
    }

    private void Chase()
    {

        Vector3 target = player.transform.position;
        if (Vector3.Distance(agent.nextPosition,target) > 1.0f && vision.aggro){
          agent.destination = target;
          transform.LookAt(player.transform);
          Debug.DrawLine(transform.position, target, Color.red);
        }          

        // Switch to investigate if reached last seen
        // if (Vector3.Distance(transform.position, target) < 0.5f)
        //     vision.state = AIState.Investigate;
    }

    private void Investigate()
    {
        Debug.DrawLine(transform.position,agent.destination);
        if (Vector3.Distance(transform.position, agent.destination) > 0.5f)
          transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(Vector3.right), 5f * Time.deltaTime);
    }
}
