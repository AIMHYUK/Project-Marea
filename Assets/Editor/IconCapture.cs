using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Marea.EditorTools
{
    /// <summary>
    /// 모델을 찍어 성장 창 아이콘(투명 배경 PNG)으로 저장한다. (+10/8)
    /// 씬 위 먼 곳에 잠깐 세우고 찍은 뒤 지운다 — 씬은 더럽히지 않는다.
    /// </summary>
    public static class IconCapture
    {
        private const string StaffPrefab = "Assets/Prefabs/PF_ServingStaff.prefab";
        private const string StaffIdle = "Assets/Art/Asset/Character/Sujung/Animation/AN_Sujung_Standing Idle.fbx";
        private const string OutDir = "Assets/Art/UI/Icons/Facility/";
        private const int Size = 512;
        private const float KeyLight = 0.8f;    // 씬 빛 위에 더하는 보조광 세기 (+10/8)
        private const float Saturation = 1.2f;  // 찍은 뒤 채도 배율 (+10/8)

        [MenuItem("Marea/아이콘 캡처/직원 (상반신)")]
        private static void CaptureStaff()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(StaffPrefab);
            var idle = AssetDatabase.LoadAllAssetsAtPath(StaffIdle).OfType<AnimationClip>()
                .FirstOrDefault(c => !c.name.StartsWith("__preview__"));
            if (prefab == null) { Debug.LogError($"[IconCapture] 프리팹 없음: {StaffPrefab}"); return; }

            // 허리 위만: 키의 위쪽 52%
            Capture(prefab, idle, OutDir + "Icon_Fac_StaffModel.png", topFraction: 0.52f, yaw: 25f, pitch: 8f);
        }

        [MenuItem("Marea/아이콘 캡처/직원 (가슴 위)")]
        private static void CaptureStaffClose()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(StaffPrefab);
            var idle = AssetDatabase.LoadAllAssetsAtPath(StaffIdle).OfType<AnimationClip>()
                .FirstOrDefault(c => !c.name.StartsWith("__preview__"));
            if (prefab == null) { Debug.LogError($"[IconCapture] 프리팹 없음: {StaffPrefab}"); return; }

            Capture(prefab, idle, OutDir + "Icon_Fac_StaffModel_Close.png", topFraction: 0.34f, yaw: 25f, pitch: 8f);
        }

        // (+10/9) 야채볶음 간 맞추기 리듬 노트 — 씬의 소금통 · 후추통 모델을 그대로 찍는다.
        private const string ShakerRoot = "Object/Restaurant/PF_Kitchen/CookingTable_MinigameView 1 1/VeggieStirFryStation/Step3Panel";
        private const string RhythmDir = "Assets/Art/UI/Minigame/Rhythm/";

        [MenuItem("Marea/아이콘 캡처/양념통 (소금 · 후추)")]
        private static void CaptureShakers()
        {
            // 패널이 꺼져 있어 GameObject.Find 로는 못 찾는다
            Transform panel = Resources.FindObjectsOfTypeAll<Transform>()
                .FirstOrDefault(t => t.gameObject.scene.IsValid() && GetPath(t) == ShakerRoot);
            if (panel == null) { Debug.LogError($"[IconCapture] 없음: {ShakerRoot}"); return; }
            Directory.CreateDirectory(RhythmDir);
            foreach (var (child, file) in new[] { ("salt", "Icon_Rhythm_Salt.png"), ("peper", "Icon_Rhythm_Pepper.png") })
            {
                Transform t = panel.Find(child);
                if (t == null) { Debug.LogError($"[IconCapture] 없음: {ShakerRoot}/{child}"); continue; }
                Capture(t.gameObject, null, RhythmDir + file, topFraction: 1f, yaw: 0f, pitch: 30f, keepRotation: true);
            }
        }

        private static string GetPath(Transform t)
        {
            string p = t.name;
            while (t.parent != null) { t = t.parent; p = t.name + "/" + p; }
            return p;
        }

        private static void Capture(GameObject prefab, AnimationClip pose, string outPath,
                                    float topFraction, float yaw, float pitch, bool keepRotation = false)
        {
            // (+10/9) 프리팹이 아니면(씬 오브젝트) 복제해서 찍는다. 씬에 놓인 기울기를 살리려면 keepRotation.
            var root = PrefabUtility.IsPartOfPrefabAsset(prefab)
                ? (GameObject)PrefabUtility.InstantiatePrefab(prefab)
                : Object.Instantiate(prefab, prefab.transform.position, prefab.transform.rotation);
            root.SetActive(true);
            root.hideFlags = HideFlags.HideAndDontSave;
            Quaternion rot = keepRotation ? prefab.transform.rotation : Quaternion.identity;
            root.transform.SetPositionAndRotation(new Vector3(0f, 5000f, 0f), rot);

            bool fog = RenderSettings.fog;
            var camGo = new GameObject("IconCaptureCam") { hideFlags = HideFlags.HideAndDontSave };
            var rt = new RenderTexture(Size * 2, Size * 2, 24, RenderTextureFormat.ARGB32);
            try
            {
                if (pose != null)
                {
                    AnimationMode.StartAnimationMode();
                    AnimationMode.BeginSampling();
                    AnimationMode.SampleAnimationClip(root, pose, 0f);
                    AnimationMode.EndSampling();
                }

                var rends = root.GetComponentsInChildren<Renderer>().Where(r => r.enabled && !(r is ParticleSystemRenderer)).ToArray();
                if (rends.Length == 0) { Debug.LogError("[IconCapture] 렌더러 없음"); return; }
                Bounds b = rends[0].bounds;
                foreach (var r in rends) b.Encapsulate(r.bounds);

                // 위쪽 일부만 프레이밍
                float h = b.size.y * topFraction;
                var focus = new Vector3(b.center.x, b.max.y - h * 0.5f, b.center.z);
                float radius = h * 0.62f;   // 폭은 안 본다 — 스킨 bounds 가 T포즈 폭으로 남아 있을 수 있다
                if (pose == null) radius = Mathf.Max(h, b.size.x, b.size.z) * 0.62f;   // (+10/9) 정지 모델은 폭도 본다 — 기울어진 양념통

                var cam = camGo.AddComponent<Camera>();
                cam.fieldOfView = 20f;
                cam.clearFlags = CameraClearFlags.SolidColor;
                cam.backgroundColor = new Color(0, 0, 0, 0);
                cam.allowHDR = false;
                cam.allowMSAA = false;
                cam.targetTexture = rt;
                float dist = radius / Mathf.Sin(cam.fieldOfView * 0.5f * Mathf.Deg2Rad);
                // 캐릭터 앞쪽에서. 씬 기울기를 살린 모델은 게임 카메라처럼 월드 +z(바깥쪽)에서 본다 (+10/9)
                var dir = Quaternion.Euler(-pitch, yaw, 0f) * (keepRotation ? Vector3.forward : root.transform.forward);
                cam.transform.position = focus + dir * dist;
                cam.transform.LookAt(focus);
                cam.nearClipPlane = 0.01f;
                cam.farClipPlane = dist * 4f;

                // 보조광: 카메라 쪽 위에서 비춘다 (+10/8)
                var key = new GameObject("IconCaptureKey") { hideFlags = HideFlags.HideAndDontSave }.AddComponent<Light>();
                key.transform.parent = camGo.transform;
                key.type = LightType.Directional;
                key.intensity = KeyLight;
                key.shadows = LightShadows.None;
                key.transform.rotation = Quaternion.LookRotation(focus - (cam.transform.position + Vector3.up * dist * 0.6f));

                RenderSettings.fog = false;
                // (+10/9) 한 번 먼저 그린다 — 셰이더가 비동기 컴파일 중이면 첫 렌더에서 모델이 빠져 빈 PNG 가 나왔다(소금통)
                bool async = ShaderUtil.allowAsyncCompilation;
                ShaderUtil.allowAsyncCompilation = false;
                cam.Render();
                ShaderUtil.allowAsyncCompilation = async;
                cam.Render();

                var prev = RenderTexture.active;
                RenderTexture.active = rt;
                var full = new Texture2D(rt.width, rt.height, TextureFormat.RGBA32, false);
                full.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
                full.Apply();
                RenderTexture.active = prev;

                var icon = TrimToSquare(full, Size);
                Saturate(icon, Saturation);
                File.WriteAllBytes(outPath, icon.EncodeToPNG());
                Object.DestroyImmediate(full);
                Object.DestroyImmediate(icon);

                AssetDatabase.ImportAsset(outPath);
                if (AssetImporter.GetAtPath(outPath) is TextureImporter ti)
                {
                    ti.textureType = TextureImporterType.Sprite;
                    ti.spriteImportMode = SpriteImportMode.Single;   // 기본이 Multiple 로 잡혀 fileID 가 달라진다 (+10/8)
                    ti.alphaIsTransparency = true;
                    ti.mipmapEnabled = false;
                    ti.SaveAndReimport();
                }
                Debug.Log($"[IconCapture] 저장: {outPath}");
            }
            finally
            {
                if (AnimationMode.InAnimationMode()) AnimationMode.StopAnimationMode();
                RenderSettings.fog = fog;
                Object.DestroyImmediate(camGo);
                Object.DestroyImmediate(root);
                rt.Release();
                Object.DestroyImmediate(rt);
            }
        }

        private static void Saturate(Texture2D tex, float amount)
        {
            var px = tex.GetPixels();
            for (int i = 0; i < px.Length; i++)
            {
                Color.RGBToHSV(px[i], out float h, out float s, out float v);
                var c = Color.HSVToRGB(h, Mathf.Clamp01(s * amount), v);
                c.a = px[i].a;
                px[i] = c;
            }
            tex.SetPixels(px);
            tex.Apply();
        }

        /// <summary>불투명한 부분만 남기고 정사각으로 자른 뒤 size 로 줄인다. 여백 6%.</summary>
        private static Texture2D TrimToSquare(Texture2D src, int size)
        {
            var px = src.GetPixels32();
            int w = src.width, hgt = src.height;
            int minX = w, minY = hgt, maxX = -1, maxY = -1;
            for (int y = 0; y < hgt; y++)
                for (int x = 0; x < w; x++)
                    if (px[y * w + x].a > 8)
                    {
                        if (x < minX) minX = x; if (x > maxX) maxX = x;
                        if (y < minY) minY = y; if (y > maxY) maxY = y;
                    }
            if (maxX < 0) { minX = 0; minY = 0; maxX = w - 1; maxY = hgt - 1; }

            int side = Mathf.CeilToInt(Mathf.Max(maxX - minX + 1, maxY - minY + 1) * 1.06f);
            int cx = (minX + maxX) / 2, cy = (minY + maxY) / 2;
            var dst = new Texture2D(size, size, TextureFormat.RGBA32, false);
            var outPx = new Color32[size * size];
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    // 상자 평균으로 줄이기 (계단 덜 생기게)
                    float fx0 = cx - side * 0.5f + (float)x * side / size;
                    float fy0 = cy - side * 0.5f + (float)y * side / size;
                    float step = (float)side / size;
                    int n = 0; float r = 0, g = 0, bl = 0, a = 0;
                    for (float sy = fy0; sy < fy0 + step; sy += 1f)
                        for (float sx = fx0; sx < fx0 + step; sx += 1f)
                        {
                            int ix = (int)sx, iy = (int)sy; n++;
                            if (ix < 0 || iy < 0 || ix >= w || iy >= hgt) continue;
                            var c = px[iy * w + ix];
                            r += c.r * c.a; g += c.g * c.a; bl += c.b * c.a; a += c.a;
                        }
                    outPx[y * size + x] = a <= 0 ? new Color32(0, 0, 0, 0)
                        : new Color32((byte)(r / a), (byte)(g / a), (byte)(bl / a), (byte)(a / Mathf.Max(1, n)));
                }
            dst.SetPixels32(outPx);
            dst.Apply();
            return dst;
        }
    }
}
