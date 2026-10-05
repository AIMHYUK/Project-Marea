using UnityEngine;
using UnityEngine.InputSystem;

namespace Marea.Cooking
{
    /// <summary>
    /// 스튜 2단계 — 냄비 안에서 움직이는 세이프존을 국자로 따라간다. (+10/2, 이슈 106)
    ///
    /// 1. 국물 면 위에 원(세이프존)이 냄비 안을 빙글빙글 돈다 — 가끔 도는 방향이 바뀌고 안팎으로 출렁인다.
    /// 2. 국자는 마우스를 따라 냄비 안에서 움직인다(누르지 않아도). 국자가 원 안이면 시간이 쌓인다.
    /// 3. 시간이 끝나면 원 안에 있던 비율이 Score다. 판정(Perfect/Good/Bad)은 컨트롤러가 한다.
    ///
    /// 예전엔 상하좌우 드래그로 게이지를 채웠다. 단계 진행 · 카메라 · 결과는 모른다 — CatchGame · PopupTouchGame과 같은 모양.
    /// </summary>
    public class StirZoneGame : MonoBehaviour
    {
        [Header("냄비")]
        [SerializeField] private StewPot pot;
        [Tooltip("국자가 움직일 수 있는 반경(m, 냄비 중심부터).")]
        [SerializeField, Min(0.05f)] private float potRadius = 0.42f;

        [Header("세이프존")]
        [Tooltip("국물 위에 보이는 원. 크기는 zoneRadius에 맞춰 바꾼다.")]
        [SerializeField] private Transform zoneVisual;
        [SerializeField, Min(0.02f)] private float zoneRadius = 0.14f;
        [Tooltip("원이 도는 궤도 반경(m).")]
        [SerializeField, Min(0f)] private float orbitRadius = 0.22f;
        [Tooltip("도는 속도 범위(도/초). 구간마다 이 안에서 바뀐다.")]
        [SerializeField] private Vector2 orbitSpeed = new Vector2(55f, 95f);
        [Tooltip("이 간격(초)마다 속도가 바뀌고, reverseChance 확률로 방향이 뒤집힌다.")]
        [SerializeField] private Vector2 segmentTime = new Vector2(1.2f, 2.2f);
        [SerializeField, Range(0f, 1f)] private float reverseChance = 0.45f;
        [Tooltip("안팎 출렁임(m).")]
        [SerializeField, Min(0f)] private float wobble = 0.08f;
        [SerializeField] private Color insideColor = new Color(0.35f, 1f, 0.4f, 0.9f);
        [SerializeField] private Color outsideColor = new Color(1f, 0.35f, 0.3f, 0.9f);

        [Header("시간")]
        [SerializeField, Min(1f)] private float duration = 8f;

        [Header("국물")]
        [Tooltip("국물이 원을 따라 도는 비율.")]
        [SerializeField, Range(0f, 1f)] private float soupFollow = 0.35f;

        private float _angle, _dir = 1f, _speed, _segmentLeft;
        private float _time, _inside;
        private bool _running;
        private Renderer _zoneRenderer;
        private MaterialPropertyBlock _mpb;

        public bool IsFinished { get; private set; }
        public bool IsInside { get; private set; }
        public float TimeLeft01 => duration > 0f ? Mathf.Clamp01(1f - _time / duration) : 0f;
        /// <summary>원 안에 있던 비율 (0~1).</summary>
        public float Score => duration > 0f ? Mathf.Clamp01(_inside / duration) : 0f;

        private void Awake()
        {
            if (pot == null) Debug.LogError($"{name}: StirZoneGame.pot이 비어 있다.", this);
            if (zoneVisual == null) Debug.LogError($"{name}: StirZoneGame.zoneVisual이 비어 있다. 세이프존이 안 보인다.", this);
            else
            {
                _zoneRenderer = zoneVisual.GetComponentInChildren<Renderer>();
                zoneVisual.gameObject.SetActive(false);
            }
            _mpb = new MaterialPropertyBlock();
        }

        public void Begin()
        {
            _time = 0f;
            _inside = 0f;
            _angle = Random.value * Mathf.PI * 2f;
            _dir = Random.value < 0.5f ? -1f : 1f;
            NewSegment();
            IsFinished = false;
            _running = pot != null;
            if (!_running) { IsFinished = true; return; }
            if (zoneVisual != null)
            {
                zoneVisual.gameObject.SetActive(true);
                zoneVisual.localScale = Vector3.one * (zoneRadius * 2f);
            }
        }

        private void NewSegment()
        {
            _speed = Random.Range(orbitSpeed.x, Mathf.Max(orbitSpeed.x, orbitSpeed.y));
            _segmentLeft = Random.Range(segmentTime.x, Mathf.Max(segmentTime.x, segmentTime.y));
        }

        private void Update()
        {
            if (!_running) return;
            float dt = Time.deltaTime;
            _time += dt;

            // 원 움직이기
            _segmentLeft -= dt;
            if (_segmentLeft <= 0f)
            {
                if (Random.value < reverseChance) _dir = -_dir;
                NewSegment();
            }
            _angle += _dir * _speed * Mathf.Deg2Rad * dt;
            float r = Mathf.Clamp(orbitRadius + Mathf.Sin(_time * 1.7f) * wobble, 0f, potRadius - zoneRadius * 0.5f);
            Vector3 center = pot.StirCenter;
            Vector3 zonePos = center + new Vector3(Mathf.Cos(_angle), 0f, Mathf.Sin(_angle)) * r;
            if (zoneVisual != null) zoneVisual.position = new Vector3(zonePos.x, zoneVisual.position.y, zonePos.z);

            // 국자 = 마우스를 국물 면에 내린 점(냄비 안으로 제한)
            Vector3 ladle = center;
            Mouse mouse = Mouse.current;
            Camera cam = Camera.main;
            if (mouse != null && cam != null)
            {
                Ray ray = cam.ScreenPointToRay(mouse.position.ReadValue());
                if (new Plane(Vector3.up, center).Raycast(ray, out float enter))
                {
                    Vector3 p = ray.GetPoint(enter) - center;
                    p.y = 0f;
                    ladle = center + Vector3.ClampMagnitude(p, potRadius);
                }
            }
            ladle = pot.SetLadlePoint(ladle);   // 국자가 실제로 간 지점(벽을 뚫지 않게 제한된다)

            Vector3 d = ladle - zonePos;
            d.y = 0f;
            IsInside = d.magnitude <= zoneRadius;
            if (IsInside) _inside += dt;
            Tint(IsInside ? insideColor : outsideColor);

            // 국물도 원을 따라 돈다 — 위에서 볼 때 각도 증가(반시계) = Y축 음의 회전
            pot.RotateLiquid(-_dir * _speed * soupFollow * dt);

            if (_time >= duration) Finish();
        }

        private void Tint(Color c)
        {
            if (_zoneRenderer == null) return;
            _zoneRenderer.GetPropertyBlock(_mpb);
            _mpb.SetColor("_BaseColor", c);
            _zoneRenderer.SetPropertyBlock(_mpb);
        }

        private void Finish()
        {
            _running = false;
            IsFinished = true;
            if (zoneVisual != null) zoneVisual.gameObject.SetActive(false);
        }

        private void OnDisable()
        {
            _running = false;
            if (zoneVisual != null) zoneVisual.gameObject.SetActive(false);
        }
    }
}
