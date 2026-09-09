using Ironfield.Drone;
using UnityEngine;

namespace Ironfield.Fx
{
    /// <summary>Fades a point light out fast, then removes itself; explosion flash.</summary>
    [RequireComponent(typeof(Light))]
    public class FlashFade : MonoBehaviour
    {
        public float speed = 5f;
        Light _l;
        float _i0;

        void Start()
        {
            _l = GetComponent<Light>();
            _i0 = Mathf.Max(0.01f, _l.intensity);
        }

        void Update()
        {
            _l.intensity = Mathf.MoveTowards(_l.intensity, 0f, _i0 * Time.deltaTime * speed);
            if (_l.intensity <= 0.01f) Destroy(gameObject);
        }
    }

    /// <summary>Drives a motor AudioSource's pitch/volume from the drone's speed.</summary>
    [RequireComponent(typeof(AudioSource))]
    public class MotorPitch : MonoBehaviour
    {
        public float speedForMaxPitch = 45f;
        AudioSource _a;
        DroneController _d;

        void Start()
        {
            _a = GetComponent<AudioSource>();
            _d = GetComponentInParent<DroneController>();
        }

        void Update()
        {
            if (_d == null || _a == null) return;
            float s = Mathf.Clamp01(_d.Speed / speedForMaxPitch);
            _a.pitch = Mathf.Lerp(0.8f, 2.1f, s) + (_d.Boosting ? 0.3f : 0f);
            _a.volume = Mathf.Lerp(0.3f, 0.7f, s);
        }
    }
}
