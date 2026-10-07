using UnityEngine;

namespace Marea.Cooking
{
    /// <summary>Emits cooking smoke only while the assigned station is cooking.</summary>
    [DisallowMultipleComponent]
    public class CookingSmokeEffect : MonoBehaviour
    {
        [SerializeField] private BaseCookingMinigame station;
        [SerializeField] private MinigameStepIndex cookingStep = MinigameStepIndex.Step2;
        [SerializeField] private ParticleSystem smoke;
        [SerializeField] private PanStirCookingController panCooking;
        [SerializeField] private SeafoodSkewersGrill skewerCooking;
        [SerializeField, Min(0f)] private float emissionRate = 10f;
        [SerializeField, Min(0.01f)] private float fadeDuration = 0.45f;

        private float _intensity;
        private bool _wasCooking;

        private void Awake()
        {
            if (station == null) station = GetComponentInParent<BaseCookingMinigame>();
            if (smoke == null) smoke = GetComponentInChildren<ParticleSystem>(true);
            ClearSmoke();
        }

        private void LateUpdate()
        {
            if (smoke == null) return;
            bool cooking = station != null && station.isActiveAndEnabled
                && station.CurrentStepIndex == cookingStep;
            if (panCooking != null)
                cooking &= panCooking.isActiveAndEnabled && panCooking.IsVeggieInPan
                    && !panCooking.IsCookCompleted && !panCooking.IsBurned;
            if (skewerCooking != null)
                cooking &= skewerCooking.isActiveAndEnabled && !skewerCooking.IsCompleted;

            if (cooking && !_wasCooking)
            {
                // A new run must not inherit particles from the previous dish.
                ClearSmoke();
                smoke.Play(true);
            }
            _wasCooking = cooking;
            _intensity = Mathf.MoveTowards(_intensity, cooking ? 1f : 0f,
                Time.deltaTime / Mathf.Max(0.01f, fadeDuration));

            // Slight density variation keeps a continuous plume from looking mechanical.
            float variation = Mathf.Lerp(0.85f, 1.15f,
                Mathf.PerlinNoise(Time.time * 0.65f, transform.position.x));
            var emission = smoke.emission;
            emission.rateOverTime = emissionRate * _intensity * variation;
            if (!cooking && _intensity <= 0f && smoke.isEmitting)
                smoke.Stop(true, ParticleSystemStopBehavior.StopEmitting);
        }

        private void OnDisable() => ClearSmoke();

        private void ClearSmoke()
        {
            _intensity = 0f;
            _wasCooking = false;
            if (smoke == null) return;
            smoke.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var emission = smoke.emission;
            emission.rateOverTime = 0f;
        }
    }
}
