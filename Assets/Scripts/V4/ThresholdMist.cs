using UnityEngine;

namespace PuzzleApple.V4
{
    public sealed class ThresholdMist : MonoBehaviour
    {
        [Range(0, .3f)] public float opacity = .035f;
        [Min(.05f)] public float flowSpeed = 1.1f;
        [Min(.1f)] public float fadeDuration = 2.5f;
        [Min(.5f)] public float radius = 4.3f;
        public Vector3 shapeScale = new Vector3(.72f, 1f, .38f);
        public Material mistMaterial;
        ThresholdDoor door;
        DoorStateEffects state;
        GameObject volume;
        MeshRenderer volumeRenderer;
        MaterialPropertyBlock properties;
        float elapsed;
        float visibility;

        void Awake()
        {
            door = GetComponentInParent<ThresholdDoor>();
            state = GetComponentInParent<DoorStateEffects>();
            properties = new MaterialPropertyBlock();
            // One volume proxy, not camera-facing particles or layered planes.
            volume = GameObject.CreatePrimitive(PrimitiveType.Cube);
            volume.name = "Ellipsoid mist volume";
            volume.transform.SetParent(transform, false);
            Destroy(volume.GetComponent<Collider>());
            volumeRenderer = volume.GetComponent<MeshRenderer>();
            volumeRenderer.sharedMaterial = mistMaterial;
            volumeRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            volumeRenderer.receiveShadows = false;
            volumeRenderer.enabled = false;
        }

        void LateUpdate()
        {
            if (!door || !mistMaterial) return;
            bool visible = state && state.PassageEnabled;
            visibility = Mathf.MoveTowards(visibility, visible ? 1 : 0, Time.deltaTime / Mathf.Max(.1f, fadeDuration));
            volumeRenderer.enabled = visibility > 0;
            if (!volumeRenderer.enabled) return;
            elapsed += Time.deltaTime;
            float reveal = Mathf.SmoothStep(0, 1, visibility);
            volume.transform.localPosition = new Vector3(0, 3, 0);
            var axes = new Vector3(Mathf.Max(.1f, shapeScale.x), Mathf.Max(.1f, shapeScale.y), Mathf.Max(.1f, shapeScale.z));
            volume.transform.localScale = axes * (radius * 2);
            properties.SetMatrix("_MistWorldToLocal", transform.worldToLocalMatrix);
            properties.SetFloat("_MistTime", elapsed * flowSpeed);
            properties.SetFloat("_MistRadius", radius);
            properties.SetVector("_MistAxes", axes);
            properties.SetFloat("_MistDensity", opacity * reveal);
            volumeRenderer.SetPropertyBlock(properties);
            volumeRenderer.enabled = true;
        }

        void OnDisable()
        {
            if (volumeRenderer) volumeRenderer.enabled = false;
            elapsed = 0;
            visibility = 0;
        }

        void OnDestroy() { if (volume) Destroy(volume); }
    }
}
