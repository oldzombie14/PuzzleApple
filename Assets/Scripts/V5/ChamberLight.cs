using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace PuzzleApple.V5
{
    public sealed class ChamberLight : MonoBehaviour
    {
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
        public float Brightness {get;private set;}=1;
        public bool TransitionEnabled {get;private set;}
        public bool Collecting {get;private set;}
        [Min(0)] public float moveSpeed=1.2f;
        Rigidbody motionBody;
        OuterRoomEdges edges;
        ObservableBehaviour cubeMotion;
        Vector3 moveDirection,roomMin,roomMax;
        public bool Moving=>entry&&entry.Entered&&entry.room.Opened&&entry.sentenceRuntime&&
            entry.sentenceRuntime.HasEffect(Cognition.CognitionSignal.LightMove)&&
            (!entry.journey||!entry.journey.BeyondChamber)&&(!entry.outdoor||!entry.outdoor.IsOutside)&&
            (!entry.portableClosure||!entry.portableClosure.Isolated);
        Material material;
        Color onEmission,onColor;
        readonly RaycastHit[] collectionHits=new RaycastHit[32];
        Image knownSymbol;
        Coroutine knownFeedback;
        Color eyeColor;
        [Min(.05f)] public float clickDuration=.25f;
        string pressedWord;
        bool pointerGesture;
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
        }
        void InitializeMotion()
        {
            motionBody=GetComponent<Rigidbody>();if(!motionBody)motionBody=gameObject.AddComponent<Rigidbody>();
            motionBody.useGravity=false;motionBody.constraints=RigidbodyConstraints.FreezeRotation;
            motionBody.interpolation=RigidbodyInterpolation.Interpolate;motionBody.isKinematic=true;
            var own=GetComponent<Collider>();if(own)Physics.IgnoreCollision(own,entry.player.GetComponent<CharacterController>());
            moveDirection=new Vector3(.6f,.65f,.45f).normalized;
            roomMin=new Vector3(-14,0,-14);roomMax=new Vector3(14,15.92f,14);
            edges=FindFirstObjectByType<OuterRoomEdges>();
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
        public bool LookingAtLight()
        {
            return LookedAtWord()=="light";
        }
        string LookedAtWord()=>LookedAtWord(out _);
        string LookedAtWord(out Renderer emitter)
        {
            emitter=null;
            if(!entry.Entered||entry.Busy||entry.ConfirmingExit||Collecting||!entry.room.Opened||!entry.player.CanInteractWithWorld)return null;
            var view=entry.player.view;
            if(entry.outdoor&&entry.outdoor.IsOutside)return entry.outdoor.World.LookedAtWord(new Ray(view.position,view.forward),out emitter);
            int count=Physics.RaycastNonAlloc(view.position,view.forward,collectionHits,collectionDistance,~0,QueryTriggerInteraction.Ignore);
            var hits=count==collectionHits.Length?Physics.RaycastAll(view.position,view.forward,collectionDistance,~0,QueryTriggerInteraction.Ignore):collectionHits;
            if(hits!=collectionHits)count=hits.Length;
            RaycastHit hit=default;float closest=float.PositiveInfinity,starReach=collectionDistance;
            for(int i=0;i<count;i++)
            {
                if(hits[i].collider.transform.IsChildOf(entry.player.transform))continue;
                if(hits[i].distance<closest){hit=hits[i];closest=hits[i].distance;}
                if(entry.journey&&!entry.journey.IsRail(hits[i].collider))starReach=Mathf.Min(starReach,hits[i].distance);
            }
            if(entry.journey&&entry.journey.Stars)
            {
                emitter=entry.journey.Stars.Pick(new Ray(view.position,view.forward),starReach);
                if(emitter)return "light";
            }
            if(!hit.collider)return null;
            if(entry.journey){var word=entry.journey.WordAt(hit.collider,hit.distance);if(word!=null){if(word=="light")emitter=hit.collider.GetComponent<Renderer>();return word;}}
            if((!entry.journey||!entry.journey.BeyondChamber)&&TargetOn&&Brightness>=.999f&&hit.collider.GetComponentInParent<ChamberLight>()==this){emitter=surface;return "light";}
            return null;
        }
        void Update()
        {
            if(TransitionEnabled)Brightness=Mathf.MoveTowards(Brightness,TargetOn?1:0,Time.deltaTime/Mathf.Max(.01f,fadeDuration));
            material.SetColor("_EmissionColor",onEmission*Mathf.SmoothStep(0,1,Brightness)*entry.sentenceRuntime.LampMultiplier);
            material.SetColor("_BaseColor",Color.Lerp(new Color(.015f,.015f,.015f),onColor,Brightness));
            if(cubeMotion)cubeMotion.Available=Moving&&TargetOn&&Brightness>=.999f;
            var mouse=Mouse.current;
            AdvancePointer(mouse!=null&&mouse.leftButton.wasPressedThisFrame,
                mouse!=null&&mouse.leftButton.isPressed,mouse!=null&&mouse.leftButton.wasReleasedThisFrame,Time.unscaledDeltaTime);
            string word=LookedAtWord();bool target=word!=null;
            bool showingKnown=knownSymbol&&knownSymbol.gameObject.activeSelf;
            bool action=observer&&observer.Showing;
            hoverEye.gameObject.SetActive((target||action)&&!showingKnown);
            var color=eyeColor;color.a*=(target?entry.board.State.Knows(word):action&&observer.Known)?knownEyeAlpha:1;hoverEye.color=color;
            if(reticle)reticle.SetActive(entry.Entered&&!entry.Busy&&!entry.ConfirmingExit&&!Collecting&&entry.player.CanInteractWithWorld&&!target&&!action&&!showingKnown);
        }
        // Ordinary nouns collect on press; observable targets reserve release for click/hold arbitration.
        public void AdvancePointer(bool pressed,bool held,bool released,float deltaTime)
        {
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
            string word=LookedAtWord(out var emitter);if(word==null)return false;
            if(knownFeedback!=null){StopCoroutine(knownFeedback);knownFeedback=null;knownSymbol.gameObject.SetActive(false);}
            if(entry.board.State.Knows(word)){ShowKnownVocabulary(word);return true;}
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
        public void ShowKnownVocabulary(string id)
        {
            if(!entry.board.State.Knows(id))return;
            if(knownFeedback!=null)StopCoroutine(knownFeedback);
            knownFeedback=StartCoroutine(ShowKnownWord(id));
        }
        IEnumerator ShowKnownWord(string id)
        {
            KnownFeedbackWord=id;
            knownSymbol.sprite=entry.board.Catalog.Word(id).Symbol;
            var rect=knownSymbol.rectTransform;
            rect.anchorMin=rect.anchorMax=rect.pivot=new Vector2(.5f,.5f);rect.anchoredPosition=Vector2.zero;
            hoverEye.gameObject.SetActive(false);reticle.SetActive(false);knownSymbol.gameObject.SetActive(true);
            for(float t=0;t<knownSymbolDuration;t+=Time.unscaledDeltaTime)
            {
                float progress=t/knownSymbolDuration;
                rect.localScale=Vector3.one*Mathf.Lerp(knownSymbolScale.x,knownSymbolScale.y,Mathf.SmoothStep(0,1,progress));
                float reveal=Mathf.SmoothStep(0,1,progress/.08f);
                float fade=1-Mathf.SmoothStep(0,1,Mathf.InverseLerp(.3f,1,progress));
                knownSymbol.color=new Color(1,1,1,knownSymbolAlpha*reveal*fade);yield return null;
            }
            knownSymbol.gameObject.SetActive(false);knownFeedback=null;KnownFeedbackWord=null;
        }
        public void CollectObservation(Cognition.ObservationLesson lesson)
        {if(!Collecting&&lesson)StartCoroutine(Collect(lesson.verb.Id,lesson));}
        IEnumerator Collect(string id,Cognition.ObservationLesson lesson=null)
        {
            Collecting=true;hoverEye.gameObject.SetActive(false);if(reticle)reticle.SetActive(false);
            var p=entry.player;bool locked=p.MovementLocked;p.MovementLocked=true;
            entry.board.Panel.InputBlocked=true;
            var word=entry.board.Catalog.Word(id);
            var rect=collectionSymbol.rectTransform;collectionSymbol.sprite=word.Symbol;collectionSymbol.gameObject.SetActive(true);
            float duration=wordDisplayDuration;
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
            if(lesson)entry.board.State.TryAcquireSentence(lesson.acquisitionId,lesson.Sentence,out acquired);
            else entry.board.State.TryAcquireWord("collect."+id,word);
            if(acquired!=null)entry.board.PlaceAcquiredSentence(acquired.Id);
            p.MovementLocked=locked;entry.board.Panel.InputBlocked=false;Collecting=false;
            if(acquired!=null)
            {
                var panel=entry.board.Panel;
                panel.SetOpen(true);
                while(panel.IsOpen&&!panel.FullyOpen)yield return null;
                if(panel.FullyOpen&&entry.board.State.Find(acquired.Id)==acquired&&acquired.Recognized)
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
