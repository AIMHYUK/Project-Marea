using System.Collections;
using Marea.Field;
using UnityEngine;
using UnityEngine.Rendering.Universal;

/// <summary>F9 고래 관찰 카메라와 같은 옆 방향 구도를 로딩 중에 사용한다.</summary>
[DefaultExecutionOrder(1000)]
public sealed class LoadingWhaleView : MonoBehaviour
{
    [SerializeField] private WhaleSwimmer whale;
    [SerializeField] private Camera viewCamera;
    [SerializeField] private GameObject menuBackdrop;
    [SerializeField, Min(1f)] private float sideDistance = 28f;
    [SerializeField] private float height = 10f;
    [SerializeField] private float lookUp = 3f;
    private bool watching;
    private Vector3 originalPosition;
    private Quaternion originalRotation;
    private Renderer[] whaleRenderers;
    private bool[] rendererEnabled;
    private int[] originalLayers;
    private bool prepared;
    private Camera warmupCamera;
    private RenderTexture warmupTexture;
    private Vector3 cameraVelocity;
    private Vector3 lookVelocity;
    private Vector3 smoothLook;

    private IEnumerator Start()
    {
        if (whale == null || viewCamera == null) { prepared = true; yield break; }
        // 타이틀을 보고 있는 동안 별도 소형 렌더 타깃으로 고래의 첫 렌더링을 준비한다.
        whaleRenderers = whale.GetComponentsInChildren<Renderer>(true);
        rendererEnabled = new bool[whaleRenderers.Length];
        originalLayers = new int[whaleRenderers.Length];
        whale.gameObject.SetActive(true);
        whale.PrepareForLoading();
        int originalMask = viewCamera.cullingMask;
        viewCamera.cullingMask &= ~(1 << 31);
        for (int i = 0; i < whaleRenderers.Length; i++)
        {
            rendererEnabled[i] = whaleRenderers[i].enabled;
            originalLayers[i] = whaleRenderers[i].gameObject.layer;
            whaleRenderers[i].gameObject.layer = 31;
        }
        var cameraObject = new GameObject("LoadingWhaleWarmupCamera", typeof(Camera), typeof(UniversalAdditionalCameraData));
        warmupCamera = cameraObject.GetComponent<Camera>();
        warmupCamera.CopyFrom(viewCamera);
        warmupCamera.cullingMask = 1 << 31;
        warmupCamera.clearFlags = CameraClearFlags.SolidColor;
        warmupCamera.depth = viewCamera.depth - 1f;
        warmupTexture = new RenderTexture(256, 144, 16);
        warmupCamera.targetTexture = warmupTexture;
        GetCameraPose(out var position, out var look);
        warmupCamera.transform.SetPositionAndRotation(position, Quaternion.LookRotation(look - position, Vector3.up));
        yield return null;
        yield return new WaitForEndOfFrame();
        yield return null;
        for (int i = 0; i < whaleRenderers.Length; i++)
        {
            whaleRenderers[i].enabled = false;
            whaleRenderers[i].gameObject.layer = originalLayers[i];
        }
        viewCamera.cullingMask = originalMask;
        ReleaseWarmup();
        prepared = true;
    }

    public IEnumerator WaitUntilPrepared()
    {
        while (!prepared) yield return null;
    }

    public void Begin()
    {
        if (whale == null || viewCamera == null || watching) return;
        originalPosition = viewCamera.transform.position;
        originalRotation = viewCamera.transform.rotation;
        if (menuBackdrop != null) menuBackdrop.SetActive(false);
        whale.gameObject.SetActive(true);
        if (whaleRenderers != null)
            for (int i = 0; i < whaleRenderers.Length; i++) whaleRenderers[i].enabled = rendererEnabled[i];
        whale.AppearForLoading();
        watching = true;
        GetCameraPose(out var position, out smoothLook);
        cameraVelocity = lookVelocity = Vector3.zero;
        viewCamera.transform.SetPositionAndRotation(position, Quaternion.LookRotation(smoothLook - position, Vector3.up));
    }

    public void End()
    {
        if (!watching) return;
        watching = false;
        if (whale != null) whale.StopAllCoroutines();
        if (whaleRenderers != null)
            foreach (var renderer in whaleRenderers) if (renderer != null) renderer.enabled = false;
        if (viewCamera != null) viewCamera.transform.SetPositionAndRotation(originalPosition, originalRotation);
        if (menuBackdrop != null) menuBackdrop.SetActive(true);
    }

    private void LateUpdate()
    {
        if (watching && whale != null && viewCamera != null) FollowWhale();
    }

    private void FollowWhale()
    {
        GetCameraPose(out var position, out var look);
        float delta = Mathf.Min(Time.unscaledDeltaTime, 1f / 30f);
        position = Vector3.SmoothDamp(viewCamera.transform.position, position, ref cameraVelocity, 0.12f, Mathf.Infinity, delta);
        smoothLook = Vector3.SmoothDamp(smoothLook, look, ref lookVelocity, 0.18f, Mathf.Infinity, delta);
        viewCamera.transform.SetPositionAndRotation(position, Quaternion.LookRotation(smoothLook - position, Vector3.up));
    }

    private void GetCameraPose(out Vector3 position, out Vector3 look)
    {
        Vector3 forward = Vector3.ProjectOnPlane(whale.transform.forward, Vector3.up).normalized;
        Vector3 side = Vector3.Cross(Vector3.up, forward);
        look = whale.VisualCenter;
        look.y = Mathf.Max(look.y + lookUp, whale.SeaLevel);
        position = new Vector3(whale.transform.position.x, whale.SeaLevel + height, whale.transform.position.z) + side * sideDistance;
    }

    private void ReleaseWarmup()
    {
        if (warmupCamera != null)
        {
            warmupCamera.enabled = false;
            warmupCamera.targetTexture = null;
            Destroy(warmupCamera.gameObject);
        }
        if (warmupTexture != null)
        {
            warmupTexture.Release();
            Destroy(warmupTexture);
        }
    }

    private void OnDestroy() => ReleaseWarmup();
}
