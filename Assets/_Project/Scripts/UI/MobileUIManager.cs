using UnityEngine;

namespace _Project.Scripts.UI
{
    public class MobileUIManager : MonoBehaviour
    {
        [Header("Включить принудительно в редакторе (для тестов)")]
        public bool forceEnableInEditor = true;

        private void Awake()
        {
            CheckPlatform();
        }

        private void CheckPlatform()
        {
            bool isMobile = false;

            // Проверяем платформу на этапе компиляции
#if UNITY_ANDROID || UNITY_IOS
            isMobile = true;
#endif

            // Если мы в редакторе и стоит галочка - показываем
#if UNITY_EDITOR
            if (forceEnableInEditor)
            {
                isMobile = true;
            }
#endif

            // Включаем или выключаем весь Canvas с мобильным управлением
            gameObject.SetActive(isMobile);
        }
    }
}