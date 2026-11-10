using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace PuzzleApple.V5
{
    public sealed class OutdoorPassage : MonoBehaviour
    {
        public WorldEntry entry;
        public bool automaticTransition=true;
        [Min(.1f)] public float exitSpeed=14,turnDuration=.85f;
        public bool Transitioning {get;private set;}
        public Outdoor.OutdoorWorld World {get;private set;}
        public bool IsOutside=>World&&World.Active;
        public int TransitionCount {get;private set;}
        public float LastExitSpeed {get;private set;}
        public Vector3 LastExitVelocity {get;private set;}
        readonly Dictionary<Renderer,bool> hidden=new Dictionary<Renderer,bool>();
        readonly Dictionary<Behaviour,bool> disabled=new Dictionary<Behaviour,bool>();
        Scene outdoorScene;
        RectTransform top,bottom;
        Image veil;
        bool tunnelBuilt;
        GameObject preparedRoot;
        Outdoor.OutdoorWorld preparedWorld;
        bool preparing,prepareFailed,warmed;
        public bool Prepared=>preparedRoot;
        public bool RenderingWarmed=>warmed;
        public float PreparationMilliseconds {get;private set;}
        public float ActivationMilliseconds {get;private set;}
        IEnumerator Start(){yield return Prepare();}
        IEnumerator Prepare()
        {
            if(preparing){while(preparing)yield return null;yield break;}
            if(preparedRoot||prepareFailed)yield break;
            preparing=true;
            var timer=System.Diagnostics.Stopwatch.StartNew();
            var request=Resources.LoadAsync<GameObject>("V5/Outdoor/OutdoorWorld");
            yield return request;
            var prefab=request.asset as GameObject;
            if(!prefab){prepareFailed=true;preparing=false;yield break;}
            outdoorScene=SceneManager.CreateScene("V5 Outdoor runtime");
            // An inactive parent suppresses Awake/OnEnable until the isolated warmup.
            var holder=new GameObject("Outdoor preparation");holder.SetActive(false);
            SceneManager.MoveGameObjectToScene(holder,outdoorScene);
            preparedRoot=Instantiate(prefab,holder.transform);
            preparedRoot.SetActive(false);preparedRoot.transform.SetParent(null,false);
            preparedRoot.transform.SetPositionAndRotation(new Vector3(2000,0,0),Quaternion.identity);
            Destroy(holder);
            preparedWorld=preparedRoot.GetComponent<Outdoor.OutdoorWorld>();preparedWorld.PrepareResources();
            // Do not interrupt active indoor gameplay if the player bypassed the menu quickly.
            if(!entry.Entered)WarmRendering();
            PreparationMilliseconds=(float)timer.Elapsed.TotalMilliseconds;preparing=false;
        }
        void WarmRendering()
        {
            if(warmed||!preparedRoot)return;
            var previousScene=SceneManager.GetActiveScene();
            var behaviours=preparedRoot.GetComponentsInChildren<MonoBehaviour>(true);
            var states=new bool[behaviours.Length];
            for(int i=0;i<behaviours.Length;i++){states[i]=behaviours[i].enabled;behaviours[i].enabled=false;}
            var go=new GameObject("Outdoor render warmup",typeof(Camera));
            SceneManager.MoveGameObjectToScene(go,outdoorScene);
            var camera=go.GetComponent<Camera>();camera.enabled=false;
            camera.CopyFrom(entry.player.view.GetComponent<Camera>());camera.enabled=false;
            camera.clearFlags=CameraClearFlags.Skybox;camera.farClipPlane=1500;
            var data=go.AddComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>();data.renderPostProcessing=true;
            var target=new RenderTexture(128,128,24,RenderTextureFormat.DefaultHDR);
            target.Create();camera.targetTexture=target;
            try
            {
                SceneManager.SetActiveScene(outdoorScene);preparedRoot.SetActive(true);
                RenderSettings.sun=preparedWorld.sun;RenderSettings.skybox=preparedWorld.environment.daySky;
                RenderSettings.ambientMode=UnityEngine.Rendering.AmbientMode.Trilight;
                RenderSettings.ambientSkyColor=preparedWorld.environment.dayAmbientSky;
                RenderSettings.ambientEquatorColor=preparedWorld.environment.dayAmbientEquator;
                RenderSettings.ambientGroundColor=preparedWorld.environment.dayAmbientGround;
                preparedWorld.clouds.UpdateProjection();
                go.transform.SetPositionAndRotation(preparedWorld.exit.position,preparedWorld.exit.rotation);
                camera.Render();warmed=true;
            }
            finally
            {
                preparedRoot.SetActive(false);
                for(int i=0;i<behaviours.Length;i++)if(behaviours[i])behaviours[i].enabled=states[i];
                SceneManager.SetActiveScene(previousScene);camera.targetTexture=null;
                target.Release();Destroy(target);Destroy(go);
            }
        }
        void Update()
        {
            if(IsOutside||Transitioning||!entry||!entry.journey||!entry.journey.floorSurface)return;
            var j=entry.journey;
            if(!j.floorSurface.HoleOpen&&j.floorSurface.HoleProgress<=0)return;
            if(!tunnelBuilt)BuildTunnel();
            var p=j.Root.InverseTransformPoint(entry.player.transform.position);
            if(automaticTransition&&!entry.Busy&&!entry.ConfirmingExit&&p.y< -3&&Mathf.Abs(p.x)<j.floorSurface.holeSize*.5f&&
                Mathf.Abs(p.z-j.pathLength-j.roomSize.z*.5f)<j.floorSurface.holeSize*.5f)StartCoroutine(Travel());
        }
        void BuildTunnel()
        {
            tunnelBuilt=true;var j=entry.journey;float width=j.floorSurface.holeSize;
            var root=new GameObject("Downward passage").transform;root.SetParent(j.Root,false);
            root.localPosition=new Vector3(0,-4.2f,j.pathLength+j.roomSize.z*.5f);
            foreach(float side in new[]{-1f,1f})foreach(bool x in new[]{false,true})
            {
                var wall=GameObject.CreatePrimitive(PrimitiveType.Cube);wall.name="Passage lining";wall.transform.SetParent(root,false);
                wall.transform.localPosition=x?new Vector3(side*(width*.5f+.1f),0,0):new Vector3(0,0,side*(width*.5f+.1f));
                wall.transform.localScale=x?new Vector3(.2f,8,width+.4f):new Vector3(width,.2f*40,.2f);
                wall.GetComponent<Renderer>().sharedMaterial=j.blackSurround;
            }
        }
        IEnumerator Travel()
        {

            Transitioning=true;TransitionCount++;entry.sentenceRuntime.presentation.Cancel();
            var player=entry.player;var panel=entry.board.Panel;
            bool oldBlocked=panel.InputBlocked,oldMove=player.MovementLocked,oldCinematic=player.CinematicLocked;
            float falling=Mathf.Abs(player.VerticalVelocity);
            entry.board.CancelDrag();panel.SetOpen(false);panel.InputBlocked=true;player.CinematicLocked=true;player.MovementLocked=true;
            Cursor.visible=false;Cursor.lockState=CursorLockMode.None;CreateOverlay();
            try
            {
                var motor=player.GetComponent<CharacterController>();
                var rotation=player.view.rotation;
                for(float t=0;t<.35f;t+=Mathf.Min(Time.unscaledDeltaTime,.033f))
                {
                    float u=t/.35f;motor.Move(Vector3.down*Mathf.Max(8,falling)*Mathf.Min(Time.unscaledDeltaTime,.033f));
                    SetBars(Mathf.SmoothStep(0,1,u));veil.color=new Color(0,0,0,Mathf.SmoothStep(0,1,Mathf.InverseLerp(.35f,1,u)));
                    yield return null;
                }
                veil.color=Color.black;
                yield return null; // Present the cover before any fallback preparation.
                yield return Prepare();
                if(prepareFailed){Debug.LogError("V5 outdoor prefab missing. Run OutdoorSetup.Build before playing.");automaticTransition=false;yield break;}
                if(!warmed)WarmRendering();
                var activation=System.Diagnostics.Stopwatch.StartNew();
                preparedRoot.SetActive(true);Physics.SyncTransforms();World=preparedWorld;
                // The original rooms and progress stay alive; only their presentation stops.
                foreach(var r in entry.gameObject.scene.GetRootGameObjects())
                {
                    foreach(var renderer in r.GetComponentsInChildren<MeshRenderer>(true))
                        if(!renderer.transform.IsChildOf(player.transform)&&!renderer.transform.IsChildOf(entry.room.transform))
                        {hidden[renderer]=renderer.forceRenderingOff;renderer.forceRenderingOff=true;}
                    foreach(var b in r.GetComponentsInChildren<Behaviour>(true))
                        if(b is RoomLighting||b is ChamberViewIsolation||b is UnityEngine.Rendering.Volume||b is Light)
                        {disabled[b]=b.enabled;b.enabled=false;}
                }
                entry.journey.enabled=false;entry.journey.movementObservation.Available=false;
                if(entry.chamberLight.observer)entry.chamberLight.observer.enabled=false;
                SceneManager.SetActiveScene(outdoorScene);
                World.Bind(entry);entry.sentenceRuntime.EnterOutdoor();
                var exit=World.exit;player.Teleport(exit.position,exit.rotation);
                ActivationMilliseconds=(float)activation.Elapsed.TotalMilliseconds;
                // Allow the real camera, sky, cookies and audio to settle under full cover.
                yield return new WaitForEndOfFrame();yield return null;
                var forward=Vector3.ProjectOnPlane(exit.forward,Vector3.up).normalized;
                LastExitSpeed=Mathf.Clamp(Mathf.Max(exitSpeed,falling),exitSpeed,22);
                Vector3 velocity=forward*LastExitSpeed+Vector3.up*3;
                LastExitVelocity=velocity;
                var start=Quaternion.LookRotation((forward*.4f+Vector3.down).normalized,Vector3.up);
                var target=Quaternion.LookRotation(forward,Vector3.up);
                for(float t=0;t<turnDuration;t+=Mathf.Min(Time.unscaledDeltaTime,.033f))
                {
                    float dt=Mathf.Min(Time.unscaledDeltaTime,.05f),u=Mathf.Clamp01(t/turnDuration);
                    velocity+=Vector3.down*20*dt;
                    var flags=motor.Move(velocity*dt);
                    if((flags&CollisionFlags.Below)!=0&&velocity.y<0)velocity.y=-2;
                    player.view.rotation=Quaternion.Slerp(start,target,Mathf.SmoothStep(0,1,u));
                    veil.color=new Color(0,0,0,1-Mathf.SmoothStep(0,1,u/.3f));
                    SetBars(1-Mathf.SmoothStep(0,1,Mathf.InverseLerp(.6f,1,u)));
                    yield return null;
                }
                player.view.rotation=target;player.AdoptCurrentView();player.Launch(velocity);
            }
            finally
            {
                SetBars(0);if(veil)veil.gameObject.SetActive(false);
                player.CinematicLocked=oldCinematic;player.MovementLocked=oldMove;
                panel.InputBlocked=oldBlocked;player.SetPanelOpen(false);player.SuppressInteractionThisFrame();Transitioning=false;
            }
        }
        void CreateOverlay()
        {
            if(top){veil.gameObject.SetActive(true);return;}
            var go=new GameObject("Passage cinematic",typeof(RectTransform),typeof(Canvas));go.transform.SetParent(transform,false);
            var canvas=go.GetComponent<Canvas>();canvas.renderMode=RenderMode.ScreenSpaceOverlay;canvas.sortingOrder=45;
            top=Image(go.transform,"Top").rectTransform;bottom=Image(go.transform,"Bottom").rectTransform;
            veil=Image(go.transform,"Transition");veil.rectTransform.anchorMin=Vector2.zero;veil.rectTransform.anchorMax=Vector2.one;
            veil.rectTransform.offsetMin=veil.rectTransform.offsetMax=Vector2.zero;veil.color=Color.clear;
        }
        static Image Image(Transform parent,string name)
        {var go=new GameObject(name,typeof(RectTransform),typeof(Image));go.transform.SetParent(parent,false);var image=go.GetComponent<Image>();image.color=Color.black;image.raycastTarget=false;return image;}
        void SetBars(float a)
        {
            if(!top)return;top.anchorMin=new Vector2(0,1-.09f*a);top.anchorMax=Vector2.one;
            bottom.anchorMin=Vector2.zero;bottom.anchorMax=new Vector2(1,.09f*a);top.offsetMin=top.offsetMax=bottom.offsetMin=bottom.offsetMax=Vector2.zero;
        }
        public bool TryWorldDrop(Cognition.CognitionBoard board,string word,Vector2 screen)
        {
            if(word!="light"||!IsOutside||Transitioning||board!=entry.board||!board.Panel.IsOpen||board.Panel.InputBlocked||!board.State.Knows(word))return false;
            if(screen.x<0||screen.y<0||screen.x>Screen.width||screen.y>Screen.height)return false;
            if(RectTransformUtility.RectangleContainsScreenPoint(board.Panel.ContentRoot,screen))return false;
            return World.TrySpawnInView(screen);
        }
        void OnDestroy()
        {
            foreach(var pair in hidden)if(pair.Key)pair.Key.forceRenderingOff=pair.Value;
            foreach(var pair in disabled)if(pair.Key)pair.Key.enabled=pair.Value;
            if(outdoorScene.IsValid()&&outdoorScene.isLoaded&&Application.isPlaying)SceneManager.UnloadSceneAsync(outdoorScene);
        }
    }
}
