using UnityEngine;
using UnityEngine.InputSystem;

namespace PuzzleApple.V5
{
    [RequireComponent(typeof(CharacterController))]
    public sealed class FirstPersonController : MonoBehaviour
    {
        public Transform view;
        public PassageJourney journey;
        public FoldingRoom closedRoom;
        public float moveSpeed=3.5f, mouseSensitivity=.09f;
        public float WalkMultiplier {get;private set;}=1;
        [Min(0)] public float jumpHeight=1.2f;
        public bool MovementLocked {get;set;}
        public bool CinematicLocked {get;set;}
        public bool PointerUIActive {get;set;}
        public bool PanelOpen {get;private set;}
        bool Respawning => journey && journey.Platform && journey.Platform.Respawning;
        public bool CanInteractWithWorld => isActiveAndEnabled&&!CinematicLocked&&!Respawning&&!PanelOpen&&closedFrame!=Time.frameCount&&Cursor.lockState==CursorLockMode.Locked;
        CharacterController motor;
        Collider support;
        Vector3 carrierTravel;
        void OnControllerColliderHit(ControllerColliderHit hit)
        {
            if(hit.normal.y>.7f)support=hit.collider;
        }
        public bool StandingOn(Collider deck)=>motor.enabled&&motor.isGrounded&&support==deck;
        public void CarryOn(Collider deck,Vector3 travel)
        {
            if(isActiveAndEnabled && StandingOn(deck))
                carrierTravel+=travel;
        }
        CollisionFlags MoveWithCarrier(Vector3 motion)
        {
            // Combining the grounding step with upward travel can cancel the lift for
            // several frames, until the rising collider pushes the capsule out of its skin.
            if(carrierTravel.y!=0){
                motor.Move(carrierTravel);
                return motor.Move(motion);
            }
            return motor.Move(motion+carrierTravel);
        }
        Vector3 normalView,launchVelocity;
        public float VerticalVelocity=>vertical;
        public void Launch(Vector3 velocity){launchVelocity=Vector3.ProjectOnPlane(velocity,Vector3.up);vertical=velocity.y;}
        Quaternion panelRotation;
        float viewRoll;
        float pitch,vertical,idleTime,panelSway,panelPitch;

        int closedFrame=-1;
        void Awake()
        {
            motor=GetComponent<CharacterController>();
            if(!closedRoom)closedRoom=FindFirstObjectByType<FoldingRoom>();
            if(!view)view=GetComponentInChildren<Camera>().transform;
            normalView=view.localPosition;AdoptCurrentView();
        }
        void OnEnable()=>LockCursor(true);
        void OnDisable()=>LockCursor(false);
        void OnApplicationFocus(bool focused){if(!focused)LockCursor(false);}
        static void LockCursor(bool locked){Cursor.lockState=locked?CursorLockMode.Locked:CursorLockMode.None;Cursor.visible=false;}
        public void RestoreGameplayCursor()
        {
            if(isActiveAndEnabled&&!CinematicLocked)LockCursor(!PanelOpen&&!PointerUIActive);
        }
        public void SuppressInteractionThisFrame()=>closedFrame=Time.frameCount;
        public void SetPanelOpen(bool open,float stop=.2f,float swayAngle=2.5f,float idlePitch=.35f)
        {
            if(PanelOpen&&!open)AdoptCurrentView();
            PanelOpen=open;closedFrame=open?-1:Time.frameCount;
            panelRotation=transform.rotation;idleTime=0;panelSway=swayAngle;panelPitch=idlePitch;
            LockCursor(!open);
        }
        public void Teleport(Vector3 position,Quaternion rotation)
        {
            support=null;carrierTravel=Vector3.zero;
            bool active=motor.enabled;motor.enabled=false;
            if(journey && journey.entry.outdoor)journey.entry.outdoor.ResetCrossing();
            transform.SetPositionAndRotation(position,rotation);motor.enabled=active;
            if(journey)journey.RefreshCollisionExclusions();
            launchVelocity=Vector3.zero;vertical=0;view.localRotation=Quaternion.identity;AdoptCurrentView();
        }
        public void AdoptCurrentView()
        {
            viewRoll=view.eulerAngles.z;
            var forward=view.forward;
            float yaw=Mathf.Atan2(forward.x,forward.z)*Mathf.Rad2Deg;
            pitch=-Mathf.Asin(Mathf.Clamp(forward.y,-1,1))*Mathf.Rad2Deg;
            transform.rotation=Quaternion.Euler(0,yaw,0);view.localRotation=Quaternion.Euler(pitch,0,viewRoll);
            panelRotation=transform.rotation;idleTime=0;
        }
        void Update()
        {
            if(CinematicLocked){
                if(carrierTravel.sqrMagnitude>0)MoveWithCarrier(-Vector3.up*motor.skinWidth);
                carrierTravel=Vector3.zero;
                return;
            }
            var key=Keyboard.current;var mouse=Mouse.current;
            if(key!=null&&key.escapeKey.wasPressedThisFrame)LockCursor(false);
            else if(!PanelOpen&&!PointerUIActive&&mouse!=null&&mouse.leftButton.wasPressedThisFrame)LockCursor(true);
            bool controls=!PanelOpen&&!PointerUIActive&&Cursor.lockState==CursorLockMode.Locked;
            if(controls&&mouse!=null)
            {
                var delta=mouse.delta.ReadValue()*mouseSensitivity;
                transform.Rotate(0,delta.x,0);pitch=Mathf.Clamp(pitch-delta.y,-89,89);
            }
            if(PanelOpen){idleTime+=Time.deltaTime;transform.rotation=panelRotation;}
            var input=controls&&key!=null?new Vector2((key.dKey.isPressed?1:0)-(key.aKey.isPressed?1:0),(key.wKey.isPressed?1:0)-(key.sKey.isPressed?1:0)):Vector2.zero;
            AdvanceMovement(input,controls,Time.deltaTime,controls&&key!=null&&key.spaceKey.wasPressedThisFrame);
        }
        internal void AdvanceMovement(Vector2 input,bool controls,float deltaTime,bool jumpPressed=false)
        {
            var pass=journey?journey.entry.outdoor:null;
            var chapter=pass && pass.InChapter?pass.CurrentRoom:null;
            float targetSpeed=chapter?chapter.WalkingMultiplier(transform.position):1;
            float transition=chapter && chapter.layout?chapter.layout.speedTransition:.4f;
            WalkMultiplier=Mathf.MoveTowards(WalkMultiplier,targetSpeed,deltaTime/Mathf.Max(.01f,transition));
            // Lock includes gravity; orientation remains independent.
            if(deltaTime>0 && MovementLocked){vertical=0;launchVelocity=Vector3.zero;
                if(carrierTravel.sqrMagnitude>0)MoveWithCarrier(-Vector3.up*motor.skinWidth);
            }
            else if(deltaTime>0)
            {
                // Collection and respawn suppress new movement input while airborne motion continues.
                if (Respawning || (journey && journey.entry.chamberLight.Collecting))
                {
                    input = Vector2.zero;
                    jumpPressed = false;
                }
                input=Vector2.ClampMagnitude(input,1);
                if(motor.isGrounded&&vertical<0)vertical=-2;
                if(controls&&jumpPressed&&motor.isGrounded&&vertical<=0&&jumpHeight>0)
                    vertical=Mathf.Sqrt(40*jumpHeight);
                bool starFall = pass && pass.InLesson && pass.Lesson.InStarfield && vertical <= 0;
                vertical = starFall ? Mathf.Max(-3f, vertical - 3.5f * deltaTime) : vertical - 20 * deltaTime;
                // Keep the short, visible opening traversable for about a second.
                if(vertical<0&&journey&&journey.enabled&&journey.InSecondRoom&&journey.floorSurface.HoleOpen)
                {
                    var feet=journey.Root.InverseTransformPoint(transform.position);
                    if(feet.y<.2f&&journey.floorSurface.FitsOpening(feet,motor.radius))
                        vertical=Mathf.Max(vertical,-2f);
                }
                var motion=((transform.right*input.x+transform.forward*input.y)*moveSpeed*WalkMultiplier+launchVelocity+Vector3.up*vertical)*deltaTime;
                launchVelocity=Vector3.MoveTowards(launchVelocity,Vector3.zero,8*deltaTime);
                if(journey&&journey.enabled)motion=journey.ConstrainMovement(transform.position,motion);
                if(closedRoom)motion=closedRoom.ConstrainClosedMovement(motor,motion);
                // Use the same ground contact while riding, including camera and UI locks.
                // A shorter gravity step must not change the capsule's skin penetration each frame.
                if(carrierTravel.sqrMagnitude>0 && motor.isGrounded && vertical<=0)
                    motion.y=-motor.skinWidth;
                var collisions=MoveWithCarrier(motion);
                if((collisions&CollisionFlags.Above)!=0&&vertical>0)vertical=0;
            }
            carrierTravel=Vector3.zero;
            float sway=PanelOpen?(Mathf.Sin(idleTime*.5f)*panelSway+Mathf.Sin(idleTime*.37f)*panelPitch)*Mathf.Clamp01(idleTime*.5f):0;

            view.localRotation=Quaternion.Euler(pitch+sway,0,viewRoll);view.localPosition=normalView;
        }
    }
}
