using UnityEngine;
using UnityEngine.InputSystem; 

namespace _Project.Scripts.Core
{
    public class DevProgressionTool : MonoBehaviour
    {
        [SerializeField] private int targetArchetypeID = 0;

        private void Update()
        {
            // Проверяем нажатие клавиши P через новую систему ввода
            if (Keyboard.current == null || !Keyboard.current.pKey.wasPressedThisFrame) return;
            var progress = ProfileController.Instance.CurrentProfile.GetProgressForArchetype(targetArchetypeID);
            progress.CurrentXP += 50f;
                
            if (progress.CurrentXP >= progress.XPToNextLevel)
            {
                progress.Level++;
                progress.CurrentXP = 0;
                progress.XPToNextLevel *= 1.5f;
                Debug.Log($"[DevTool] Уровень повышен! Новый уровень: {progress.Level}");
            }

            ProfileController.Instance.SaveGame();
            Debug.Log($"[DevTool] Опыт добавлен. Уровень: {progress.Level}, XP: {progress.CurrentXP}");
        }
    }
}