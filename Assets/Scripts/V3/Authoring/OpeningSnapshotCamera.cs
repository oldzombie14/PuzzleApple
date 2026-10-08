using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace PuzzleApple.V3
{
    // Retained for the archived capture rig. Playable word collection reads saved textures.
    [RequireComponent(typeof(Camera))]
    public sealed class OpeningSnapshotCamera : MonoBehaviour
    {
        public string wordId;
        [Tooltip("Optional moving subject. The authored camera pose is stored relative to it.")]
        public Transform subject;
        public bool isolateSubject=true;
        public Sprite symbolSubject;
        public Vector3 studioDirection=new Vector3(0,.15f,-1);
        public Vector3 studioUp=Vector3.up;
        public Color studioBackground=new Color(.16f,.16f,.16f,1);
        public Vector2Int resolution=new Vector2Int(512,640);
        Vector3 localPosition;
        Quaternion localRotation;
        bool initialized;
        void Awake()=>InitializePose();
        public void InitializePose()
        {
            if(initialized)return;
            if(subject){localPosition=subject.InverseTransformPoint(transform.position);localRotation=Quaternion.Inverse(subject.rotation)*transform.rotation;}
            initialized=true;GetComponent<Camera>().enabled=false;
        }
        public void AlignToSubject()
        {
            InitializePose();
            if(subject)transform.SetPositionAndRotation(subject.TransformPoint(localPosition),subject.rotation*localRotation);
        }
        public void Capture(WordLibrary library)
        {
            if(isolateSubject&&(subject||symbolSubject)){SnapshotStudio.Capture(this,library);return;}
            AlignToSubject();var camera=GetComponent<Camera>();
            var previous=camera.targetTexture;float aspect=camera.aspect;
            var frame=RenderTexture.GetTemporary(Mathf.Max(16,resolution.x),Mathf.Max(16,resolution.y),24,RenderTextureFormat.ARGB32);
            try
            {
                camera.targetTexture=frame;camera.aspect=(float)frame.width/frame.height;
                foreach(var mirror in FindObjectsByType<PlanarMirror>(FindObjectsSortMode.None))mirror.RenderForCamera(camera);
                RenderPipeline.SubmitRenderRequest(camera,new UniversalRenderPipeline.SingleCameraRequest{destination=frame});
                library.Remember(wordId,frame);
            }
            finally{camera.targetTexture=previous;camera.aspect=aspect;RenderTexture.ReleaseTemporary(frame);}
        }
    }
}
