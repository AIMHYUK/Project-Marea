using UnityEngine;

namespace Marea.Cooking
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(AudioSource))]
    public class CookingStageLoopAudio : MonoBehaviour
    {
        [SerializeField] private BaseCookingMinigame station;
        [SerializeField] private MinigameStepIndex cookingStep = MinigameStepIndex.Step2;
        [SerializeField] private SeafoodSkewersGrill skewerGrill;
        [SerializeField] private PanStirCookingController panCooking;
        private AudioSource _source;

        private void Awake()
        {
            _source = GetComponent<AudioSource>();
            _source.playOnAwake = false;
            _source.loop = true;
            if (station == null) station = GetComponentInParent<BaseCookingMinigame>();
        }

        private void LateUpdate()
        {
            bool cooking = station != null && station.isActiveAndEnabled &&
                station.CurrentStepIndex == cookingStep;
            if (skewerGrill != null)
                cooking &= skewerGrill.isActiveAndEnabled && !skewerGrill.IsCompleted;
            if (panCooking != null)
                cooking &= panCooking.isActiveAndEnabled && panCooking.IsVeggieInPan &&
                    !panCooking.IsCookCompleted && !panCooking.IsBurned;
            if (cooking)
            {
                if (!_source.isPlaying && _source.clip != null) _source.Play();
            }
            else if (_source.isPlaying) _source.Stop();
        }

        private void OnDisable()
        {
            if (_source != null) _source.Stop();
        }
    }
}
