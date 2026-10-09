using Marea.Core;
using Marea.Data;
using UnityEngine;
using UnityEngine.AI;

namespace Marea.Field
{
    /// <summary>
    /// (+10/9) 넘어질 때 들고 있던 그릇이 앞으로 포물선을 그리며 빙글 날아가 바닥에 떨어진 뒤 사라진다.
    /// 플레이어(PlayerTrip) · 서빙 직원(ServingStaff.Trip)이 같이 쓴다.
    ///
    /// 물리를 안 쓴다 — 데크 구멍은 셰이더라 바닥 콜라이더는 그대로고, Rigidbody면 어디로 굴러갈지 못 정한다.
    /// 착지점은 던진 사람 발밑에서 NavMesh를 따라 앞으로 — 데크 끝 · 난간에서 멈춰 물 위 허공에 안 떨어진다.
    /// </summary>
    public class TossedDish : MonoBehaviour
    {
        // 몸이 넘어지며 0.8초 동안 앞으로 5m쯤 쏠린다(Tripping 루트 모션) — 그릇은 같은 시간에 그보다 조금 앞에 떨어진다.
        private const float Distance = 6.5f;
        private const float Height = 1.2f;
        private const float Duration = 0.8f;
        private const float LingerSeconds = 0.6f;
        private const float SpinDegrees = 540f;

        private Vector3 _from, _to, _spinAxis;
        private Quaternion _startRotation;
        private float _t;

        /// <summary>손에서 떼어 던진다. dish는 이제 이 컴포넌트가 지운다.</summary>
        public static void Launch(GameObject dish, Vector3 forward, Vector3 feet)
        {
            if (dish == null) return;
            forward.y = 0f;
            if (forward.sqrMagnitude < 0.0001f) forward = Vector3.forward;
            forward.Normalize();

            dish.transform.SetParent(null, true);
            TossedDish toss = dish.AddComponent<TossedDish>();
            toss._from = dish.transform.position;
            Vector3 land = feet + forward * Distance;
            if (NavMesh.Raycast(feet, land, out NavMeshHit hit, NavMesh.AllAreas)) land = hit.position;   // 데크 끝에서 멈춘다
            toss._to = land + Vector3.up * 0.05f;
            toss._spinAxis = Vector3.Cross(Vector3.up, forward);
            toss._startRotation = dish.transform.rotation;
        }

        private void Update()
        {
            if (_t >= 1f) return;
            _t = Mathf.Min(1f, _t + Time.deltaTime / Duration);

            Vector3 p = Vector3.Lerp(_from, _to, _t);
            p.y += 4f * Height * _t * (1f - _t);
            transform.position = p;
            transform.rotation = Quaternion.AngleAxis(SpinDegrees * _t, _spinAxis) * _startRotation;

            if (_t < 1f) return;
            Vfx.Play(VfxId.Dust, _to, 0.5f);
            Destroy(gameObject, LingerSeconds);
        }
    }
}
