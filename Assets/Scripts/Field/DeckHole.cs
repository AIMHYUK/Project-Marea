using System.Collections.Generic;
using UnityEngine;

namespace Marea.Field
{
    /// <summary>
    /// (+10/9) 켜져 있는 동안 데크(MM_Shader_DeckHole 재질)에서 판자 한 장을 뜯어낸다. 수리 장판의 부서진 모습 안에 둔다 —
    /// 고치면 부서진 모습이 꺼지면서 구멍도 닫힌다.
    ///
    /// 켜질 때 아래 데크의 텍스처 판자 하나에 딱 맞춘다: 그 판자 가운데로 옮기고, 결 방향으로 돌리고, 폭을 판자 폭으로.
    /// 데크마다 결 · 판자 폭이 다르다(식당 Z결 0.34m, Lv03 X결). 셰이더 쪽은 Assets/Shaders/DeckHole/DeckHole.hlsl.
    /// </summary>
    public class DeckHole : MonoBehaviour
    {
        [Tooltip("뜯어낸 판자 길이(m, 결 방향).")]
        [SerializeField, Min(0.1f)] private float length = 1.1f;

        [Tooltip("이 오브젝트 높이 위아래 이만큼만 뚫는다 — 데크 윗면만, 아래 기둥 · 물은 그대로.")]
        [SerializeField, Min(0.01f)] private float heightTolerance = 0.15f;

        [Tooltip("구멍에 놓인 뜯긴 판자. 폭(X)을 데크 판자 폭 × plankWidthRatio로 맞춘다. 기울기 · 길이는 프리팹 그대로.")]
        [SerializeField] private Transform plank;

        [Tooltip("뜯긴 판자 폭 = 데크 판자 폭 × 이 값. 1보다 크면 구멍 가장자리에 걸친다.")]
        [SerializeField, Min(0.1f)] private float plankWidthRatio = 1.27f;

        private const string HoleShader = "Shader Graphs/MM_Shader_DeckHole";
        private const float PlanksPerTexture = 16f;   // 데크 나무 텍스처(Wood_01 BaseColor) 가로에 판자 16장

        private const int Max = 16;   // DeckHole.hlsl의 DECK_HOLE_MAX와 같다
        private static readonly List<DeckHole> Active = new();
        private static readonly Vector4[] Centers = new Vector4[Max];
        private static readonly Vector4[] Sizes = new Vector4[Max];
        private static readonly int CenterId = Shader.PropertyToID("_DeckHoleCenter");
        private static readonly int SizeId = Shader.PropertyToID("_DeckHoleSize");
        private static readonly int CountId = Shader.PropertyToID("_DeckHoleCount");

        private float _width = 0.3f;

        private void Awake() => FitToPlank();

        private void OnEnable()
        {
            Active.Add(this);
            Push();
        }

        private void OnDisable()
        {
            Active.Remove(this);
            Push();
        }

        /// <summary>아래 데크에서 이 자리가 속한 텍스처 판자를 찾아 그 판자 가운데 · 결 방향 · 폭에 맞춘다.</summary>
        private void FitToPlank()
        {
            Vector3 p = transform.position;
            foreach (MeshRenderer r in FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None))
            {
                if (r.sharedMaterial == null || r.sharedMaterial.shader.name != HoleShader) continue;
                Bounds b = r.bounds;
                if (p.x < b.min.x || p.x > b.max.x || p.z < b.min.z || p.z > b.max.z || Mathf.Abs(p.y - b.max.y) > 0.5f) continue;
                if (!TopFace(r, out Vector3 origin, out float u0, out Vector3 perU, out Vector3 perV)) continue;

                // 판자 하나 폭만큼의 월드 이동. 재질 Tiling(_UV.x)이 U를 늘린다.
                float tiling = r.sharedMaterial.GetVector("_UV").x;
                Vector3 perPlank = perU / (tiling * PlanksPerTexture);
                float u = u0 + Vector3.Dot(p - origin, perU) / perU.sqrMagnitude;
                float k = u * tiling * PlanksPerTexture;
                Vector3 fit = p + perPlank * (Mathf.Floor(k) + 0.5f - k);

                // (+10/10) 높이도 데크 윗면에 붙인다. 선장 이벤트는 NavMesh 높이에 장판을 놓는데, 식당 데크는 살짝 기울어
                // NavMesh가 윗면보다 0.16m 뜬 곳이 있다 — heightTolerance(0.15)를 넘어 구멍이 안 뚫리고 판자도 떴다.
                Vector3 normal = Vector3.Cross(perU, perV);
                if (Mathf.Abs(normal.y) > 1e-4f)
                    fit.y = origin.y - (normal.x * (fit.x - origin.x) + normal.z * (fit.z - origin.z)) / normal.y;
                transform.position = fit;

                Vector3 along = new Vector3(perV.x, 0f, perV.z);
                transform.rotation = Quaternion.LookRotation(along.normalized, Vector3.up);
                _width = perPlank.magnitude;

                if (plank != null)
                {
                    MeshFilter mf = plank.GetComponentInChildren<MeshFilter>();
                    float meshWidth = mf != null && mf.sharedMesh != null ? mf.sharedMesh.bounds.size.x : 0f;
                    if (meshWidth > 0f) plank.localScale = new Vector3(_width / meshWidth * plankWidthRatio, plank.localScale.y, plank.localScale.z);
                }
                return;
            }
            Debug.LogWarning($"{name}: 아래에 구멍 재질({HoleShader}) 데크가 없다. 데크가 안 뚫리고 판자만 보인다.", this);
        }

        /// <summary>
        /// 윗면 한 꼭짓점(origin, 그 점의 u)과 uv 1당 월드 이동(perU · perV). ProBuilder 상자라 윗면은 축 정렬 사각형이다.
        /// </summary>
        private static bool TopFace(MeshRenderer r, out Vector3 origin, out float u0, out Vector3 perU, out Vector3 perV)
        {
            origin = perU = perV = Vector3.zero;
            u0 = 0f;
            MeshFilter mf = r.GetComponent<MeshFilter>();
            if (mf == null || mf.sharedMesh == null) return false;
            Mesh mesh = mf.sharedMesh;
            Vector3[] v = mesh.vertices;
            Vector2[] uv = mesh.uv;
            Vector3[] n = mesh.normals;
            Transform t = r.transform;
            for (int a = 0; a < v.Length; a++)
            {
                if (t.TransformDirection(n[a]).y < 0.9f) continue;
                for (int b = 0; b < v.Length; b++)
                {
                    if (b == a || t.TransformDirection(n[b]).y < 0.9f) continue;
                    Vector2 d = uv[b] - uv[a];
                    Vector3 w = t.TransformPoint(v[b]) - t.TransformPoint(v[a]);
                    if (Mathf.Abs(d.x) > 0.01f && Mathf.Abs(d.y) < 0.01f) perU = w / d.x;
                    if (Mathf.Abs(d.y) > 0.01f && Mathf.Abs(d.x) < 0.01f) perV = w / d.y;
                }
                if (perU == Vector3.zero || perV == Vector3.zero) return false;
                origin = t.TransformPoint(v[a]);
                u0 = uv[a].x;
                return true;
            }
            return false;
        }

        private static void Push()
        {
            if (Active.Count > Max)
                Debug.LogError($"데크 구멍이 {Active.Count}개라 셰이더 한도 {Max}개를 넘는다. 넘친 자리는 데크가 안 뚫린다.");

            int n = Mathf.Min(Active.Count, Max);
            for (int i = 0; i < n; i++)
            {
                DeckHole h = Active[i];
                Transform t = h.transform;
                // 판자 결이 월드 X나 Z라 돌린 사각형을 감싸는 월드 축 상자가 곧 판자 한 장이다.
                Vector3 ax = t.right * (h._width * 0.5f), az = t.forward * (h.length * 0.5f);
                Centers[i] = t.position;
                Sizes[i] = new Vector4(Mathf.Abs(ax.x) + Mathf.Abs(az.x), Mathf.Abs(ax.z) + Mathf.Abs(az.z), h.heightTolerance, 0f);
            }
            Shader.SetGlobalVectorArray(CenterId, Centers);
            Shader.SetGlobalVectorArray(SizeId, Sizes);
            Shader.SetGlobalFloat(CountId, n);
        }
    }
}
