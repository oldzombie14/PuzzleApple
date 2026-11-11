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
        [Min(.1f)] public float exitSpeed=4.5f,turnDuration=.85f;
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
        RawImage crossingFrame;
        public const float TransferDepth=-.8f;
        const float WindowDepth=TransferDepth;
        bool tunnelBuilt;
        Renderer window;
        Camera windowCamera;
        RenderTexture windowTexture;
        Material windowMaterial;
        readonly Plane[] viewPlanes=new Plane[6];
        readonly List<Behaviour> previewDisabled=new List<Behaviour>();
        float nextWindowFrame;
        public int TransferredLamps {get;private set;}
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
            PreparationMilliseconds=(float)timer.Elapsed.TotalMilliseconds;preparing=false;
        }
        void StartWindow()
        {
            if(windowCamera||!preparedWorld)return;
            // Keep outdoor physics live without letting its weather, music or lights own the indoor scene.
            foreach(var behaviour in preparedRoot.GetComponentsInChildren<Behaviour>(true))
            {
                if(!behaviour.enabled||behaviour is Outdoor.OpeningLight||behaviour is Outdoor.OpeningLightField)continue;
                previewDisabled.Add(behaviour);behaviour.enabled=false;
            }
            preparedWorld.field.observer=entry.player.transform;
            preparedRoot.SetActive(true);
            Physics.SyncTransforms();
            var go=new GameObject("Outdoor view through floor",typeof(Camera));
            SceneManager.MoveGameObjectToScene(go,outdoorScene);
            windowCamera=go.GetComponent<Camera>();windowCamera.enabled=false;
            windowCamera.clearFlags=CameraClearFlags.Skybox;windowCamera.farClipPlane=1500;
            windowTexture=new RenderTexture(512,512,24,RenderTextureFormat.ARGB32){name="Floor opening outdoor view"};
            windowTexture.Create();windowCamera.targetTexture=windowTexture;
            windowMaterial=new Material(entry.journey.blackSurround);
            windowMaterial.SetColor("_BaseColor",Color.white);
            windowMaterial.SetTexture("_BaseMap",windowTexture);
            window.sharedMaterial=windowMaterial;
            RenderWindow();
        }
        Vector3 OutletCentre()
        {
            var floor=entry.journey.floorSurface;
            var centre=preparedWorld.exit.position;
            centre.y=Mathf.Max(centre.y,preparedWorld.ground.bounds.max.y+floor.HoleWidth*.5f+.15f);
            return centre;
        }
        void RenderWindow()
        {
            var j=entry.journey;var floor=j.floorSurface;
            var eye=j.Root.InverseTransformPoint(entry.player.view.position);
            float half=floor.HoleWidth*.5f;
            float x=Mathf.Clamp(eye.x,-half,half);
            // Limit vertical parallax so the horizon and ground remain visible from the near lip.
            float y=Mathf.Clamp((eye.z-floor.HoleCentreZ)*.25f,-half*.35f,half*.35f);
            float depth=Mathf.Clamp(eye.y-WindowDepth,.35f,6);
            var outlet=preparedWorld.exit;
            windowCamera.transform.SetPositionAndRotation(OutletCentre()+outlet.right*x+outlet.up*y-outlet.forward*depth,outlet.rotation);
            windowCamera.nearClipPlane=depth;
            windowCamera.projectionMatrix=Matrix4x4.Frustum(-half-x,half-x,-half-y,half-y,depth,windowCamera.farClipPlane);
            var previous=SceneManager.GetActiveScene();
            try
            {
                SceneManager.SetActiveScene(outdoorScene);
                preparedWorld.sun.enabled=true;
                RenderSettings.sun=preparedWorld.sun;RenderSettings.skybox=preparedWorld.environment.daySky;
                RenderSettings.ambientMode=UnityEngine.Rendering.AmbientMode.Trilight;
                RenderSettings.ambientSkyColor=preparedWorld.environment.dayAmbientSky;
                RenderSettings.ambientEquatorColor=preparedWorld.environment.dayAmbientEquator;
                RenderSettings.ambientGroundColor=preparedWorld.environment.dayAmbientGround;
                preparedWorld.clouds.UpdateProjection();
                windowCamera.Render();warmed=true;
            }
            finally{preparedWorld.sun.enabled=false;SceneManager.SetActiveScene(previous);}
        }
        void LateUpdate()
        {
            if(!windowCamera||!window||IsOutside||Time.unscaledTime<nextWindowFrame)return;
            var j=entry.journey;
            if(!j.InSecondRoom||entry.portableClosure&&entry.portableClosure.Isolated)return;
            GeometryUtility.CalculateFrustumPlanes(entry.player.view.GetComponent<Camera>(),viewPlanes);
            if(!GeometryUtility.TestPlanesAABB(viewPlanes,window.bounds))return;
            nextWindowFrame=Time.unscaledTime+1f/24;
            RenderWindow();
        }
        public bool TransferLamp(DriftingLight source,Vector3 local,float fallingSpeed)
        {
            if(!preparedWorld||!preparedRoot.activeSelf)return false;
            var outlet=preparedWorld.exit;var floor=entry.journey.floorSurface;
            Vector3 position=OutletCentre()+outlet.right*local.x+outlet.up*(local.z-floor.HoleCentreZ)+outlet.forward*.25f;
            var lamp=preparedWorld.field.Spawn(position,source.transform.lossyScale.x);
            if(!lamp)return false;
            lamp.GetComponent<Outdoor.LightMotion>().Launch(outlet.forward*Mathf.Clamp(fallingSpeed,9.1f,22)+Vector3.up*3,preparedWorld.ground);
            TransferredLamps++;return true;
        }
        void ReleaseWindow()
        {
            if(windowCamera){windowCamera.targetTexture=null;Destroy(windowCamera.gameObject);}
            if(window)window.gameObject.SetActive(false);
            if(windowTexture){windowTexture.Release();Destroy(windowTexture);}
            if(windowMaterial)Destroy(windowMaterial);
        }
        void Update()
        {
            if(IsOutside||Transitioning||!entry||!entry.journey||!entry.journey.floorSurface)return;
            var j=entry.journey;
            if(!j.floorSurface.HoleOpen&&j.floorSurface.HoleProgress<=0)return;
            if(!tunnelBuilt)BuildTunnel();
            if(!windowCamera&&preparedWorld)StartWindow();
            var p=j.Root.InverseTransformPoint(entry.player.transform.position);
            if(automaticTransition&&!entry.Busy&&!entry.ConfirmingExit&&p.y<TransferDepth&&
                j.floorSurface.FitsOpening(p,0))StartCoroutine(Travel());
        }
        void BuildTunnel()
        {
            tunnelBuilt=true;var j=entry.journey;float width=j.floorSurface.HoleWidth;
            var root=new GameObject("Downward passage").transform;root.SetParent(j.Root,false);
            root.localPosition=new Vector3(0,-4.005f,j.floorSurface.HoleCentreZ);
            var quad=GameObject.CreatePrimitive(PrimitiveType.Quad);quad.name="View through floor opening";
            quad.transform.SetParent(j.Root,false);
            quad.transform.localPosition=new Vector3(0,WindowDepth,j.floorSurface.HoleCentreZ);
            quad.transform.localRotation=Quaternion.Euler(90,0,0);
            quad.transform.localScale=new Vector3(width,width,1);
            var collider=quad.GetComponent<Collider>();collider.enabled=false;Destroy(collider);
            window=quad.GetComponent<Renderer>();window.sharedMaterial=j.blackSurround;
            window.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;
            var lining=new MaterialPropertyBlock();
            lining.SetFloat("_SurfaceMode",0);
            lining.SetFloat("_EdgeWidth",0);
            lining.SetFloat("_LiningShade",.8f);
            foreach(float side in new[]{-1f,1f})foreach(bool x in new[]{false,true})
            {
                var wall=GameObject.CreatePrimitive(PrimitiveType.Cube);wall.name="Passage lining";wall.transform.SetParent(root,false);
                wall.transform.localPosition=x?new Vector3(side*(width*.5f+.1f),0,0):new Vector3(0,0,side*(width*.5f+.1f));
                wall.transform.localScale=x?new Vector3(.2f,8,width+.4f):new Vector3(width,.2f*40,.2f);
                var renderer=wall.GetComponent<Renderer>();
                renderer.sharedMaterial=j.floorSurface.floorMaterial;
                renderer.SetPropertyBlock(lining);
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
            RenderTexture lastIndoor=null;
            try
            {
                var motor=player.GetComponent<CharacterController>();
                if(!preparedRoot)yield return Prepare();
                if(prepareFailed){Debug.LogError("V5 outdoor prefab missing. Run OutdoorSetup.Build before playing.");automaticTransition=false;yield break;}
                if(!windowCamera)StartWindow();
                for(float elapsed=0;elapsed<.2f;)
                {
                    float dt=Mathf.Min(Time.unscaledDeltaTime,.033f);
                    elapsed+=dt;
                    // Keep the camera on the visible side of the portal until the handoff.
                    float clearance=entry.journey.Root.InverseTransformPoint(player.view.position).y-WindowDepth-.18f;
                    motor.Move(Vector3.down*Mathf.Min(Mathf.Max(0,clearance),falling*dt));
                    SetBars(Mathf.SmoothStep(0,1,elapsed/.2f));
                    yield return null;
                }
                var worldFrame=player.view.GetComponent<Camera>().targetTexture;
                if(worldFrame)
                {
                    var descriptor=worldFrame.descriptor;
                    descriptor.depthBufferBits=0;
                    descriptor.msaaSamples=1;
                    lastIndoor=RenderTexture.GetTemporary(descriptor);
                    Graphics.Blit(worldFrame,lastIndoor);
                    crossingFrame.texture=lastIndoor;
                    crossingFrame.color=Color.white;
                    crossingFrame.gameObject.SetActive(true);
                }
                ReleaseWindow();
                var activation=System.Diagnostics.Stopwatch.StartNew();
                preparedRoot.SetActive(true);Physics.SyncTransforms();World=preparedWorld;
                // The original rooms and progress stay alive; only their presentation stops.
                foreach(var r in entry.gameObject.scene.GetRootGameObjects())
                {
                    foreach(var renderer in r.GetComponentsInChildren<MeshRenderer>(true))
                        if(!renderer.transform.IsChildOf(player.transform)&&!renderer.transform.IsChildOf(entry.room.transform))
                        {hidden[renderer]=renderer.forceRenderingOff;renderer.forceRenderingOff=true;}
                    foreach(var b in r.GetComponentsInChildren<Behaviour>(true))
                        if(b is RoomLighting||b is UnityEngine.Rendering.Volume||b is Light)
                        {disabled[b]=b.enabled;b.enabled=false;}
                }
                entry.journey.enabled=false;entry.journey.movementObservation.Available=false;
                if(entry.chamberLight.observer)entry.chamberLight.observer.enabled=false;
                SceneManager.SetActiveScene(outdoorScene);
                foreach(var behaviour in previewDisabled)if(behaviour)behaviour.enabled=true;
                previewDisabled.Clear();
                World.Bind(entry);entry.sentenceRuntime.EnterOutdoor();
                var exit=World.exit;player.Teleport(exit.position,exit.rotation);
                ActivationMilliseconds=(float)activation.Elapsed.TotalMilliseconds;
                var forward=Vector3.ProjectOnPlane(exit.forward,Vector3.up).normalized;
                LastExitSpeed=exitSpeed*Mathf.Lerp(.8f,1,Mathf.Clamp01(falling/10));
                Vector3 velocity=forward*LastExitSpeed+Vector3.up*1.4f;
                LastExitVelocity=velocity;
                var start=Quaternion.LookRotation(forward+Vector3.down*.45f,Vector3.up);
                var target=Quaternion.LookRotation(forward,Vector3.up);
                player.view.rotation=start;
                // Render the outdoor pose once behind the held portal image.
                yield return new WaitForEndOfFrame();
                for(float elapsed=0;elapsed<turnDuration;)
                {
                    float dt=Mathf.Min(Time.unscaledDeltaTime,.033f);
                    elapsed+=dt;
                    float u=Mathf.Clamp01(elapsed/turnDuration);
                    velocity+=Vector3.down*20*dt;
                    var flags=motor.Move(velocity*dt);
                    if((flags&CollisionFlags.Below)!=0&&velocity.y<0)velocity.y=-2;
                    float horizontal=Mathf.MoveTowards(Vector3.Dot(velocity,forward),0,4*dt);
                    velocity=forward*horizontal+Vector3.up*velocity.y;
                    player.view.rotation=Quaternion.Slerp(start,target,Mathf.SmoothStep(0,1,u));
                    crossingFrame.color=new Color(1,1,1,1-Mathf.SmoothStep(0,1,elapsed/.18f));
                    SetBars(1-Mathf.SmoothStep(0,1,Mathf.InverseLerp(.6f,1,u)));
                    yield return null;
                }
                player.view.rotation=target;player.AdoptCurrentView();player.Launch(velocity);
            }
            finally
            {
                SetBars(0);
                crossingFrame.gameObject.SetActive(false);crossingFrame.texture=null;
                if(lastIndoor)RenderTexture.ReleaseTemporary(lastIndoor);
                player.CinematicLocked=oldCinematic;player.MovementLocked=oldMove;
                panel.InputBlocked=oldBlocked;player.SetPanelOpen(false);player.SuppressInteractionThisFrame();Transitioning=false;
            }
        }
        void CreateOverlay()
        {
            if(top)return;
            var go=new GameObject("Passage cinematic",typeof(RectTransform),typeof(Canvas));go.transform.SetParent(transform,false);
            var canvas=go.GetComponent<Canvas>();canvas.renderMode=RenderMode.ScreenSpaceOverlay;canvas.sortingOrder=45;
            var rect=go.GetComponent<RectTransform>();
            rect.anchorMin=Vector2.zero;rect.anchorMax=Vector2.one;
            rect.offsetMin=rect.offsetMax=Vector2.zero;
            top=Image(go.transform,"Top").rectTransform;bottom=Image(go.transform,"Bottom").rectTransform;
            crossingFrame=new GameObject("Transition",typeof(RectTransform),typeof(RawImage)).GetComponent<RawImage>();
            crossingFrame.transform.SetParent(go.transform,false);crossingFrame.transform.SetAsFirstSibling();
            crossingFrame.raycastTarget=false;
            crossingFrame.rectTransform.anchorMin=Vector2.zero;crossingFrame.rectTransform.anchorMax=Vector2.one;
            crossingFrame.rectTransform.offsetMin=crossingFrame.rectTransform.offsetMax=Vector2.zero;
            crossingFrame.gameObject.SetActive(false);
        }
        static Image Image(Transform parent,string name)
        {var go=new GameObject(name,typeof(RectTransform),typeof(Image));go.transform.SetParent(parent,false);var image=go.GetComponent<Image>();image.color=Color.black;image.raycastTarget=false;return image;}
        void SetBars(float a)
        {
            if(!top)return;top.anchorMin=new Vector2(0,1-.09f*a);top.anchorMax=Vector2.one;
            bottom.anchorMin=Vector2.zero;bottom.anchorMax=new Vector2(1,.09f*a);top.offsetMin=top.offsetMax=bottom.offsetMin=bottom.offsetMax=Vector2.zero;
            top.gameObject.SetActive(a>0);bottom.gameObject.SetActive(a>0);
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
            ReleaseWindow();
            foreach(var pair in hidden)if(pair.Key)pair.Key.forceRenderingOff=pair.Value;
            foreach(var pair in disabled)if(pair.Key)pair.Key.enabled=pair.Value;
            if(outdoorScene.IsValid()&&outdoorScene.isLoaded&&Application.isPlaying)SceneManager.UnloadSceneAsync(outdoorScene);
        }
    }
}
