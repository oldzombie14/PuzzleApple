using UnityEngine;
using System.Collections.Generic;
using PuzzleApple.V5.Cognition;

namespace PuzzleApple.V5
{
    public sealed class ChapterRoom : MonoBehaviour
    {
        public Transform arrival, gardenThreshold;
        public Transform paintingFixture;
        public Renderer paintingLens;
        public Light paintingSpot;
        public bool floatingLamp;
        Vector3 hoverOrigin;
        bool wasMoving;
        float hoverPhase;
        public FoldingRoom box;
        public Collider mirror, floor;
        public Rigidbody lamp;
        public Renderer emitter;
        public Light bulb;
        [Tooltip("Exhibit and narrative lighting that does not take part in light sentences.")]
        public Light[] artLights=System.Array.Empty<Light>();
        readonly List<ConnectedLight> lights=new List<ConnectedLight>();
        sealed class ConnectedLight
        {
            public Light source;
            public bool inside;
            public float onIntensity,level=1,target=1;
        }
        WorldEntry entry;
        float brightness=1, target=1, paintingSpotInitial=1;
        public float Brightness=>brightness;
        Vector3 direction=new Vector3(-.6f,-.65f,.45f).normalized;
        MaterialPropertyBlock properties;
        Color emission;
        FoldingInterior whiteInterior;

        public void Bind(WorldEntry owner)
        {
            entry=owner;
            if(lamp)hoverOrigin=lamp.position;
            if(box)whiteInterior=box.GetComponent<FoldingInterior>();
            var bounds=new Bounds(arrival?arrival.position:transform.position,Vector3.zero);
            if(box)foreach(var panel in box.movingColliders)bounds.Encapsulate(panel.bounds);
            foreach(var root in gameObject.scene.GetRootGameObjects())
                foreach(var source in root.GetComponentsInChildren<Light>(true))
                    if(System.Array.IndexOf(artLights,source)<0)
                        lights.Add(new ConnectedLight{source=source,inside=!box||bounds.Contains(source.transform.position),onIntensity=source.intensity});
            properties=new MaterialPropertyBlock();
            if(!box)
            {
                // The adjacent gallery can go dark while its open frame still shows this room.
                var probe=new UnityEngine.Rendering.SphericalHarmonicsL2();
                probe.AddAmbientLight(new Color(.22f,.17f,.13f));
                var probes=new[]{probe};
                foreach(var root in gameObject.scene.GetRootGameObjects())
                    foreach(var surface in root.GetComponentsInChildren<MeshRenderer>(true))
                    {
                        surface.lightProbeUsage=UnityEngine.Rendering.LightProbeUsage.CustomProvided;
                        surface.GetPropertyBlock(properties);
                        properties.CopySHCoefficientArraysFrom(probes);
                        surface.SetPropertyBlock(properties);
                    }
                properties.Clear();
            }
            var groundRenderer=floor.GetComponent<Renderer>();
            groundRenderer.GetPropertyBlock(properties);
            properties.SetFloat("_PlayableFloor",1);
            groundRenderer.SetPropertyBlock(properties);
            properties.Clear();
            if(paintingSpot)paintingSpotInitial=Mathf.Max(.001f,paintingSpot.intensity);
            var luminous=emitter?emitter:paintingLens;
            if(luminous)emission=luminous.sharedMaterial.GetColor("_EmissionColor");
            if(lamp)Physics.IgnoreCollision(lamp.GetComponent<Collider>(),entry.player.GetComponent<CharacterController>());
        }
        public bool LightWouldChange(bool on)=>lights.Exists(l=>(l.inside||box&&box.Progress>0)&&(on?l.level<.999f:l.level>.001f));
        public void SetLight(bool on)
        {
            target=on?1:0;
            foreach(var light in lights)
                if(light.inside||box&&box.Progress>0)light.target=target;
        }
        public void HoldLight()
        {
            target=brightness;
            foreach(var light in lights)light.target=light.level;
        }
        void LateUpdate()
        {
            if(!entry)return;
            float step=Time.unscaledDeltaTime/Mathf.Max(.01f,entry.chamberLight.fadeDuration);
            brightness=Mathf.MoveTowards(brightness,target,step);
            foreach(var light in lights)
            {
                light.level=Mathf.MoveTowards(light.level,light.target,step);
                light.source.intensity=light.onIntensity*light.level*entry.sentenceRuntime.LampMultiplier;
            }
            if(emitter)
            {
                properties.SetColor("_EmissionColor",emission*brightness*entry.sentenceRuntime.LampMultiplier);
                properties.SetColor("_BaseColor",Color.Lerp(Color.gray*.05f,Color.white,brightness));
                emitter.SetPropertyBlock(properties);
            }
            if(paintingLens)
            {
                float glow=Mathf.Clamp01(paintingSpot.intensity/paintingSpotInitial);
                properties.SetColor("_EmissionColor",emission*glow);
                properties.SetColor("_BaseColor",Color.Lerp(Color.gray*.05f,Color.white,glow));
                paintingLens.SetPropertyBlock(properties);
            }
            if(!box)return;
            whiteInterior.darkLevel=Mathf.Lerp(.035f,1.65f,brightness);
            RenderSettings.ambientMode=UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight=Color.white*Mathf.Lerp(.075f,.68f,brightness);
        }
        public string WordAt(Collider hit,out Renderer lightSurface)
        {
            lightSurface=null;
            if(paintingFixture&&hit.transform.IsChildOf(paintingFixture)&&paintingSpot.intensity>=paintingSpotInitial*.999f){lightSurface=paintingLens;return "light";}
            if(hit==mirror)return "mirror";
            if(hit==floor)return "floor";
            if(lamp&&hit.attachedRigidbody==lamp&&brightness>=.999f){lightSurface=emitter;return "light";}
            return null;
        }
        void FixedUpdate()
        {
            if(!lamp||!entry||entry.portableClosure&&entry.portableClosure.Isolated)return;
            var runtime=entry.sentenceRuntime;
            bool wander=runtime.HasEffect(CognitionSignal.LightMove);

            bool descend=runtime.HasEffect(CognitionSignal.LightFloorMove);
            if((wander||descend)&&brightness<=0)return;
            bool here=entry.outdoor.CurrentRoom==this;
            if(!here||!wander&&!descend)
            {
                if(floatingLamp)
                {
                    if(wasMoving){hoverOrigin=lamp.position;hoverPhase=0;}
                    hoverPhase+=Time.fixedDeltaTime;
                    var destination=hoverOrigin+Vector3.up*(.16f*Mathf.Sin(hoverPhase*1.05f));
                    var delta=destination-lamp.position;
                    if(delta.sqrMagnitude>.000001f&&!lamp.SweepTest(delta.normalized,out _,delta.magnitude+.015f))lamp.MovePosition(destination);
                }
                wasMoving=false;
                return;
            }
            wasMoving=true;
            Vector3 travel=descend?Vector3.down:direction;
            float step=entry.chamberLight.moveSpeed*Time.fixedDeltaTime;
            if(lamp.SweepTest(travel,out var hit,step+.015f,QueryTriggerInteraction.Ignore))
            {
                step=Mathf.Clamp(hit.distance-.015f,0,step);
                if(wander)direction=Vector3.Reflect(direction,hit.normal).normalized;
            }
            lamp.MovePosition(lamp.position+travel*step);
        }
    }
}
