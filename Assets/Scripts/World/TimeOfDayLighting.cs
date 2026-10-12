using UnityEngine;
using UnityEngine.Rendering;

namespace Marea.World
{
    /// <summary>
    /// 시각에 따라 맵 조명을 바꾼다. <see cref="WorldClock"/> 의 구독자다. (+10/12)
    ///
    /// 2차 분담 문서 4단계의 「하늘·전체 조명·식당 조명 전환」 자리다. 시계는 시간만 세고,
    /// 조명은 여기서 매 프레임 <see cref="WorldClock.NormalizedTime"/> 을 읽어 곡선으로 칠한다.
    /// 시간대 이벤트를 안 쓰는 이유: 경계에서 뚝 끊기지 않고 하루 내내 서서히 바뀌어야 한다.
    ///
    /// 바꾸는 것: 해(Directional) 색·세기·고도 / 환경광 / 반사 세기 / 스카이박스 색·노출 / 안개 색 / 램프 세기 / 바다 색.
    /// 씬에 라이트맵·라이트 프로브가 없어서(10/12 확인) 전부 실시간이라 이것만으로 맵 전체가 따라온다.
    /// 나중에 라이트를 구우면 구운 부분은 안 어두워진다 — 그때 이 파일을 다시 봐야 한다.
    ///
    /// 원래 값은 켤 때 읽어 두고 끌 때 되돌린다. 스카이박스·바다는 에셋을 직접 칠하면 플레이를 꺼도
    /// 남으므로 복사본을 만들어 칠한다.
    ///
    /// 바다(UberStylizedWater)는 조명을 거의 안 받고 자기 색으로 그려서, 해만 줄이면 밤에도
    /// 바다가 한낮처럼 밝다(10/12 캡처로 확인). 그래서 색 속성에 시간대 색(waterTint)을 직접 곱한다.
    /// </summary>
    public class TimeOfDayLighting : MonoBehaviour
    {
        [Header("대상 (비우면 찾는다)")]
        [Tooltip("비워두면 WorldClock.Instance 를 쓴다.")]
        [SerializeField] private WorldClock clock;
        [Tooltip("비워두면 RenderSettings.sun 을 쓴다.")]
        [SerializeField] private Light sun;
        [Tooltip("밤에 밝아지는 램프들. 요리 미니게임 조명은 넣지 않는다 — 그건 시각과 상관없이 늘 같아야 한다.")]
        [SerializeField] private Light[] lamps;

        [Header("해 — 가로축은 하루(0 = 자정, 0.5 = 정오)")]
        [SerializeField] private Gradient sunColor = Keys(
            (0f, new Color(0.55f, 0.65f, 1f)), (4.5f, new Color(0.55f, 0.65f, 1f)), (5.5f, new Color(1f, 0.70f, 0.62f)),
            (7.5f, new Color(1f, 0.93f, 0.82f)), (12f, new Color(1f, 0.957f, 0.839f)),
            (18.5f, new Color(1f, 0.62f, 0.40f)), (20.5f, new Color(0.55f, 0.65f, 1f)));
        [Tooltip("원래 세기에 곱한다.")]
        [SerializeField] private AnimationCurve sunIntensity = Curve(
            (0f, 0.35f), (4.5f, 0.35f), (6f, 0.55f), (8f, 0.95f), (12f, 1f), (16.5f, 0.95f), (19f, 0.6f), (20.5f, 0.35f), (24f, 0.35f));
        [Tooltip("해의 고도(X 회전). 아침·저녁에 낮아져 그림자가 길어진다. 밤엔 달빛이라 다시 높다.")]
        [SerializeField] private AnimationCurve sunPitch = Curve(
            (0f, 60f), (4.5f, 60f), (6f, 28f), (12f, 73f), (18f, 28f), (20.5f, 60f), (24f, 60f));

        [Header("환경광 · 안개 — 원래 값에 곱하는 색")]
        [SerializeField] private Gradient ambientTint = Keys(
            (0f, new Color(0.40f, 0.48f, 0.78f)), (4.5f, new Color(0.40f, 0.48f, 0.78f)), (5.5f, new Color(0.72f, 0.62f, 0.75f)),
            (7.5f, Color.white), (16.5f, Color.white), (18.5f, new Color(1f, 0.80f, 0.66f)), (20.5f, new Color(0.40f, 0.48f, 0.78f)));

        [Header("스카이박스")]
        [Tooltip("원래 _Tint 에 곱한다.")]
        [SerializeField] private Gradient skyTint = Keys(
            (0f, new Color(0.45f, 0.55f, 1f)), (4.5f, new Color(0.45f, 0.55f, 1f)), (5.5f, new Color(1f, 0.75f, 0.80f)),
            (7.5f, Color.white), (16.5f, Color.white), (18.5f, new Color(1f, 0.72f, 0.55f)), (20.5f, new Color(0.45f, 0.55f, 1f)));
        [Tooltip("원래 _Exposure 에 곱한다.")]
        [SerializeField] private AnimationCurve skyExposure = Curve(
            (0f, 0.28f), (4.5f, 0.28f), (6f, 0.65f), (8f, 1f), (16.5f, 1f), (19f, 0.7f), (20.5f, 0.3f), (24f, 0.28f));

        [Header("바다")]
        [Tooltip("조명을 안 받는 물 재질들. 이 재질을 쓰는 렌더러를 찾아 복사본으로 바꿔 끼우고 아래 색을 곱한다.")]
        [SerializeField] private Material[] waterMaterials;
        [Tooltip("물 색에 곱하는 색. (+10/12) 환경광 색을 같이 쓰다가 밤 바다가 덜 어두워 따로 뺐다.")]
        [SerializeField] private Gradient waterTint = Keys(
            (0f, new Color(0.06f, 0.10f, 0.24f)), (4.5f, new Color(0.06f, 0.10f, 0.24f)), (5.5f, new Color(0.35f, 0.32f, 0.50f)),
            (7.5f, Color.white), (16.5f, Color.white), (18.5f, new Color(0.92f, 0.72f, 0.60f)), (20.5f, new Color(0.06f, 0.10f, 0.24f)));
        [Tooltip("바다 몸통 색 속성. (+10/12) 이 셰이더는 이 두 색을 값보다 훨씬 밝게 그려서 위 색을 곱해도 밤 바다가 "
               + "중간 파랑으로 남았다(색을 하나씩 검게 해 보며 확인). 그래서 밤엔 아래 두 값을 더 건다.")]
        [SerializeField] private string[] waterBodyColors = { "_Color_Shallow", "_Color_Deep" };
        [Tooltip("밤(램프 곡선 1)일 때 몸통 색에 한 번 더 곱하는 값. 작을수록 진하고 어둡다.")]
        [Range(0f, 1f)]
        [SerializeField] private float waterNightDarken = 0.35f;
        [Tooltip("밤일 때 몸통 색의 알파를 1 쪽으로 얼마나 올릴까. 바다가 반투명이라 그대로 두면 해저가 비친다. 0 = 그대로, 1 = 완전 불투명.")]
        [Range(0f, 1f)]
        [SerializeField] private float waterNightOpacity = 0.85f;

        [Header("반사")]
        [Tooltip("환경 반사 세기(원래 값 ×). 반사는 낮 하늘을 비추므로 밤엔 줄인다. 바다가 밤에 회색으로 뜨는 원인이었다. (+10/12)")]
        [SerializeField] private AnimationCurve reflection = Curve(
            (0f, 0.25f), (4.5f, 0.25f), (6.5f, 0.7f), (8f, 1f), (16.5f, 1f), (19f, 0.7f), (20.5f, 0.25f), (24f, 0.25f));
        [Tooltip("곱하지 않을 색 속성. 그림자·물속 색은 이미 어둡다.")]
        [SerializeField] private string[] waterSkipColors = { "_Underwater_Color", "_ShadowColor" };

        [Header("램프")]
        [Tooltip("0 = 낮(약하게), 1 = 밤(밝게).")]
        [SerializeField] private AnimationCurve lampNight = Curve(
            (0f, 1f), (5f, 1f), (7f, 0f), (17f, 0f), (19.5f, 1f), (24f, 1f));
        [Tooltip("낮에 원래 세기의 몇 배로 둘까.")]
        [SerializeField] private float lampDayScale = 0.3f;
        [Tooltip("밤에 원래 세기의 몇 배로 둘까.")]
        [SerializeField] private float lampNightScale = 1.5f;

        private Color _sunColor0;
        private float _sunIntensity0;
        private Quaternion _sunRotation0;
        private SphericalHarmonicsL2 _ambient0;
        private Color _fog0;
        private float _reflection0;
        private Material _sky0, _sky;
        private Color _skyTint0;
        private float _skyExposure0;
        private float[] _lamp0;
        private readonly System.Collections.Generic.List<WaterCopy> _water = new System.Collections.Generic.List<WaterCopy>();

        private class WaterCopy
        {
            public Material Original, Copy;
            public int[] Ids;
            public Color[] Colors;
            public bool[] Body;
            public readonly System.Collections.Generic.List<(Renderer renderer, int slot)> Uses =
                new System.Collections.Generic.List<(Renderer, int)>();
        }

        private static readonly int TintId = Shader.PropertyToID("_Tint");
        private static readonly int ExposureId = Shader.PropertyToID("_Exposure");

        private void OnEnable()
        {
            if (clock == null) clock = WorldClock.Instance;
            if (sun == null) sun = RenderSettings.sun;

            // 시계가 없으면 하루 종일 원래 조명 그대로라 "시간이 안 간다"와 구분이 안 된다.
            if (clock == null)
            {
                Debug.LogError("[TimeOfDayLighting] WorldClock을 못 찾았습니다. 맵 조명이 바뀌지 않습니다.", this);
                enabled = false;
                return;
            }
            if (sun == null)
                Debug.LogError("[TimeOfDayLighting] 해(Directional Light)가 없습니다. 인스펙터에 넣거나 " +
                               "Lighting 창의 Sun Source를 채우세요. 해는 빼고 나머지만 바꿉니다.", this);

            if (sun != null)
            {
                _sunColor0 = sun.color;
                _sunIntensity0 = sun.intensity;
                _sunRotation0 = sun.transform.rotation;
            }

            _ambient0 = RenderSettings.ambientProbe;
            _fog0 = RenderSettings.fogColor;
            _reflection0 = RenderSettings.reflectionIntensity;

            _sky0 = RenderSettings.skybox;
            if (_sky0 != null && _sky0.HasProperty(TintId) && _sky0.HasProperty(ExposureId))
            {
                _sky = new Material(_sky0) { name = _sky0.name + " (시각)" };
                _skyTint0 = _sky0.GetColor(TintId);
                _skyExposure0 = _sky0.GetFloat(ExposureId);
                RenderSettings.skybox = _sky;
            }

            HookWater();

            if (lamps == null) lamps = new Light[0];
            _lamp0 = new float[lamps.Length];
            for (int i = 0; i < lamps.Length; i++)
                if (lamps[i] != null) _lamp0[i] = lamps[i].intensity;
        }

        private void OnDisable()
        {
            if (sun != null)
            {
                sun.color = _sunColor0;
                sun.intensity = _sunIntensity0;
                sun.transform.rotation = _sunRotation0;
            }

            RenderSettings.ambientProbe = _ambient0;
            RenderSettings.fogColor = _fog0;
            RenderSettings.reflectionIntensity = _reflection0;

            if (_sky != null)
            {
                RenderSettings.skybox = _sky0;
                Destroy(_sky);
                _sky = null;
            }

            if (_lamp0 != null)
                for (int i = 0; i < lamps.Length && i < _lamp0.Length; i++)
                    if (lamps[i] != null) lamps[i].intensity = _lamp0[i];

            foreach (WaterCopy w in _water)
            {
                foreach (var (renderer, slot) in w.Uses)
                {
                    if (renderer == null) continue;
                    Material[] mats = renderer.sharedMaterials;
                    mats[slot] = w.Original;
                    renderer.sharedMaterials = mats;
                }
                Destroy(w.Copy);
            }
            _water.Clear();
        }

        private void HookWater()
        {
            _water.Clear();
            if (waterMaterials == null || waterMaterials.Length == 0) return;

            foreach (Material original in waterMaterials)
            {
                if (original == null) continue;

                var w = new WaterCopy { Original = original, Copy = new Material(original) { name = original.name + " (시각)" } };
                var ids = new System.Collections.Generic.List<int>();
                var colors = new System.Collections.Generic.List<Color>();
                var opaque = new System.Collections.Generic.List<bool>();
                Shader shader = original.shader;
                for (int i = 0; i < shader.GetPropertyCount(); i++)
                {
                    if (shader.GetPropertyType(i) != ShaderPropertyType.Color) continue;
                    string n = shader.GetPropertyName(i);
                    if (System.Array.IndexOf(waterSkipColors, n) >= 0) continue;
                    ids.Add(Shader.PropertyToID(n));
                    colors.Add(original.GetColor(n));
                    opaque.Add(System.Array.IndexOf(waterBodyColors, n) >= 0);
                }
                w.Ids = ids.ToArray();
                w.Colors = colors.ToArray();
                w.Body = opaque.ToArray();

                foreach (Renderer r in FindObjectsByType<Renderer>(FindObjectsSortMode.None))
                {
                    Material[] mats = r.sharedMaterials;
                    bool hit = false;
                    for (int s = 0; s < mats.Length; s++)
                    {
                        if (mats[s] != original) continue;
                        mats[s] = w.Copy;
                        w.Uses.Add((r, s));
                        hit = true;
                    }
                    if (hit) r.sharedMaterials = mats;
                }

                // 아무 데도 안 쓰면 바다가 그대로 밝은데 이유를 모른다. 조용히 넘어가지 않는다.
                if (w.Uses.Count == 0)
                    Debug.LogWarning($"[TimeOfDayLighting] 물 재질 '{original.name}'을 쓰는 렌더러가 씬에 없습니다. 바다 색이 안 바뀝니다.", this);

                _water.Add(w);
            }
        }

        private void Update()
        {
            float t = clock.NormalizedTime;
            float h = t * 24f;

            if (sun != null)
            {
                sun.color = sunColor.Evaluate(t);
                sun.intensity = _sunIntensity0 * sunIntensity.Evaluate(h);
                Vector3 e = _sunRotation0.eulerAngles;
                sun.transform.rotation = Quaternion.Euler(sunPitch.Evaluate(h), e.y, e.z);
            }

            Color amb = ambientTint.Evaluate(t);
            RenderSettings.ambientProbe = Tint(_ambient0, amb);
            RenderSettings.fogColor = _fog0 * amb;

            RenderSettings.reflectionIntensity = _reflection0 * reflection.Evaluate(h);

            Color sea = waterTint.Evaluate(t);
            float night = lampNight.Evaluate(h);
            float bodyScale = Mathf.Lerp(1f, waterNightDarken, night);
            foreach (WaterCopy w in _water)
                for (int i = 0; i < w.Ids.Length; i++)
                {
                    Color c = w.Colors[i];
                    // 알파는 투명도다. 밤에만 몸통 색을 불투명 쪽으로 올리고 나머지는 그대로 둔다.
                    float k = w.Body[i] ? bodyScale : 1f;
                    float a = w.Body[i] ? Mathf.Lerp(c.a, 1f, night * waterNightOpacity) : c.a;
                    w.Copy.SetColor(w.Ids[i], new Color(c.r * sea.r * k, c.g * sea.g * k, c.b * sea.b * k, a));
                }

            if (_sky != null)
            {
                _sky.SetColor(TintId, _skyTint0 * skyTint.Evaluate(t));
                _sky.SetFloat(ExposureId, _skyExposure0 * skyExposure.Evaluate(h));
            }

            float lamp = Mathf.Lerp(lampDayScale, lampNightScale, lampNight.Evaluate(h));
            for (int i = 0; i < lamps.Length; i++)
                if (lamps[i] != null) lamps[i].intensity = _lamp0[i] * lamp;
        }

        /// <summary>환경광 SH 계수에 채널별로 색을 곱한다. 9개 계수 전부라 방향성은 그대로 둔다.</summary>
        private static SphericalHarmonicsL2 Tint(SphericalHarmonicsL2 sh, Color c)
        {
            for (int k = 0; k < 9; k++)
            {
                sh[0, k] *= c.r;
                sh[1, k] *= c.g;
                sh[2, k] *= c.b;
            }
            return sh;
        }

        // ── 기본값 만들기. 가로축은 시(0~24)로 적고 Gradient는 0~1로 바꿔 넣는다. ──

        private static Gradient Keys(params (float hour, Color color)[] keys)
        {
            // Gradient는 0과 1을 이어주지 않는다. 자정을 넘을 때 끊기지 않게 양 끝 색을 같게 둔다.
            // 키는 최대 8개라 keys는 7개까지다.
            var colors = new GradientColorKey[keys.Length + 1];
            for (int i = 0; i < keys.Length; i++) colors[i] = new GradientColorKey(keys[i].color, keys[i].hour / 24f);
            colors[keys.Length] = new GradientColorKey(keys[0].color, 1f);
            var g = new Gradient();
            g.SetKeys(colors, new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 1f) });
            return g;
        }

        private static AnimationCurve Curve(params (float hour, float value)[] keys)
        {
            // SmoothTangents 는 평평한 구간 옆에서 키 값을 넘어 처진다(밤 반사가 0.25 아래로 내려갔다).
            // 꼭짓점·평평한 키는 기울기 0, 나머지는 양옆 기울기 중 작은 쪽을 써서 넘치지 않게 한다.
            var frames = new Keyframe[keys.Length];
            for (int i = 0; i < keys.Length; i++)
            {
                float slope = 0f;
                if (i > 0 && i < keys.Length - 1)
                {
                    float l = (keys[i].value - keys[i - 1].value) / (keys[i].hour - keys[i - 1].hour);
                    float r = (keys[i + 1].value - keys[i].value) / (keys[i + 1].hour - keys[i].hour);
                    if (l * r > 0f) slope = Mathf.Sign(l) * Mathf.Min(Mathf.Abs(l), Mathf.Abs(r));
                }
                frames[i] = new Keyframe(keys[i].hour, keys[i].value, slope, slope);
            }
            return new AnimationCurve(frames);
        }
    }
}
