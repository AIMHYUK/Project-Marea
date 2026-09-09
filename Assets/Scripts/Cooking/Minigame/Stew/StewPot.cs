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

        private void Awake()
        {
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
