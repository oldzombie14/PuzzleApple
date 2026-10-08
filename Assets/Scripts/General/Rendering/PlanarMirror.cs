using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace PuzzleApple
{
    [DefaultExecutionOrder(1000), RequireComponent(typeof(Renderer))]
    public sealed class PlanarMirror : MonoBehaviour
    {
        [SerializeField] Camera sourceCamera;
        [SerializeField, Range(256, 2048)] int resolution = 1024;
        [SerializeField, Range(1, 3)] int maxReflectionDepth = 2;
        [SerializeField] Vector3 localSurfacePoint = new Vector3(0, 0, .5f);
        [SerializeField] Vector3 localSurfaceNormal = Vector3.forward;
        Renderer surface;
        readonly List<Pass> passes = new List<Pass>();
        readonly Plane[] frustum = new Plane[6];
        sealed class Pass
        {
            public Camera camera;
            public RenderTexture texture;
            public readonly UniversalRenderPipeline.SingleCameraRequest request = new UniversalRenderPipeline.SingleCameraRequest();
        }
        struct SavedSurface
        {
            public Renderer renderer;
            public MaterialPropertyBlock properties;
        }
        void OnEnable()
        {
            surface = GetComponent<Renderer>();
            if (!sourceCamera) sourceCamera = Camera.main;
        }
        void LateUpdate()
        {
            if (sourceCamera && sourceCamera.isActiveAndEnabled) RenderForCamera(sourceCamera);
        }
        public void RenderForCamera(Camera camera)
        {
            if (!camera || !isActiveAndEnabled) return;
            var mirrors = FindObjectsByType<PlanarMirror>(FindObjectsSortMode.None);
            var texture = RenderView(camera, camera.projectionMatrix, 0, Mathf.Clamp(maxReflectionDepth, 1, 3), mirrors);
            if (texture) Bind(texture);
        }
        bool VisibleFrom(Camera camera)
        {
            if (!surface) surface = GetComponent<Renderer>();
            if (!surface.enabled || !surface.gameObject.activeInHierarchy) return false;
            var point = transform.TransformPoint(localSurfacePoint);
            var normal = transform.TransformDirection(localSurfaceNormal).normalized;
            if (Vector3.Dot(normal, camera.transform.position - point) <= .01f) return false;
            GeometryUtility.CalculateFrustumPlanes(camera, frustum);
            return GeometryUtility.TestPlanesAABB(frustum, surface.bounds);
        }
        RenderTexture RenderView(Camera source, Matrix4x4 baseProjection, int depth, int limit, PlanarMirror[] mirrors)
        {
            if (!VisibleFrom(source)) return null;
            var pass = EnsurePass(source, depth);
            var camera = pass.camera;
            camera.CopyFrom(source);
            camera.enabled = false;
            camera.targetTexture = pass.texture;
            camera.cameraType = CameraType.Reflection;
            camera.useOcclusionCulling = false;
            var data = camera.GetUniversalAdditionalCameraData();
            data.renderPostProcessing = false;
            data.renderShadows = true;
            data.requiresColorOption = CameraOverrideOption.Off;
            data.requiresDepthOption = CameraOverrideOption.Off;
            var point = transform.TransformPoint(localSurfacePoint);
            var normal = transform.TransformDirection(localSurfaceNormal).normalized;
            var reflection = ReflectionMatrix(point, normal);
            camera.transform.SetPositionAndRotation(reflection.MultiplyPoint(source.transform.position),
                Quaternion.LookRotation(reflection.MultiplyVector(source.transform.forward), reflection.MultiplyVector(source.transform.up)));
            camera.worldToCameraMatrix = source.worldToCameraMatrix * reflection;
            // Start from the root projection, never accumulate another mirror's oblique near plane.
            camera.projectionMatrix = baseProjection;
            var clipPoint = camera.worldToCameraMatrix.MultiplyPoint(point + normal * .01f);
            var clipNormal = camera.worldToCameraMatrix.MultiplyVector(normal).normalized;
            camera.projectionMatrix = camera.CalculateObliqueMatrix(new Vector4(clipNormal.x, clipNormal.y, clipNormal.z, -Vector3.Dot(clipPoint, clipNormal)));

            var saved = new List<SavedSurface>();
            bool oldCulling = GL.invertCulling;
            bool wasHidden = surface.forceRenderingOff;
            try
            {
                foreach (var other in mirrors)
                {
                    if (!other || other == this || !other.isActiveAndEnabled) continue;
                    if (!other.surface) other.surface = other.GetComponent<Renderer>();
                    var previous = new MaterialPropertyBlock();
                    other.surface.GetPropertyBlock(previous);
                    saved.Add(new SavedSurface { renderer = other.surface, properties = previous });
                    // At the depth limit, neutral glass replaces stale or recursively sampled textures.
                    var nested = depth + 1 < limit ? other.RenderView(camera, baseProjection, depth + 1, limit, mirrors) : null;
                    other.Bind(nested);
                }
                surface.forceRenderingOff = true;
                // The handedness alternates on every bounce, including even-depth reflections.
                GL.invertCulling = camera.worldToCameraMatrix.determinant > 0;
                pass.request.destination = pass.texture;
                RenderPipeline.SubmitRenderRequest(camera, pass.request);
            }
            finally
            {
                GL.invertCulling = oldCulling;
                surface.forceRenderingOff = wasHidden;
                foreach (var entry in saved) if (entry.renderer) entry.renderer.SetPropertyBlock(entry.properties);
            }
            return pass.texture;
        }
        void Bind(Texture texture)
        {
            if (!surface) surface = GetComponent<Renderer>();
            var properties = new MaterialPropertyBlock();
            surface.GetPropertyBlock(properties);
            properties.SetTexture("_ReflectionTex", texture ? texture : Texture2D.blackTexture);
            properties.SetFloat("_ReflectionReady", texture ? 1 : 0);
            surface.SetPropertyBlock(properties);
        }
        public static Matrix4x4 ReflectionMatrix(Vector3 point, Vector3 normal)
        {
            normal.Normalize();float d = -Vector3.Dot(normal, point);
            var matrix = Matrix4x4.identity;
            for (int row = 0; row < 3; row++)
            {
                for (int col = 0; col < 3; col++) matrix[row, col] -= 2 * normal[row] * normal[col];
                matrix[row, 3] = -2 * d * normal[row];
            }
            return matrix;
        }
        Pass EnsurePass(Camera source, int depth)
        {
            while (passes.Count <= depth) passes.Add(new Pass());
            var pass = passes[depth];
            if (!pass.camera)
            {
                var go = new GameObject("Mirror reflection camera " + depth) { hideFlags = HideFlags.HideAndDontSave };
                pass.camera = go.AddComponent<Camera>();pass.camera.enabled = false;
                go.AddComponent<UniversalAdditionalCameraData>();
            }
            int width = Mathf.Max(128, Mathf.Min(resolution, Mathf.Max(256, source.pixelWidth)) >> depth);
            int height = Mathf.Max(128, Mathf.RoundToInt(width / source.aspect));
            if (!pass.texture || pass.texture.width != width || pass.texture.height != height)
            {
                if (pass.texture) { pass.texture.Release(); Dispose(pass.texture); }
                pass.texture = new RenderTexture(width, height, 24, RenderTextureFormat.DefaultHDR)
                {
                    name = "Planar reflection depth " + depth, hideFlags = HideFlags.HideAndDontSave,
                    filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp, antiAliasing = 2
                };
                pass.texture.Create();
            }
            return pass;
        }
        static void Dispose(Object value)
        {
            if (Application.isPlaying) Destroy(value); else DestroyImmediate(value);
        }
        public void ReleaseResources()
        {
            if (surface) Bind(null);
            foreach (var pass in passes)
            {
                if (pass.camera) Dispose(pass.camera.gameObject);
                if (pass.texture) { pass.texture.Release(); Dispose(pass.texture); }
            }
            passes.Clear();
        }
        void OnDisable() => ReleaseResources();
    }
}
