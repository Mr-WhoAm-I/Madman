using Unity.Collections;
using Unity.Entities;
using UnityEngine;
using _Project.Scripts.Data;

namespace _Project.Scripts.ECS.Authoring
{
    // Структура, которая будет храниться в памяти ECS
    public struct EnemyPrefabElement : IBufferElementData
    {
        public FixedString64Bytes EnemyName; // Имя карточки (чтобы Режиссер мог найти нужного врага)
        public Entity PrefabEntity;          // Сама запеченная сущность
    }

    public class EnemyRegistryAuthoring : MonoBehaviour
    {
        [Header("База всех врагов в игре")]
        [Tooltip("Перетащи сюда все карточки EnemyDefinitionData")]
        public EnemyDefinitionData[] allEnemies;

        class Baker : Baker<EnemyRegistryAuthoring>
        {
            public override void Bake(EnemyRegistryAuthoring authoring)
            {
                var entity = GetEntity(TransformUsageFlags.None);
                
                // Добавляем буфер (список) к нашему объекту-реестру
                var buffer = AddBuffer<EnemyPrefabElement>(entity);

                foreach (var enemy in authoring.allEnemies)
                {
                    if (enemy != null && enemy.enemyPrefab != null)
                    {
                        buffer.Add(new EnemyPrefabElement
                        {
                            // FixedString64Bytes - это специальная строка, которая быстро работает в ECS
                            EnemyName = new FixedString64Bytes(enemy.name),
                            PrefabEntity = GetEntity(enemy.enemyPrefab, TransformUsageFlags.Dynamic)
                        });
                    }
                }
            }
        }
    }
}