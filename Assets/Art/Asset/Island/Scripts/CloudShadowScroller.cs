using UnityEngine;
using UnityEngine.Rendering;

namespace Marea.Island
{
    /// <summary>
    /// 하늘에 띄운 "그림자만 드리우는" 구름 판을 바람에 흘리고, 렌더하는 카메라 위로 따라가게 한다. (+10/5)
    ///
    /// 구름 생김새(크기·양·외곽 흐림·세기)는 머티리얼 M_CloudShadow 에서 조절한다 — 여기서는 안 건드린다.
    /// 이 컴포넌트는 _CloudOffset(바람)만 MaterialPropertyBlock 으로 넣는다.
    ///
    /// 라이트 쿠키 대신 이 방식을 쓰는 이유: 물(UberStylizedWater)이 쿠키는 안 받고 섀도맵만 받는다.
    /// 판이 150m를 넘으면 비스듬한 해 때문에 섀도맵 깊이 클램핑이 큰 삼각형 전체를 망가뜨려
    /// 그림자가 사라진다 (10/5 실측: 150m 정상, 400m 사라짐). 그래서 판은 작게 두고 카메라를 따라간다.
    /// 구름 무늬는 셰이더에서 월드 XZ로 찍으므로 판이 움직여도 제자리다.
    /// </summary>
    [ExecuteAlways]
    [RequireComponent(typeof(Renderer))]
    public class CloudShadowScroller : MonoBehaviour
    {
        private static readonly int CloudOffsetId = Shader.PropertyToID("_CloudOffset");
        private static readonly int CloudSizeId = Shader.PropertyToID("_CloudSize");

        [Header("바람")]
        [Tooltip("구름이 흘러가는 방향 (월드 X/Z)")]
        [SerializeField] private Vector2 _windDirection = new Vector2(1f, 0.3f);

        [Tooltip("초당 이동 거리 (m)")]
        [SerializeField] private float _speed = 0.8f;

        [Header("판")]
        [Tooltip("판이 떠 있는 높이 (월드 Y)")]
        [SerializeField] private float _height = 45f;

        [Tooltip("카메라가 보는 쪽으로 판 중심을 얼마나 당길지 (m)")]
        [SerializeField] private float _lookAhead = 25f;

        private Renderer _renderer;
        private MaterialPropertyBlock _block;
        private Vector2 _offset;

        private void OnEnable()
        {
            _renderer = GetComponent<Renderer>();
            _block ??= new MaterialPropertyBlock();
            RenderPipelineManager.beginCameraRendering += FollowCamera;
            Apply();
        }

        private void OnDisable()
        {
            RenderPipelineManager.beginCameraRendering -= FollowCamera;
        }

        private void Update()
        {
            if (!Application.isPlaying) return;
            var mat = _renderer.sharedMaterial;
            float cloudSize = mat != null && mat.HasProperty(CloudSizeId) ? Mathf.Max(mat.GetFloat(CloudSizeId), 0.01f) : 35f;
            _offset += _windDirection.normalized * (_speed / cloudSize * Time.deltaTime);
            _offset.x = Mathf.Repeat(_offset.x, 1f);   // 부동소수 정밀도 유지
            _offset.y = Mathf.Repeat(_offset.y, 1f);
            Apply();
        }

        // 그림자 컬링 전에 불린다 — 씬뷰·게임뷰 어느 카메라든 그 카메라 아래에 구름이 있게.
        private void FollowCamera(ScriptableRenderContext context, Camera cam)
        {
            if (cam.cameraType == CameraType.Preview || cam.cameraType == CameraType.Reflection) return;
            Vector3 forward = cam.transform.forward;
            forward.y = 0f;
            Vector3 center = cam.transform.position + forward.normalized * _lookAhead;
            transform.position = new Vector3(center.x, _height, center.z);
        }

        private void Apply()
        {
            if (_renderer == null) return;
            _renderer.GetPropertyBlock(_block);
            _block.SetVector(CloudOffsetId, new Vector4(_offset.x, _offset.y, 0f, 0f));
            _renderer.SetPropertyBlock(_block);
        }
    }
}
