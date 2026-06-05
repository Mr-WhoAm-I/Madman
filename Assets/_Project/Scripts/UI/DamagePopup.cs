using _Project.Scripts.Data.Weapons;
using TMPro;
using UnityEngine;
using UnityEngine.Pool;

namespace _Project.Scripts.UI
{
    public class DamagePopup : MonoBehaviour
    {
        [SerializeField] private TextMeshPro _textMesh;
        
        private IObjectPool<DamagePopup> _pool;
        private DamagePopupConfig _config;
        
        private float _timer;
        private Vector3 _moveVector;
        private Color _currentColor;

        // Добавлен аргумент bool isCrit
        public void Setup(float damage, WeaponElementalType element, bool isCrit, DamagePopupConfig config, IObjectPool<DamagePopup> pool)
        {
            _pool = pool;
            _config = config;
            _timer = config.lifeTime;

            var settings = config.GetSettings(element);
            
            // Если это крит - переопределяем цвет, размер и префикс
            string finalPrefix = isCrit ? config.critPrefix : settings.prefix;
            _currentColor = isCrit ? settings.critColor : settings.color;
            float finalFontSize = isCrit ? settings.fontSize * config.critSizeMultiplier : settings.fontSize;

            // Применяем настройки
            _textMesh.text = $"{finalPrefix}{Mathf.RoundToInt(damage)}";
            _textMesh.color = _currentColor;
            _textMesh.fontSize = finalFontSize;
            if (settings.font != null) _textMesh.font = settings.font;

            // Задаем случайное направление
            float randomX = Random.Range(-config.randomJitter, config.randomJitter);
            _moveVector = new Vector3(randomX, config.moveSpeed, 0f);
        }

        private void Update()
        {
            // Анимация движения
            transform.position += _moveVector * Time.deltaTime;

            // Анимация затухания (Alpha)
            _timer -= Time.deltaTime;
            float fadeRatio = _timer / _config.lifeTime;
            
            _currentColor.a = fadeRatio;
            _textMesh.color = _currentColor;

            if (_timer <= 0f)
            {
                _pool.Release(this); // Возвращаем себя в пул
            }
        }
    }
}