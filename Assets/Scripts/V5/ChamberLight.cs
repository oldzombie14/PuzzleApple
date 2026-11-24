using System.Collections;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace PuzzleApple.V5
{
    public sealed class ChamberLight : MonoBehaviour, ISwitchableLight, ISentenceMovable, IBrightenableLight,
        ISentenceOutputState<EmissionState>, ISentenceOutputState<DriftPose>
    {
        internal SentenceStateOutput LightOutput;
        public SentenceMotionOutput MotionOutput {get;internal set;}
        public WorldEntry entry;
        public Renderer surface;
        public Image hoverEye, collectionSymbol;
        public GameObject reticle;
        public RectTransform hud;
        public BehaviourObserver observer;
        public bool ShowingKnown=>knownSymbol&&knownSymbol.gameObject.activeSelf;
        public string KnownFeedbackWord {get;private set;}
        [Min(1)] public float collectionDistance=40;
        [Min(.01f)] public float fadeDuration=1.2f;
        public float wordDisplayDuration=1.6f, wordFadeInDuration=.3f, wordFlyDuration=.6f;
        [Range(0,1)] public float knownEyeAlpha=.35f, knownSymbolAlpha=.9f;
        [Min(.1f)] public float knownSymbolDuration=.9f;
        public Vector2 knownSymbolScale=new Vector2(.8f,1.2f);
        public bool TargetOn {get;private set;}=true;
        float brightness = 1;
        public float Brightness
        {
            get => brightness;
            private set { if (brightness == value) return; brightness = value; BrightnessChanged?.Invoke(); }
        }
        public event System.Action BrightnessChanged;

        DriftPose ISentenceOutputState<DriftPose>.CaptureState() =>
            new DriftPose {position = motionBody.position, direction = moveDirection};
        public bool TryRestoreState(DriftPose pose)
        {
            if (!motionBody || !pose.Valid || pose.direction == Vector3.zero) return false;
            moveDirection = pose.direction;
            DriftPose.Place(motionBody, pose.position);
            if (!motionBody.isKinematic) motionBody.linearVelocity = Moving ? moveDirection * moveSpeed : Vector3.zero;
            return true;
        }
        public EmissionState CaptureState() => new EmissionState { brightness = Brightness };

        public bool TryRestoreState(EmissionState state)
        {
            if (!material || !entry || !entry.sentenceRuntime ||
                !(state.brightness >= 0 && state.brightness <= 1)) return false;
            Brightness = state.brightness;
            ApplyEmission();
            return true;
        }
        public bool TransitionEnabled {get;private set;}
        public bool Collecting {get;private set;}
        [Min(0)] public float moveSpeed=1.2f;
        Rigidbody motionBody;
        OuterRoomEdges edges;
        ObservableBehaviour cubeMotion;
        Vector3 moveDirection,roomMin,roomMax;
        public bool Moving=>Brightness>0&&entry&&entry.Entered&&entry.room.Opened&&entry.sentenceRuntime&&
            (MotionOutput?.HasMotion(SentenceMotion.Light)==true)&&
            (!entry.outdoor||!entry.outdoor.IsOutside&&!entry.outdoor.InChapter)&&
            (!entry.portableClosure||!entry.portableClosure.Isolated);
        public Vector3 RoomCentre=>(roomMin+roomMax)*.5f;
        Material material;
        Color onEmission,onColor;
        readonly RaycastHit[] collectionHits=new RaycastHit[32];
        Image knownSymbol, knownPrefix;
        Coroutine knownFeedback;
        Color eyeColor;
        [Min(.05f)] public float clickDuration=.25f;
        string pressedWord;
        bool pointerGesture,altarPressed;
        float pointerDuration;
        void Awake(){material=surface.material;onEmission=material.GetColor("_EmissionColor");onColor=material.GetColor("_BaseColor");material.EnableKeyword("_EMISSION");hoverEye.gameObject.SetActive(false);collectionSymbol.gameObject.SetActive(false);if(reticle)reticle.SetActive(false);}
        void Start()
        {
            InitializeMotion();
            var example=entry.journey?entry.journey.movementObservation:null;
            if(observer&&example&&example.lesson)
            {
                cubeMotion=gameObject.AddComponent<ObservableBehaviour>();
                cubeMotion.lesson=example.lesson;
                cubeMotion.aimToleranceDegrees=example.aimToleranceDegrees;
                cubeMotion.members=new[]{GetComponent<Collider>()};
                cubeMotion.Available=false;
                System.Array.Resize(ref observer.targets,observer.targets.Length+1);
                observer.targets[observer.targets.Length-1]=cubeMotion;
            }
            eyeColor=hoverEye.color;
            collectionSymbol.material=hoverEye.material;
            knownSymbol=Instantiate(collectionSymbol,collectionSymbol.transform.parent);
            knownSymbol.name="Known word feedback";knownSymbol.raycastTarget=false;
            knownSymbol.material=hoverEye.material;knownSymbol.gameObject.SetActive(false);
            knownPrefix=Instantiate(collectionSymbol,knownSymbol.transform);
            knownPrefix.name="Known phrase prefix";knownPrefix.raycastTarget=false;
            knownPrefix.rectTransform.anchorMin=knownPrefix.rectTransform.anchorMax=knownPrefix.rectTransform.pivot=new Vector2(.5f,.5f);
            knownPrefix.rectTransform.localScale=Vector3.one;knownPrefix.gameObject.SetActive(false);
        }
        void InitializeMotion()
        {
            motionBody=GetComponent<Rigidbody>();if(!motionBody)motionBody=gameObject.AddComponent<Rigidbody>();
            motionBody.useGravity=false;motionBody.constraints=RigidbodyConstraints.FreezeRotation;
            motionBody.interpolation=RigidbodyInterpolation.Interpolate;motionBody.isKinematic=true;
            var own=GetComponent<Collider>();if(own)Physics.IgnoreCollision(own,entry.player.GetComponent<CharacterController>());
            moveDirection=new Vector3(.6f,.65f,.45f).normalized;
            roomMin=new Vector3(-14,0,-14);roomMax=new Vector3(14,15.92f,14);
            edges=FindObjectsByType<OuterRoomEdges>(FindObjectsSortMode.None).FirstOrDefault(e=>e.surfaces?.Length==6 && !e.GetComponentInParent<PassageJourney>());
            if(edges&&edges.surfaces!=null)
            {
                foreach(var wall in edges.surfaces)
                {
                    if(!wall)continue;var b=wall.bounds;
                    int axis=b.size.x<b.size.y?(b.size.x<b.size.z?0:2):(b.size.y<b.size.z?1:2);
                    if(b.center[axis]<surface.bounds.center[axis])roomMin[axis]=b.max[axis];else roomMax[axis]=b.min[axis];
                }
            }
        }
        void FixedUpdate()
        {
            if(!motionBody)return;
            if(!Moving)
            {if(!motionBody.isKinematic){motionBody.linearVelocity=Vector3.zero;motionBody.isKinematic=true;}return;}
            if(motionBody.isKinematic){motionBody.isKinematic=false;motionBody.collisionDetectionMode=CollisionDetectionMode.ContinuousDynamic;}
            var position=motionBody.position;var extent=surface.bounds.extents+Vector3.one*.03f;
            for(int axis=0;axis<3;axis++)
            {
                float lo=roomMin[axis]+extent[axis],hi=roomMax[axis]-extent[axis];
                if(position[axis]>=lo&&position[axis]<=hi)continue;
                bool low=position[axis]<lo;
                position[axis]=low?lo:hi;
                var normal=Vector3.zero;
                normal[axis]=low?1:-1;
                var point=position;
                point[axis]=low?roomMin[axis]:roomMax[axis];
                Bounce(normal,point,true);
            }
            motionBody.position=position;motionBody.linearVelocity=moveDirection.normalized*moveSpeed;
        }
        void OnCollisionEnter(Collision collision)
        {
            if(!Moving)return;
            foreach(var contact in collision.contacts)
            {
                var point=contact.point;
                bool shell=false;
                for(int axis=0;axis<3;axis++)
                    if(Mathf.Abs(contact.normal[axis])>.95f&&
                        Mathf.Min(Mathf.Abs(point[axis]-roomMin[axis]),Mathf.Abs(point[axis]-roomMax[axis]))<.08f)shell=true;
                Bounce(contact.normal,point,shell);
            }
        }
        void Bounce(Vector3 normal,Vector3 point,bool shell)
        {
            if(Vector3.Dot(moveDirection,normal)>=0)return;
            moveDirection=Vector3.Reflect(moveDirection,normal).normalized;
            if(shell&&Brightness>.001f&&edges&&entry.journey&&entry.journey.floorSurface)
                edges.Impact(point,normal,entry.journey.floorSurface);
        }
        public void SetOn(bool value){TargetOn=value;TransitionEnabled=true;}
        public void HoldCurrentState()=>TransitionEnabled=false;
        public bool WouldChange(bool on)=>Mathf.Abs(Brightness-(on?1:0))>.001f;
        public bool Settling(bool on)=>Brightness!=(on?1:0);
        void ISwitchableLight.Hold()=>HoldCurrentState();
        bool IBrightenableLight.Lit=>Brightness>0;
        public bool CanMove(SentenceMotion motion)=>motion==SentenceMotion.Light&&Brightness>0;
        public bool LookingAtLight()
        {
            return LookedAtWord()=="light";
        }
        string LookedAtWord()=>LookedAtWord(out _,out _);
        string LookedAtWord(out Renderer emitter,out Collider target)
        {
            emitter=null;target=null;
            if(!entry.Entered||entry.Busy||entry.ConfirmingExit||Collecting||!entry.room.Opened||!entry.player.CanInteractWithWorld)return null;
            var view=entry.player.view;
            if(entry.outdoor&&entry.outdoor.IsOutside)return entry.outdoor.World.LookedAtWord(new Ray(view.position,view.forward),out emitter,out target);
            var ray = new Ray(view.position, view.forward);
            if(entry.outdoor && entry.outdoor.InLesson)
                return entry.outdoor.Lesson.WordAt(ray,collectionDistance,out emitter,out target);
            emitter = PickStar(ray, collectionDistance, out var hit);
            if(emitter)return "light";
            target=hit.collider;
            if(!target)return null;
            if(entry.outdoor&&entry.outdoor.InChapter)return entry.outdoor.CurrentRoom.WordAt(hit.collider,out emitter);
            if(entry.journey){var word=entry.journey.WordAt(hit.collider,hit.distance);if(word!=null){if(word=="light")emitter=hit.collider.GetComponent<Renderer>();return word;}}
            if(hit.distance<=7 && entry.board.State.Knows("floor") && edges && edges.surfaces.Contains(hit.collider.GetComponent<Renderer>()) && hit.normal.y>.9f && hit.point.y<RoomCentre.y)return "floor";
            if((!entry.journey||!entry.journey.BeyondChamber)&&Brightness>0&&hit.collider.GetComponentInParent<ChamberLight>()==this){emitter=surface;return "light";}
            return null;
        }

        public Renderer PickStar(Ray ray, float reach, out RaycastHit hit)
        {
            int count=Physics.RaycastNonAlloc(ray,collectionHits,reach,~0,QueryTriggerInteraction.Ignore);
            var hits=count==collectionHits.Length?Physics.RaycastAll(ray,reach,~0,QueryTriggerInteraction.Ignore):collectionHits;
            if(hits!=collectionHits)count=hits.Length;
            hit=default;float closest=float.PositiveInfinity,starReach=reach;
            for(int i=0;i<count;i++)
            {
                if(hits[i].collider.transform.IsChildOf(entry.player.transform))continue;
                if(hits[i].distance<closest){hit=hits[i];closest=hits[i].distance;}
                if(entry.journey&&!entry.journey.IsRail(hits[i].collider))starReach=Mathf.Min(starReach,hits[i].distance);
            }
            var ascent=entry.journey?entry.journey.Ascent:null;
            if(ascent && ascent.Stars && entry.journey.enabled && !ascent.Playing && !entry.journey.EndingDoor.Paused){
                var star=ascent.Stars.Pick(ray,Mathf.Min(reach,closest));
                if(star)return star;
            }
            return entry.journey && entry.journey.InCorridor && entry.journey.ActiveStars
                ? entry.journey.ActiveStars.Pick(ray,starReach) : null;
        }
        void ApplyEmission()
        {
            material.SetColor("_EmissionColor",onEmission*Mathf.SmoothStep(0,1,Brightness)*entry.sentenceRuntime.LampBoost(PassageJourney.LightSpace.Chamber));
            material.SetColor("_BaseColor",Color.Lerp(new Color(.015f,.015f,.015f),onColor,Brightness));
        }

        void Update()
        {
            if(TransitionEnabled)Brightness=RoomLighting.AdvanceLevel(Brightness,TargetOn?1:0,Time.unscaledDeltaTime/Mathf.Max(.01f,fadeDuration),LightOutput?.FinishFraction??0);
            ApplyEmission();
            if(cubeMotion)cubeMotion.Available=Moving&&Brightness>0;
            var mouse=Mouse.current;
            AdvancePointer(mouse!=null&&mouse.leftButton.wasPressedThisFrame,
                mouse!=null&&mouse.leftButton.isPressed,mouse!=null&&mouse.leftButton.wasReleasedThisFrame,Time.unscaledDeltaTime);
            string word=LookedAtWord();bool target=word!=null || entry.journey && entry.journey.Ascent && entry.journey.Ascent.LookingAtSymbol();
            bool showingKnown=knownSymbol&&knownSymbol.gameObject.activeSelf;
            bool action=observer&&observer.Showing;
            hoverEye.gameObject.SetActive((target||action)&&!showingKnown);
            var color=eyeColor;color.a*=(target?entry.board.State.Knows(word):action&&observer.Known)?knownEyeAlpha:1;hoverEye.color=color;
            if(reticle)reticle.SetActive(entry.Entered&&!entry.Busy&&!entry.ConfirmingExit&&!Collecting&&entry.player.CanInteractWithWorld&&!target&&!action&&!showingKnown);
        }
        // Ordinary nouns collect on press; observable targets reserve release for click/hold arbitration.
        public void AdvancePointer(bool pressed,bool held,bool released,float deltaTime)
        {
            if(entry.journey && entry.journey.Ascent && entry.journey.Ascent.LookingAtSymbol()){
                if(pressed){altarPressed=true;pointerGesture=false;pressedWord=null;pointerDuration=0;}
                if(altarPressed && (held||released))pointerDuration+=deltaTime;
                if(released && altarPressed){altarPressed=false;if(pointerDuration<clickDuration)entry.journey.Ascent.Ignite();}
                return;
            }
            altarPressed=false;
            if(pressed)
            {
                pointerGesture=entry.player.CanInteractWithWorld&&!Collecting&&!entry.Busy&&!entry.ConfirmingExit;
                pressedWord=pointerGesture?LookedAtWord():null;pointerDuration=0;
                if(pressedWord!=null&&(!observer||!observer.isActiveAndEnabled||!observer.FindTarget()))
                {
                    pointerGesture=false;pressedWord=null;TryCollect();
                    return;
                }
            }
            if(pointerGesture&&(held||released))pointerDuration+=Mathf.Max(0,deltaTime);
            if(pointerGesture&&(!entry.player.CanInteractWithWorld||Collecting||entry.Busy||entry.ConfirmingExit))
            {pointerGesture=false;pressedWord=null;}
            if(observer&&observer.isActiveAndEnabled)observer.Advance(pointerGesture&&held,deltaTime);
            if(released)
            {
                bool click=pointerGesture&&pointerDuration<clickDuration&&pressedWord!=null&&pressedWord==LookedAtWord();
                pointerGesture=false;pressedWord=null;
                if(click)TryCollect();
            }
            else if(!held) {pointerGesture=false;pressedWord=null;}
        }
        public bool TryCollect()
        {
            string word=LookedAtWord(out var emitter,out var target);if(word==null)return false;
            if(knownFeedback!=null){StopCoroutine(knownFeedback);knownFeedback=null;knownSymbol.gameObject.SetActive(false);}
            if(word=="mirror" && entry.outdoor && entry.outdoor.InLesson && entry.outdoor.Lesson.CollectMirror())return true;
            bool litDoor=false;
            if(word=="door" && target){
                if(entry.outdoor && entry.outdoor.InLesson){
                    litDoor=entry.outdoor.Lesson.DoorLit;
                }else if(entry.outdoor && entry.outdoor.IsOutside){
                    var world=entry.outdoor.World;
                    litDoor=target.GetComponentInParent<Outdoor.ThresholdDoor>()==world.door && world.doors.appearance.SentenceLit;
                }else if(entry.journey){
                    var j=entry.journey;
                    if(j.EndingDoor && j.EndingDoor.Owns(target)){
                        if(j.EndingDoor.TryCollectLesson())return true;
                        litDoor=j.EndingDoor.DoorGlow>0;
                    }else{
                        var motion=j.EntranceDoor && j.EntranceDoor.Owns(target)?j.EntranceDoor.MotionOutput:j.WhiteDoorMotionOutput;
                        litDoor=motion?.EmitsLight==true;
                    }
                }
            }
            if(entry.board.State.Knows(word)){ShowKnownVocabulary(word,litDoor?"light":null);return true;}
            if(word=="light")
            {
                var library=entry.board.GetComponentInChildren<WordLibrary>(true);
                if(!library)library=FindFirstObjectByType<WordLibrary>();
                if(library)CaptureLight(library,emitter);
            }
            StartCoroutine(Collect(word));return true;
        }
        void CaptureLight(WordLibrary library,Renderer emitter)
        {
            var camera=entry.player.view.GetComponent<Camera>();
            var original=camera.targetTexture;
            if(!emitter||!original){library.Remember("light",original);return;}
            var saved=new MaterialPropertyBlock();
            var print=new MaterialPropertyBlock();
            emitter.GetPropertyBlock(saved);
            emitter.GetPropertyBlock(print);
            const float capturePeak=1.15f;
            var emission=print.HasColor("_EmissionColor")?print.GetColor("_EmissionColor"):emitter.sharedMaterial.GetColor("_EmissionColor");
            float peak=emission.maxColorComponent;
            print.SetColor("_EmissionColor",emission*(capturePeak/Mathf.Max(capturePeak,peak)));
            var frame=RenderTexture.GetTemporary(original.descriptor);
            var active=RenderTexture.active;
            try
            {
                emitter.SetPropertyBlock(print);
                camera.targetTexture=frame;
                camera.Render();
                library.Remember("light",frame);
            }
            finally
            {
                camera.targetTexture=original;
                emitter.SetPropertyBlock(saved.isEmpty?null:saved);
                RenderTexture.active=active;
                RenderTexture.ReleaseTemporary(frame);
            }
        }
        public void ShowKnownVocabulary(string id,string prefix=null)
        {
            if(!entry.board.State.Knows(id))return;
            if(knownFeedback!=null)StopCoroutine(knownFeedback);
            knownFeedback=StartCoroutine(ShowKnownWord(id,prefix));
        }
        IEnumerator ShowKnownWord(string id,string prefix)
        {
            KnownFeedbackWord=prefix==null?id:prefix+" "+id;
            bool pair=prefix!=null;
            knownPrefix.gameObject.SetActive(pair);
            if(pair)knownPrefix.sprite=entry.board.Catalog.Word(prefix).Symbol;
            knownSymbol.sprite=entry.board.Catalog.Word(id).Symbol;
            var rect=knownSymbol.rectTransform;
            rect.anchorMin=rect.anchorMax=rect.pivot=new Vector2(.5f,.5f);rect.anchoredPosition=Vector2.zero;
            hoverEye.gameObject.SetActive(false);reticle.SetActive(false);knownSymbol.gameObject.SetActive(true);
            float stride=pair?rect.rect.width:0;
            knownPrefix.rectTransform.anchoredPosition=Vector2.left*stride;
            for(float t=0;t<knownSymbolDuration;t+=Time.unscaledDeltaTime)
            {
                float progress=t/knownSymbolDuration;
                rect.localScale=Vector3.one*Mathf.Lerp(knownSymbolScale.x,knownSymbolScale.y,Mathf.SmoothStep(0,1,progress));
                rect.anchoredPosition=Vector2.right*(.5f*stride*rect.localScale.x);
                float reveal=Mathf.SmoothStep(0,1,progress/.08f);
                float fade=1-Mathf.SmoothStep(0,1,Mathf.InverseLerp(.3f,1,progress));
                knownSymbol.color=new Color(1,1,1,knownSymbolAlpha*reveal*fade);
                if(pair)knownPrefix.color=knownSymbol.color;
                yield return null;
            }
            knownSymbol.gameObject.SetActive(false);knownFeedback=null;KnownFeedbackWord=null;
        }
        public void CollectObservation(Cognition.ObservationLesson lesson,bool wordOnly=false,bool showWord=true)
        {if(!Collecting&&lesson)StartCoroutine(Collect(lesson.verb.Id,lesson,wordOnly,showWord));}
        IEnumerator Collect(string id,Cognition.ObservationLesson lesson=null,bool wordOnly=false,bool showWord=true)
        {
            Collecting=true;hoverEye.gameObject.SetActive(false);if(reticle)reticle.SetActive(false);
            entry.board.Panel.InputBlocked=true;
            var word=entry.board.Catalog.Word(id);
            var rect=collectionSymbol.rectTransform;collectionSymbol.sprite=word.Symbol;collectionSymbol.gameObject.SetActive(showWord);
            float duration=showWord?wordDisplayDuration:0;
            for(float t=0;t<duration;t+=Time.unscaledDeltaTime)
            {
                float fly=Mathf.SmoothStep(0,1,Mathf.InverseLerp(duration-wordFlyDuration,duration,t));
                float reveal=Mathf.SmoothStep(0,1,t/Mathf.Max(.01f,wordFadeInDuration));
                float alpha=reveal*(1-fly*.7f);
                collectionSymbol.color=new Color(1,1,1,alpha);rect.localScale=Vector3.one*Mathf.Lerp(Mathf.Lerp(.9f,1,reveal),.4f,fly);
                rect.anchoredPosition=Vector2.Lerp(Vector2.zero,new Vector2(-hud.rect.width*.42f,-hud.rect.height*.4f),fly);yield return null;
            }
            collectionSymbol.gameObject.SetActive(false);
            Cognition.CognitionState.Group acquired=null;
            if(lesson&&!wordOnly)entry.board.State.TryAcquireSentence(lesson.acquisitionId,lesson.Sentence,out acquired);
            else entry.board.State.TryAcquireWord("collect."+id,word);
            if(acquired!=null)entry.board.PlaceAcquiredSentence(acquired.Id);
            entry.board.Panel.InputBlocked=false;Collecting=false;
            if(acquired!=null)
            {
                var panel=entry.board.Panel;
                panel.SetOpen(true);
                while(panel.IsOpen&&!panel.FullyOpen)yield return null;
                if(panel.FullyOpen&&entry.board.State.Find(acquired.Id)==acquired&&acquired.Effective)
                    entry.feedback.Play(acquired.Id,entry.sentenceRuntime.SustainsFeedback(acquired));
            }
        }
        void OnDisable()
        {
            pointerGesture=false;pressedWord=null;pointerDuration=0;
            if(knownFeedback!=null)StopCoroutine(knownFeedback);
            knownFeedback=null;KnownFeedbackWord=null;if(knownSymbol)knownSymbol.gameObject.SetActive(false);
        }
        void OnDestroy(){if(material)Destroy(material);if(knownSymbol)Destroy(knownSymbol.gameObject);}
    }
}
