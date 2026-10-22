using UnityEngine;

namespace PuzzleApple.V4
{
    public sealed class ThresholdFish : MonoBehaviour
    {
        public Transform tail;
        public Vector3 center;
        public Vector3 radii = new Vector3(1.3f, .6f, .65f);
        public float phase, orbitSpeed = .25f, tailSpeed = 5;
        Vector3 Point(float t) => center + new Vector3(Mathf.Sin(t)*radii.x,
            Mathf.Sin(t*2+phase)*radii.y, Mathf.Cos(t)*radii.z);
        void Update()
        {
            float t = Time.time * orbitSpeed + phase;
            transform.localPosition = Point(t);
            var forward = (Point(t+.015f)-Point(t)).normalized;
            transform.localRotation = Quaternion.LookRotation(forward,Vector3.up)
                * Quaternion.Euler(0,0,Mathf.Sin(t+phase)*8);
            if (tail) tail.localRotation = Quaternion.Euler(0,Mathf.Sin(Time.time*tailSpeed+phase)*23,0);
        }
    }
}
