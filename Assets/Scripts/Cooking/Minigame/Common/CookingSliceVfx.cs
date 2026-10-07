using UnityEngine;

namespace Marea.Cooking
{
    [RequireComponent(typeof(ParticleSystem))]
    public class CookingSliceVfx : MonoBehaviour
    {
        private void OnEnable() => Clear();
        private void OnDisable() => Clear();

        private void Clear()
        {
            GetComponent<ParticleSystem>().Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        }

        /// <summary>Shows a brief blade streak along the cut, facing the cooking camera.</summary>
        public static void Play(ParticleSystem effect, Vector3 cutPoint, Vector3 cutNormal)
        {
            if (effect == null) return;
            effect.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            Camera camera = Camera.main;
            Vector3 facing = camera != null ? -camera.transform.forward : Vector3.up;
            Vector3 stroke = Vector3.Cross(cutNormal, Vector3.up);
            stroke = Vector3.ProjectOnPlane(stroke, facing);
            if (stroke.sqrMagnitude < 0.001f)
                stroke = camera != null ? camera.transform.up : Vector3.forward;
            effect.transform.SetPositionAndRotation(
                cutPoint + facing * 0.035f + Vector3.up * 0.025f,
                Quaternion.LookRotation(facing, stroke.normalized));
            // The cooking variant only uses the blade streak, without combat impact bursts.
            effect.Play(false);
        }
    }
}
