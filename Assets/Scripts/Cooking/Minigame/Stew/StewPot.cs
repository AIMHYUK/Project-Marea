using UnityEngine;

namespace Marea.Cooking
{
    public class StewPot : MonoBehaviour
    {
        [Header("3D 오브젝트 바인딩")]
        [SerializeField] private Transform ladlePivot;
        [SerializeField] private Transform stewLiquid;

        [Header("국자 애니메이션 설정")]
        [SerializeField] private float ladleStirRadius = 0.25f;
        [SerializeField] private float stirSmoothSpeed = 10f;
        [SerializeField] private float liquidSpinSpeed = 120f;

        private Vector3 _targetLadleLocalPos;
        private Vector3 _initialLadleLocalPos;

        /// <summary>국물이 연결돼 있는가. 컨트롤러가 연출을 맡기기 전에 본다. (#48)</summary>
        public bool HasLiquid => stewLiquid != null;

        private void Awake()
        {
            // 전에는 컨트롤러가 이 칸이 비면 이름으로 국물을 뒤졌다 — 그리고 그 이름을 가진
            // 오브젝트가 없어서 조용히 실패했다. 폴백을 지웠으니 여기서 드러내야 한다. (#48)
            if (stewLiquid == null)
            {
                Debug.LogError($"{name}: stewLiquid가 비어 있다. 국물이 돌지 않는다. "
                             + "PF_Soup_Set 프리팹에서 SM_Soup_Pot_Inside를 연결할 것.", this);
            }

            if (ladlePivot != null)
            {
                _initialLadleLocalPos = ladlePivot.localPosition;
                _targetLadleLocalPos = _initialLadleLocalPos;
                ladlePivot.gameObject.SetActive(true);
            }

            if (stewLiquid != null)
            {
                stewLiquid.gameObject.SetActive(true);
            }
        }

        /// <summary>
        /// 국물을 Y축으로 이만큼 돌린다. (#48)
        ///
        /// 전에는 StewMinigameController가 국물 Transform을 직접 들고 돌렸다. 냄비가 자기
        /// 부품을 아는 게 맞고, 그래야 컨트롤러가 국물을 찾을 일 자체가 없어진다.
        /// </summary>
        public void RotateLiquid(float degrees)
        {
            if (stewLiquid == null) return;

            stewLiquid.Rotate(Vector3.up, degrees, Space.Self);
        }

        public void ResetPosition()
        {
            if (ladlePivot != null)
            {
                ladlePivot.localPosition = _initialLadleLocalPos;
                _targetLadleLocalPos = _initialLadleLocalPos;
            }
        }

        public void AnimateStir(StirDirection direction)
        {
            Vector3 offset = direction switch
            {
                StirDirection.Up => new Vector3(0f, 0.05f, ladleStirRadius),
                StirDirection.Down => new Vector3(0f, -0.05f, -ladleStirRadius),
                StirDirection.Left => new Vector3(-ladleStirRadius, 0f, 0f),
                StirDirection.Right => new Vector3(ladleStirRadius, 0f, 0f),
                _ => Vector3.zero
            };

            _targetLadleLocalPos = _initialLadleLocalPos + offset;

            if (stewLiquid != null)
            {
                stewLiquid.Rotate(Vector3.up, liquidSpinSpeed * Time.deltaTime, Space.Self);
            }
        }

        private void Update()
        {
            if (ladlePivot == null) return;

            ladlePivot.localPosition = Vector3.Lerp(
                ladlePivot.localPosition,
                _targetLadleLocalPos,
                Time.deltaTime * stirSmoothSpeed
            );

            _targetLadleLocalPos = Vector3.Lerp(
                _targetLadleLocalPos,
                _initialLadleLocalPos,
                Time.deltaTime * 3f
            );
        }
    }
}
