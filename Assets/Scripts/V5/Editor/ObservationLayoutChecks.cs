using System;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

namespace PuzzleApple.V5.Editor
{
    public static class ObservationLayoutChecks
    {
        public static string Run()
        {
            var e=UnityEngine.Object.FindFirstObjectByType<WorldEntry>();var source=e.chamberLight;
            foreach(var size in new[]{new Vector2Int(1920,1080),new Vector2Int(1280,720),new Vector2Int(1024,768),new Vector2Int(2560,1080)})
            {
                var root=new GameObject("Temporary observation layout check");
                var frame=new RenderTexture(size.x,size.y,16);
                try
                {
                    var camera=root.AddComponent<Camera>();camera.enabled=false;camera.orthographic=true;camera.targetTexture=frame;
                    var ui=new GameObject("Canvas",typeof(RectTransform),typeof(Canvas),typeof(CanvasScaler));ui.transform.SetParent(root.transform,false);
                    var canvas=ui.GetComponent<Canvas>();canvas.renderMode=RenderMode.ScreenSpaceCamera;canvas.worldCamera=camera;canvas.planeDistance=1;
                    var scaler=ui.GetComponent<CanvasScaler>();scaler.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;scaler.referenceResolution=new Vector2(1920,1080);scaler.matchWidthOrHeight=.5f;
                    var eye=UnityEngine.Object.Instantiate(source.hoverEye,ui.transform,false);eye.gameObject.SetActive(true);
                    var ring=UnityEngine.Object.Instantiate(source.observer.ring,ui.transform,false);ring.gameObject.SetActive(true);
                    Canvas.ForceUpdateCanvases();
                    var mesh=eye.canvasRenderer.GetMesh();var vertices=mesh.vertices;var uv=mesh.uv;
                    var content=eye.GetComponent<SpriteContentAlignment>().contentCentre;
                    Vector2 sample=new Vector2((eye.sprite.rect.x+content.x*eye.sprite.rect.width)/eye.sprite.texture.width,
                        (eye.sprite.rect.y+content.y*eye.sprite.rect.height)/eye.sprite.texture.height);
                    float x=Mathf.LerpUnclamped(vertices.Min(v=>v.x),vertices.Max(v=>v.x),(sample.x-uv.Min(v=>v.x))/(uv.Max(v=>v.x)-uv.Min(v=>v.x)));
                    float y=Mathf.LerpUnclamped(vertices.Min(v=>v.y),vertices.Max(v=>v.y),(sample.y-uv.Min(v=>v.y))/(uv.Max(v=>v.y)-uv.Min(v=>v.y)));
                    var eyePoint=camera.WorldToViewportPoint(eye.transform.TransformPoint(new Vector3(x,y,0)));
                    var ringPoint=camera.WorldToViewportPoint(ring.transform.TransformPoint(Vector3.zero));
                    Check(Mathf.Abs(eyePoint.x-ringPoint.x)*size.x<.6f&&Mathf.Abs(eyePoint.y-ringPoint.y)*size.y<.6f,"visible eye and ring centres at "+size);
                    foreach(float state in new[]{1f,.35f})foreach(float progress in new[]{0f,.25f,.75f,1f})
                    {
                        ring.SetState(progress,state);Canvas.ForceUpdateCanvases();var colours=ring.canvasRenderer.GetMesh().colors32;
                        Check(colours.Length>0,"ring geometry");
                        if(progress>0)
                        {
                            Check(colours.Max(c=>c.a)>colours.Min(c=>c.a)*5,"completed arc contrast for state "+state);
                            Check(ring.progressThickness>ring.thickness,"completed arc is wider than track");
                        }
                    }
                }
                finally{UnityEngine.Object.DestroyImmediate(root);frame.Release();UnityEngine.Object.DestroyImmediate(frame);}
            }
            return "PASS: visible eye/ring alignment at 1920x1080, 1280x720, 1024x768, 2560x1080; idle/quarter/three-quarter/full progress in unknown and known states";
        }
        static void Check(bool value,string message){if(!value)throw new Exception("FAIL: "+message);}
    }
}
