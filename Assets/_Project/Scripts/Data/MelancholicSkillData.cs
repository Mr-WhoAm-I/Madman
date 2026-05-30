using Fusion;
using UnityEngine;

namespace _Project.Scripts.Data
{
    [CreateAssetMenu(fileName = "MelancholicSkillData", menuName = "Madman/Skills/Melancholic Skill Data")]
    public class MelancholicSkillData : SkillData
    {
        [Header("Пассивный навык: Тяжесть бытия")]
        [Tooltip("Процент замедления от обычных выстрелов (Например, 0.2f = враг теряет 20% скорости)")]
        public float slowPercentage = 0.2f;
        
        [Tooltip("Сколько стаков Апатии нужно для полной заморозки (Базово 3)")]
        public int apathyStacksToFreeze = 3;
        
        [Tooltip("Длительность полной заморозки в секундах")]
        public float freezeDuration = 2.0f;

        [Header("Активный навык: Цепная буря (Ульта)")]
        [Tooltip("Префаб сетевого ледяного снаряда")]
        public NetworkPrefabRef iceProjectilePrefab;

        [Tooltip("Скорость полета основного ледяного шара")]
        public float projectileSpeed = 12f;

        [Tooltip("Максимальная дальность полета до авто-взрыва")]
        public float projectileMaxDistance = 15f;

        [Tooltip("Радиус ледяного взрыва (Эпицентр)")]
        public float chainExplosionRadius = 4f;
        
        [Tooltip("Урон от взрыва Цепной бури")]
        public float chainExplosionDamage = 150f;

        [Header("Настройки осколков (Цепная реакция)")]
        [Tooltip("Количество дополнительных целей, в которые отскочит лед после попадания")]
        public int chainTargetsCount = 3;

        [Tooltip("Скорость полета самонаводящихся осколков")]
        public float shardSpeed = 18f;

        [Tooltip("Процент урона осколка от базового урона взрыва (Например, 0.5f = 50% от 150)")]
        public float shardDamageMultiplier = 0.5f;

        [Tooltip("Радиус поиска целей для осколков (добавляется к радиусу взрыва)")]
        public float shardSearchRadius = 8f;

        [Header("Будущие улучшения (Параметризация перков)")]
        [Tooltip("Множитель урона по полностью замороженным врагам (Хрупкий лед: +50% = 1.5f)")]
        public float frozenDamageMultiplier = 1.5f;

        [Tooltip("Радиус пассивной генерации Апатии вокруг игрока (Аура уныния. 0 = выключено)")]
        public float auraRadius = 0f;

        [Tooltip("Раз в сколько секунд Аура уныния накладывает стак")]
        public float auraTickRate = 2f;

        [Tooltip("Сколько процентов щита получает Меланхолик при заморозке врага (Ледяной доспех)")]
        public float shieldPerFreeze = 0.05f;

        [Tooltip("Количество ледяных шипов, вылетающих из убитого замороженного моба (Осколочный взрыв)")]
        public int shardsOnDeath = 0;
    }
}