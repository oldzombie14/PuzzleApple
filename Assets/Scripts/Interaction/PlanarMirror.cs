using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace PuzzleApple
{
    // One reflected camera, rendered before the main view; no player mesh is required.
    [DefaultExecutionOrder(1000), RequireComponent(typeof(Renderer))]
    public sealed class PlanarMirror : MonoBehaviour
    {
        [SerializeField] Camera sourceCamera;
        [SerializeField, Range(256, 2048)] int resolution = 1024;
        [SerializeField] Vector3 localSurfacePoint = new Vector3(0, 0, .5f);
        [SerializeField] Vector3 localSurfaceNormal = Vector3.forward;
        Renderer surface;
        Camera reflectionCamera;
        RenderTexture reflection;
        MaterialPropertyBlock properties;
        readonly UniversalRenderPipeline.SingleCameraRequest request = new UniversalRenderPipeline.SingleCameraRequest();
        readonly Plane[] frustum = new Plane[6];

        void OnEnable()
        {
            surface = GetComponent<Renderer>();
            properties = new MaterialPropertyBlock();
            if (!sourceCamera) sourceCamera = Camera.main;
        }
        void LateUpdate()
        {
            if (!sourceCamera || !sourceCamera.isActiveAndEnabled) return;
            Vector3 point = transform.TransformPoint(localSurfacePoint);
            Vector3 normal = transform.TransformDirection(localSurfaceNormal).normalized;
            if (Vector3.Dot(normal, sourceCamera.transform.position - point) <= .01f) return;
            GeometryUtility.CalculateFrustumPlanes(sourceCamera, frustum);
            if (!GeometryUtility.TestPlanesAABB(frustum, surface.bounds)) return;
            EnsureResources();
            reflectionCamera.CopyFrom(sourceCamera);
            reflectionCamera.enabled = false;
            reflectionCamera.targetTexture = reflection;
            reflectionCamera.cameraType = CameraType.Reflection;
            reflectionCamera.useOcclusionCulling = false;
            var data = reflectionCamera.GetUniversalAdditionalCameraData();
            data.renderPostProcessing = false;
            data.renderShadows = true;
            data.requiresColorOption = CameraOverrideOption.Off;
            data.requiresDepthOption = CameraOverrideOption.Off;

            float d = -Vector3.Dot(normal, point);
            Matrix4x4 mirror = Matrix4x4.identity;
            for (int row = 0; row < 3; row++)
            {
                for (int col = 0; col < 3; col++) mirror[row, col] -= 2 * normal[row] * normal[col];
                mirror[row, 3] = -2 * d * normal[row];
            }
            reflectionCamera.transform.SetPositionAndRotation(mirror.MultiplyPoint(sourceCamera.transform.position),
                Quaternion.LookRotation(mirror.MultiplyVector(sourceCamera.transform.forward), mirror.MultiplyVector(sourceCamera.transform.up)));
            reflectionCamera.worldToCameraMatrix = sourceCamera.worldToCameraMatrix * mirror;
            Vector3 clipPoint = reflectionCamera.worldToCameraMatrix.MultiplyPoint(point + normal * .01f);
            Vector3 clipNormal = reflectionCamera.worldToCameraMatrix.MultiplyVector(normal).normalized;
            reflectionCamera.projectionMatrix = sourceCamera.CalculateObliqueMatrix(new Vector4(
                clipNormal.x, clipNormal.y, clipNormal.z, -Vector3.Dot(clipPoint, clipNormal)));
            bool oldCulling = GL.invertCulling;
            bool wasHidden = surface.forceRenderingOff;
            try
            {
                surface.forceRenderingOff = true;
                GL.invertCulling = !oldCulling;
                request.destination = reflection;
                RenderPipeline.SubmitRenderRequest(reflectionCamera, request);
            }
            finally { GL.invertCulling = oldCulling; surface.forceRenderingOff = wasHidden; }
            surface.GetPropertyBlock(properties);
            properties.SetTexture("_ReflectionTex", reflection);
            properties.SetFloat("_ReflectionReady", 1);
            surface.SetPropertyBlock(properties);
        }
        void EnsureResources()
        {
            if (!reflectionCamera)
            {
                var go = new GameObject("Mirror reflection camera") { hideFlags = HideFlags.HideAndDontSave };
                reflectionCamera = go.AddComponent<Camera>();
                reflectionCamera.enabled = false;
                go.AddComponent<UniversalAdditionalCameraData>();
            }
            int width = Mathf.Min(resolution, Mathf.Max(256, sourceCamera.pixelWidth));
            int height = Mathf.Max(128, Mathf.RoundToInt(width / sourceCamera.aspect));
            if (reflection && reflection.width == width && reflection.height == height) return;
            if (reflection) { reflection.Release(); Destroy(reflection); }
            reflection = new RenderTexture(width, height, 24, RenderTextureFormat.DefaultHDR)
                { name = "Planar mirror reflection", hideFlags = HideFlags.HideAndDontSave, filterMode = FilterMode.Bilinear };
            reflection.Create();
        }
        void OnDisable()
        {
            if (surface && properties != null)
            {
                surface.GetPropertyBlock(properties);
                properties.SetFloat("_ReflectionReady", 0);
                // MaterialPropertyBlock.SetTexture rejects null, including during Play Mode teardown.
                properties.SetTexture("_ReflectionTex", Texture2D.blackTexture);
                surface.SetPropertyBlock(properties);
            }
            if (reflectionCamera) Destroy(reflectionCamera.gameObject);
            if (reflection) { reflection.Release(); Destroy(reflection); }
        }
    }
}
