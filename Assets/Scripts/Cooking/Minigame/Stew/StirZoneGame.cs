using Marea.Core;
using Marea.Data;
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
        [Tooltip("(+10/6) 국물이 국자를 따라 도는 비율. 1이면 국자와 같은 빠르기로 돈다.")]
        [SerializeField, Range(0f, 1f)] private float soupFollow = 0.5f;
        [Tooltip("(+10/6) 국물이 국자 빠르기를 따라잡는 시간(초). 국자를 멈추면 이만큼에 걸쳐 서서히 멈춘다.")]
        [SerializeField, Min(0.01f)] private float soupLag = 0.5f;
        [Tooltip("(+10/6) 국자가 냄비 중심에서 이보다 가까우면 도는 방향을 안 잰다(m) — 중심 근처에선 조금만 움직여도 각도가 튄다.")]
        [SerializeField, Min(0f)] private float minStirRadius = 0.05f;

        [Header("연출 (+10/6, 이슈 117) — 기획 「휘젓기」 VFX_04")]
        [Tooltip("국자를 빨리 돌릴 때 국자 자리에서 튀는 국물.")]
        [SerializeField] private VfxId stirSplashVfx = VfxId.Splash;
        [SerializeField] private Color soupTint = new Color(0.85f, 0.7f, 0.5f, 1f);
        [Tooltip("국자가 이보다 빨리 돌 때만 튄다(도/초).")]
        [SerializeField, Min(0f)] private float splashSpeed = 150f;
        [Tooltip("튀는 간격(초).")]
        [SerializeField, Min(0.05f)] private float splashInterval = 0.35f;
        [SerializeField, Min(0.1f)] private float splashScale = 0.7f;

        [Header("소리 (+10/6, 이슈 117) — 기획 sfx_stir_loop · sfx_gauge")]
        [Tooltip("국자를 돌리는 동안. 크기는 국물 도는 빠르기를 따른다.")]
        [SerializeField] private AudioClip stirLoop;
        [SerializeField, Range(0f, 1f)] private float stirVolume = 0.7f;
        [Tooltip("국물이 이 빠르기(도/초)일 때 stirVolume 그대로. 느리면 작아진다.")]
        [SerializeField, Min(1f)] private float stirFullSpeed = 120f;
        [Tooltip("국자가 원(세이프존) 안에 있는 동안 — 게이지가 차는 소리.")]
        [SerializeField] private AudioClip gaugeLoop;
        [SerializeField, Range(0f, 1f)] private float gaugeVolume = 0.35f;

        private SoundLoop _stirSound, _gaugeSound;

        private float _angle, _dir = 1f, _speed, _segmentLeft;
        private float _time, _inside;
        private Vector3 _lastLadle;      // 지난 프레임 국자 위치(냄비 중심 기준, 수평)
        private bool _hasLastLadle;
        private float _soupSpeed;        // 국물 회전 속도(도/초, Rotate 기준 — 위에서 볼 때 시계방향이 +)
        private float _nextSplash;
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
            _hasLastLadle = false;
            _soupSpeed = 0f;
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

            StirSoup(ladle - center, dt);
            if (Mathf.Abs(_soupSpeed) >= splashSpeed * soupFollow && Time.time >= _nextSplash)
            {
                _nextSplash = Time.time + splashInterval;
                Vfx.Play(stirSplashVfx, ladle, splashScale, soupTint);   // (+10/6)
            }

            // (+10/6)
            float stir = Mathf.Clamp01(Mathf.Abs(_soupSpeed) / stirFullSpeed);
            SoundManager.Hold(ref _stirSound, stirLoop, stir > 0.05f, stir * stirVolume);
            SoundManager.Hold(ref _gaugeSound, gaugeLoop, IsInside, gaugeVolume);

            if (_time >= duration) Finish();
        }

        /// <summary>
        /// (+10/6) 국물이 국자가 도는 방향으로 돈다 — 세이프존이 돌고, 국자가 따라 돌고, 국물은 국자를 따라 돈다.
        /// 예전엔 국물이 세이프존 방향으로 돌아서 국자를 반대로 돌려도 국물은 원 방향으로 돌았다.
        /// 국자의 냄비 중심 기준 각속도를 재서 soupLag 동안 따라잡는다. SignedAngle과 Rotate는 같은 부호(위에서 볼 때 시계방향 +)다.
        /// </summary>
        private void StirSoup(Vector3 ladleOffset, float dt)
        {
            ladleOffset.y = 0f;
            float ladleSpeed = 0f;
            if (_hasLastLadle && dt > 0f
                && ladleOffset.magnitude > minStirRadius && _lastLadle.magnitude > minStirRadius)
            {
                ladleSpeed = Vector3.SignedAngle(_lastLadle, ladleOffset, Vector3.up) / dt;
                ladleSpeed = Mathf.Clamp(ladleSpeed, -540f, 540f);   // 마우스가 중심을 가로지를 때 튀는 값
            }
            _lastLadle = ladleOffset;
            _hasLastLadle = true;

            _soupSpeed = Mathf.Lerp(_soupSpeed, ladleSpeed * soupFollow, 1f - Mathf.Exp(-dt / soupLag));
            pot.RotateLiquid(_soupSpeed * dt);
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
            StopSounds();
        }

        private void OnDisable()
        {
            _running = false;
            if (zoneVisual != null) zoneVisual.gameObject.SetActive(false);
            StopSounds();
        }

        private void StopSounds()
        {
            _stirSound.Stop();
            _gaugeSound.Stop();
        }
    }
}
