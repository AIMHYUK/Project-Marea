using System;
using System.Collections;
using UnityEngine;

namespace Marea.Field
{
    /// <summary>
    /// 가끔 섬 둘레 바다에 나타나는 거대 고래. (+10/2)
    ///
    /// 모델은 뼈만 있고 애니메이션 클립이 없다. 그래서 헤엄은 코드로 만든다:
    /// 몸통 뼈(머리→꼬리)를 시간차를 두고 위아래로 굽혀 꼬리를 치고, 지느러미를 살짝 젓는다.
    ///
    /// 한 번 나타나는 흐름: 기다림 → 카메라 가까운 경로 고르기 → 깊은 곳에서 떠오름 → 수면을 따라 헤엄
    /// → 고개 숙여 잠수 → 숨김. 카메라 근처 경로가 없으면 그번은 건너뛴다(아무도 못 보는 등장은 의미가 없다).
    /// </summary>
    public class WhaleSwimmer : MonoBehaviour
    {
        [Serializable]
        private struct SwimPath
        {
            public Transform from;
            public Transform to;
        }

        [Header("모델")]
        [Tooltip("고래 모델 루트. 숨을 땐 이걸 끈다.")]
        [SerializeField] private Transform model;
        [Tooltip("몸통 뼈, 머리 → 꼬리 순서.")]
        [SerializeField] private Transform[] spine;
        [Tooltip("지느러미 뿌리 뼈들. 살짝 젓는다.")]
        [SerializeField] private Transform[] fins;

        [Header("경로")]
        [SerializeField] private SwimPath[] paths;
        [Tooltip("경로 위 점이 하나라도 지금 화면 안 · 카메라에서 이 거리 안이어야 고른다.")]
        [SerializeField, Min(5f)] private float cameraRange = 70f;
        [Tooltip("해수면 높이(월드 y).")]
        [SerializeField] private float seaLevel = 6.9f;

        [Header("등장")]
        [SerializeField] private Vector2 intervalRange = new Vector2(60f, 150f);
        [Tooltip("처음 등장까지 기다리는 시간.")]
        [SerializeField, Min(0f)] private float firstDelay = 20f;
        [SerializeField, Min(0.1f)] private float swimSpeed = 2.5f;
        [Tooltip("헤엄칠 때 모델 기준점이 해수면 아래 이만큼. 등 · 나무가 물 위로 나온다.")]
        [SerializeField] private float surfaceDepth = 2.2f;
        [SerializeField] private float hiddenDepth = 14f;
        [SerializeField, Min(0.1f)] private float riseTime = 5f;
        [SerializeField, Min(0.1f)] private float diveTime = 6f;
        [SerializeField, Range(0f, 45f)] private float divePitch = 25f;

        [Header("헤엄 동작")]
        [Tooltip("꼬리 끝에서의 굽힘 각도. 머리 쪽으로 갈수록 작아진다.")]
        [SerializeField, Range(0f, 45f)] private float tailAmplitude = 30f;
        [SerializeField, Min(0f)] private float strokeFrequency = 0.5f;
        [Tooltip("머리→꼬리로 물결이 넘어가는 시간차(한 마디당 라디안).")]
        [SerializeField] private float phaseStep = 0.6f;
        [Tooltip("꼬리를 칠 때 몸 전체가 반대로 끄덕이는 각도.")]
        [SerializeField, Range(0f, 10f)] private float bodyRock = 3f;
        [SerializeField, Range(0f, 40f)] private float finAmplitude = 20f;
        [SerializeField, Range(0f, 2f)] private float bobHeight = 0.35f;

        private Quaternion[] _spineBase;
        private Quaternion[] _finBase;
        private float _swimTime;
        private bool _swimming;

        private void Awake()
        {
            if (model == null) Debug.LogError($"{name}: WhaleSwimmer.model이 비어 있다.", this);
            if (spine == null || spine.Length == 0) Debug.LogError($"{name}: WhaleSwimmer.spine이 비어 있다. 꼬리를 못 친다.", this);
            if (paths == null || paths.Length == 0) Debug.LogError($"{name}: WhaleSwimmer.paths가 비어 있다. 나타날 곳이 없다.", this);

            _spineBase = CaptureBase(spine);
            _finBase = CaptureBase(fins);
            if (model != null) model.gameObject.SetActive(false);
        }

        private void OnEnable() => StartCoroutine(Loop());

        private static Quaternion[] CaptureBase(Transform[] bones)
        {
            if (bones == null) return Array.Empty<Quaternion>();
            var r = new Quaternion[bones.Length];
            for (int i = 0; i < bones.Length; i++) r[i] = bones[i] != null ? bones[i].localRotation : Quaternion.identity;
            return r;
        }

        private IEnumerator Loop()
        {
            yield return new WaitForSeconds(firstDelay);
            while (true)
            {
                if (TryPickPath(out SwimPath p)) yield return Appear(p);
                yield return new WaitForSeconds(UnityEngine.Random.Range(intervalRange.x, Mathf.Max(intervalRange.x, intervalRange.y)));
            }
        }

        private bool TryPickPath(out SwimPath picked)
        {
            picked = default;
            Camera cam = Camera.main;
            if (cam == null || paths == null) return false;

            int count = 0;
            foreach (SwimPath p in paths)
            {
                if (p.from == null || p.to == null) continue;
                if (!IsOnScreen(cam, p)) continue;
                // 저수지 샘플링 — 조건 맞는 것 중 하나를 고르게 뽑는다.
                count++;
                if (UnityEngine.Random.Range(0, count) == 0) picked = p;
            }
            return count > 0;
        }

        // 카메라가 플레이어 앞쪽만 내려다봐서 "가까운 경로"로는 부족하다 — 경로 위 해수면 점이 실제로 화면에 들어오는지 본다.
        private bool IsOnScreen(Camera cam, SwimPath p)
        {
            const int samples = 12;
            for (int i = 0; i <= samples; i++)
            {
                Vector3 pt = Vector3.Lerp(p.from.position, p.to.position, i / (float)samples);
                pt.y = seaLevel;
                if (Vector3.Distance(pt, cam.transform.position) > cameraRange) continue;
                Vector3 v = cam.WorldToViewportPoint(pt);
                if (v.z > 0f && v.x > 0.05f && v.x < 0.95f && v.y > 0.05f && v.y < 0.95f) return true;
            }
            return false;
        }

        /// <summary>바로 한 번 지나가게 한다 (테스트 · 연출용).</summary>
        [ContextMenu("Appear Now")]
        public void AppearNow()
        {
            if (!Application.isPlaying) return;
            StopAllCoroutines();
            if (TryPickPath(out SwimPath p)) StartCoroutine(AppearThenLoop(p));
            else
            {
                Debug.LogWarning($"{name}: 카메라 {cameraRange}m 안에 경로가 없다. 첫 경로로 나타난다.", this);
                StartCoroutine(AppearThenLoop(paths[0]));
            }
        }

        /// <summary>지금 헤엄치는 중인가(떠오름 ~ 잠수 끝). 숨어 있으면 false. (+10/5)</summary>
        public bool IsSwimming => _swimming;

        public float SeaLevel => seaLevel;

        /// <summary>
        /// (+10/5) 화면 조건 없이 카메라에서 가장 가까운 경로로 바로 한 번 지나가게 한다 — 개발용 단축키(WhaleDebugKey)가 부른다.
        /// 지나간 뒤엔 평소 루프(intervalRange 간격)로 돌아간다.
        /// </summary>
        public void AppearNearest()
        {
            if (!Application.isPlaying || paths == null || paths.Length == 0) return;
            Camera cam = Camera.main;
            Vector3 eye = cam != null ? cam.transform.position : transform.position;

            SwimPath best = paths[0];
            float bestDist = float.MaxValue;
            foreach (SwimPath p in paths)
            {
                if (p.from == null || p.to == null) continue;
                // 선분 위 가장 가까운 점까지의 수평 거리
                Vector3 a = Flat(p.from.position), ab = Flat(p.to.position) - a, e = Flat(eye);
                float s = ab.sqrMagnitude > 0f ? Mathf.Clamp01(Vector3.Dot(e - a, ab) / ab.sqrMagnitude) : 0f;
                float d = Vector3.Distance(e, a + ab * s);
                if (d < bestDist) { bestDist = d; best = p; }
            }

            StopAllCoroutines();
            _swimming = false;
            ResetBones();
            StartCoroutine(AppearThenLoop(best));
        }

        // 중간에 끊고 다시 나타날 때 뼈가 굽은 채로 시작하지 않게.
        private void ResetBones()
        {
            for (int i = 0; i < spine.Length; i++) if (spine[i] != null) spine[i].localRotation = _spineBase[i];
            for (int i = 0; i < fins.Length; i++) if (fins[i] != null) fins[i].localRotation = _finBase[i];
        }

        private IEnumerator AppearThenLoop(SwimPath p)
        {
            yield return Appear(p);
            yield return Loop();
        }

        private IEnumerator Appear(SwimPath p)
        {
            if (model == null) yield break;

            Vector3 a = Flat(p.from.position), b = Flat(p.to.position);
            if (UnityEngine.Random.value < 0.5f) (a, b) = (b, a);   // 같은 경로라도 방향은 그때그때
            Vector3 dir = (b - a).normalized;
            Quaternion heading = Quaternion.LookRotation(dir, Vector3.up);
            float total = Vector3.Distance(a, b);
            float surfaceY = seaLevel - surfaceDepth, hiddenY = seaLevel - hiddenDepth;

            model.gameObject.SetActive(true);
            _swimming = true;

            // 떠오르기 + 헤엄 + 잠수를 한 줄기 이동으로 — 이동 내내 앞으로 간다.
            float swimDuration = total / swimSpeed;
            float t = 0f;
            while (t < swimDuration)
            {
                t += Time.deltaTime;
                float along = Mathf.Clamp01(t / swimDuration);
                Vector3 pos = Vector3.Lerp(a, b, along);

                float rise = Mathf.Clamp01(t / riseTime);
                float dive = Mathf.Clamp01((t - (swimDuration - diveTime)) / diveTime);
                float y = Mathf.Lerp(hiddenY, surfaceY, Smooth(rise));
                y = Mathf.Lerp(y, hiddenY, Smooth(dive));
                y += Mathf.Sin(_swimTime * strokeFrequency * Mathf.PI * 2f) * bobHeight * (1f - dive);

                // 떠오를 땐 고개를 들고, 잠수할 땐 숙인다.
                float pitch = -divePitch * 0.5f * (1f - rise) + divePitch * Smooth(dive);
                pitch += Mathf.Sin(_swimTime * strokeFrequency * Mathf.PI * 2f + Mathf.PI) * bodyRock;   // 꼬리와 반대로 끄덕
                transform.SetPositionAndRotation(new Vector3(pos.x, y, pos.z), heading * Quaternion.Euler(pitch, 0f, 0f));
                yield return null;
            }

            _swimming = false;
            model.gameObject.SetActive(false);
        }

        private void LateUpdate()
        {
            if (!_swimming) return;
            _swimTime += Time.deltaTime;
            float w = _swimTime * strokeFrequency * Mathf.PI * 2f;

            // 꼬리를 위아래로 친다 — 고래 몸의 오른쪽 축을 각 뼈의 부모 기준으로 바꿔 돌린다.
            Vector3 right = transform.right;
            for (int i = 0; i < spine.Length; i++)
            {
                Transform bone = spine[i];
                if (bone == null) continue;
                float k = spine.Length > 1 ? (float)i / (spine.Length - 1) : 1f;   // 머리 0 → 꼬리 1
                // (+10/2) k*k는 꼬리 끝만 움직여 12m 고래에서 끝이 0.6m밖에 안 흔들렸다 — 몸통 뒤 절반도 같이 휜다.
                float angle = Mathf.Sin(w - i * phaseStep) * tailAmplitude * Mathf.Pow(k, 1.3f);
                Vector3 axis = bone.parent != null ? bone.parent.InverseTransformDirection(right) : right;
                bone.localRotation = Quaternion.AngleAxis(angle, axis) * _spineBase[i];
            }

            for (int i = 0; i < fins.Length; i++)
            {
                Transform fin = fins[i];
                if (fin == null) continue;
                float angle = Mathf.Sin(w + i) * finAmplitude;
                Vector3 axis = fin.parent != null ? fin.parent.InverseTransformDirection(right) : right;
                fin.localRotation = Quaternion.AngleAxis(angle, axis) * _finBase[i];
            }
        }

        private static float Smooth(float x) => x * x * (3f - 2f * x);
        private static Vector3 Flat(Vector3 v) => new Vector3(v.x, 0f, v.z);
    }
}
