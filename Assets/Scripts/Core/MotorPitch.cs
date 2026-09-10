using Ironfield.Drone;
using UnityEngine;

namespace Ironfield.Fx
{
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
