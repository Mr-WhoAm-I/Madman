using UnityEngine;

namespace _Project.Scripts.UI
{
    public class UIWindow : MonoBehaviour
    {
        public UIWindowType WindowType;

        public virtual void Open()
        {
            Debug.Log("[UIWindowOpenerButton] Open");
            gameObject.SetActive(true);
        }

        public virtual void Close()
        {
            gameObject.SetActive(false);
        }
    }
}