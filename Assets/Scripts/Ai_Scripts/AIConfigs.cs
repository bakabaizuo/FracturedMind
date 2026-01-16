using System;
using UnityEngine;
using UnityEngine.AI;

namespace AIConfigs
{
  [Flags]
  public enum AITargetMask : ulong
  {
    None = 0UL,
    Player = 1UL << 0,
    Allies = 1UL << 1,
    Enemies = 1UL << 2,
    Objectives = 1UL << 3,
    LightSources = 1UL << 4,
    NoiseSources = 1UL << 5,
  }

  public struct AIVisionSettings
  {
    public readonly float maxViewDistance;
    public readonly float minViewDistance;
    public readonly float fovAngle;
    public readonly float eyeHeight;

    public AIVisionSettings(float maxViewDistance, float minViewDistance, float fovAngle, float eyeHeight)
    {
      this.maxViewDistance = maxViewDistance;
      this.minViewDistance = minViewDistance;
      this.fovAngle = fovAngle;
      this.eyeHeight = eyeHeight;
    }
  }

  public struct AIAttentionSettings
  {
    public readonly int alertLength;
    public readonly int detectLength;

    public AIAttentionSettings(int alertLength, int detectLength)
    {
      this.alertLength = alertLength;
      this.detectLength = detectLength;
    }
  }

  /// <summary>
  /// Binds navigation/animation controllers and vision origin used by perception code.
  /// </summary>
  public sealed class AIAgentBindings
  {
    public readonly NavMeshAgent navMeshAgent;
    public readonly Component playerController; // Use your concrete player controller type if available.
    public readonly Transform visionOrigin;
    public readonly float visionConeRadius;

    public AIAgentBindings(NavMeshAgent navMeshAgent, Component playerController, Transform visionOrigin, float visionConeRadius)
    {
      this.navMeshAgent = navMeshAgent;
      this.playerController = playerController;
      this.visionOrigin = visionOrigin != null ? visionOrigin : navMeshAgent != null ? navMeshAgent.transform : null;
      this.visionConeRadius = visionConeRadius;
    }
  }

  /// <summary>
  /// Bastardized "sharedcomponent" for QueryParameters.
  /// </summary>
  public ref struct targettingList
  {
    public static readonly QueryParameters[] targetLists =
    {
      new(
        0x80,
        false,
        default,
        false)
    };
  }
}
