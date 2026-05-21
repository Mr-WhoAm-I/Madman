using System.Collections.Generic;
using UnityEngine;

namespace _Project.Scripts.Gameplay
{
    public class SpawnZone : MonoBehaviour
    {
        // Статический словарь, чтобы наш Режиссер Волн мог мгновенно найти любую зону по её ID
        public static readonly Dictionary<int, SpawnZone> AllZones = new();

        [Header("Настройки зоны")]
        [Tooltip("Уникальный номер зоны (соответствует ID в Сценарии Волны)")]
        public int zoneID = 1;
        
        [Tooltip("Длина линии (разлома), вдоль которой появляются враги")]
        public float lineLength = 10f;

        private void OnEnable()
        {
            // Как только зона появляется на сцене, она заносит себя в общий список
            AllZones[zoneID] = this;
        }

        private void OnDisable()
        {
            AllZones.Remove(zoneID);
        }

        // Этот метод будет вызывать Режиссер, чтобы понять, куда воткнуть врага
        public Vector3 GetRandomPoint()
        {
            // Выбираем случайную точку от левого края до правого
            var randomOffset = Random.Range(-lineLength / 2f, lineLength / 2f);
            
            // transform.right учитывает вращение объекта! 
            // Если ты повернешь зону в Unity, враги всё равно будут спавниться строго вдоль этой повернутой линии.
            return transform.position + transform.right * randomOffset;
        }

        // МАГИЯ ДЛЯ РЕДАКТОРА: Рисуем линию, чтобы её было видно при сборке уровня
        private void OnDrawGizmos()
        {
            Gizmos.color = Color.magenta;
            var start = transform.position - transform.right * (lineLength / 2f);
            var end = transform.position + transform.right * (lineLength / 2f);
            
            // Рисуем сам разлом
            Gizmos.DrawLine(start, end);
            
            // Рисуем отметки (центр и края)
            Gizmos.color = Color.red;
            Gizmos.DrawSphere(transform.position, 0.2f); // Центр
            Gizmos.DrawSphere(start, 0.1f); // Левый край
            Gizmos.DrawSphere(end, 0.1f); // Правый край
        }
    }
}