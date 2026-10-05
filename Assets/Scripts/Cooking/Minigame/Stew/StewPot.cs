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

        private Renderer _liquidRenderer;

        /// <summary>(+10/2) 원을 그려 저을 때의 중심 — 국물 면 가운데(월드).</summary>
        public Vector3 StirCenter
        {
            get
            {
                if (_liquidRenderer == null && stewLiquid != null) _liquidRenderer = stewLiquid.GetComponentInChildren<Renderer>();
                return _liquidRenderer != null ? _liquidRenderer.bounds.center : transform.position;
            }
        }

        [Tooltip("(+10/2) 2단계에서 국자가 냄비 중심에 다가갈 수 있는 최소 거리(m).")]
        [SerializeField, Min(0f)] private float minLadleRadius = 0.06f;

        private Quaternion _initialLadleLocalRot;
        private Quaternion _targetLadleLocalRot;

        /// <summary>
        /// (+10/2) 국자로 국물 면의 이 점(월드)을 젓는다 — 2단계에서 마우스를 따라 세이프존을 쫓는다.
        /// 국자는 기울어진 긴 막대라 통째로 옮기면 머리가 냄비 벽을 뚫는다. 그래서 평소 자세 그대로
        /// 냄비 중심축을 돌아 커서 방향으로 돌리고, 중심과의 거리는 평소 자리보다 바깥으로는 못 가게 한다.
        /// 실제로 국자가 간 지점(국물 면 위)을 돌려준다. 호출이 끊기면 Update가 평소 자리로 되돌린다.
        /// </summary>
        public Vector3 SetLadlePoint(Vector3 worldPoint)
        {
            Vector3 center = StirCenter;
            if (ladlePivot == null) return center;
            Transform space = ladlePivot.parent != null ? ladlePivot.parent : ladlePivot;

            Vector3 restWorld = space.TransformPoint(_initialLadleLocalPos);
            Vector3 restOff = restWorld - center; restOff.y = 0f;
            float restR = restOff.magnitude;
            if (restR < 0.01f) { restOff = Vector3.forward; restR = 0.01f; }

            Vector3 d = worldPoint - center; d.y = 0f;
            Vector3 dir = d.sqrMagnitude > 1e-6f ? d.normalized : restOff.normalized;
            float r = Mathf.Clamp(d.magnitude, minLadleRadius, restR + 0.02f);

            Quaternion turn = Quaternion.AngleAxis(Vector3.SignedAngle(restOff, dir, Vector3.up), Vector3.up);
            Vector3 newWorld = center + turn * (restOff.normalized * r) + Vector3.up * (restWorld.y - center.y);
            _targetLadleLocalPos = space.InverseTransformPoint(newWorld);
            _targetLadleLocalRot = Quaternion.Inverse(space.rotation) * (turn * (space.rotation * _initialLadleLocalRot));

            return center + dir * r;
        }

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
                _initialLadleLocalRot = ladlePivot.localRotation;
                _targetLadleLocalRot = _initialLadleLocalRot;
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
                ladlePivot.localRotation = _initialLadleLocalRot;
                _targetLadleLocalRot = _initialLadleLocalRot;
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

            // (+10/2) 2단계에서 냄비 중심을 돌며 저으면 자세도 같이 돈다.
            ladlePivot.localRotation = Quaternion.Slerp(ladlePivot.localRotation, _targetLadleLocalRot, Time.deltaTime * stirSmoothSpeed);
            _targetLadleLocalRot = Quaternion.Slerp(_targetLadleLocalRot, _initialLadleLocalRot, Time.deltaTime * 3f);
        }
    }
}
