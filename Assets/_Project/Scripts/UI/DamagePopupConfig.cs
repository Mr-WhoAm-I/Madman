using System;
using System.Collections.Generic;
using _Project.Scripts.Data.Weapons;
using TMPro;
using UnityEngine;

namespace _Project.Scripts.UI
{
    [Serializable]
    public struct DamageVisualSettings
    {
        public WeaponElementalType element;
        public Color color;
        public Color critColor;
        public float fontSize;
        public TMP_FontAsset font;
        public string prefix; 
    }

    [CreateAssetMenu(fileName = "DamagePopupConfig", menuName = "Madman/UI/Damage Popup Config")]
    public class DamagePopupConfig : ScriptableObject
    {
        [Header("Глобальные настройки")]
        public float lifeTime = 1.0f;
        public float moveSpeed = 2.0f;
        public float randomJitter = 1.5f; 

        [Header("Стили стихий")]
        public List<DamageVisualSettings> visualSettings;

        [Header("Настройки Крита")]
        public float critSizeMultiplier = 1.5f;
        public string critPrefix = "КРИТ ";

        public DamageVisualSettings GetSettings(WeaponElementalType element)
        {
            foreach (var setting in visualSettings)
            {
                if (setting.element == element) return setting;
            }
            return visualSettings.Count > 0 ? visualSettings[0] : default; 
        }
    }
}