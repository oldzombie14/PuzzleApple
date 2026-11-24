using UnityEngine;

namespace PuzzleApple.V4
{
    public sealed class FloatingFlowers : MonoBehaviour
    {
        public Transform[] flowers;
        [Range(0, .3f)] public float amplitude = .09f;
        public float period = 6, swayDegrees = 2;
        Vector3[] positions;
        Quaternion[] rotations;
        void Awake()
        {
            positions = new Vector3[flowers.Length]; rotations = new Quaternion[flowers.Length];
            for (int i = 0; i < flowers.Length; i++) { positions[i] = flowers[i].localPosition; rotations[i] = flowers[i].localRotation; }
        }
        void Update()
        {
            for (int i = 0; i < flowers.Length; i++)
            {
                if (!flowers[i]) continue;
                float t = Time.time * (2 * Mathf.PI / Mathf.Max(1, period)) + i * 2.39996f;
                flowers[i].localPosition = positions[i] + new Vector3(Mathf.Sin(t * .71f) * amplitude * .3f, Mathf.Sin(t) * amplitude, Mathf.Cos(t * .83f) * amplitude * .3f);
                flowers[i].localRotation = rotations[i] * Quaternion.Euler(Mathf.Sin(t * .7f) * swayDegrees, 0, Mathf.Cos(t * .9f) * swayDegrees);
            }
        }
    }
}
