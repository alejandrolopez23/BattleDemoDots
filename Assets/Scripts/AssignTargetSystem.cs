﻿using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Rendering;
using UnityEngine;
using Random = Unity.Mathematics.Random;

public struct Target : IComponentData
{
    public Entity Value;
}

public class AssignTargetSystem : JobComponentSystem
{
    
    EntityQuery m_AllSoldiers;
    EntityQuery m_SoldiersWithoutTarget;
    EndSimulationEntityCommandBufferSystem endSimCommandBuffer;
    struct AssignTargetJob:IJobForEachWithEntity<SoldierTag>
    {
        //public EntityCommandBuffer commandBuffer;
        public EntityCommandBuffer.Concurrent commandBuffer;
        [Unity.Collections.ReadOnly, DeallocateOnJobCompletion] public NativeArray<Entity> potentialTargets;
        public Unity.Mathematics.Random random;
        // add Target componentData
        public void Execute(Entity entity, int index, [ReadOnly]ref SoldierTag soldierTag)
        {
            Target target = new Target();
            int n = random.NextInt(0, potentialTargets.Length);
            target.Value = potentialTargets[n];
            commandBuffer.AddComponent(index, entity , target );
        }
    }

    protected override void OnCreateManager()
    {
        m_AllSoldiers = GetEntityQuery(
            ComponentType.ReadOnly<SoldierTag>(),
            ComponentType.ReadOnly<RenderMesh>() // renderMesh ? 
        );
        m_SoldiersWithoutTarget = GetEntityQuery(
            ComponentType.ReadOnly<SoldierTag>(),
            ComponentType.ReadOnly<RenderMesh>(),
            ComponentType.Exclude<Target>());
        endSimCommandBuffer = World.GetOrCreateSystem<EndSimulationEntityCommandBufferSystem>();
    }

    protected override JobHandle OnUpdate(JobHandle handle)
    {
        // filter by SharedComponentData
        // We can also filter using a componentData for each team 
        var redRenderMesh  = EntityManager.GetSharedComponentData<RenderMesh>(GetSingleton<BattleConfigData>().prefabRed);
        var blueRenderMesh = EntityManager.GetSharedComponentData<RenderMesh>(GetSingleton<BattleConfigData>().prefabBlue);
        // get red soldiers
        m_AllSoldiers.SetFilter(redRenderMesh);
        NativeArray<Entity> redSoldierEntities = m_AllSoldiers.ToEntityArray(Allocator.TempJob);
        // get blue soldiers
        m_AllSoldiers.SetFilter(blueRenderMesh);
        NativeArray<Entity> blueSoldierEntities = m_AllSoldiers.ToEntityArray(Allocator.TempJob);
        
        //CONFIGURE RED SOLDIERS
        // Schedule Job only on the Group of m_SoldiersWithoutTarget
        // Red soldiers without target 
        var random = new Unity.Mathematics.Random((uint)UnityEngine.Random.Range(1, 100000));
        // using the simple random for this example 
        //https://forum.unity.com/threads/mathematics-random-with-in-ijobprocesscomponentdata.598192/
        
        if (blueSoldierEntities.Length > 0)
        {
            m_SoldiersWithoutTarget.SetFilter(redRenderMesh); // red soldiers without target  
            handle = new AssignTargetJob
            {
                // when to use ToConcurrent ? 
                commandBuffer = endSimCommandBuffer.CreateCommandBuffer().ToConcurrent(),
                potentialTargets = blueSoldierEntities,
                random = random
            //}.ScheduleGroup(m_SoldiersWithoutTarget, handle);
            }.Schedule(m_SoldiersWithoutTarget, handle);

            endSimCommandBuffer.AddJobHandleForProducer(handle);
        }
        else
        {
            blueSoldierEntities.Dispose();
            
        }
        
        //CONFIGURE BLUE SOLDIERS
        if (redSoldierEntities.Length > 0)
        {
            m_SoldiersWithoutTarget.SetFilter(blueRenderMesh); // blue soldiers without target 
            handle = new AssignTargetJob
            {
                commandBuffer = endSimCommandBuffer.CreateCommandBuffer().ToConcurrent(),
                potentialTargets = redSoldierEntities,
                random = random
            //}.ScheduleGroup(m_SoldiersWithoutTarget, handle);
            }.Schedule(m_SoldiersWithoutTarget, handle);

            endSimCommandBuffer.AddJobHandleForProducer(handle);
        }
        else
        {
            redSoldierEntities.Dispose();
        }

        // dispose
        return handle;
    }
}
