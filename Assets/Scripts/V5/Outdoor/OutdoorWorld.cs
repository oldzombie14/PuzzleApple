using System.Linq;
using PuzzleApple.V5.Cognition;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace PuzzleApple.V5.Outdoor
{
    public sealed class OutdoorWorld : MonoBehaviour
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
        public Transform exit;
        [Min(1)] public float airSpawnDistance=8;
        [Min(0)] public float spawnSearchRadius=2,minimumLampSpacing=1.2f;
        [Range(1,64)] public int spawnAttempts=32;
        [Min(0)] public float spawnClearance=.06f;
        public WorldEntry Entry {get;private set;}
        public SessionAudio Audio {get;private set;}
        public bool Active {get;private set;}
        bool ownsMaterials;
        Material groundMaterial, luminousGround;
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
            // The V5 copies are assets. Runtime cloud projection must never dirty them.
            PrepareResources();
            RenderSettings.sun=sun;RenderSettings.skybox=environment.daySky;
            var camera=entry.player.view.GetComponent<Camera>();camera.clearFlags=CameraClearFlags.Skybox;camera.farClipPlane=1500;
            var data=camera.GetComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>();if(data)data.renderPostProcessing=true;
            Audio=gameObject.AddComponent<SessionAudio>();Audio.Play(music);
            // Keep one board, its acquisition IDs, notes and sentences across the passage.
            foreach(var id in new[]{"light","floor"})if(!entry.board.State.Knows(id))entry.board.State.TryAcquireWord("outdoor.previous."+id,entry.board.Catalog.Word(id));
            ApplyMotion(entry.board.State.HasEffect,false);
        }
        public void ApplyMotion(System.Func<CognitionSignal,bool> hasEffect,bool split)
        {
            lights.Apply(hasEffect);doors.Apply(hasEffect,split);world.Apply(hasEffect);
        }
        public string LookedAtWord(Ray ray)=>LookedAtWord(ray,out _);
        public string LookedAtWord(Ray ray,out Renderer emitter)
        {
            emitter=null;
            if(!Active)return null;
            var camera=Entry.player.view.GetComponent<Camera>();
            var hits=Physics.RaycastAll(ray,camera.farClipPlane,~0,QueryTriggerInteraction.Ignore);
            var hit=hits.Where(h=>!h.collider.transform.IsChildOf(Entry.player.transform)).OrderBy(h=>h.distance).FirstOrDefault();
            if(hit.collider)
            {
                if(hit.collider==ground&&hit.distance<=7)return "floor";
                var lamp=hit.collider.GetComponentInParent<OpeningLight>();
                if(lamp&&lamp!=stateSource&&lamp.TargetOn&&lamp.Brightness>=.999f&&lamp.RevealAmount>=.99f&&hit.distance<=7){emitter=lamp.surface;return "light";}
                if(hit.collider.GetComponentInParent<ThresholdDoor>()==door&&hit.distance<=door.collectionDistance)return "door";
                return null;
            }
            // Same visible-disc limit as V4; no selection through foreground geometry.
            return stateSource.TargetOn&&stateSource.Brightness>=.999f&&environment.NightBlend<.98f&&
                Vector3.Dot(ray.direction,-sun.transform.forward)>=.9995f?"light":null;
        }
        public bool TrySpawnInView(Vector2 screen)
        {
            if(!Active||!Entry||!Entry.outdoor||Entry.outdoor.Transitioning||Entry.Busy||Entry.ConfirmingExit||
                Entry.portableClosure&&Entry.portableClosure.Isolated)return false;
            var camera=Entry.player.view.GetComponent<Camera>();
            var ray=camera.ScreenPointToRay(screen);
            float radius=field.scaleRange.y*.8661f+spawnClearance;
            Vector3 preferred=ray.GetPoint(airSpawnDistance);
            if(Physics.Raycast(ray,out var hit,35,~0,QueryTriggerInteraction.Ignore))preferred=hit.point+hit.normal*radius;
            preferred.y=Mathf.Max(preferred.y,ground.bounds.max.y+radius);
            for(int i=0;i<spawnAttempts;i++)
            {
                // Search from near to far; spacing constrains creation, never later clustering.
                var point=preferred+(i==0?Vector3.zero:Random.onUnitSphere*spawnSearchRadius*Mathf.Sqrt((float)i/spawnAttempts));
                if(point.y<ground.bounds.max.y+radius||(point-camera.transform.position).sqrMagnitude<1)continue;
                if(!ground.Raycast(new Ray(point+Vector3.up*2,Vector3.down),out var support,1000))continue;
                var view=camera.WorldToViewportPoint(point);
                if(view.z<=0||view.x<.05f||view.x>.95f||view.y<.05f||view.y>.95f)continue;
                if(Physics.CheckSphere(point,radius,~0,QueryTriggerInteraction.Ignore))continue;
                if(field.ActiveLamps.Any(l=>l&&(l.transform.position-point).sqrMagnitude<minimumLampSpacing*minimumLampSpacing))continue;
                var delta=point-camera.transform.position;
                if(Physics.Raycast(camera.transform.position,delta.normalized,delta.magnitude-.1f,~0,QueryTriggerInteraction.Ignore))continue;
                return field.Spawn(point)!=null;
            }
            return false;
        }
        public Vector3 Focus(SentencePresentation.Focus focus)
        {
            if(focus==SentencePresentation.Focus.Sky)return Entry.player.view.position+Vector3.ProjectOnPlane(Entry.player.view.forward,Vector3.up).normalized*10+Vector3.up*6;
            if(focus==SentencePresentation.Focus.Door)return door.transform.TransformPoint(new Vector3(0,3,0));
            if(focus==SentencePresentation.Focus.Floor)return Entry.player.view.position+Vector3.ProjectOnPlane(Entry.player.view.forward,Vector3.up).normalized*10+Vector3.down*2;
            var candidates=field.ActiveLamps.Where(l=>l&&(l.transform.position-Entry.player.transform.position).sqrMagnitude<2500).ToArray();
            if(candidates.Length>0)return candidates.Aggregate(Vector3.zero,(p,l)=>p+l.transform.position)/candidates.Length;
            return Entry.player.view.position-sun.transform.forward*100;
        }
        void OnDestroy()
        {
            if(luminousGround)Destroy(luminousGround);
            if(ownsMaterials&&clouds&&clouds.cloudPlane)Destroy(clouds.cloudPlane.sharedMaterial);
            if(ownsMaterials&&environment&&environment.daySky)Destroy(environment.daySky);
        }
    }
}
