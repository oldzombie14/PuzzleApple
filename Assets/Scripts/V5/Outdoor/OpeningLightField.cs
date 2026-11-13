using System.Collections.Generic;
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
        bool moving;
        public void SetMoving(bool value)
        {
            moving=value;
            foreach(var lamp in lamps)if(lamp)
            {
                var motion=lamp.GetComponent<LightMotion>();var direction=motion.Direction;
                motion.SetMoving(value);if(direction.sqrMagnitude>.001f)motion.RestoreDirection(direction);
            }
        }
        public OpeningLight Spawn(Vector3 position,float transferredScale=0)
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
            var go=GameObject.CreatePrimitive(PrimitiveType.Cube);go.name="Created light";go.SetActive(false);
            go.transform.SetParent(transform,false);go.transform.position=position;
            go.transform.rotation=Quaternion.FromToRotation(new Vector3(1,-1,1),Vector3.down);
            go.transform.localScale=Vector3.one*(transferredScale>0?transferredScale:Random.Range(scaleRange.x,scaleRange.y));
            var renderer=go.GetComponent<Renderer>();renderer.shadowCastingMode=ShadowCastingMode.Off;
            var lamp=go.AddComponent<OpeningLight>();lamp.surface=renderer;lamp.appearanceSource=stateSource;
            var glow=new GameObject("Local glow",typeof(Light));glow.transform.SetParent(go.transform,false);
            lamp.glow=glow.GetComponent<Light>();lamp.glow.type=LightType.Point;lamp.glow.range=2.5f;lamp.glow.intensity=.7f;lamp.glow.shadows=LightShadows.None;
            var motion=go.AddComponent<LightMotion>();motion.speed=moveSpeed;
            if(transferredScale<=0)lamp.BeginReveal(revealMaterial,Time.time,revealDuration);
            go.SetActive(true);motion.SetMoving(moving);
            if(moving){var d=motion.Direction;d.y=Mathf.Max(.25f,Mathf.Abs(d.y));motion.RestoreDirection(d);}
            if(observer)Physics.IgnoreCollision(go.GetComponent<Collider>(),observer.GetComponentInParent<CharacterController>(),true);
            lamps.Add(lamp);return lamp;
        }
        void LateUpdate()
        {
            lamps.RemoveAll(l=>!l);
            if(!observer)return;
            lamps.Sort((a,b)=>(a.transform.position-observer.position).sqrMagnitude.CompareTo((b.transform.position-observer.position).sqrMagnitude));
            for(int i=0;i<lamps.Count;i++)lamps[i].illuminate=i<maxPointLights&&(lamps[i].transform.position-observer.position).sqrMagnitude<pointLightDistance*pointLightDistance;
        }
        void OnDestroy(){if(revealMaterial)Destroy(revealMaterial);}
    }
}
