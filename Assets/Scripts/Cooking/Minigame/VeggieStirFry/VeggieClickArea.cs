using UnityEngine;

namespace Marea.Cooking
{
    internal static class VeggieClickArea
    {
        // 화면에 보이는 모델 전체를 선택한다. 도마 등 주변 콜라이더에 막히지 않는다.
        public static bool Contains(Camera camera, GameObject target, Vector2 position,
            float padding, out float score)
        {
            score = float.PositiveInfinity;
            if (camera == null || target == null || !target.activeInHierarchy) return false;

            Vector2 min = new Vector2(float.PositiveInfinity, float.PositiveInfinity);
            Vector2 max = new Vector2(float.NegativeInfinity, float.NegativeInfinity);
            bool hasBounds = false;
            foreach (Renderer renderer in target.GetComponentsInChildren<Renderer>())
            {
                if (!renderer.enabled ||
                    !(renderer is MeshRenderer || renderer is SkinnedMeshRenderer)) continue;
                Bounds bounds = renderer.localBounds;
                for (int i = 0; i < 8; i++)
                {
                    Vector3 corner = bounds.center + Vector3.Scale(bounds.extents,
                        new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1,
                            (i & 4) == 0 ? -1 : 1));
                    Vector3 screen = camera.WorldToScreenPoint(renderer.transform.TransformPoint(corner));
                    if (screen.z <= camera.nearClipPlane) continue;
                    min = Vector2.Min(min, screen);
                    max = Vector2.Max(max, screen);
                    hasBounds = true;
                }
            }
            if (!hasBounds) return false;

            float margin = padding * camera.pixelHeight / 1080f;
            Rect area = Rect.MinMaxRect(min.x - margin, min.y - margin,
                max.x + margin, max.y + margin);
            if (!area.Contains(position)) return false;

            // 겹치는 클릭 영역은 클릭 지점에 더 가까운 모델을 선택한다.
            score = (position - area.center).sqrMagnitude;
            return true;
        }
    }
}
