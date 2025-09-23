using Unity.Burst;
using Unity.Entities;

partial struct EnemyMoveSystem : ISystem
{
    [BurstCompile]
    public void OnCreate(ref SystemState state)
    {
      state.RequireForUpdate<Tags.SelfMove>();
//      state.RequireForUpdate<Transform>();
    }

    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        
    }

    [BurstCompile]
    public void OnDestroy(ref SystemState state)
    {
        
    }
}
