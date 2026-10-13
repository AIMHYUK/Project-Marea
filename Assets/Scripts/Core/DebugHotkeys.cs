using Marea.World;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace Marea.Core
{
    /// <summary>
    /// (+10/13) 시연 · 녹화용 단축키. 씬에 둘 필요 없이 게임이 켜질 때 스스로 생겨 씬을 넘어가도 남는다.
    ///   F7 — 게임 시각 +6시간 (WorldClock.AddHours)
    ///   F8 — 연출 씬(Main_Showcase)으로
    /// 에디터와, MAREA_DEV_KEYS를 넣어 뽑은 시연용 빌드에서만 생긴다. 제출용 빌드엔 없다.
    /// 에디터에선 스튜 미니게임 중 StewVfxDebug(F7 · F8)와 겹친다. 그쪽은 릴리스 빌드에서 꺼진다.
    /// </summary>
    public class DebugHotkeys : MonoBehaviour
    {
        private const string ShowcaseScene = "Main_Showcase";
        private const float SkipHours = 6f;

#if UNITY_EDITOR || MAREA_DEV_KEYS
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
#endif
        private static void Boot()
        {
            var go = new GameObject(nameof(DebugHotkeys));
            DontDestroyOnLoad(go);
            go.AddComponent<DebugHotkeys>();
        }

        private void Update()
        {
            Keyboard kb = Keyboard.current;
            if (kb == null) return;

            if (kb.f7Key.wasPressedThisFrame)
            {
                if (WorldClock.Instance != null) WorldClock.Instance.AddHours(SkipHours);
                else Debug.LogWarning("[DebugHotkeys] 이 씬엔 WorldClock이 없어 시간을 못 넘긴다.");
            }

            if (kb.f8Key.wasPressedThisFrame && SceneManager.GetActiveScene().name != ShowcaseScene)
            {
                Time.timeScale = 1f;
                SceneManager.LoadScene(ShowcaseScene);
            }
        }
    }
}
