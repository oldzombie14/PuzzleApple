using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace PuzzleApple.V5
{
    public sealed class OutdoorPassage : MonoBehaviour
    {
        public WorldEntry entry;
        public bool automaticTransition=true;
        [Min(.1f)] public float exitSpeed=4.5f;
        public bool Transitioning {get;private set;}
        public Outdoor.OutdoorWorld World {get;private set;}
        public bool IsOutside=>outside;
        bool outside;
        public ChapterRoom Chapter {get;private set;}
        public bool InChapter=>Chapter&&Chapter.gameObject.activeInHierarchy;
        const string ChapterPath="Assets/Scenes/V5/ChapterI.unity";
        Scene chapterScene, gardenScene;
        public ChapterRoom Garden {get;private set;}
        public ChapterRoom CurrentRoom=>InChapter&&Garden&&Vector3.Dot(entry.player.transform.position-Chapter.gardenThreshold.position,Chapter.gardenThreshold.forward)<0?Garden:Chapter;
        Transform chapterFrame;
        Vector3 previousFeet;
        bool tracking;
        CameraClearFlags indoorClearFlags;
        float indoorFarClip;
        public int TransitionCount {get;private set;}
        public float LastExitSpeed {get;private set;}
        public Vector3 LastExitVelocity {get;private set;}
        readonly Dictionary<Renderer,bool> hidden=new Dictionary<Renderer,bool>();
        readonly Dictionary<Behaviour,bool> disabled=new Dictionary<Behaviour,bool>();
        Scene outdoorScene;
        public const float TransferDepth=-.45f;
        const float WindowDepth=TransferDepth;
        bool tunnelBuilt;
        Renderer window;
        Camera windowCamera;
        RenderTexture windowTexture;
        Material windowMaterial;
        readonly Plane[] viewPlanes=new Plane[6];
        readonly List<Behaviour> previewDisabled=new List<Behaviour>();
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
            var playerCamera=entry.player.view.GetComponent<Camera>();
            windowTexture=new RenderTexture(Mathf.Max(1,playerCamera.pixelWidth),Mathf.Max(1,playerCamera.pixelHeight),24,RenderTextureFormat.ARGB32){name="Floor opening outdoor view"};
            windowTexture.Create();windowCamera.targetTexture=windowTexture;
            windowMaterial=new Material(entry.journey.floorPortalShader);
            windowMaterial.SetTexture("_BaseMap",windowTexture);
            window.sharedMaterial=windowMaterial;
            RenderWindow();
        }
        Vector3 OutletCentre()
        {
            var floor=entry.journey.floorSurface;
            var centre=preparedWorld.exit.position;
            centre.y=Mathf.Max(centre.y,preparedWorld.ground.bounds.max.y+floor.HoleWidth*.5f+entry.player.view.localPosition.y+.15f);
            return centre;
        }
        // Looking down through the floor corresponds to looking forward out of the door.
        Quaternion PortalRotation=>preparedWorld.exit.rotation*Quaternion.Euler(-90,0,0)*Quaternion.Inverse(entry.journey.Root.rotation);
        Vector3 ThroughPortal(Vector3 point)
        {
            var j=entry.journey;
            var centre=j.Root.TransformPoint(new Vector3(0,WindowDepth,j.floorSurface.HoleCentreZ));
            return OutletCentre()+PortalRotation*(point-centre);
        }
        Quaternion OutletView(Transform eye)
        {
            var forward=PortalRotation*eye.forward;
            if(Vector3.Cross(Vector3.up,forward).sqrMagnitude<.000001f)
                forward=(forward+preparedWorld.exit.forward*.001f).normalized;
            return Quaternion.LookRotation(forward,Vector3.up);
        }
        void RenderWindow()
        {
            var eye=entry.player.view;
            var source=eye.GetComponent<Camera>();
            var outlet=preparedWorld.exit;
            windowCamera.transform.SetPositionAndRotation(ThroughPortal(eye.position),OutletView(eye));
            windowCamera.nearClipPlane=source.nearClipPlane;
            windowCamera.fieldOfView=source.fieldOfView;
            windowCamera.aspect=source.aspect;
            windowCamera.ResetProjectionMatrix();
            // Clip the world behind the exit, keeping the same screen rays as the player.
            var view=windowCamera.worldToCameraMatrix;
            var normal=view.MultiplyVector(outlet.forward);
            var point=view.MultiplyPoint(OutletCentre());
            float depth=Vector3.Dot(point,normal);
            if(depth>source.nearClipPlane)
                windowCamera.projectionMatrix=windowCamera.CalculateObliqueMatrix(new Vector4(normal.x,normal.y,normal.z,-Vector3.Dot(normal,point)));
            float approach=Mathf.InverseLerp(1.3f,.5f,depth)*Mathf.InverseLerp(.08f,.45f,depth);
            windowMaterial.SetFloat("_Shimmer",approach*Mathf.Clamp01(-entry.player.VerticalVelocity/6));
            var previous=SceneManager.GetActiveScene();
            try
            {
                SceneManager.SetActiveScene(outdoorScene);
                preparedWorld.sun.enabled=true;
                RenderSettings.sun=preparedWorld.sun;
                preparedWorld.environment.ApplyCurrentLighting();
                windowCamera.Render();warmed=true;
            }
            finally{preparedWorld.sun.enabled=false;SceneManager.SetActiveScene(previous);}
        }
        void LateUpdate()
        {
            if(!windowCamera||!window||InChapter||IsOutside)return;
            var j=entry.journey;
            if(!j.InSecondRoom||entry.portableClosure&&entry.portableClosure.Isolated)return;
            var feet=j.Root.InverseTransformPoint(entry.player.transform.position);
            var eye=j.Root.InverseTransformPoint(entry.player.view.position);
            if(automaticTransition&&!Transitioning&&!entry.Busy&&!entry.ConfirmingExit&&eye.y<=WindowDepth+entry.player.view.GetComponent<Camera>().nearClipPlane&&j.floorSurface.FitsOpening(feet,0))
            {
                Travel();
                return;
            }
            GeometryUtility.CalculateFrustumPlanes(entry.player.view.GetComponent<Camera>(),viewPlanes);
            if(!GeometryUtility.TestPlanesAABB(viewPlanes,window.bounds))return;
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
            if(IsOutside||InChapter)
            {
                CheckFrames();
                return;
            }
            if(Transitioning||!entry||!entry.journey||!entry.journey.floorSurface)return;
            var j=entry.journey;
            if(!j.floorSurface.HoleOpen&&j.floorSurface.HoleProgress<=0)return;
            if(!tunnelBuilt)BuildTunnel();
            if(!windowCamera&&preparedWorld)StartWindow();
        }
        void CheckFrames()
        {
            Vector3 feet=entry.player.transform.position;
            bool ready=entry.room.Opened&&!Transitioning&&!entry.Busy&&!entry.ConfirmingExit&&!entry.chamberLight.Collecting&&!entry.board.Panel.IsOpen;
            if(ready&&tracking)
            {
                if(InChapter&&ReachedMirror(feet))
                    LeaveChapter();
                else if(IsOutside&&Crossed(chapterFrame,feet,1.28f,3.65f))
                    StartCoroutine(EnterChapter());
                else if(IsOutside&&World.doors.appearance.PassageEnabled&&Crossed(World.door.transform,feet,2.07f,5.92f))
                    ReturnIndoors();
            }
            previousFeet=entry.player.transform.position;
            tracking=ready;
        }
        bool ReachedMirror(Vector3 feet)
        {
            var step=Vector3.ProjectOnPlane(feet-previousFeet,Vector3.up);
            if(step.sqrMagnitude<.000001f)return false;
            var eyeOffset=entry.player.view.position-feet;
            var controller=entry.player.GetComponent<CharacterController>();
            // The physical mirror surround stops the capsule slightly in front of the glass.
            return Chapter.mirror.Raycast(new Ray(previousFeet+eyeOffset,step.normalized),out _,
                step.magnitude+controller.radius+controller.skinWidth+.16f);
        }
        bool Crossed(Transform frame,Vector3 feet,float halfWidth,float height)
        {
            if(!frame)return false;
            var a=frame.InverseTransformPoint(previousFeet+Vector3.up);
            var b=frame.InverseTransformPoint(feet+Vector3.up);
            if((a.z<0)==(b.z<0)||Mathf.Abs(a.z-b.z)<.0001f)return false;
            var at=Vector3.Lerp(a,b,a.z/(a.z-b.z));
            return Mathf.Abs(at.x)<halfWidth&&at.y>0&&at.y<height;
        }
        IEnumerator EnterChapter()
        {
            Transitioning=true;
            var player=entry.player;
            player.MovementLocked=true;
            entry.board.Panel.InputBlocked=true;
            try
            {
                if(!Chapter)
                {
                    yield return SceneManager.LoadSceneAsync(ChapterPath,LoadSceneMode.Additive);
                    chapterScene=SceneManager.GetSceneByPath(ChapterPath);
                    foreach(var root in chapterScene.GetRootGameObjects())
                    {
                        Chapter=root.GetComponentInChildren<ChapterRoom>(true);
                        if(Chapter)break;
                    }
                    if(!Chapter){Debug.LogError("Missing V5 ChapterRoom in "+ChapterPath);yield break;}
                    const string gardenPath="Assets/Scenes/V5/ChapterI_Garden.unity";
                    yield return SceneManager.LoadSceneAsync(gardenPath,LoadSceneMode.Additive);
                    gardenScene=SceneManager.GetSceneByPath(gardenPath);
                    foreach(var root in gardenScene.GetRootGameObjects())
                    {
                        Garden=root.GetComponentInChildren<ChapterRoom>(true);
                        if(Garden)break;
                    }
                    Chapter.Bind(entry);
                    Garden.Bind(entry);
                }
                entry.room.GetComponent<FoldingInterior>().enabled=false;
                outside=false;
                SetChapterActive(true);
                preparedRoot.SetActive(false);
                SceneManager.SetActiveScene(chapterScene);
                player.Teleport(Chapter.arrival.position,Chapter.arrival.rotation);
                entry.sentenceRuntime.ChangeSpace();
            }
            finally{entry.board.Panel.InputBlocked=false;player.MovementLocked=false;Transitioning=false;tracking=false;}
        }
        void SetChapterActive(bool active)
        {
            foreach(var root in chapterScene.GetRootGameObjects())root.SetActive(active);
            foreach(var root in gardenScene.GetRootGameObjects())root.SetActive(active);
        }
        void LeaveChapter()
        {
            outside=true;
            SetChapterActive(false);
            entry.room.GetComponent<FoldingInterior>().enabled=true;
            SceneManager.SetActiveScene(outdoorScene);
            preparedRoot.SetActive(true);
            entry.player.Teleport(chapterFrame.position-chapterFrame.forward*1.8f,Quaternion.LookRotation(-chapterFrame.forward));
            World.Audio.ResumeTrack();
            World.ApplyMotion(entry.sentenceRuntime.HasEffect,false);
            entry.sentenceRuntime.ChangeSpace();
        }
        void ReturnIndoors()
        {
            outside=false;
            preparedRoot.SetActive(false);
            SceneManager.SetActiveScene(entry.gameObject.scene);
            foreach(var pair in hidden)if(pair.Key)pair.Key.forceRenderingOff=pair.Value;
            foreach(var pair in disabled)if(pair.Key)pair.Key.enabled=pair.Value;
            hidden.Clear();disabled.Clear();
            entry.journey.enabled=true;
            if(entry.chamberLight.observer)entry.chamberLight.observer.enabled=true;
            var camera=entry.player.view.GetComponent<Camera>();
            camera.clearFlags=indoorClearFlags;camera.farClipPlane=indoorFarClip;
            var floor=entry.journey.floorSurface;
            var point=entry.journey.Root.TransformPoint(new Vector3(0,.15f,floor.HoleCentreZ-floor.HoleWidth*.5f-1.2f));
            entry.player.Teleport(point,entry.journey.Root.rotation*Quaternion.Euler(0,180,0));
            window.gameObject.SetActive(true);
            StartWindow();
            entry.sentenceRuntime.ChangeSpace();
            tracking=false;
        }
        void BuildTunnel()
        {
            tunnelBuilt=true;var j=entry.journey;float width=j.floorSurface.HoleWidth;
            var root=new GameObject("Downward passage").transform;root.SetParent(j.Root,false);
            // Keep the capsule inside the shaft until its eye crosses the portal.
            float liningHeight=-WindowDepth+entry.player.GetComponent<CharacterController>().height;
            root.localPosition=new Vector3(0,-liningHeight*.5f-.005f,j.floorSurface.HoleCentreZ);
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
                wall.transform.localScale=x?new Vector3(.2f,liningHeight,width+.4f):new Vector3(width,liningHeight,.2f);
                var renderer=wall.GetComponent<Renderer>();
                renderer.sharedMaterial=j.floorSurface.floorMaterial;
                renderer.SetPropertyBlock(lining);
            }
        }
        void Travel()
        {
            Transitioning=true;TransitionCount++;entry.sentenceRuntime.presentation.Cancel();
            var player=entry.player;var panel=entry.board.Panel;
            bool oldBlocked=panel.InputBlocked,oldMove=player.MovementLocked,oldCinematic=player.CinematicLocked;
            var motor=player.GetComponent<CharacterController>();
            var incoming=motor.velocity;
            incoming.y=player.VerticalVelocity;
            entry.board.CancelDrag();panel.SetOpen(false);panel.InputBlocked=true;player.CinematicLocked=true;player.MovementLocked=true;
            try
            {
                var eyePosition=ThroughPortal(player.view.position);
                var rotation=OutletView(player.view);
                var exit=preparedWorld.exit;
                var velocity=PortalRotation*incoming;
                LastExitSpeed=Mathf.Clamp(Vector3.Dot(velocity,exit.forward),0,exitSpeed);
                velocity=Vector3.ProjectOnPlane(velocity,exit.forward)+exit.forward*LastExitSpeed;
                LastExitVelocity=velocity;
                ReleaseWindow();
                var activation=System.Diagnostics.Stopwatch.StartNew();
                preparedRoot.SetActive(true);Physics.SyncTransforms();World=preparedWorld;outside=true;
                // The original rooms and progress stay alive; only their presentation stops.
                foreach(var r in entry.gameObject.scene.GetRootGameObjects())
                {
                    foreach(var renderer in r.GetComponentsInChildren<MeshRenderer>(true))
                        if(!renderer.transform.IsChildOf(player.transform)&&!renderer.transform.IsChildOf(entry.room.transform))
                        {hidden[renderer]=renderer.forceRenderingOff;renderer.forceRenderingOff=true;}
                    foreach(var b in r.GetComponentsInChildren<Behaviour>(true))
                        if(b is RoomLighting||b is UnityEngine.Rendering.Volume&&!b.GetComponentInParent<IlluminationEffects>()||b is Light)
                        {disabled[b]=b.enabled;b.enabled=false;}
                }
                entry.journey.enabled=false;entry.journey.movementObservation.Available=false;
                if(entry.chamberLight.observer)entry.chamberLight.observer.enabled=false;
                SceneManager.SetActiveScene(outdoorScene);
                foreach(var behaviour in previewDisabled)if(behaviour)behaviour.enabled=true;
                previewDisabled.Clear();
                var camera=player.view.GetComponent<Camera>();
                indoorClearFlags=camera.clearFlags;indoorFarClip=camera.farClipPlane;
                if(!World.Active)
                {
                    World.Bind(entry);
                    chapterFrame=World.chapterFrame;
                }
                else
                {
                    camera.clearFlags=CameraClearFlags.Skybox;camera.farClipPlane=1500;
                    World.Audio.ResumeTrack();
                    World.ApplyMotion(entry.sentenceRuntime.HasEffect,false);
                }
                entry.sentenceRuntime.ChangeSpace();
                World.environment.ApplyCurrentLighting();
                player.Teleport(eyePosition-Vector3.up*player.view.localPosition.y,exit.rotation);
                player.view.rotation=rotation;player.AdoptCurrentView();
                player.Launch(velocity);
                tracking=false;
                ActivationMilliseconds=(float)activation.Elapsed.TotalMilliseconds;
            }
            finally
            {
                player.CinematicLocked=oldCinematic;player.MovementLocked=oldMove;
                panel.InputBlocked=oldBlocked;player.SetPanelOpen(false);player.SuppressInteractionThisFrame();Transitioning=false;
            }
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
            if(gardenScene.IsValid()&&gardenScene.isLoaded&&Application.isPlaying)SceneManager.UnloadSceneAsync(gardenScene);
            if(chapterScene.IsValid()&&chapterScene.isLoaded&&Application.isPlaying)SceneManager.UnloadSceneAsync(chapterScene);
            if(outdoorScene.IsValid()&&outdoorScene.isLoaded&&Application.isPlaying)SceneManager.UnloadSceneAsync(outdoorScene);
        }
    }
}
