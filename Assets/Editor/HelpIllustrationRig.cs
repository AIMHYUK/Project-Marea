using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Rendering.Universal;

/// <summary>
/// (+10/13) 도움말 일러스트(Assets/Art/UI/Help/Illust) 촬영용 — 물건 하나만 투명 배경으로 찍는다. 다시 찍을 때 쓴다.
/// 원본을 복제해 빈 곳(y -1000)의 전용 레이어로 옮기고, 움직이는 코드 · 이펙트를 끈 뒤 전용 카메라로 찍는다.
/// 플레이 중 살아 있는 캐릭터를 복제하면 그 순간 자세(뼈 위치)가 그대로 남는다.
/// </summary>
public static class HelpIllustrationRig
{
    private const int Layer = 31;
    private static readonly Vector3 Stage = new(0f, -1000f, 0f);
    /// <summary>켜면 꺼진 자식 · 렌더러까지 다 켜서 찍는다(이벤트 때만 보이는 고래 등).</summary>
    public static bool ForceShow;

    /// <summary>꺼진 것까지 이름 경로로 찾는다.</summary>
    public static GameObject FindAny(string path)
    {
        foreach (Transform t in Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            string p = t.name; for (Transform q = t.parent; q != null; q = q.parent) p = q.name + "/" + p;
            if (p == path) return t.gameObject;
        }
        return null;
    }

    /// <param name="yaw">물건을 어느 쪽에서 보나(도). 0 = 물건의 -Z 쪽(앞)에서.</param>
    /// <param name="pitch">내려다보는 각(도).</param>
    /// <param name="keepDecals">데칼(구멍 장판)도 찍는다.</param>
    public static string Capture(Object source, string outPath, float yaw = 30f, float pitch = 25f, int width = 1400, int height = 900, bool keepDecals = false, Vector3? euler = null)
    {
        GameObject src = source as GameObject;
        if (src == null && source is Component c) src = c.gameObject;
        if (src == null) return "원본 없음";

        GameObject go = Object.Instantiate(src);
        go.SetActive(true);
        go.transform.SetPositionAndRotation(Stage, euler.HasValue ? Quaternion.Euler(euler.Value) : Quaternion.Euler(0f, src.transform.eulerAngles.y, 0f));
        go.transform.localScale = src.transform.lossyScale;
        Strip(go, keepDecals);
        if (ForceShow)
            foreach (Renderer r in go.GetComponentsInChildren<Renderer>(true))
                if (r is MeshRenderer || r is SkinnedMeshRenderer) { r.enabled = true; for (Transform t = r.transform; t != go.transform; t = t.parent) t.gameObject.SetActive(true); }
        foreach (Transform t in go.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = Layer;

        var rs = new List<Renderer>();
        foreach (Renderer r in go.GetComponentsInChildren<Renderer>())
            if (r.enabled && (r is MeshRenderer || r is SkinnedMeshRenderer)) rs.Add(r);
        if (rs.Count == 0) { Object.DestroyImmediate(go); return "렌더러 없음"; }
        Bounds b = rs[0].bounds;
        foreach (Renderer r in rs) b.Encapsulate(r.bounds);

        var camGo = new GameObject("HelpIllustrationCam");
        var cam = camGo.AddComponent<Camera>();
        cam.cullingMask = 1 << Layer;
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(0, 0, 0, 0);
        cam.allowHDR = false; cam.allowMSAA = true;
        cam.fieldOfView = 20f; cam.nearClipPlane = 0.05f; cam.farClipPlane = 500f;
        var data = camGo.AddComponent<UniversalAdditionalCameraData>();
        data.renderPostProcessing = false; data.renderShadows = false;

        Quaternion look = Quaternion.Euler(pitch, yaw + go.transform.eulerAngles.y + 180f, 0f);
        float radius = b.extents.magnitude;
        float dist = radius / Mathf.Sin(cam.fieldOfView * 0.5f * Mathf.Deg2Rad) * 1.05f;
        camGo.transform.SetPositionAndRotation(b.center - look * Vector3.forward * dist, look);

        var rt = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32) { antiAliasing = 4 };
        cam.targetTexture = rt; cam.Render();
        RenderTexture.active = rt;
        var tex = new Texture2D(width, height, TextureFormat.RGBA32, false);
        tex.ReadPixels(new Rect(0, 0, width, height), 0, 0); tex.Apply();
        System.IO.File.WriteAllBytes(outPath, tex.EncodeToPNG());
        cam.targetTexture = null; RenderTexture.active = null;
        Object.DestroyImmediate(rt); Object.DestroyImmediate(tex);
        Object.DestroyImmediate(camGo); Object.DestroyImmediate(go);
        return $"ok {outPath} bounds={b.size}";
    }

    private static void Strip(GameObject go, bool keepDecals)
    {
        foreach (Animator a in go.GetComponentsInChildren<Animator>(true)) a.enabled = false;
        foreach (SkinnedMeshRenderer s in go.GetComponentsInChildren<SkinnedMeshRenderer>(true)) s.updateWhenOffscreen = true;   // 옮긴 자리에서 잘리지 않게
        foreach (NavMeshAgent a in go.GetComponentsInChildren<NavMeshAgent>(true)) Object.DestroyImmediate(a);
        foreach (MonoBehaviour m in go.GetComponentsInChildren<MonoBehaviour>(true))
            if (!(keepDecals && m is DecalProjector)) m.enabled = false;
        foreach (ParticleSystem p in go.GetComponentsInChildren<ParticleSystem>(true)) p.gameObject.SetActive(false);
        foreach (LineRenderer l in go.GetComponentsInChildren<LineRenderer>(true)) l.enabled = false;
        foreach (Canvas cv in go.GetComponentsInChildren<Canvas>(true)) cv.gameObject.SetActive(false);
        if (!keepDecals)
            foreach (DecalProjector d in go.GetComponentsInChildren<DecalProjector>(true)) d.enabled = false;
    }
}
