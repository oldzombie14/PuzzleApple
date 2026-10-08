using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace PuzzleApple.V3
{
    // Visual-only copies: no gameplay components, colliders or world lighting enter a photo.
    public static class SnapshotStudio
    {
        public static void Capture(OpeningSnapshotCamera source,WordLibrary library)
        {
            int layer=LayerMask.NameToLayer("V3 Snapshot");
            if(layer<0)throw new System.InvalidOperationException("V3 Snapshot layer is missing.");
            var root=new GameObject("Snapshot studio"){hideFlags=HideFlags.HideAndDontSave,layer=layer};
            root.transform.position=new Vector3(10000,10000,10000);
            var materials=new List<Material>();
            RenderTexture frame=null;
            try
            {
                var renderers=new List<Renderer>();
                if(source.symbolSubject)
                {
                    var g=new GameObject("Symbol"){layer=layer};g.transform.SetParent(root.transform,false);
                    var sprite=g.AddComponent<SpriteRenderer>();sprite.sprite=source.symbolSubject;renderers.Add(sprite);
                }
                else
                {
                    foreach(var original in source.subject.GetComponentsInChildren<MeshRenderer>())
                    {
                        var mesh=original.GetComponent<MeshFilter>();if(!mesh||!mesh.sharedMesh||!original.enabled)continue;
                        var g=new GameObject(original.name){layer=layer};g.transform.SetParent(root.transform,false);
                        g.transform.localPosition=Quaternion.Inverse(source.subject.rotation)*(original.transform.position-source.subject.position);
                        g.transform.localRotation=Quaternion.Inverse(source.subject.rotation)*original.transform.rotation;
                        g.transform.localScale=original.transform.lossyScale;
                        g.AddComponent<MeshFilter>().sharedMesh=mesh.sharedMesh;
                        var copy=g.AddComponent<MeshRenderer>();var mats=original.sharedMaterials;
                        for(int i=0;i<mats.Length;i++)
                        {
                            if(!mats[i]||mats[i].shader.name!="PuzzleApple/Planar Mirror")continue;
                            var glass=new Material(Shader.Find("Universal Render Pipeline/Lit"));
                            glass.SetColor("_BaseColor",new Color(.48f,.48f,.48f));glass.SetFloat("_Smoothness",.7f);
                            mats[i]=glass;materials.Add(glass);
                        }
                        copy.sharedMaterials=mats;copy.shadowCastingMode=ShadowCastingMode.Off;
                        copy.reflectionProbeUsage=ReflectionProbeUsage.Off;copy.lightProbeUsage=LightProbeUsage.CustomProvided;
                        var sh=new SphericalHarmonicsL2();sh.AddAmbientLight(new Color(.25f,.25f,.25f));
                        var properties=new MaterialPropertyBlock();original.GetPropertyBlock(properties);
                        properties.CopySHCoefficientArraysFrom(new[]{sh});
                        properties.SetVector("_GlossyEnvironmentColor",new Vector4(.3f,.3f,.3f,1));copy.SetPropertyBlock(properties);
                        renderers.Add(copy);
                    }
                }
                if(renderers.Count==0)return;
                var bounds=renderers[0].bounds;foreach(var renderer in renderers)bounds.Encapsulate(renderer.bounds);
                var cameraNode=new GameObject("Standard camera"){layer=layer};cameraNode.transform.SetParent(root.transform,false);
                var camera=cameraNode.AddComponent<Camera>();camera.enabled=false;camera.orthographic=true;
                camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=source.studioBackground;
                camera.cullingMask=1<<layer;camera.nearClipPlane=.01f;camera.allowHDR=true;
                var direction=source.symbolSubject?Vector3.back:source.studioDirection.normalized;
                if(direction.sqrMagnitude<.5f)direction=Vector3.back;
                float distance=Mathf.Max(2,bounds.size.magnitude*2);
                camera.transform.position=bounds.center+direction*distance;camera.transform.LookAt(bounds.center,source.symbolSubject?Vector3.up:source.studioUp);
                camera.farClipPlane=distance+bounds.size.magnitude+2;
                camera.aspect=(float)source.resolution.x/source.resolution.y;
                float x=0,y=0;
                for(int i=0;i<8;i++)
                {
                    var corner=bounds.center+Vector3.Scale(bounds.extents,new Vector3((i&1)==0?-1:1,(i&2)==0?-1:1,(i&4)==0?-1:1));
                    var local=camera.transform.InverseTransformPoint(corner);x=Mathf.Max(x,Mathf.Abs(local.x));y=Mathf.Max(y,Mathf.Abs(local.y));
                }
                camera.orthographicSize=Mathf.Max(y,x/camera.aspect)*1.18f;
                var data=cameraNode.AddComponent<UniversalAdditionalCameraData>();data.renderPostProcessing=false;data.renderShadows=false;
                for(int i=0;i<2;i++)
                {
                    var lightNode=new GameObject("Studio light"){layer=layer};lightNode.transform.SetParent(root.transform,false);
                    var light=lightNode.AddComponent<Light>();light.type=LightType.Directional;light.cullingMask=1<<layer;
                    light.color=Color.white;light.intensity=i==0?.65f:.35f;light.shadows=LightShadows.None;
                    light.transform.rotation=camera.transform.rotation*Quaternion.Euler(i==0?25:-15,i==0?-30:45,0);
                }
                frame=RenderTexture.GetTemporary(Mathf.Max(16,source.resolution.x),Mathf.Max(16,source.resolution.y),24,RenderTextureFormat.ARGB32,RenderTextureReadWrite.sRGB);
                camera.targetTexture=frame;
                RenderPipeline.SubmitRenderRequest(camera,new UniversalRenderPipeline.SingleCameraRequest{destination=frame});
                library.Remember(source.wordId,frame);
            }
            finally
            {
                root.SetActive(false);
                if(frame)RenderTexture.ReleaseTemporary(frame);
                foreach(var material in materials)if(Application.isPlaying)Object.Destroy(material);else Object.DestroyImmediate(material);
                if(Application.isPlaying)Object.Destroy(root);else Object.DestroyImmediate(root);
            }
        }
    }
}
