using _Project.Scripts.Data.Weapons;
using _Project.Scripts.ECS.Systems.Combat;
using UnityEngine;
using UnityEngine.Pool;

namespace _Project.Scripts.UI
{
    public class DamagePopupManager : MonoBehaviour
    {
        [SerializeField] private DamagePopup _popupPrefab;
        [SerializeField] private DamagePopupConfig _config;
        
        private ObjectPool<DamagePopup> _pool;

        private void Awake()
        {
            // Инициализация пула
            _pool = new ObjectPool<DamagePopup>(
                createFunc: () => Instantiate(_popupPrefab, transform),
                actionOnGet: popup => popup.gameObject.SetActive(true),
                actionOnRelease: popup => popup.gameObject.SetActive(false),
                actionOnDestroy: popup => Destroy(popup.gameObject),
                collectionCheck: false,
                defaultCapacity: 50,
                maxSize: 200 // Защита от переполнения памяти
            );
        }

        private void OnEnable()
        {
            DamageSystem.OnEnemyDamaged += SpawnPopup;
        }

        private void OnDisable()
        {
            DamageSystem.OnEnemyDamaged -= SpawnPopup;
        }

        // Добавлен bool isCrit
        private void SpawnPopup(Vector3 position, float damage, WeaponElementalType element, bool isCrit)
        {
            Vector3 spawnPos = position + Vector3.up * 0.5f;

            var popup = _pool.Get();
            popup.transform.position = spawnPos;
            
            // Передаем isCrit
            popup.Setup(damage, element, isCrit, _config, _pool);
        }
    }
}