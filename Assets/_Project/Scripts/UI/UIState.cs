using UnityEngine;

namespace _Project.Scripts.UI
{
    public static class UIState
    {
        private static int _inputBlockers;

        public static bool IsInputBlocked => _inputBlockers > 0;

        public static void BlockInput()
        {
            _inputBlockers++;
        }

        public static void UnblockInput()
        {
            _inputBlockers = Mathf.Max(0, _inputBlockers - 1);
        }
        public static void ResetAllBlockers()
        {
            _inputBlockers = 0;
        }

#if UNITY_EDITOR
        public static int DebugBlockers => _inputBlockers;
#endif
    }
}