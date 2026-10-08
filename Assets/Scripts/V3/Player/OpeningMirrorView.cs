using UnityEngine;
using UnityEngine.Rendering;

namespace PuzzleApple.V3
{
    // Runs after the controller poses its ordinary view, and before PlanarMirror renders.
    [DefaultExecutionOrder(200)]
    public sealed class OpeningMirrorView : MonoBehaviour
    {
        public FirstPersonController player;
        public V3Presentation presentation;
        [Tooltip("Object reflection plane. Local forward is its normal; used for the opposite-wall mirror.")]
        public Transform plane;
        [Tooltip("Fixed room-center player-view reflection plane. Local forward is its normal; never parent it to the movable mirror.")]
        public Transform selfPlane;
        public Camera reflectedCamera;
        public bool Reflected { get; private set; }
        public Vector3 ReflectPoint(Vector3 point)=>point-2*Vector3.Dot(point-plane.position,plane.forward)*plane.forward;
        public Vector3 ReflectDirection(Vector3 direction)=>Vector3.Reflect(direction,plane.forward);
        public Quaternion ReflectRotation(Quaternion rotation)=>Quaternion.LookRotation(ReflectDirection(rotation*Vector3.forward),ReflectDirection(rotation*Vector3.up));
        public Vector3 ReflectSelfPoint(Vector3 point)=>point-2*Vector3.Dot(point-selfPlane.position,selfPlane.forward)*selfPlane.forward;
        public Quaternion ReflectSelfRotation(Quaternion rotation)=>Quaternion.LookRotation(Vector3.Reflect(rotation*Vector3.forward,selfPlane.forward),Vector3.Reflect(rotation*Vector3.up,selfPlane.forward));
        public void SetReflected(bool value)
        {
            if(Reflected==value)return;
            Reflected=value;presentation.SetSplit(value);
            if(!value&&reflectedCamera)reflectedCamera.enabled=false;
        }
        void LateUpdate()
        {
            if(!Reflected||!reflectedCamera||!selfPlane)return;
            var view=player.view;
            reflectedCamera.transform.SetPositionAndRotation(ReflectSelfPoint(view.position),ReflectSelfRotation(view.rotation));
            reflectedCamera.fieldOfView=presentation.view.fieldOfView;
        }
        void OnEnable()=>RenderPipelineManager.beginCameraRendering+=BeforeCamera;
        void BeforeCamera(ScriptableRenderContext context,Camera camera)
        {
            if(!Reflected||camera!=reflectedCamera)return;
            foreach(var mirror in FindObjectsByType<PlanarMirror>(FindObjectsSortMode.None))mirror.RenderForCamera(camera);
        }
        void OnDisable()
        {
            RenderPipelineManager.beginCameraRendering-=BeforeCamera;
            if(presentation)SetReflected(false);
        }
    }
}
