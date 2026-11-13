using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using PuzzleApple.V5.Cognition;

namespace PuzzleApple.V5
{
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
        public float FloorIllumination=>Mathf.SmoothStep(0,1,levels.z);
        public float LampMultiplier => 1 + Mathf.SmoothStep(0,1,levels.w);
        const float FadeSeconds=1.4f;

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
            if(!source||levels.Equals(targets))return;
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
            Shader.SetGlobalFloat("_P5DoorGlow",door*2.5f);
            Shader.SetGlobalFloat("_P5FloorGlow",floor*.85f);
            if(outside)
            {
                passage.World.doors.appearance.SetIlluminated(door);
                passage.World.SetFloorIlluminated(floor);
                passage.World.stateSource.SetLampMultiplier(LampMultiplier);
            }
        }
        void OnDestroy()
        {
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
