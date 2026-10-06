using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace Marea.Food
{
    /// <summary>
    /// 칼집 데칼 하나를 위에서부터 그라데이션으로 드러낸다. (+10/5)
    /// 생선 칼집 내기 미니게임(B)이 SetProgress(0~1)만 부른다 — 드래그 진행도든 시간이든 같은 입구.
    ///
    /// 셰이더는 SG_Decal_CutReveal. DecalProjector는 MaterialPropertyBlock을 못 받아서
    /// 실행 중에만 머티리얼을 복제해 데칼마다 따로 쓴다 (칼집 3개 = 3개, 배칭 손해는 무시할 수준).
    /// 에디터에선 복제하지 않는다 — 공유 머티리얼(_Reveal 1) 그대로 보여 배치하기 쉽다.
    /// </summary>
    [RequireComponent(typeof(DecalProjector))]
    public class CutDecalReveal : MonoBehaviour
    {
        private static readonly int RevealId = Shader.PropertyToID("_Reveal");

        [SerializeField, Range(0f, 1f)] private float _progress;

        private DecalProjector _projector;
        private Material _instance;

        public float Progress => _progress;

        private void Awake()
        {
            _projector = GetComponent<DecalProjector>();
            if (_projector.material == null)
            {
                Debug.LogError($"{name}: DecalProjector에 머티리얼이 없다.", this);
                return;
            }
            _instance = new Material(_projector.material);
            _projector.material = _instance;
            Apply();
        }

        public void SetProgress(float value)
        {
            _progress = Mathf.Clamp01(value);
            Apply();
        }

        private void Apply()
        {
            if (_instance != null) _instance.SetFloat(RevealId, _progress);
        }

        // (+10/5) 실행 중 Inspector 슬라이더도 바로 보이게. 에디터에선 _instance가 없어 아무 일도 안 한다.
        private void OnValidate() => Apply();

        private void OnDestroy()
        {
            if (_instance != null) Destroy(_instance);
        }
    }
}
