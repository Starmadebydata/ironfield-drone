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
}
