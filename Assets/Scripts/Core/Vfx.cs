using Marea.Data;
using UnityEngine;
using UnityEngine.UI;

namespace Marea.Core
{
    /// <summary>
    /// 공용 파티클 창구. PF_Systems에 하나. 효과는 VfxId로만 부른다 — 어떤 프리팹인지는 VfxLibrary 표가 안다. (+10/6, 이슈 117)
    ///
    ///   Vfx.Play(VfxId.Dust, position);                       // 한 번 터지고 알아서 사라진다
    ///   var steam = Vfx.PlayLoop(VfxId.Steam, pot, offset);   // 계속 — 끝날 때 steam.Stop()
    ///   Vfx.PlayOnUI(VfxId.Sparkle, iconRectTransform);       // UI(화면 고정 캔버스) 위에
    ///
    /// UI 위 파티클 — 화면 고정(Overlay) 캔버스는 어떤 카메라보다 위에 그려져 파티클이 그 밑에 깔린다.
    /// 그래서 UI 전용 카메라가 파티클만(UIVfx 레이어) 투명 바탕 텍스처에 그리고, 그 텍스처를 맨 위 캔버스에
    /// 더하기(Additive)로 얹는다. UI 캔버스 설정은 안 건드린다. 반짝임 · 빛처럼 밝은 효과에 맞는 방식이다
    /// (검은 바탕에 더하므로 어두운 효과는 안 보인다).
    ///
    /// 반복 효과는 Stop()하면 방출을 멈추고 남은 입자가 사라진 뒤 지운다. 한 번짜리는 수명이 끝나면 지운다
    /// (Cartoon FX 프리팹은 스스로도 지운다 — 둘 다 걸려도 문제없다).
    /// </summary>
    public class Vfx : MonoBehaviour
    {
        public static Vfx Instance { get; private set; }

        [SerializeField] private VfxLibrary library;

        [Header("UI 위 파티클")]
        [Tooltip("UI 카메라만 그리는 레이어. 본 카메라에서는 뺀다.")]
        [SerializeField] private string uiLayerName = "UIVfx";
        [Tooltip("UI 카메라 텍스처를 화면에 더하기로 얹는 재질(Marea/UI Additive).")]
        [SerializeField] private Material uiAdditiveMaterial;
        [Tooltip("UI 파티클 1 단위가 화면 몇 픽셀인가. 클수록 UI 위 효과가 커 보인다.")]
        [SerializeField, Min(1f)] private float uiPixelsPerUnit = 120f;
        [Tooltip("UI 파티클 캔버스 정렬 순서. 다른 UI보다 위.")]
        [SerializeField] private int uiSortingOrder = 500;

        private int _uiLayer = -1;
        private Camera _uiCam;
        private RawImage _uiImage;
        private RenderTexture _uiTexture;
        private Transform _uiRoot;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Debug.LogError($"[Vfx] 씬에 둘 이상 있다 — '{name}'은 끈다.", this);
                enabled = false;
                return;
            }
            Instance = this;
            if (library == null) Debug.LogError("[Vfx] VfxLibrary가 비어 있다. 파티클이 하나도 안 나온다.", this);
            _uiLayer = LayerMask.NameToLayer(uiLayerName);
            if (_uiLayer < 0) Debug.LogError($"[Vfx] '{uiLayerName}' 레이어가 없다. UI 위 파티클이 안 나온다.", this);
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
            if (_uiTexture != null) _uiTexture.Release();
        }

        // ── 월드 ──

        /// <summary>한 번 터지는 효과. 수명이 끝나면 사라진다. 효과가 없으면 null.</summary>
        public static GameObject Play(VfxId id, Vector3 position, float scale = 1f, Color? tint = null, Quaternion? rotation = null)
        {
            if (!Ready(id, out VfxLibrary.Entry e)) return null;
            GameObject go = Instance.Spawn(e, position, rotation ?? Quaternion.identity, null, scale, tint);
            ScheduleCleanup(go, false);
            return go;
        }

        /// <summary>계속 나는 효과(불꽃 · 증기). parent를 따라다닌다. 끝낼 때 반환값의 Stop().</summary>
        public static VfxLoop PlayLoop(VfxId id, Transform parent, Vector3 localOffset = default, float scale = 1f, Color? tint = null)
        {
            if (parent == null || !Ready(id, out VfxLibrary.Entry e)) return default;
            GameObject go = Instance.Spawn(e, parent.TransformPoint(localOffset), Quaternion.identity, parent, scale, tint);
            SetLooping(go, true);
            return new VfxLoop(go);
        }

        // ── UI ──

        /// <summary>UI 요소 위에 한 번 터지는 효과.</summary>
        public static GameObject PlayOnUI(VfxId id, RectTransform target, float scale = 1f, Color? tint = null)
        {
            if (target == null || !Ready(id, out VfxLibrary.Entry e) || !Instance.EnsureUiRig()) return null;
            GameObject go = Instance.Spawn(e, Instance.UiPoint(target), Quaternion.identity, Instance._uiRoot, scale, tint);
            SetLayer(go, Instance._uiLayer);
            ScheduleCleanup(go, false);
            return go;
        }

        /// <summary>UI 요소 위에 계속 나는 효과. 끝낼 때 Stop().</summary>
        public static VfxLoop PlayLoopOnUI(VfxId id, RectTransform target, float scale = 1f, Color? tint = null)
        {
            if (target == null || !Ready(id, out VfxLibrary.Entry e) || !Instance.EnsureUiRig()) return default;
            GameObject go = Instance.Spawn(e, Instance.UiPoint(target), Quaternion.identity, Instance._uiRoot, scale, tint);
            SetLayer(go, Instance._uiLayer);
            SetLooping(go, true);
            return new VfxLoop(go);
        }

        // ── 안쪽 ──

        private static bool Ready(VfxId id, out VfxLibrary.Entry entry)
        {
            entry = default;
            if (id == VfxId.None) return false;
            if (Instance == null || !Instance.enabled)
            {
                Debug.LogError($"[Vfx] 씬에 Vfx가 없다 — {id}를 못 띄운다. PF_Systems를 둘 것.");
                return false;
            }
            if (Instance.library == null || !Instance.library.TryGet(id, out entry))
            {
                Debug.LogError($"[Vfx] VfxLibrary에 {id} 항목(프리팹)이 없다.", Instance);
                return false;
            }
            return true;
        }

        private GameObject Spawn(VfxLibrary.Entry e, Vector3 position, Quaternion rotation, Transform parent, float scale, Color? tint)
        {
            GameObject go = Instantiate(e.prefab, position, rotation * e.prefab.transform.rotation, parent);
            go.transform.localScale = e.prefab.transform.localScale * (e.scale * scale);
            // 부모에 달 땐 부모 크기를 상쇄한다 — 크기는 월드 기준으로 정한다(냄비 · 조리대가 키워져 있어도 같은 크기).
            if (parent != null)
            {
                float p = Mathf.Abs(parent.lossyScale.x);
                if (p > 1e-4f) go.transform.localScale /= p;
            }

            // 표의 색(비어 있으면 흰색) × 부르는 쪽 색
            Color c = e.tint == default ? Color.white : e.tint;
            if (tint.HasValue) c *= tint.Value;
            foreach (ParticleSystem ps in go.GetComponentsInChildren<ParticleSystem>(true))
            {
                ParticleSystem.MainModule main = ps.main;
                main.scalingMode = ParticleSystemScalingMode.Hierarchy;   // 루트 크기로 통째로 키우고 줄인다
                if (c != Color.white) main.startColor = Multiply(main.startColor, c);
            }
            return go;
        }

        private static ParticleSystem.MinMaxGradient Multiply(ParticleSystem.MinMaxGradient g, Color c)
        {
            switch (g.mode)
            {
                case ParticleSystemGradientMode.Color:
                    return new ParticleSystem.MinMaxGradient(g.color * c);
                case ParticleSystemGradientMode.TwoColors:
                    return new ParticleSystem.MinMaxGradient(g.colorMin * c, g.colorMax * c);
                default:
                    // 그라디언트는 색 하나로 바꾼다 — 재사용할 때 색을 정하는 게 목적이다.
                    return new ParticleSystem.MinMaxGradient(c);
            }
        }

        private static void SetLooping(GameObject go, bool loop)
        {
            foreach (ParticleSystem ps in go.GetComponentsInChildren<ParticleSystem>(true))
            {
                if (ps.main.loop == loop) continue;
                ps.Stop(false, ParticleSystemStopBehavior.StopEmittingAndClear);
                ParticleSystem.MainModule main = ps.main;
                main.loop = loop;
                ps.Play(false);
            }
        }

        /// <summary>한 번짜리 — 가장 긴 (재생 시간 + 입자 수명) 뒤에 지운다.</summary>
        private static void ScheduleCleanup(GameObject go, bool fromStop)
        {
            if (go == null) return;
            float longest = 0f;
            foreach (ParticleSystem ps in go.GetComponentsInChildren<ParticleSystem>(true))
            {
                ParticleSystem.MainModule m = ps.main;
                float life = m.startLifetime.constantMax + m.startDelay.constantMax;
                longest = Mathf.Max(longest, fromStop ? life : m.duration + life);
            }
            Destroy(go, longest + 0.5f);
        }

        private static void SetLayer(GameObject go, int layer)
        {
            foreach (Transform t in go.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = layer;
        }

        // UI 카메라 · 캔버스는 처음 UI 효과를 낼 때 만든다. UI 효과를 안 쓰는 씬은 비용이 없다.
        private bool EnsureUiRig()
        {
            if (_uiCam != null) return true;
            if (_uiLayer < 0) return false;
            if (uiAdditiveMaterial == null)
            {
                Debug.LogError("[Vfx] uiAdditiveMaterial이 비어 있다. UI 위 파티클이 안 보인다.", this);
                return false;
            }

            // 월드에서 멀리 떨어진 곳에 둔다 — 본 카메라 화면에 섞이지 않게.
            var rig = new GameObject("UiVfxRig");
            rig.transform.SetParent(transform, false);
            rig.transform.position = new Vector3(0f, -5000f, 0f);

            _uiCam = new GameObject("UiVfxCamera").AddComponent<Camera>();
            _uiCam.transform.SetParent(rig.transform, false);
            _uiCam.orthographic = true;
            _uiCam.cullingMask = 1 << _uiLayer;
            _uiCam.clearFlags = CameraClearFlags.SolidColor;
            _uiCam.backgroundColor = new Color(0f, 0f, 0f, 0f);
            _uiCam.nearClipPlane = 0.1f;
            _uiCam.farClipPlane = 50f;
            _uiCam.depth = -50;

            _uiRoot = new GameObject("Effects").transform;
            _uiRoot.SetParent(rig.transform, false);

            var canvas = new GameObject("UiVfxCanvas").AddComponent<Canvas>();
            canvas.transform.SetParent(transform, false);
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = uiSortingOrder;
            _uiImage = new GameObject("Image").AddComponent<RawImage>();
            _uiImage.transform.SetParent(canvas.transform, false);
            _uiImage.raycastTarget = false;
            _uiImage.material = uiAdditiveMaterial;
            RectTransform rt = _uiImage.rectTransform;
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = rt.offsetMax = Vector2.zero;

            ResizeUiTexture();
            return true;
        }

        private void ResizeUiTexture()
        {
            int w = Mathf.Max(1, Screen.width), h = Mathf.Max(1, Screen.height);
            if (_uiTexture != null && _uiTexture.width == w && _uiTexture.height == h) return;
            if (_uiTexture != null) _uiTexture.Release();
            _uiTexture = new RenderTexture(w, h, 16, RenderTextureFormat.ARGB32) { name = "UiVfx" };
            _uiCam.targetTexture = _uiTexture;
            _uiCam.orthographicSize = h * 0.5f / uiPixelsPerUnit;
            _uiImage.texture = _uiTexture;
        }

        /// <summary>UI 요소의 화면 위치 → UI 카메라 앞 월드 점.</summary>
        private Vector3 UiPoint(RectTransform target)
        {
            Canvas canvas = target.GetComponentInParent<Canvas>();
            Camera eventCam = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay ? canvas.worldCamera : null;
            Vector2 screen = RectTransformUtility.WorldToScreenPoint(eventCam, target.TransformPoint(target.rect.center));
            return _uiCam.ScreenToWorldPoint(new Vector3(screen.x, screen.y, 10f));
        }

        private void LateUpdate()
        {
            if (_uiCam == null) return;
            ResizeUiTexture();
            // 효과가 없으면 카메라 · 이미지를 쉰다.
            bool any = _uiRoot.childCount > 0;
            _uiCam.enabled = any;
            _uiImage.enabled = any;
            // 본 카메라가 UIVfx 레이어를 그리지 않게(미니게임에서 카메라가 바뀌어도).
            Camera main = Camera.main;
            if (main != null && (main.cullingMask & (1 << _uiLayer)) != 0) main.cullingMask &= ~(1 << _uiLayer);
        }
    }

    /// <summary>계속 나는 효과 손잡이. Stop()하면 방출을 멈추고 남은 입자가 사라진 뒤 지운다. 두 번 불러도 된다.</summary>
    public readonly struct VfxLoop
    {
        private readonly GameObject _go;
        public VfxLoop(GameObject go) => _go = go;
        public bool IsAlive => _go != null;
        public Transform Transform => _go != null ? _go.transform : null;

        public void Stop()
        {
            if (_go == null) return;
            foreach (ParticleSystem ps in _go.GetComponentsInChildren<ParticleSystem>(true))
                ps.Stop(false, ParticleSystemStopBehavior.StopEmitting);
            _go.transform.SetParent(null, true);   // 부모가 꺼져도 남은 입자가 마저 사라지게
            ScheduleCleanupAfterStop(_go);
        }

        private static void ScheduleCleanupAfterStop(GameObject go)
        {
            float longest = 0f;
            foreach (ParticleSystem ps in go.GetComponentsInChildren<ParticleSystem>(true))
                longest = Mathf.Max(longest, ps.main.startLifetime.constantMax);
            Object.Destroy(go, longest + 0.5f);
        }
    }
}
