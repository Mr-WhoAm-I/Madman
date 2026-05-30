using Unity.Entities;
using _Project.Scripts.ECS.Components;

namespace _Project.Scripts.ECS.Systems
{
    [UpdateInGroup(typeof(FusionUpdateGroup))]
    [UpdateAfter(typeof(DamageSystem))] // Гарантируем, что смерть обрабатывается после расчета урона
    public partial struct EnemyDeathSystem : ISystem
    {
        public void OnUpdate(ref SystemState state)
        {
            var ecb = new EntityCommandBuffer(Unity.Collections.Allocator.Temp);

            // Ищем всех врагов, на которых повесили метку DeathTagComponent
            foreach (var (enemyTag, entity) in SystemAPI.Query<RefRO<EnemyTagComponent>>().WithAll<DeathTagComponent>().WithEntityAccess())
            {
                // Задел на будущее: 
                // Если (SystemAPI.HasComponent<ApathyDebuffComponent>(entity) && Заморожен) 
                // -> Спавним ледяные шипы Меланхолика
                
                // Удаляем сущность из памяти ECS
                ecb.DestroyEntity(entity);
            }

            ecb.Playback(state.EntityManager);
            ecb.Dispose();
        }
    }
}