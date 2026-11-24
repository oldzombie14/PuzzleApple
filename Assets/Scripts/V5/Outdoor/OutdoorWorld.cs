using System.Linq;
using PuzzleApple.V5.Cognition;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace PuzzleApple.V5.Outdoor
{
    public sealed class OutdoorWorld : MonoBehaviour, ISwitchableLight, ISwitchableDoor, ISentenceMovable, IBrightenableLight, ISentenceOutputState<OutdoorWorld.LightState>
    {
        public Collider ground;
        public Light sun;
        public CloudLightCookie clouds;
        public NightEnvironment environment;
        public OpeningLight stateSource;
        public OpeningLightField field;
        public ThresholdDoor door;
        public LightSentenceEffects lights;
        public DoorSentenceEffects doors;
        public WorldSentenceEffects world;
        public MusicTrack music;
        public Transform exit, chapterFrame;
        public ChapterWordMarker IntroWord {get;private set;}
        public ChapterWordMarker ChapterWord {get;private set;}
        [Min(1)] public float airSpawnDistance=8;
        [Min(0)] public float spawnSearchRadius=2,minimumLampSpacing=1.2f;
        [Range(1,64)] public int spawnAttempts=32;
        [Min(0)] public float spawnClearance=.06f;
        public WorldEntry Entry {get;private set;}
        public SessionAudio Audio {get;private set;}
        public bool Active {get;private set;}
        bool ownsMaterials;
        Material groundMaterial, luminousGround;
        // Outdoor light state: the lamps follow stateSource and the sky follows them into night.
        [System.Serializable]
        public struct LightState
        {
            public EmissionState lamps;
            public float nightBlend;
        }
        public LightState CaptureState() => new LightState { lamps = stateSource.CaptureState(), nightBlend = environment.NightBlend };
        public bool TryRestoreState(LightState state)
        {
            if (!environment || !stateSource || !(state.nightBlend >= 0 && state.nightBlend <= 1) ||
                !stateSource.TryRestoreState(state.lamps)) return false;
            environment.RestoreBlend(state.nightBlend);
            return true;
        }
        bool ISwitchableLight.WouldChange(bool on)=>on
            ?stateSource.Brightness<.999f||environment.NightBlend>.001f
            :stateSource.Brightness>.001f||environment.NightBlend<.999f;
        bool ISwitchableLight.Settling(bool on)=>stateSource.Brightness!=(on?1:0) || environment.NightBlend!=(on?0:1);
        void ISwitchableLight.SetOn(bool on)=>stateSource.SetOn(on);
        void ISwitchableLight.Hold()=>stateSource.HoldCurrentState();
        bool IBrightenableLight.Lit=>stateSource.Brightness>0;
        // The registered emission output drives the threshold door's brightness transition.
        bool ISwitchableDoor.WouldChange(bool open)=>door.Revealed&&(open?doors.appearance.Brightness<.999f:doors.appearance.Brightness>.001f);
        void ISwitchableDoor.SetOpen(bool open)=>doors.SetState(open);
        void ISwitchableDoor.Hold()=>doors.HoldState();
        void ISwitchableDoor.Release()=>doors.HoldState();
        // Outdoor motion: lamps, the threshold door and the ground each move by their own effects.
        bool ISentenceMovable.CanMove(SentenceMotion motion)
        {
            if (motion.Subject == SentenceCapabilityCatalog.Default.light) return lights.CanMove(motion);
            if (motion == SentenceMotion.FloorToLight || motion == SentenceMotion.FloorToDoor) return world.CanRise(motion);
            if (motion == SentenceMotion.Door) return door.Revealed;
            return motion == SentenceMotion.Floor;
        }
        public void SetFloorIlluminated(float value)
        {
            if(value==0&&!luminousGround)return;
            var surface=ground.GetComponent<Renderer>();
            if(!luminousGround)
            {
                groundMaterial=surface.sharedMaterial;
                luminousGround=new Material(groundMaterial);
                luminousGround.EnableKeyword("_EMISSION");

            }
            luminousGround.SetColor("_EmissionColor",Color.white*(.85f*value));
            surface.sharedMaterial=value>0?luminousGround:groundMaterial;
        }
        public void PrepareResources()
        {
            if(ownsMaterials)return;
            clouds.cloudPlane.sharedMaterial=new Material(clouds.cloudPlane.sharedMaterial);
            environment.daySky=new Material(environment.daySky);clouds.sky=environment.daySky;ownsMaterials=true;
        }
        public void Bind(WorldEntry entry)
        {
            Entry=entry;Active=true;
            field.observer=entry.player.transform;clouds.player=entry.player.transform;
            lights.observer=entry.player.transform;world.player=entry.player;
            door.InitializeAtExit(entry.player.view);
            var frame=door.transform;
            var left=FrameBounds(frame,frame.Find("Left pier").GetComponent<MeshFilter>());
            var right=FrameBounds(frame,frame.Find("Right pier").GetComponent<MeshFilter>());
            var sill=FrameBounds(frame,frame.Find("Sill").GetComponent<MeshFilter>());
            var lintel=FrameBounds(frame,frame.Find("Lintel").GetComponent<MeshFilter>());
            var openingCenter=new Vector3((left.max.x+right.min.x)*.5f,(sill.max.y+lintel.min.y)*.5f,(sill.center.z+lintel.center.z)*.5f);
            var pictureCenter=FrameBounds(chapterFrame,chapterFrame.GetComponentInChildren<MeshFilter>()).center;
            IntroWord=new GameObject("Intro core word").AddComponent<ChapterWordMarker>();
            IntroWord.transform.SetParent(frame,false);IntroWord.Build("light",1.03f,openingCenter);
            ChapterWord=new GameObject("Chapter I core word").AddComponent<ChapterWordMarker>();
            ChapterWord.transform.SetParent(chapterFrame,false);ChapterWord.Build("mirror",.64f,pictureCenter);
            // The V5 copies are assets. Runtime cloud projection must never dirty them.
            PrepareResources();
            RenderSettings.sun=sun;RenderSettings.skybox=environment.daySky;
            var camera=entry.player.view.GetComponent<Camera>();camera.clearFlags=CameraClearFlags.Skybox;camera.farClipPlane=1500;
            var data=camera.GetComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>();if(data)data.renderPostProcessing=true;
            Audio=entry.gameObject.AddComponent<SessionAudio>();Audio.Play(music);
            // Keep one board, its acquisition IDs, notes and sentences across the passage.
            SentenceSceneBindings.BindOutdoor(this);
        }
        static Bounds FrameBounds(Transform frame,MeshFilter surface)
        {
            // Measure in the frame's own axes so rotated and scaled parents keep the same anchor.
            var mesh=surface.sharedMesh.bounds;
            var matrix=frame.worldToLocalMatrix*surface.transform.localToWorldMatrix;
            var bounds=new Bounds(matrix.MultiplyPoint3x4(mesh.center),Vector3.zero);
            for(int x=-1;x<=1;x+=2)for(int y=-1;y<=1;y+=2)for(int z=-1;z<=1;z+=2)
                bounds.Encapsulate(matrix.MultiplyPoint3x4(mesh.center+Vector3.Scale(mesh.extents,new Vector3(x,y,z))));
            return bounds;
        }
        public string LookedAtWord(Ray ray)=>LookedAtWord(ray,out _);
        public string LookedAtWord(Ray ray,out Renderer emitter)=>LookedAtWord(ray,out emitter,out _);
        public string LookedAtWord(Ray ray,out Renderer emitter,out Collider target)
        {
            emitter=null;target=null;
            if(!Active)return null;
            var camera=Entry.player.view.GetComponent<Camera>();
            var hits=Physics.RaycastAll(ray,camera.farClipPlane,~0,QueryTriggerInteraction.Ignore);
            var hit=hits.Where(h=>!h.collider.transform.IsChildOf(Entry.player.transform)).OrderBy(h=>h.distance).FirstOrDefault();
            target=hit.collider;
            if(hit.collider)
            {
                if(hit.collider==ground&&hit.distance<=7&&Entry.board.State.Knows("floor"))return "floor";
                if(chapterFrame&&hit.collider.transform.IsChildOf(chapterFrame)&&hit.distance<=7)return "door";
                var lamp=hit.collider.GetComponentInParent<OpeningLight>();
                if(lamp&&lamp!=stateSource&&lamp.EmitsLight&&hit.distance<=7){emitter=lamp.surface;return "light";}
                if(hit.collider.GetComponentInParent<ThresholdDoor>()==door&&hit.distance<=door.collectionDistance)return "door";
                return null;
            }
            // Same visible-disc limit as V4; no selection through foreground geometry.
            return stateSource.Brightness>0&&environment.NightBlend<1&&
                Vector3.Dot(ray.direction,-sun.transform.forward)>=.9995f?"light":null;
        }
        public bool TrySpawnInView(Vector2 screen){if(!Active || !Entry)return false;var camera=Entry.player.view.GetComponent<Camera>();return TrySpawnInView(camera.ScreenPointToRay(screen),GeometryUtility.CalculateFrustumPlanes(camera),InstanceShape.Cube,GetComponent<WorldDropRegion>());}
        public bool TrySpawnInView(Ray ray,Plane[] frustum,InstanceShape shape,WorldDropRegion region)
        {
            if(!Active||!Entry||!Entry.outdoor||Entry.outdoor.Transitioning||Entry.Busy||Entry.ConfirmingExit||
                Entry.portableClosure&&Entry.portableClosure.Isolated)return false;
            return TryPlaceLamp(ray,frustum,shape,region,field,ground);
        }
        public bool TryPlaceLamp(Ray ray,Plane[] frustum,InstanceShape shape,WorldDropRegion region,OpeningLightField field,Collider ground)
        {
            if(!region || !region.Accepts("light") || System.Array.IndexOf(region.shapes,shape)<0 || PlayerInstance.Remaining<=0)return false;
            float radius=OpeningLightField.CreatedDiameter*.5f+spawnClearance;
            Vector3 preferred=ray.GetPoint(airSpawnDistance);
            if(Physics.Raycast(ray,out var hit,35,~0,QueryTriggerInteraction.Ignore))preferred=hit.point+hit.normal*radius;
            preferred.y=Mathf.Max(preferred.y,ground.bounds.max.y+radius);
            for(int i=0;i<spawnAttempts;i++)
            {
                // Search from near to far; spacing constrains creation, never later clustering.
                var point=preferred+(i==0?Vector3.zero:Random.onUnitSphere*spawnSearchRadius*Mathf.Sqrt((float)i/spawnAttempts));
                if(point.y<ground.bounds.max.y+radius||(point-ray.origin).sqrMagnitude<1)continue;
                if(!ground.Raycast(new Ray(point+Vector3.up*2,Vector3.down),out var support,1000))continue;
                if(!GeometryUtility.TestPlanesAABB(frustum,new Bounds(point,Vector3.one*radius*2)))continue;
                if(Physics.CheckSphere(point,radius,~0,QueryTriggerInteraction.Ignore))continue;
                if(field.ActiveLamps.Any(l=>l&&(l.transform.position-point).sqrMagnitude<minimumLampSpacing*minimumLampSpacing))continue;
                var delta=point-ray.origin;
                if(Physics.Raycast(ray.origin,delta.normalized,delta.magnitude-.1f,~0,QueryTriggerInteraction.Ignore))continue;
                return field.Spawn(point,0,shape,region)!=null;
            }
            return false;
        }
        void OnDestroy()
        {
            if(luminousGround)Destroy(luminousGround);
            if(ownsMaterials&&clouds&&clouds.cloudPlane)Destroy(clouds.cloudPlane.sharedMaterial);
            if(ownsMaterials&&environment&&environment.daySky)Destroy(environment.daySky);
        }
    }
}
