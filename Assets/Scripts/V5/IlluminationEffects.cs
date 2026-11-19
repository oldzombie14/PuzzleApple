using System.Linq;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using PuzzleApple.V5.Cognition;

namespace PuzzleApple.V5
{
    // Illumination sentences: light world (exposure), light door (door glow + bloom), light floor
    // (floor glow) and light light (lamp boost). Fades the four levels from the applied effects and
    // presents the post-processing and global glow; lamps, doors and floors read the levels
    // (LampMultiplier, FloorIllumination, _P5DoorGlow/_P5FloorGlow) themselves. For feedback and
    // shots, world/door/floor always show; light light needs a lit IBrightenableLight in the
    // player's space (Chapter I room, outdoor world, tutorial spheres, corridor stars, far-room
    // lamps, chamber lamp).
    public sealed class IlluminationEffects : MonoBehaviour
    {
        Volume volume;
        VolumeProfile profile;
        ColorAdjustments exposure;
        Volume bloomVolume;
        VolumeProfile bloomProfile;
        Vignette vignette;
        SentenceRuntime source;
        Vector4 levels, targets;
        public float WorldExposure=>exposure.postExposure.value;
        public float FloorIllumination=>Mathf.SmoothStep(0,1,levels.z);
        public float LampMultiplier => 1 + Mathf.SmoothStep(0,1,levels.w);
        readonly Vector4[] localLevels=new Vector4[10];
        static int Index(PassageJourney.LightSpace space){
            int index=0;
            for(int mask=(int)space;mask>1;mask>>=1)index++;
            return index;
        }
        public float LampBoost(PassageJourney.LightSpace space)=>1+Mathf.SmoothStep(0,1,localLevels[Index(space)].w);
        const float FadeSeconds=1.4f;
        const float DoorGlowStrength=.9f;

        void Awake()
        {
            var obj=new GameObject("Sentence lighting");
            obj.transform.SetParent(transform,false);
            volume=obj.AddComponent<Volume>();
            volume.isGlobal=true;
            volume.priority=100;
            profile=ScriptableObject.CreateInstance<VolumeProfile>();
            volume.sharedProfile=profile;
            exposure=profile.Add<ColorAdjustments>();
            exposure.postExposure.Override(0);
            vignette=profile.Add<Vignette>();
            vignette.intensity.Override(0);

            var doorObject=new GameObject("Door bloom");
            doorObject.transform.SetParent(obj.transform,false);
            bloomVolume=doorObject.AddComponent<Volume>();
            bloomVolume.isGlobal=true;
            bloomVolume.priority=101;
            bloomProfile=ScriptableObject.CreateInstance<VolumeProfile>();
            bloomVolume.sharedProfile=bloomProfile;
            var bloom=bloomProfile.Add<Bloom>();
            bloom.threshold.Override(1.2f);
            bloom.intensity.Override(.65f);
            bloom.scatter.Override(.65f);
            bloomVolume.weight=0;
        }

        static bool Illuminates(CognitionSignal s)=>s==CognitionSignal.IlluminateWorld||s==CognitionSignal.IlluminateDoor
            ||s==CognitionSignal.IlluminateFloor||s==CognitionSignal.IlluminateLight;
        public static bool Handles(CognitionState.Group g)=>g!=null&&g.Signals.Any(Illuminates);

        public void Bind(SentenceRuntime runtime){source=runtime;}

        // Visible change for feedback where the player stands.
        public bool WouldChange(CognitionState.Group g)
        {
            if(g.Signals.Any(s=>s!=CognitionSignal.IlluminateLight&&Illuminates(s)))return true;
            var target=Brightened;
            return target!=null&&target.Lit;
        }

        IBrightenableLight Brightened
        {
            get
            {
                var entry=source.entry;
                if(entry.outdoor&&entry.outdoor.InChapter)return entry.outdoor.CurrentRoom;
                if(entry.outdoor&&entry.outdoor.IsOutside)return entry.outdoor.World;
                var j=entry.journey;
                if(j && j.PlayerSpace==PassageJourney.LightSpace.Ascent)return j.Ascent.Stars;
                if(j&&j.InTutorial)return j.Tutorial;
                if(j&&j.InCorridor)return j.CorridorGlow;
                if(j&&j.InSecondRoom)return j.FarRoomGlow;
                return entry.chamberLight;
            }
        }

        public void Apply(SentenceRuntime runtime)
        {
            source=runtime;
            targets=new Vector4(runtime.HasEffect(CognitionSignal.IlluminateWorld)?1:0,
                runtime.HasEffect(CognitionSignal.IlluminateDoor)?1:0,
                runtime.HasEffect(CognitionSignal.IlluminateFloor)?1:0,
                runtime.HasEffect(CognitionSignal.IlluminateLight)?1:0);
            RenderLevels();
        }

        void Update()
        {
            if(!source)return;
            var j=source.entry.journey;
            if(j && j.Root && j.Tutorial){
                Shader.SetGlobalMatrix("_P5JourneyInverse",j.Root.worldToLocalMatrix);
                Shader.SetGlobalVector("_P5SpaceEnds",new Vector4(0,j.pathLength,j.Tutorial.ExitZ,j.RoomStart));
                Shader.SetGlobalFloat("_P5AscentStart",j.RoomStart+j.roomSize.z);
            }
            var passage=source.entry.outdoor;
            Shader.SetGlobalFloat("_P5ChapterMode",passage && passage.InChapter?2:passage && passage.IsOutside?1:0);
            if(passage && passage.InChapter){
                var room=passage.Vitruvian.roomFill;
                Shader.SetGlobalVector("_P5VitruvianCentre",room.transform.TransformPoint(room.interior.center));
                Shader.SetGlobalVector("_P5VitruvianExtents",Vector3.Scale(room.interior.extents,room.transform.lossyScale));
                var gate=passage.Chapter.gardenThreshold;
                Shader.SetGlobalVector("_P5GardenPlane",new Vector4(gate.forward.x,gate.forward.y,gate.forward.z,-Vector3.Dot(gate.forward,gate.position)));
            }
            for(int i=0;i<localLevels.Length;i++){
                var space=(PassageJourney.LightSpace)(1<<i);
                var v=localLevels[i];
                float stepSize=Time.unscaledDeltaTime/FadeSeconds;
                v.y=Mathf.MoveTowards(v.y,source.HasEffect(CognitionSignal.IlluminateDoor,space)?1:0,stepSize);
                v.z=Mathf.MoveTowards(v.z,source.HasEffect(CognitionSignal.IlluminateFloor,space)?1:0,stepSize);
                v.w=Mathf.MoveTowards(v.w,source.HasEffect(CognitionSignal.IlluminateLight,space)?1:0,stepSize);
                localLevels[i]=v;
            }
            Shader.SetGlobalVectorArray("_P5SpaceSentenceGlow",localLevels);
            if(levels.Equals(targets))return;
            float step=Time.unscaledDeltaTime/FadeSeconds;
            levels.x=Mathf.MoveTowards(levels.x,targets.x,step);
            levels.y=Mathf.MoveTowards(levels.y,targets.y,step);
            levels.w=Mathf.MoveTowards(levels.w,targets.w,step);
            levels.z=Mathf.MoveTowards(levels.z,targets.z,step);
            RenderLevels();
        }

        void RenderLevels()
        {
            var passage=source.entry.outdoor;
            bool outside=passage&&passage.IsOutside;
            vignette.active=!outside;
            exposure.active=levels.x>0;
            exposure.postExposure.value=2.4f*Mathf.SmoothStep(0,1,levels.x);
            float door=Mathf.SmoothStep(0,1,levels.y);
            float floor=FloorIllumination;
            bloomVolume.weight=outside?0:door;
            float doorGlow=door*DoorGlowStrength;
            Shader.SetGlobalFloat("_P5DoorGlow",doorGlow*2.5f);
            Shader.SetGlobalFloat("_P5FloorGlow",floor*.85f);
            if(outside)
            {
                passage.World.doors.appearance.SetIlluminated(doorGlow);
                passage.World.SetFloorIlluminated(floor);
                passage.World.stateSource.SetLampMultiplier(LampMultiplier);
            }
        }
        void OnDestroy()
        {
            System.Array.Clear(localLevels,0,localLevels.Length);
            Shader.SetGlobalVectorArray("_P5SpaceSentenceGlow",localLevels);
            Shader.SetGlobalFloat("_P5DoorGlow",0);
            Shader.SetGlobalFloat("_P5FloorGlow",0);
            if(bloomProfile){foreach(var component in bloomProfile.components)Destroy(component);Destroy(bloomProfile);}
            if(profile)
            {
                foreach(var component in profile.components)Destroy(component);
                Destroy(profile);
            }
        }
    }
}
