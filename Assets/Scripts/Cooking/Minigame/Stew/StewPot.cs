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

        // (+10/6) 피벗(SM_scoop)은 국물 속 머리가 아니라 중심에서 0.31m 떨어진 엉뚱한 점이고, 실제 막대가 국물 면을
        // 뚫는 점은 0.13m였다 — 판정은 피벗 근처, 눈에 보이는 막대는 18cm 안쪽이라 원을 따라가는 게 어긋났다.
        [Tooltip("(+10/6) 국자 막대가 국물 면을 뚫고 나오는 점 — 국자(ladlePivot) 아래 빈 오브젝트. 마우스는 이 점을 옮기고, 세이프존 판정도 이 점으로 한다. 비우면 피벗.")]
        [SerializeField] private Transform ladleShaftPoint;
        [Tooltip("(+10/6) 막대가 냄비 중심에서 갈 수 있는 최대 거리(m) — 국물 반경(0.64)에서 국자 머리 폭을 뺀 값.")]
        [SerializeField, Min(0.05f)] private float maxShaftRadius = 0.5f;

        private Vector3 _shaftOffsetSpace;   // 평소 자세에서 피벗 → 막대 점 (냄비 공간 벡터)

        /// <summary>(+10/6) 지금 막대가 국물 면에 닿은 점(월드, 높이는 국물 면). 보이는 그대로라 판정에 쓴다.</summary>
        public Vector3 LadleShaftPoint
        {
            get
            {
                Transform t = ladleShaftPoint != null ? ladleShaftPoint : ladlePivot;
                Vector3 p = t != null ? t.position : StirCenter;
                p.y = StirCenter.y;
                return p;
            }
        }

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

            // (+10/6) 막대 점을 커서로 옮긴다 — 평소 자세를 냄비 중심축으로 돌리고, 막대 점이 커서에 오도록 피벗을 놓는다.
            Vector3 pivotRest = space.TransformPoint(_initialLadleLocalPos);
            Vector3 shaftOff = space.TransformVector(_shaftOffsetSpace);
            Vector3 restDir = pivotRest + shaftOff - center; restDir.y = 0f;
            if (restDir.sqrMagnitude < 1e-6f) restDir = Vector3.forward;

            Vector3 d = worldPoint - center; d.y = 0f;
            Vector3 dir = d.sqrMagnitude > 1e-6f ? d.normalized : restDir.normalized;
            float r = Mathf.Clamp(d.magnitude, minLadleRadius, maxShaftRadius);

            Quaternion turn = Quaternion.AngleAxis(Vector3.SignedAngle(restDir, dir, Vector3.up), Vector3.up);
            Vector3 pivot = center + dir * r - turn * shaftOff;
            pivot.y = pivotRest.y;
            _targetLadleLocalPos = space.InverseTransformPoint(pivot);
            _targetLadleLocalRot = Quaternion.Inverse(space.rotation) * (turn * (space.rotation * _initialLadleLocalRot));

            return LadleShaftPoint;   // 지금 보이는 막대 자리 — 판정이 보이는 것과 같게
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
                Transform space = ladlePivot.parent != null ? ladlePivot.parent : ladlePivot;
                Vector3 off = ladleShaftPoint != null ? ladleShaftPoint.position - ladlePivot.position : Vector3.zero;
                off.y = 0f;
                _shaftOffsetSpace = space.InverseTransformVector(off);
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

        [Tooltip("(+10/7) 끓기 전 맑은 물 — 냄비 아래 Water(투명한 물 면 + 그 밑 냄비 바닥 원판). 1단계 동안만 켠다. "
               + "국물 메시엔 건더기가 붙어 있고, 냄비 모델 안쪽 바닥에도 수프가 칠해져 있어서 바닥 원판으로 가린다.")]
        [SerializeField] private GameObject water;

        /// <summary>
        /// (+10/7) 맑은 물 ↔ 수프. 물이면 국물은 그리지만 않는다(forceRenderingOff) — 꺼 버리면 bounds가 비어서
        /// 국물 면 높이(StirCenter · CatchGame.splashSurface)를 못 잰다.
        /// </summary>
        public void ShowWater(bool on)
        {
            if (_liquidRenderer == null && stewLiquid != null) _liquidRenderer = stewLiquid.GetComponentInChildren<Renderer>();
            if (on && water == null)
            {
                Debug.LogError($"{name}: StewPot.water가 비어 있다. 1단계에도 수프가 보인다.", this);
                return;
            }
            if (_liquidRenderer != null) _liquidRenderer.forceRenderingOff = on;
            if (water != null) water.SetActive(on);
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
