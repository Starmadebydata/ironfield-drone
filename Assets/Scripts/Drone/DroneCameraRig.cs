using UnityEngine;

namespace Ironfield.Drone
{
    /// <summary>
    /// Lightweight chase camera: sits behind and above the drone, smooths its
    /// position, looks slightly ahead of travel, adds a boost FOV kick and a
    /// short shake on demand. A plain Camera follower (no Cinemachine) so its
    /// behaviour is fully deterministic for a headless build.
    /// </summary>
    [RequireComponent(typeof(Camera))]
    public class DroneCameraRig : MonoBehaviour
    {
        public Transform target;                 // the drone
        public Vector3 localOffset = new Vector3(0f, 2.2f, -6.5f);
        public float positionLerp = 8f;
        public float rotationLerp = 10f;
        public float lookAhead = 6f;

        [Header("FOV")]
        public float baseFov = 62f;
        public float boostFov = 74f;
        public float fovLerp = 6f;

        Camera _cam;
        DroneController _drone;
        Rigidbody _targetRb;
        float _shake;
        float _shakeMag;

        void Awake()
        {
            _cam = GetComponent<Camera>();
            _cam.fieldOfView = baseFov;
        }

        public void Bind(Transform droneTransform)
        {
            target = droneTransform;
            _drone = droneTransform ? droneTransform.GetComponent<DroneController>() : null;
            _targetRb = droneTransform ? droneTransform.GetComponent<Rigidbody>() : null;
            if (target) SnapToTarget();
        }

        void OnEnable()
        {
            if (target && _drone == null) Bind(target);
        }

        void LateUpdate()
        {
            if (!target) return;
            float dt = Time.deltaTime;

            Vector3 desiredPos = target.TransformPoint(localOffset);
            transform.position = Vector3.Lerp(transform.position, desiredPos,
                1f - Mathf.Exp(-positionLerp * dt));

            Vector3 vel = _targetRb ? _targetRb.linearVelocity : Vector3.zero;
            Vector3 lookAt = target.position + target.forward * 2f + vel.normalized * lookAhead;
            Quaternion desiredRot = Quaternion.LookRotation(lookAt - transform.position, Vector3.up);
            transform.rotation = Quaternion.Slerp(transform.rotation, desiredRot,
                1f - Mathf.Exp(-rotationLerp * dt));

            if (_shake > 0f)
            {
                _shake -= dt;
                transform.position += Random.insideUnitSphere * (_shakeMag * Mathf.Clamp01(_shake));
            }

            float wantFov = (_drone != null && _drone.Boosting) ? boostFov : baseFov;
            _cam.fieldOfView = Mathf.Lerp(_cam.fieldOfView, wantFov,
                1f - Mathf.Exp(-fovLerp * dt));
        }

        public void Shake(float duration, float magnitude)
        {
            _shake = Mathf.Max(_shake, duration);
            _shakeMag = Mathf.Max(_shakeMag, magnitude);
        }

        void SnapToTarget()
        {
            transform.position = target.TransformPoint(localOffset);
            transform.rotation = Quaternion.LookRotation(target.forward, Vector3.up);
        }
    }
}
