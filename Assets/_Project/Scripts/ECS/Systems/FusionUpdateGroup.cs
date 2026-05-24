using Unity.Entities;

namespace _Project.Scripts.ECS.Systems
{
    // Убрали DisableAutoCreation, чтобы Unity сама нашла и положила сюда наши системы
    public partial class FusionUpdateGroup : ComponentSystemGroup
    {
        protected override void OnUpdate()
        {
            // ОСТАВЛЯЕМ ПУСТЫМ! 
            // Unity будет дергать этот метод каждый кадр своего Update, но мы ничего не делаем,
            // чтобы физика не считалась дважды и не ломала сетевую синхронизацию.
        }

        public void ManualUpdate()
        {
            // Этот метод будет дергать Photon Fusion!
            // base.OnUpdate() запустит все дочерние системы строго в нужный сетевой тик.
            base.OnUpdate();
        }
    }
}