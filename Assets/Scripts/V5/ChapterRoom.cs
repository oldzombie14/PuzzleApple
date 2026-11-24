using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using PuzzleApple.V5.Cognition;

namespace PuzzleApple.V5
{
    public sealed class ChapterRoom : MonoBehaviour, ISwitchableLight, ISentenceMovable, IBrightenableLight, ISentenceOutputState<ChapterRoom.LightState>, ISentenceOutputState<ChapterRoom.MotionState>
    {
        internal SentenceStateOutput LightOutput;
        public SentenceMotionOutput MotionOutput {get;internal set;}
        public Transform arrival, gardenThreshold;
        public ChapterLayoutSettings layout;
        public float WalkingMultiplier(Vector3 feet)=>box && boxFill && boxFill.Contains(feet+Vector3.up)?1:layout?layout.walkingMultiplier:1.3f;
        public Transform paintingFixture;
        public Renderer paintingLens;
        public Light paintingSpot;
        public bool floatingLamp;
        Vector3 hoverOrigin;
        float hoverPhase;
        public FoldingRoom box;
        public Collider mirror, floor;
        public Rigidbody lamp;
        public Renderer emitter;
        public Light bulb;
        [Tooltip("Exhibit and narrative lighting that does not take part in light sentences.")]
        public Light[] artLights=System.Array.Empty<Light>();
        [Header("Room fill (shared RoomLightStyle)")]
        [Tooltip("Fill of the space the player stands in once the box is open, or of the whole room when there is no box.")]
        public RoomLightBalance roomFill;
        [Tooltip("Fill of the folded arrival box; blends into Room Fill as the box unfolds.")]
        public RoomLightBalance boxFill;
        readonly List<MeshRenderer> probed=new List<MeshRenderer>();
        Color probedFill=new Color(-1,-1,-1);
        readonly List<ConnectedLight> lights=new List<ConnectedLight>();
        sealed class ConnectedLight
        {
            public Light source;
            public bool inside, driving;
            public float onIntensity,level=1,target=1;
        }
        public MirrorStructure mirrors;
        public VitruvianPuzzle puzzle;
        ChapterPaintingLight[] paintings;
        public WorldEntry Entry {get;private set;}
        WorldEntry entry=>Entry;
        bool lightDriving;
        float brightness=1, target=1, paintingSpotInitial=1;
        public float Brightness=>brightness;
        public void RegisterSentenceSubjects(SentenceEntityRegistry registry, SentenceStateOutput output)
        {
            if (lamp) SentenceSceneBindings.BindLightSubject(registry, lamp, () => Space, () => brightness, output);
            foreach (var light in lights)
                if (light.source && !light.source.GetComponentInParent<Outdoor.OpeningLight>())
                    SentenceSceneBindings.BindLightSubject(registry, light.source, () => Space, () => light.source.intensity, output);
        }
        public bool FitsArrivalBox(CharacterController motor)
        {
            if(!boxFill)return false;
            var bounds=motor.bounds;
            bounds.Expand(new Vector3(2*motor.skinWidth,-.2f,2*motor.skinWidth));
            return boxFill.Contains(bounds.min) && boxFill.Contains(bounds.max);
        }
        Vector3 direction=new Vector3(-.6f,-.65f,.45f).normalized;
        MaterialPropertyBlock properties;
        Color emission;
        RoomLighting introLighting;

        public void Bind(WorldEntry owner)
        {
            Entry=owner;
            paintings=gameObject.scene.GetRootGameObjects().SelectMany(root=>root.GetComponentsInChildren<ChapterPaintingLight>(true)).ToArray();
            if(lamp)hoverOrigin=lamp.position;
            if(box)introLighting=FindFirstObjectByType<RoomLighting>(FindObjectsInactive.Include);
            foreach(var root in gameObject.scene.GetRootGameObjects())
                foreach(var source in root.GetComponentsInChildren<Light>(true))
                    if(System.Array.IndexOf(artLights,source)<0)
                        lights.Add(new ConnectedLight{source=source,inside=!box||boxFill.Contains(source.transform.position),onIntensity=source.intensity});
            properties=new MaterialPropertyBlock();
            if(!box)
            {
                // The adjacent gallery can go dark while its open frame still shows this room,
                // so this room carries its own ambient as a custom probe fed by its RoomLightBalance.
                foreach(var root in gameObject.scene.GetRootGameObjects())
                    foreach(var surface in root.GetComponentsInChildren<MeshRenderer>(true))
                    {
                        surface.lightProbeUsage=UnityEngine.Rendering.LightProbeUsage.CustomProvided;
                        probed.Add(surface);
                    }
                ApplyProbe(roomFill?roomFill.FillColor:new Color(.22f,.17f,.13f));
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
            SentenceSceneBindings.BindChapter(this);
        }
        public bool LightWouldChange(bool on)=>lights.Exists(l=>(l.inside||box&&box.Progress>0)&&(on?l.level<.999f:l.level>.001f));
        public void SetLight(bool on)
        {
            lightDriving=true;
            target=on?1:0;
            foreach(var light in lights)
                if(light.inside||box&&box.Progress>0){light.target=target;light.driving=true;}
        }
        public void HoldLight()
        {
            lightDriving=false;
            target=brightness;
            foreach(var light in lights){light.target=light.level;light.driving=false;}
        }
        bool ISwitchableLight.WouldChange(bool on)=>LightWouldChange(on);
        bool ISwitchableLight.Settling(bool on)=>target==(on?1:0) && brightness!=target || lights.Exists(light=>light.target==(on?1:0) && light.level!=light.target);
        void ISwitchableLight.SetOn(bool on)=>SetLight(on);
        void ISwitchableLight.Hold()=>HoldLight();
        bool IBrightenableLight.Lit=>Brightness>0 || puzzle && (puzzle.ball.Brightness>0 || puzzle.source.Brightness>0);
        internal bool SupportsNativeMotion(SentenceMotion motion)=>
            motion==SentenceMotion.Light && (puzzle || lamp || paintings.Any(p=>p.swing)) || motion==SentenceMotion.LightToFloor && lamp;
        internal bool NativeMotionWouldChange(SentenceMotion motion)=>
            motion==SentenceMotion.Light && (puzzle && puzzle.ball.EmitsLight || paintings.Any(p=>p.swing && p.Lit)) ||
            lamp&&Brightness>0&&(motion==SentenceMotion.Light || motion==SentenceMotion.LightToFloor&&!lamp.SweepTest(Vector3.down,out _,.02f,QueryTriggerInteraction.Ignore));
        bool ISentenceMovable.CanMove(SentenceMotion motion)=>NativeMotionWouldChange(motion) || puzzle && puzzle.CanMove(motion);
        [System.Serializable]
        public struct LightState
        {
            public EmissionState room;
            public EmissionState[] fixtures;
        }
        public LightState CaptureState() => new LightState {
            room = new EmissionState(brightness),
            fixtures = lights.Select(light => new EmissionState(light.level)).ToArray()
        };
        public bool TryRestoreState(LightState state)
        {
            if (!entry || !state.room.Valid || state.fixtures == null || state.fixtures.Length != lights.Count ||
                state.fixtures.Any(level => !level.Valid)) return false;
            brightness = state.room.brightness;
            if (!lightDriving) target = brightness;
            for (int i = 0; i < lights.Count; i++)
            {
                var light = lights[i];
                light.level = state.fixtures[i].brightness;
                if (!light.driving) light.target = light.level;
            }
            ApplyLights(0);
            return true;
        }

        void LateUpdate()
        {
            if(!entry)return;
            float step=Time.unscaledDeltaTime/Mathf.Max(.01f,entry.chamberLight.fadeDuration);
            ApplyLights(step);
            foreach(var painting in paintings)painting.Tick((MotionOutput?.HasMotion(SentenceMotion.Light)==true));
        }
        void ApplyLights(float step)
        {
            float fraction=step>0?(LightOutput?.FinishFraction??0):0;
            brightness=RoomLighting.AdvanceLevel(brightness,target,step,fraction);
            float unfold=box?Mathf.SmoothStep(0,1,box.Progress):1;
            foreach(var light in lights)
            {
                light.level=RoomLighting.AdvanceLevel(light.level,light.target,step,fraction);
                float output=light.onIntensity;
                if(box)output=light.inside?introLighting.FoldingIntensity(output,unfold):output*unfold;
                light.source.intensity=output*light.level*entry.sentenceRuntime.LampBoost(Space);
            }
            if(emitter)
            {
                properties.SetColor("_EmissionColor",emission*brightness*entry.sentenceRuntime.LampBoost(Space));
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
            if (!isActiveAndEnabled) return;
            if(!box)
            {
                if(roomFill)ApplyProbe(roomFill.FillColor);
                return;
            }
            RenderSettings.ambientMode=UnityEngine.Rendering.AmbientMode.Flat;
            roomFill.closedFill=introLighting.closedAmbient;
            roomFill.openness=unfold;
            boxFill.enabled=!box.Opened;
            boxFill.closedFill=introLighting.closedAmbient;
            boxFill.openness=unfold;
            RenderSettings.ambientLight=introLighting.FoldingAmbient(roomFill.FillColor,unfold);
        }
        void ApplyProbe(Color fill)
        {
            if(Mathf.Abs(fill.r-probedFill.r)+Mathf.Abs(fill.g-probedFill.g)+Mathf.Abs(fill.b-probedFill.b)<.003f)return;
            probedFill=fill;
            var probe=new UnityEngine.Rendering.SphericalHarmonicsL2();
            probe.AddAmbientLight(fill);
            var probes=new[]{probe};
            foreach(var surface in probed)
            {
                if(!surface)continue;
                surface.GetPropertyBlock(properties);
                properties.CopySHCoefficientArraysFrom(probes);
                surface.SetPropertyBlock(properties);
            }
            properties.Clear();
        }
        public string WordAt(Collider hit,out Renderer lightSurface)
        {
            lightSurface=null;
            if(paintingFixture&&hit.transform.IsChildOf(paintingFixture)&&paintingSpot.intensity>0){lightSurface=paintingLens;return "light";}
            var painting=hit.GetComponentInParent<ChapterPaintingLight>();
            if(painting && painting.Lit){lightSurface=painting.lens;return "light";}
            if(puzzle && puzzle.WordAt(hit,out lightSurface))return "light";
            if(hit==mirror || mirrors && mirrors.IsMirror(hit))return "mirror";
            if(hit==floor && entry.board.State.Knows("floor"))return "floor";
            if(lamp&&hit.attachedRigidbody==lamp&&brightness>0){lightSurface=emitter;return "light";}
            return null;
        }
        public PassageJourney.LightSpace Space=>entry.outdoor.Vitruvian==this?PassageJourney.LightSpace.Vitruvian:entry.outdoor.Garden==this?PassageJourney.LightSpace.Garden:PassageJourney.LightSpace.Gallery;
        [System.Serializable]
        public struct MotionState
        {
            public PassageJourney.LightSpace space;
            public DriftPose lamp;
            public Vector3 hoverOrigin;
            public float hoverPhase;
            public float[] paintingPhases;
            public VitruvianPuzzle.OrbitState orbit;
        }
        MotionState ISentenceOutputState<MotionState>.CaptureState() => new MotionState {
            space = Space, lamp = lamp ? new DriftPose { position = lamp.position, direction = direction, parked = parked } : default,
            hoverOrigin = hoverOrigin, hoverPhase = hoverPhase, paintingPhases = paintings.Select(p => p.Phase).ToArray(),
            orbit = puzzle ? puzzle.CaptureOrbit() : default
        };
        public bool TryRestoreState(MotionState state)
        {
            if (Space != state.space || paintings == null || state.paintingPhases == null ||
                state.paintingPhases.Length != paintings.Length || state.paintingPhases.Any(p => !float.IsFinite(p)) ||
                !DriftPose.Finite(state.hoverOrigin) || !float.IsFinite(state.hoverPhase) ||
                lamp && (!state.lamp.Valid || state.lamp.direction == Vector3.zero)) return false;
            if (puzzle && !puzzle.TryRestoreOrbit(state.orbit)) return false;
            for (int i = 0; i < paintings.Length; i++) paintings[i].RestorePhase(state.paintingPhases[i]);
            if (lamp)
            {
                DriftPose.Place(lamp, state.lamp.position);
                direction = state.lamp.direction;
                parked = state.lamp.parked;
                hoverOrigin = state.hoverOrigin;
                hoverPhase = state.hoverPhase;
            }
            return true;
        }

        bool parked;
        void FixedUpdate()
        {
            if(!lamp||!entry||entry.portableClosure&&entry.portableClosure.Isolated)return;
            bool wander=(MotionOutput?.HasMotion(SentenceMotion.Light)==true);

            bool descend=(MotionOutput?.HasMotion(SentenceMotion.LightToFloor)==true);
            if((wander||descend)&&brightness<=0)return;
            if(descend || wander)parked=true;
            if(!wander&&!descend)
            {
                if(floatingLamp && !parked)
                {
                    hoverPhase+=Time.fixedDeltaTime;
                    var destination=hoverOrigin+Vector3.up*(.16f*Mathf.Sin(hoverPhase*1.05f));
                    var delta=destination-lamp.position;
                    if(delta.sqrMagnitude>.000001f&&!lamp.SweepTest(delta.normalized,out _,delta.magnitude+.015f))lamp.MovePosition(destination);
                }
                return;
            }
            Vector3 travel=descend?Vector3.down:direction;
            float step=entry.chamberLight.moveSpeed*Time.fixedDeltaTime;
            if(descend && MotionOutput!=null && floor){
                float toFloor=lamp.position.y-floor.bounds.max.y;
                step=MotionOutput.DirectedStep(step,Mathf.Max(0,toFloor),Time.fixedDeltaTime);
            }
            if(lamp.SweepTest(travel,out var hit,step+.015f,QueryTriggerInteraction.Ignore))
            {
                step=Mathf.Clamp(hit.distance-.015f,0,step);
                if(wander)direction=Vector3.Reflect(direction,hit.normal).normalized;
            }
            lamp.MovePosition(lamp.position+travel*step);
        }
    }
}
