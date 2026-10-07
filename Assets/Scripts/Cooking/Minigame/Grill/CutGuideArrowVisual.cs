using UnityEngine;

namespace Marea.Cooking
{
    public class CutGuideArrowVisual : MonoBehaviour
    {
        [SerializeField] private Transform movingCue;
        [SerializeField] private Vector3[] localPath;
        [SerializeField, Min(0.5f)] private float cycleDuration = 1.5f;
        private float _startedAt;
        private Vector3 _cueScale;
        private bool _hasCueScale;

        private void OnEnable()
        {
            _startedAt = Time.unscaledTime;
            if (movingCue != null && !_hasCueScale)
            {
                _cueScale = movingCue.localScale;
                _hasCueScale = true;
            }
        }

        private void Update()
        {
            if (movingCue == null || localPath == null || localPath.Length < 2) return;
            float phase = Mathf.Repeat((Time.unscaledTime - _startedAt) / cycleDuration, 1f);
            float index = phase * (localPath.Length - 1);
            int segment = Mathf.Min(Mathf.FloorToInt(index), localPath.Length - 2);
            movingCue.localPosition = Vector3.Lerp(localPath[segment], localPath[segment + 1], index - segment);
            movingCue.localScale = _cueScale * Mathf.Sin(phase * Mathf.PI);
        }
    }
}
