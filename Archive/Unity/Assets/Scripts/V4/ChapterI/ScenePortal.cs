using System.Collections;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace PuzzleApple.V4
{
    // The transform's +Z points into its own room; both apertures have matching dimensions.
    [DefaultExecutionOrder(900)]
    public sealed class ScenePortal : MonoBehaviour
    {
        public string destinationScene;
        public string linkId = "garden";
        public Vector2 aperture = new Vector2(3, 3.4f);
        public Renderer surface;
        public Collider loadingBarrier;
        public int resolution = 1024;
        public ScenePortal Destination { get; private set; }
        public Camera ViewCamera => viewCamera;
        public bool Ready => Destination && Destination.isActiveAndEnabled;
        SessionSceneManager session;
        Camera viewCamera;
        RenderTexture texture;
        MaterialPropertyBlock properties;
        Vector3 previousEye;
        bool tracking;
        Bounds apertureBounds;
        Material apertureMaterial;
        readonly Plane[] frustum = new Plane[6];
        readonly UniversalRenderPipeline.SingleCameraRequest request = new UniversalRenderPipeline.SingleCameraRequest();
        static readonly Quaternion HalfTurn = Quaternion.Euler(0, 180, 0);
        IEnumerator Start()
        {
            if (surface)
            {
                apertureBounds = surface.localBounds;
                apertureMaterial = surface.material;
                apertureMaterial.SetFloat("_DepthClip", 0);
            }
            session = FindFirstObjectByType<SessionSceneManager>();
            while (!session || !session.Current) yield return null;
            yield return session.PreparePreview(destinationScene);
            var entry = session.LoadedEntry(destinationScene);
            if (entry)
                foreach (var portal in entry.content.GetComponentsInChildren<ScenePortal>(true))
                    if (portal.linkId == linkId) { Destination = portal; break; }
            if (!Destination) Debug.LogError("Portal counterpart missing: " + destinationScene, this);
            if (loadingBarrier) loadingBarrier.enabled = !Ready;
        }
        public Vector3 MapPoint(Vector3 point) => Destination.transform.TransformPoint(HalfTurn * transform.InverseTransformPoint(point));
        public Quaternion MapRotation(Quaternion rotation) => Destination.transform.rotation * HalfTurn * Quaternion.Inverse(transform.rotation) * rotation;
        void LateUpdate()
        {
            if (!session || !session.Current || session.Current.gameObject.scene != gameObject.scene || !Ready)
            { tracking = false; return; }
            if (loadingBarrier) loadingBarrier.enabled = false;
            var player = session.player;
            var eye = transform.InverseTransformPoint(player.view.position);
            if (tracking && previousEye.z > 0 && eye.z <= 0)
            {
                float t = previousEye.z / (previousEye.z - eye.z);
                var crossing = Vector3.Lerp(previousEye, eye, t);
                if (Mathf.Abs(crossing.x) < aperture.x * .5f - .15f && Mathf.Abs(crossing.y) < aperture.y * .5f)
                {
                    var entry = session.LoadedEntry(destinationScene);
                    var position = MapPoint(player.transform.position);
                    // Keep the capsule just inside the destination, avoiding a reverse crossing on the next frame.
                    float distance = Vector3.Dot(position - Destination.transform.position, Destination.transform.forward);
                    if (distance < .08f) position += Destination.transform.forward * (.08f - distance);
                    if (session.TraversePortal(entry, position, MapRotation(player.view.rotation)))
                    {
                        Destination.tracking = false; tracking = false; return;
                    }
                }
            }
            previousEye = eye; tracking = true;
            RenderView(player.view.GetComponent<Camera>());
        }
        public void RenderView(Camera source)
        {
            if (!source || !Ready || !surface || !surface.enabled) return;
            if (Vector3.Dot(transform.forward, source.transform.position - transform.position) <= 0) return;
            // Match the shader's near-plane clamp during CPU frustum culling.
            var padding = surface.transform.InverseTransformVector(source.transform.forward * source.nearClipPlane);
            var bounds = apertureBounds;
            bounds.Encapsulate(apertureBounds.min + padding);
            bounds.Encapsulate(apertureBounds.max + padding);
            surface.localBounds = bounds;
            GeometryUtility.CalculateFrustumPlanes(source, frustum);
            if (!GeometryUtility.TestPlanesAABB(frustum, surface.bounds)) return;
            if (!viewCamera)
            {
                var go = new GameObject("Portal view " + destinationScene, typeof(Camera), typeof(UniversalAdditionalCameraData));
                go.hideFlags = HideFlags.HideAndDontSave;
                viewCamera = go.GetComponent<Camera>(); viewCamera.enabled = false;
            }
            int width = Mathf.Max(256, Mathf.Min(resolution, source.pixelWidth));
            int height = Mathf.Max(128, Mathf.RoundToInt(width / source.aspect));
            if (!texture || texture.width != width || texture.height != height)
            {
                if (texture) { texture.Release(); Destroy(texture); }
                texture = new RenderTexture(width, height, 24, RenderTextureFormat.DefaultHDR) { name = "Portal " + destinationScene, filterMode = FilterMode.Bilinear };
                texture.Create();
            }
            viewCamera.CopyFrom(source); viewCamera.enabled = false;
            viewCamera.cameraType = CameraType.Game;
            viewCamera.targetTexture = texture; viewCamera.useOcclusionCulling = false;
            viewCamera.transform.SetPositionAndRotation(MapPoint(source.transform.position), MapRotation(source.transform.rotation));
            viewCamera.ResetWorldToCameraMatrix();
            viewCamera.projectionMatrix = source.projectionMatrix;
            var point = viewCamera.worldToCameraMatrix.MultiplyPoint(Destination.transform.position + Destination.transform.forward * .01f);
            var normal = viewCamera.worldToCameraMatrix.MultiplyVector(Destination.transform.forward).normalized;
            viewCamera.projectionMatrix = viewCamera.CalculateObliqueMatrix(new Vector4(normal.x, normal.y, normal.z, -Vector3.Dot(point, normal)));
            var data = viewCamera.GetUniversalAdditionalCameraData();
            data.renderPostProcessing = false; data.renderShadows = true;
            data.requiresColorOption = CameraOverrideOption.Off; data.requiresDepthOption = CameraOverrideOption.Off;
            bool hidden = Destination.surface.forceRenderingOff;
            try
            {
                Destination.surface.forceRenderingOff = true;
                request.destination = texture;
                RenderPipeline.SubmitRenderRequest(viewCamera, request);
            }
            finally { if (Destination && Destination.surface) Destination.surface.forceRenderingOff = hidden; }
            if (properties == null) properties = new MaterialPropertyBlock();
            surface.GetPropertyBlock(properties);
            properties.SetTexture("_ReflectionTex", texture); properties.SetFloat("_ReflectionReady", 1);
            surface.SetPropertyBlock(properties);
        }
        void OnDestroy()
        {
            if (apertureMaterial) Destroy(apertureMaterial);
        }
        void OnDisable()
        {
            tracking = false;
            if (viewCamera) Destroy(viewCamera.gameObject);
            if (texture) { texture.Release(); Destroy(texture); }
        }
    }
}
