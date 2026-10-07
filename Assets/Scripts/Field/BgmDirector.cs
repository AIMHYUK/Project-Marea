using Marea.Cooking;
using Marea.Core;
using Marea.Restaurant;
using UnityEngine;

namespace Marea.Field
{
    /// <summary>
    /// 지금 상황에 맞는 BGM을 고른다. (+10/8) 기획 사운드 리스트 SYS-02 · 리듬게임 BGM.
    ///
    /// 미니게임 중(완성 팝업까지)이면 minigameTrack, 영업 중이면 businessTrack, 둘 다 아니면 BgmPlayer의 기본 BGM(SYS-01).
    /// 미니게임 · 영업 코드(B)를 건드리지 않고 매 프레임 상태를 보고 BgmPlayer.Override로 덮는다.
    /// 같은 곡이면 Override가 아무것도 안 하니 매 프레임 불러도 된다.
    /// </summary>
    public class BgmDirector : MonoBehaviour
    {
        [Tooltip("영업 중 BGM(SYS-02). 비우면 영업 중에도 기본 BGM.")]
        [SerializeField] private AudioClip businessTrack;
        [Tooltip("미니게임 중 BGM(리듬게임 BGM). 비우면 미니게임 중에도 바깥 BGM 그대로.")]
        [SerializeField] private AudioClip minigameTrack;

        private BaseCookingMinigame[] _minigames;
        private AudioClip _current;
        private bool _started;

        private void Start()
        {
            _minigames = FindObjectsByType<BaseCookingMinigame>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            if (_minigames.Length == 0)
                Debug.LogWarning($"{name}: 씬에 BaseCookingMinigame이 없다. 미니게임 BGM이 안 바뀐다.", this);
            if (BgmPlayer.Instance == null)
            {
                Debug.LogError($"{name}: 씬에 BgmPlayer가 없다. BGM을 못 바꾼다.", this);
                enabled = false;
            }
        }

        private void Update()
        {
            AudioClip want = Pick();
            if (_started && want == _current) return;
            _started = true;
            _current = want;
            if (want != null) BgmPlayer.Override(want);
            else BgmPlayer.ClearOverride();
        }

        private AudioClip Pick()
        {
            if (minigameTrack != null)
            {
                foreach (BaseCookingMinigame m in _minigames)
                    if (m != null && m.IsRunning) return minigameTrack;
                // 완성 팝업이 떠 있는 동안도 미니게임 곡 — 끝나자마자 바꾸면 페이드 구간이 팝업과 겹쳐 조용해진다.
                if (DishRevealUI.Instance != null && DishRevealUI.Instance.isActiveAndEnabled) return minigameTrack;
            }

            BusinessManager bm = BusinessManager.Instance;
            if (businessTrack != null && bm != null && bm.CurrentState == BusinessState.Open) return businessTrack;
            return null;
        }
    }
}
