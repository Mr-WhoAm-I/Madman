using UnityEngine;

namespace _Project.Scripts.UI
{
    public class UIWindow : MonoBehaviour
    {
        public UIWindowType WindowType;

        public virtual void Open()
        {
            if (gameObject.activeSelf)
                return;

            gameObject.SetActive(true);
            UIState.BlockInput();
        }

        public virtual void Close()
        {
            if (!gameObject.activeSelf)
                return;

            gameObject.SetActive(false);
            UIState.UnblockInput();
        }
    }
}