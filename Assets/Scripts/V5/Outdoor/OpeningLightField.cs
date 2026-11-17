using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Rendering;

namespace PuzzleApple.V5.Outdoor
{
    // V4 lamp appearance and physics; this level creates records only on a world drop.
    public sealed class OpeningLightField : MonoBehaviour
    {
        public OpeningLight stateSource;
        public Collider ground;
        public Transform observer;
        public Vector2 scaleRange=new Vector2(.12f,.35f),clearanceRange=new Vector2(.15f,.8f);
        public float revealDuration=.65f,moveSpeed=1.2f;
        public int maxPointLights=6;
        public float pointLightDistance=14;
        public IEnumerable<OpeningLight> ActiveLamps=>lamps;
        public int ActiveCount=>lamps.Count;
        readonly List<OpeningLight> lamps=new List<OpeningLight>();
        Material revealMaterial;
        Mesh diamond;
        public Material RevealMaterial=>revealMaterial;
        public int PlayerCount=>lamps.Count(l=>l && l.TryGetComponent<PlayerInstance>(out _));
        public void Remove(OpeningLight lamp)=>lamps.Remove(lamp);
        public static float Radius(float scale,InstanceShape shape)=>scale*(shape==InstanceShape.Cube?.8661f:shape==InstanceShape.Sphere?.62f:.82f);
        GameObject CreateBody(InstanceShape shape)
        {
            if(shape!=InstanceShape.Octahedron)return GameObject.CreatePrimitive(shape==InstanceShape.Cube?PrimitiveType.Cube:PrimitiveType.Sphere);
            if(!diamond){
                diamond=new Mesh{name="Octahedral light"};
                var points=new[]{Vector3.up,Vector3.right,Vector3.forward,Vector3.left,Vector3.back,Vector3.down};
                var corners=new[]{0,2,1,0,3,2,0,4,3,0,1,4,5,1,2,5,2,3,5,3,4,5,4,1};
                diamond.vertices=corners.Select(i=>points[i]*.5f).ToArray();
                diamond.triangles=Enumerable.Range(0,corners.Length).ToArray();diamond.RecalculateNormals();diamond.RecalculateBounds();
            }
            var go=new GameObject("Octahedral light",typeof(MeshFilter),typeof(MeshRenderer),typeof(MeshCollider));
            go.GetComponent<MeshFilter>().sharedMesh=diamond;var collider=go.GetComponent<MeshCollider>();collider.sharedMesh=diamond;collider.convex=true;
            return go;
        }
        bool moving;
        public void SetMoving(bool value)
        {
            moving=value;
            foreach(var lamp in lamps)if(lamp)
            {
                lamp.GetComponent<LightMotion>().SetMoving(value);
            }
        }
        public OpeningLight Spawn(Vector3 position,float transferredScale=0,InstanceShape shape=InstanceShape.Cube,WorldDropRegion creator=null)
        {
            if(!stateSource||!stateSource.AppearanceMaterial)return null;
            if(!revealMaterial)
            {
                revealMaterial=new Material(stateSource.AppearanceMaterial);
                revealMaterial.SetFloat("_Surface",1);revealMaterial.SetFloat("_SrcBlend",(float)BlendMode.SrcAlpha);
                revealMaterial.SetFloat("_DstBlend",(float)BlendMode.OneMinusSrcAlpha);revealMaterial.SetFloat("_ZWrite",0);
                revealMaterial.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");revealMaterial.DisableKeyword("_ALPHAPREMULTIPLY_ON");
                revealMaterial.SetOverrideTag("RenderType","Transparent");revealMaterial.renderQueue=(int)RenderQueue.Transparent;
            }
            var go=CreateBody(shape);go.name="Created "+shape+" light";go.SetActive(false);
            go.transform.SetParent(transform,false);go.transform.position=position;
            if(shape==InstanceShape.Cube)go.transform.rotation=Quaternion.FromToRotation(new Vector3(1,-1,1),Vector3.down);
            go.transform.localScale=Vector3.one*(transferredScale>0?transferredScale:Random.Range(scaleRange.x,scaleRange.y));
            if(shape!=InstanceShape.Cube)go.transform.localScale*=2*Radius(1,shape);
            var renderer=go.GetComponent<Renderer>();renderer.shadowCastingMode=ShadowCastingMode.Off;
            var lamp=go.AddComponent<OpeningLight>();lamp.surface=renderer;lamp.appearanceSource=stateSource;
            var glow=new GameObject("Local glow",typeof(Light));glow.transform.SetParent(go.transform,false);
            lamp.glow=glow.GetComponent<Light>();lamp.glow.type=LightType.Point;lamp.glow.range=2.5f;lamp.glow.intensity=.7f;lamp.glow.shadows=LightShadows.None;
            var motion=go.AddComponent<LightMotion>();motion.speed=moveSpeed;
            if(transferredScale<=0)lamp.BeginReveal(revealMaterial,Time.time,revealDuration);
            go.SetActive(true);motion.SetMoving(moving);
            if(moving){var d=motion.Direction;d.y=Mathf.Max(.25f,Mathf.Abs(d.y));motion.RestoreDirection(d);}
            if(observer)Physics.IgnoreCollision(go.GetComponent<Collider>(),observer.GetComponentInParent<CharacterController>(),true);
            if(creator)go.AddComponent<PlayerInstance>().Bind(this,lamp,shape,creator);
            lamps.Add(lamp);return lamp;
        }
        void LateUpdate()
        {
            lamps.RemoveAll(l=>!l);
            if(!observer)return;
            lamps.Sort((a,b)=>(a.transform.position-observer.position).sqrMagnitude.CompareTo((b.transform.position-observer.position).sqrMagnitude));
            for(int i=0;i<lamps.Count;i++)lamps[i].illuminate=i<maxPointLights&&(lamps[i].transform.position-observer.position).sqrMagnitude<pointLightDistance*pointLightDistance;
        }
        void OnDestroy(){if(diamond)Destroy(diamond);if(revealMaterial)Destroy(revealMaterial);}
    }
}
