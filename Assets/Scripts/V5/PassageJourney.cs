using System.Collections.Generic;
using UnityEngine;

namespace PuzzleApple.V5
{
    public sealed class PassageJourney : MonoBehaviour
    {
        public WorldEntry entry;
        public Material whitePath,blackSurround,roomMaterial,lampMaterial;
        public Shader floorPortalShader;
        public UnityEngine.Rendering.Volume glowVolume;
        [Min(8)] public float pathLength=26;
        public Vector3 roomSize=new Vector3(16,8,18);
        [Range(2,24)] public int lampCount=10;
        public Vector2 lampScaleRange=new Vector2(.12f,.35f);
        [Min(0)] public float localLightIntensity=4f,localLightRange=7.5f;
        [Range(0,2)] public float emissionStrength=.35f;
        [Range(0,.5f)] public float ambientIntensity=.1f;
        [Min(.1f)] public float lampSpeed=1.2f,lightFadeDuration=1.5f,doorDuration=1.65f;
        [Range(90,170)] public float openAngle=135f;
        [Range(0,.1f)] public float fogDensity=.018f;
        [Range(0,.05f)] public float corridorFogDensity=.012f;
        [Header("Corridor art trials")]
        public bool pureWhiteCorridor=true;
        public bool ViewBeyondChamber {get;private set;}
        public float CorridorBlend {get;private set;}
        public bool collideLamps=true;
        public ObservableBehaviour movementObservation;
        public FloorImpactSurface floorSurface;
        public FloorDirectedMotion floorMotion;
        public bool LampsMoving=>entry&&entry.board&&entry.board.State!=null&&movementObservation&&
            (entry.sentenceRuntime&&(entry.sentenceRuntime.PlayerDirectedLampMotion||
                movementObservation.lesson&&entry.board.State.HasAcquired(movementObservation.lesson.acquisitionId))
                ? Brightness<=0||entry.sentenceRuntime.HasEffect(Cognition.CognitionSignal.LightMove)
                : movementObservation.HasEffect(entry.board.State,entry.board.Catalog,Cognition.CognitionSignal.LightMove));
        [Tooltip("Prototype choice, not a final design restriction.")]
        public bool allowCorridorClosure=true;
        public bool BeyondChamber {get;private set;}
        public bool InSecondRoom {get;private set;}
        public bool InCorridor=>BeyondChamber&&!InSecondRoom;
        public float DoorwayLighting {get;private set;}
        public bool DoorOpen {get;private set;}
        public float DoorProgress {get;private set;}
        public bool LightsOn {get;private set;}
        public bool LampsLit=>LightsOn&&Brightness>=.999f;
        public float Brightness {get;private set;}
        public bool LightTransitionEnabled {get;private set;}
        public bool DoorTransitionEnabled {get;private set;}
        public Transform Root {get;private set;}
        public Transform Door {get;private set;}
        public IReadOnlyList<DriftingLight> Lamps=>lamps;
        readonly List<DriftingLight> lamps=new List<DriftingLight>();
        Material lampInk;
        Material doorEdge,corridorMaterial;
        public CorridorStars Stars {get;private set;}
        public float CorridorBrightness {get;private set;}=1;
        public bool CorridorOn {get;private set;}=true;
        bool corridorTransition;
        public bool CorridorLightChanging=>corridorTransition&&CorridorBrightness!=(CorridorOn?1:0);
        readonly List<Collider> rails=new List<Collider>();
        public bool IsRail(Collider collider)=>rails.Contains(collider);
        Transform doorHinge;

        readonly List<Renderer> roomEdges=new List<Renderer>();
        Renderer doorLeaf;
        MaterialPropertyBlock doorSurface;

        Color emission;
        Color initialBackground;
        UnityEngine.Rendering.Universal.UniversalAdditionalCameraData cameraData;
        bool initialPostProcessing;
        float pathWidth;
        public float PathWidth=>pathWidth;
        float chamberBoundary;
        public void Build(Transform entrance,float width,float openingHeight,float wallThickness=.4f,Material openingEdgeMaterial=null)
        {
            Root=new GameObject("Passage and dark room - runtime").transform;Root.SetParent(transform,false);
            Root.SetPositionAndRotation(entrance.position,entrance.rotation);pathWidth=width;
            chamberBoundary=-wallThickness*.5f;
            initialBackground=entry.player.view.GetComponent<Camera>().backgroundColor;
            cameraData=entry.player.view.GetComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>();
            initialPostProcessing=cameraData&&cameraData.renderPostProcessing;
            // The sill supplies the walking surface across the full wall thickness.
            float pathStart=wallThickness*.5f, roomStart=pathLength-.1f;
            corridorMaterial=new Material(whitePath);
            var path=Block("White path",new Vector3(0,-.15f,(pathStart+roomStart)*.5f),new Vector3(width,.3f,roomStart-pathStart),corridorMaterial);
            var pathSurface=new MaterialPropertyBlock();pathSurface.SetFloat("_FrontWhite",0);
            pathSurface.SetColor("_ShadeColor",new Color(.42f,.42f,.42f,1));
            pathSurface.SetFloat("_CorridorPureWhite",pureWhiteCorridor?1:0);path.SetPropertyBlock(pathSurface);
            // Black enclosure hides the chamber and sky, while invisible rails retain the path boundary.
            foreach(float side in new[]{-1f,1f})
            {
                Block("Black surround",new Vector3(side*6,4,pathLength*.5f),new Vector3(.2f,8,pathLength),blackSurround);
                var rail=Block("Path boundary",new Vector3(side*(width*.5f+.1f),2,pathLength*.5f),new Vector3(.2f,4,pathLength),blackSurround);
                rail.enabled=false;
                rails.Add(rail.GetComponent<Collider>());
            }
            Block("Black above",new Vector3(0,8,pathLength*.5f),new Vector3(12,.2f,pathLength),blackSurround);
            Block("Black below",new Vector3(0,-2,pathLength*.5f),new Vector3(12,.2f,pathLength),blackSurround);
            // The entrance, path, door leaf and destination opening share one size.
            float doorWidth=width,doorHeight=openingHeight;
            doorHinge=new GameObject("Door hinge").transform;
            doorHinge.SetParent(Root,false);
            doorHinge.localPosition=new Vector3(-doorWidth*.5f,0,pathLength-.15f);
            doorLeaf=Block("White hinged door",new Vector3(0,doorHeight*.5f,pathLength),new Vector3(doorWidth,doorHeight,.3f),corridorMaterial);
            Door=doorLeaf.transform;
            Door.SetParent(doorHinge,true);
            Door.gameObject.layer=29;
            var knob=GameObject.CreatePrimitive(PrimitiveType.Sphere);
            knob.name="Round handle";
            knob.transform.SetParent(doorHinge,false);
            knob.transform.localPosition=new Vector3(doorWidth-.32f,doorHeight*.43f,-.10f);
            knob.transform.localScale=Vector3.one*.26f;
            knob.gameObject.layer=29;
            var knobRenderer=knob.GetComponent<Renderer>();
            knobRenderer.sharedMaterial=corridorMaterial;
            var knobInk=new MaterialPropertyBlock();
            knobInk.SetFloat("_CorridorPureWhite",pureWhiteCorridor?1:0);
            knobInk.SetFloat("_RoundOutline",1);
            knobRenderer.SetPropertyBlock(knobInk);
            UpdateDoorAppearance();
            if(openingEdgeMaterial)
            {
                doorEdge=new Material(openingEdgeMaterial);doorEdge.SetFloat("_Visibility",1);
                // Keep only narrow flowing outlines, outside the stationary white leaf.
                foreach(float depth in new[]{.16f,-.16f})foreach(float side in new[]{-1f,1f})
                {
                    var upright=WallPassage.CreateFilament(Root,doorEdge,"Open door vertical filaments",new Vector3(side*(doorWidth*.5f-.05f),doorHeight*.5f,pathLength+depth),doorHeight);
                    var cross=WallPassage.CreateFilament(Root,doorEdge,"Open door horizontal filaments",new Vector3(0,side<0?.05f:doorHeight-.05f,pathLength+depth),doorWidth);
                    cross.transform.localRotation=Quaternion.Euler(0,0,90);
                    if(depth>0){roomEdges.Add(upright);roomEdges.Add(cross);upright.enabled=cross.enabled=false;}
                }
            }
            float sideWidth=(roomSize.x-doorWidth)*.5f;
            foreach(float side in new[]{-1f,1f})
                RoomBlock("Door wall",new Vector3(side*(doorWidth*.5f+sideWidth*.5f),roomSize.y*.5f,pathLength),new Vector3(sideWidth,roomSize.y,.3f),Vector3.forward);
            RoomBlock("Door lintel",new Vector3(0,(doorHeight+roomSize.y)*.5f,pathLength),new Vector3(doorWidth,roomSize.y-doorHeight,.3f),Vector3.forward);
            RoomBlock("Room floor",new Vector3(0,-.15f,(roomStart+pathLength+roomSize.z)*.5f),new Vector3(roomSize.x,.3f,pathLength+roomSize.z-roomStart),Vector3.up);
            if(floorSurface)floorSurface.Build(Root.Find("Room floor").GetComponent<Renderer>());
            RoomBlock("Room ceiling",new Vector3(0,roomSize.y+.15f,pathLength+roomSize.z*.5f),new Vector3(roomSize.x,.3f,roomSize.z),Vector3.down);
            RoomBlock("Room far wall",new Vector3(0,roomSize.y*.5f,pathLength+roomSize.z),new Vector3(roomSize.x,roomSize.y,.3f),Vector3.back);
            foreach(float side in new[]{-1f,1f})RoomBlock("Room side wall",new Vector3(side*roomSize.x*.5f,roomSize.y*.5f,pathLength+roomSize.z*.5f),new Vector3(.3f,roomSize.y,roomSize.z),Vector3.left*side);
            if(floorSurface)floorSurface.BuildWalls();
            lampInk=new Material(lampMaterial);emission=lampInk.GetColor("_EmissionColor");
            lampInk.EnableKeyword("_EMISSION");
            lampInk.SetFloat("_EnvironmentReflections",0);lampInk.EnableKeyword("_ENVIRONMENTREFLECTIONS_OFF");
            lampInk.SetFloat("_SpecularHighlights",0);lampInk.EnableKeyword("_SPECULARHIGHLIGHTS_OFF");
            for(int i=0;i<lampCount;i++)
            {
                float scale=Random.Range(Mathf.Max(.05f,lampScaleRange.x),Mathf.Max(lampScaleRange.x,lampScaleRange.y));
                var r=Block("Drifting light "+i,new Vector3(Random.Range(-roomSize.x*.4f,roomSize.x*.4f),Random.Range(1,roomSize.y-1),pathLength+Random.Range(1,roomSize.z-1)),Vector3.one*scale,lampInk);
                var go=r.gameObject;go.layer=29;go.transform.rotation=Quaternion.FromToRotation(Vector3.one.normalized,Vector3.down);
                go.GetComponent<BoxCollider>().enabled=false;Destroy(go.GetComponent<BoxCollider>());var sphere=go.AddComponent<SphereCollider>();sphere.radius=.65f;
                var motion=go.AddComponent<DriftingLight>();motion.journey=this;motion.speed=lampSpeed;
                var lightObject=new GameObject("Soft local light",typeof(Light));lightObject.transform.SetParent(go.transform,false);
                var light=lightObject.GetComponent<Light>();light.type=LightType.Point;light.range=localLightRange;light.intensity=0;light.shadows=LightShadows.Soft;light.cullingMask=~0;motion.glow=light;
                r.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;lamps.Add(motion);
            }
            if(!collideLamps)for(int a=0;a<lamps.Count;a++)for(int b=a+1;b<lamps.Count;b++)Physics.IgnoreCollision(lamps[a].GetComponent<SphereCollider>(),lamps[b].GetComponent<SphereCollider>());
            RefreshCollisionExclusions();
            if(movementObservation)
            {
                movementObservation.members=lamps.ConvertAll(l=>l.GetComponent<SphereCollider>()).ToArray();
                movementObservation.Available=false;
            }
            var starRoot=new GameObject("Passage stars");
            starRoot.transform.SetParent(Root,false);
            Stars=starRoot.AddComponent<CorridorStars>();
            Stars.Build(this);
            ApplyLight();
        }
        public void RemoveLamp(DriftingLight lamp)
        {
            lamps.Remove(lamp);
            if(movementObservation)movementObservation.members=lamps.ConvertAll(l=>l.GetComponent<SphereCollider>()).ToArray();
        }
        public void RefreshCollisionExclusions()
        {
            var player=entry.player.GetComponent<CharacterController>();
            foreach(var lamp in lamps)Physics.IgnoreCollision(lamp.GetComponent<SphereCollider>(),player,true);
        }
        Renderer Block(string name,Vector3 position,Vector3 size,Material material)
        {
            var go=GameObject.CreatePrimitive(PrimitiveType.Cube);go.name=name;go.transform.SetParent(Root,false);go.transform.localPosition=position;go.transform.localScale=size;
            var r=go.GetComponent<Renderer>();r.sharedMaterial=material;return r;
        }
        void RoomBlock(string name,Vector3 p,Vector3 size,Vector3 inward)
        {
            var surface=Block(name,p,size,roomMaterial);
            surface.gameObject.layer=29;
            var properties=new MaterialPropertyBlock();
            properties.SetVector("_RoomShellInward",inward);
            surface.SetPropertyBlock(properties);
        }
        public void OpenDoor()=>SetDoor(true);
        public void SetDoor(bool value){DoorOpen=value;DoorTransitionEnabled=true;}
        public void HoldDoor()=>DoorTransitionEnabled=false;
        public void SetLights(bool value)
        {
            LightsOn=value;LightTransitionEnabled=true;
            if(InCorridor&&(!entry.portableClosure||!entry.portableClosure.Isolated))
            {CorridorOn=value;corridorTransition=true;}
        }
        public void HoldLights(){LightTransitionEnabled=false;corridorTransition=false;}
        public string WordAt(Collider target,float distance)
        {
            if(target.transform.IsChildOf(doorHinge)&&distance<=7)
                return DoorProgress<=0&&Root.InverseTransformPoint(entry.player.view.position).z>pathLength?null:"door";
            if(InSecondRoom&&floorSurface&&floorSurface.Owns(target)&&distance<=7)return "floor";
            if(InSecondRoom&&LampsLit&&target.GetComponent<DriftingLight>())return "light";
            return null;
        }
        void Update()
        {
            if(!Root)return;
            if(!entry.portableClosure||!entry.portableClosure.Isolated)
            {
                var p=Root.InverseTransformPoint(entry.player.transform.position);
                BeyondChamber=p.z>chamberBoundary&&p.z<pathLength+roomSize.z+1&&Mathf.Abs(p.x)<roomSize.x*.5f+1;
                InSecondRoom=BeyondChamber&&p.z>pathLength+.3f;
            }

            float viewZ=Root.InverseTransformPoint(entry.player.view.position).z;
            ViewBeyondChamber=BeyondChamber&&viewZ>chamberBoundary;
            CorridorBlend=ViewBeyondChamber?Mathf.SmoothStep(0,1,Mathf.InverseLerp(chamberBoundary,chamberBoundary+2,viewZ)):0;
            float viewDepth=viewZ-pathLength;
            DoorwayLighting=BeyondChamber?Mathf.SmoothStep(0,1,Mathf.InverseLerp(-2,2,viewDepth)):0;
            if(DoorTransitionEnabled)
            {
                float next=Mathf.MoveTowards(DoorProgress,DoorOpen?1:0,Time.deltaTime/Mathf.Max(.01f,doorDuration));
                if(next!=DoorProgress&&!PlayerBlocksSwing(next))
                {
                    DoorProgress=next;
                    doorHinge.localRotation=Quaternion.Euler(0,SwingAngle(DoorProgress),0);
                }
            }
            bool corridorSide=Root.InverseTransformPoint(entry.player.view.position).z<pathLength;
            foreach(var edge in roomEdges)edge.enabled=!corridorSide&&DoorProgress>0;
            if(LightTransitionEnabled)Brightness=Mathf.MoveTowards(Brightness,LightsOn?1:0,Time.unscaledDeltaTime/Mathf.Max(.01f,lightFadeDuration));ApplyLight();
            if(corridorTransition)
            {
                CorridorBrightness=Mathf.MoveTowards(CorridorBrightness,CorridorOn?1:0,Time.unscaledDeltaTime/Mathf.Max(.01f,lightFadeDuration));
                float level=Mathf.SmoothStep(0,1,CorridorBrightness);
                corridorMaterial.SetFloat("_CorridorLight",Mathf.Lerp(.065f,1,level));
                if(doorEdge)doorEdge.SetFloat("_Visibility",Mathf.Lerp(.09f,1,level));
            }
            UpdateDoorAppearance();
            if(movementObservation)movementObservation.Available=InSecondRoom&&LampsMoving&&(!entry.portableClosure||!entry.portableClosure.Isolated);
            if(glowVolume)glowVolume.weight=entry.portableClosure&&entry.portableClosure.Isolated?0:DoorwayLighting;
            entry.player.view.GetComponent<Camera>().backgroundColor=ViewBeyondChamber?Color.black:initialBackground;
            if(cameraData)cameraData.renderPostProcessing=initialPostProcessing;
        }
        float SwingAngle(float progress)=>openAngle*Mathf.SmoothStep(0,1,progress);
        bool PlayerBlocksSwing(float next)
        {
            var motor=entry.player.GetComponent<CharacterController>();
            Vector3 centre=Root.InverseTransformPoint(motor.bounds.center)-doorHinge.localPosition;
            if(DoorOpen&&centre.z>.3f+motor.radius)return false;
            if(centre.y-motor.height*.5f>Door.localScale.y||centre.y+motor.height*.5f<0)return false;
            float padding=motor.radius+.16f;
            if(!DoorOpen&&Mathf.Abs(centre.z-.15f)<padding&&centre.x>=-padding&&centre.x<=pathWidth+padding)return true;
            float angle=Mathf.Atan2(-centre.z,centre.x)*Mathf.Rad2Deg;
            angle=Mathf.Clamp(angle,SwingAngle(Mathf.Min(next,DoorProgress)),SwingAngle(Mathf.Max(next,DoorProgress)));
            Vector3 local=Quaternion.Euler(0,-angle,0)*centre;
            float x=Mathf.Clamp(local.x,0,pathWidth);
            float z=Mathf.Clamp(local.z,0,.3f);
            return new Vector2(local.x-x,local.z-z).sqrMagnitude<padding*padding;
        }
        void ApplyLight()
        {
            if(!lampInk)return;
            float amount=Mathf.SmoothStep(0,1,Brightness);
            float boosted=amount*(entry.sentenceRuntime?entry.sentenceRuntime.LampMultiplier:1);
            lampInk.SetColor("_BaseColor",Color.Lerp(Color.black,Color.white,amount));lampInk.SetColor("_EmissionColor",emission*(boosted*emissionStrength));
            foreach(var l in lamps){l.glow.intensity=localLightIntensity*boosted;l.GetComponent<Renderer>().enabled=amount>0;}
        }
        void UpdateDoorAppearance()
        {
            if(doorSurface==null)doorSurface=new MaterialPropertyBlock();
            doorSurface.SetFloat("_IsDoorLeaf",1);
            doorSurface.SetFloat("_CorridorPureWhite",pureWhiteCorridor?1:0);
            doorSurface.SetFloat("_DoorBackBrightness",1);
            var wallInk=floorSurface?floorSurface.floorMaterial:null;
            doorSurface.SetColor("_DoorBackColor",wallInk&&wallInk.HasProperty("_FloorColor")?wallInk.GetColor("_FloorColor"):Color.white);
            doorSurface.SetFloat("_LightSteps",floorSurface?floorSurface.lightSteps:4);
            doorSurface.SetFloat("_LightStepBlend",floorSurface&&floorSurface.steppedLighting?floorSurface.lightStepBlend:0);
            doorSurface.SetFloat("_LightBandEdge",floorSurface?floorSurface.lightBandEdge:.45f);
            doorLeaf.SetPropertyBlock(doorSurface);
        }
        public Vector3 ConstrainMovement(Vector3 position,Vector3 motion)
        {
            if(!Root||!InCorridor)return motion;
            var p=Root.InverseTransformPoint(position);var delta=Root.InverseTransformDirection(motion);
            // Restrict horizontal travel to the path, retaining vertical motion for the entry jump.
            var desired=p+delta;desired.x=Mathf.Clamp(desired.x,-pathWidth*.5f+.3f,pathWidth*.5f-.3f);
            return Root.TransformVector(desired-p);
        }
        void OnDestroy(){if(corridorMaterial)Destroy(corridorMaterial);if(lampInk)Destroy(lampInk);if(doorEdge)Destroy(doorEdge);}
    }
}
