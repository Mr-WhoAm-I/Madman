using Unity.Entities;

namespace _Project.Scripts.ECS.Components
{
    public struct SkillConfigComponent : IComponentData
    {
        // Общая база расстояний и радиусов
        public float CastDistance;
        public float EffectRadius;
        
        // Параметры Истерика
        public float DashSpeed;
        public float DashDuration;
        public float FuryHealthThreshold;
        public float FurySpeedMultiplier;
        public float FuryLifesteal;
        public int TornadoBulletMultiplier;
        
        // Параметры Параноика
        public float ShieldCapacity;
        public float ShieldRechargeTime;
        public float ShieldAuraRadius;
        public int MaxTurrets;
        public float TurretLifeTime;
        // --- ДОБАВЛЕНО ДЛЯ ПЕРКОВ ПАРАНОИКА ---
        public float ShieldReflect; 
        public bool TurretExplode;  
        public bool TurretCryo;     

        // Параметры Шизоида
        public float InstabilityTimePerStack;
        public int InstabilityMaxStacks;
        public float InstabilityDamagePerStack;
        public float InvisibilityDuration;
        public float CloneExplosionDamage;
        public float CloneExplosionRadius;
        // --- ДОБАВЛЕНО ДЛЯ ПЕРКОВ ШИЗОИДА ---
        public float CloneRadiusMult; 
        public int MiniClones;        
        
        // Параметры Меланхолика
        public float FrostSlowMultiplier;
        public int ApathyMaxStacks;
        public float FreezeDuration;
        public int ChainTargetsCount;
        public float ChainExplosionDamage;
        // --- ДОБАВЛЕНО ДЛЯ ПЕРКОВ МЕЛАНХОЛИКА ---
        public int ShrapnelDeath;         
        public float FrostVulnerability;  
        
        // --- БАЗОВЫЕ ХАРАКТЕРИСТИКИ ---
        public float BaseDamage;
        public float CritChance;
        public float CritDamage;
        public float MagnetRadius;
        public float CooldownReduction;

        // --- ОРУЖЕЙНЫЕ МОДИФИКАТОРЫ ---
        public int PierceCount;
        public int RicochetCount;
        public int ExtraProjectiles;
        public float Lifesteal;
    }
}