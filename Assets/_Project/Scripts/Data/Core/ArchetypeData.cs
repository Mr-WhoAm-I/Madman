using _Project.Scripts.Data.Skills;
using _Project.Scripts.Data.Weapons;
using UnityEngine;

namespace _Project.Scripts.Data.Core
{
    [CreateAssetMenu(fileName = "NewArchetype", menuName = "Madman/Archetype Data")]
    public class ArchetypeData : ScriptableObject
    {
        [Header("Идентификация")]
        public string archetypeName = "Новый класс";
        [TextArea] public string description = "Описание класса";
        public int archetypeID;

        [Header("Базовые характеристики")]
        public float maxHealth = 100f;
        public float moveSpeed = 5f;
        public float maxMana = 100f;

        [Header("Инвентарь и Оружие")]
        public int weaponSlotsCount = 1;
        public WeaponCategory allowedWeaponCategory;

        [Header("Стартовое Оружие")]
        public WeaponData defaultWeapon;

        [Header("Уникальный Навык")]
        public SkillData activeSkillData; // Полиморфизм в действии!
    }
}