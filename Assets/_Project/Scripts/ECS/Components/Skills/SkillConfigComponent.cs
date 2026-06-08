using Unity.Entities;

namespace _Project.Scripts.ECS.Components.Skills
{
    public struct SkillConfigComponent : IComponentData
    {
        // Общая база расстояний и радиусов
        public float CastDistance;
        public float EffectRadius;
        
        public float BaseMaxMana;
        public float ManaRegenRate;
        public float ManaRegenCooldown;
        public float ManaCost;
        
        // Параметры Истерика
        public float DashSpeed;
        public float DashDuration;
        public float FuryHealthThreshold;
        public float FurySpeedMultiplier;
        public float FuryLifesteal;
        public int TornadoBulletMultiplier;
        public bool ForceFuryOnUltimate;
        public float OverloadDuration; // Сколько секунд длится перегрузка
        
        // Параметры Параноика
        public float ShieldCapacity;
        public float ShieldRechargeTime;
        public float ShieldAuraRadius;
        public int MaxTurrets;
        public float TurretLifeTime;
        public float ShieldReflect; 
        public bool TurretExplode;  
        public bool TurretCryo;  
        public float TurretHealAura; // Сколько ХП в секунду восстанавливает турель (0 = не лечит)

        // Параметры Шизоида (Базовые)
        public float InstabilityTimePerStack;
        public int InstabilityMaxStacks;
        public float InstabilityDamagePerStack;
        public float InvisibilityDuration;
        public float CloneExplosionDamage;
        public float CloneExplosionRadius;
        
        // --- 10 ПЕРКОВ ИЗ МАГАЗИНА (ШИЗОИД) ---
        public int MaxInstability;       // Дополнительные стаки (будем прибавлять к базовым)
        public float CloneRadiusMult;    // Множитель радиуса
        public int MiniClones;           // Количество мини-клонов
        public float InvisDuration;      // Доп. время инвиза
        public float CloneToxicCloudDPS; // Урон облака
        public float CloneShootingMult;  // Множитель урона клона
        public float ShadowStrikeMult;   // Урон из тени
        public float ClonePoisonDPS;     // Урон яда от пуль
        public float InvisSpeedMult;     // Множитель скорости
        public float KillCooldownReduction; // Срез КД ульты
        
        // Параметры Меланхолика
        public float FrostSlowMultiplier;
        public int ApathyMaxStacks;
        public float FreezeDuration;
        public int ChainTargetsCount;
        public float ChainExplosionDamage;
        public int ShrapnelDeath;         
        public float FrostVulnerability;  
        public float AuraRadius;          // ДОБАВЛЕНО: Аура уныния
        public float ShieldPerFreeze;
        
        // --- БАЗОВЫЕ ХАРАКТЕРИСТИКИ ---
        public float BaseDamage;
        public float CritChance;
        public float CritDamage;
        public float MagnetRadius;
        public float CooldownReduction;
        public int MaxRerolls;
        public float MinDiscount;

        // --- ОРУЖЕЙНЫЕ МОДИФИКАТОРЫ ---
        public int PierceCount;
        public int RicochetCount;
        public int ExtraProjectiles;
        public float Lifesteal;
    }
}