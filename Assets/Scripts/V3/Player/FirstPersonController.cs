using UnityEngine;
using UnityEngine.InputSystem;
using PuzzleApple.V3.Cognition;

namespace PuzzleApple.V3
{
    [RequireComponent(typeof(CharacterController))]
    public sealed class FirstPersonController : MonoBehaviour
    {
        public Transform view;
        public CognitionBoard cognition;
        public float moveSpeed = 3.5f, mouseSensitivity = .09f;
        [Header("First steps")]
        public float learningDistance=4.6f, learnMoveDistance=2.3f;
        [Range(.05f,1)] public float initialSpeedFraction=.26f;
        public float learningYawAmplitude=2.8f, learningBobAmplitude=.025f;
        [Range(0,.6f)] public float learningHesitation=.38f;
        public float WalkingConfidence => Mathf.SmoothStep(0,1,Mathf.InverseLerp(.15f,1,WalkedDistance/learningDistance));
        public bool Carried { get; set; }
        public Vector3 CarryTarget { get; set; }
        public bool AppleIdentity { get; set; }
        public bool PanelOpen { get; private set; }
        public bool Locked { get; private set; }
        public float WalkedDistance { get; private set; }
        public bool CanInteractWithWorld => !PanelOpen && !Locked && closedFrame != Time.frameCount;
        CharacterController motor;
        Vector3 spawn, normalView, panelDirection;
        float vertical, pitch, cameraHeight, idleTime;
        float stepPhase,pushTime,learningYaw,learningBob;
        Quaternion panelRotation;
        int closedFrame = -1;
        public void SetPresentationLocked(bool value) { Locked = value; }
        public void SetPanelOpen(bool value, float stop = .2f, float yaw = 2.5f, float idlePitch = .35f)
        {
            PanelOpen = value; closedFrame = value ? -1 : Time.frameCount;
            panelDirection = transform.forward; panelRotation = transform.rotation; idleTime = 0;
            Cursor.lockState = value ? CursorLockMode.None : CursorLockMode.Locked; Cursor.visible = value;
        }
        void Awake()
        {
            motor = GetComponent<CharacterController>(); if (!view) view = GetComponentInChildren<Camera>().transform;
            normalView = view.localPosition; cameraHeight = normalView.y; spawn = transform.position;
        }
        void OnEnable() { Cursor.lockState = CursorLockMode.Locked; Cursor.visible = false; }
        void OnDisable() { Cursor.lockState = CursorLockMode.None; Cursor.visible = true; }
        void Update()
        {
            var key = Keyboard.current; var mouse = Mouse.current;
            if (key != null && key.escapeKey.wasPressedThisFrame) { Cursor.lockState = CursorLockMode.None; Cursor.visible = true; }
            if (!PanelOpen && !Locked && mouse != null && mouse.leftButton.wasPressedThisFrame) { Cursor.lockState = CursorLockMode.Locked; Cursor.visible = false; }
            bool controls = !PanelOpen && !Locked && Cursor.lockState == CursorLockMode.Locked;
            if (controls && mouse != null)
            {
                Vector2 delta = mouse.delta.ReadValue() * mouseSensitivity;
                transform.Rotate(0, delta.x, 0); pitch = Mathf.Clamp(pitch - delta.y, -85, 85);
            }
            if (PanelOpen && !Locked)
            {
                idleTime += Time.deltaTime;
                transform.rotation = panelRotation * Quaternion.Euler(0, Mathf.Sin(idleTime * .5f) * 1.5f * Mathf.Clamp01(idleTime), 0);
            }
            view.localRotation = Quaternion.Euler(pitch, 0, 0);
            cameraHeight = Mathf.MoveTowards(cameraHeight, AppleIdentity ? .42f : normalView.y, Time.deltaTime * 1.8f);
            view.localPosition = new Vector3(normalView.x, cameraHeight, normalView.z);
            float height = AppleIdentity ? .55f : 1.3f;
            motor.height = height; motor.center = Vector3.up * height * .5f;
            var before = transform.position;
            if (Carried && !Locked)
            {
                vertical = 0;
                var target = CarryTarget;
                // Rise clear of the pedestal before moving across it.
                if (transform.position.y < target.y - .07f && Vector2.Distance(new Vector2(target.x,target.z),new Vector2(before.x,before.z)) > .4f)
                    target = new Vector3(before.x, target.y, before.z);
                motor.Move(Vector3.ClampMagnitude(target - before, 2.3f * Time.deltaTime));
            }
            else
            {
                Vector2 input = controls && key != null ? new Vector2((key.dKey.isPressed?1:0)-(key.aKey.isPressed?1:0),(key.wKey.isPressed?1:0)-(key.sKey.isPressed?1:0)) : Vector2.zero;
                input = Vector2.ClampMagnitude(input,1);
                bool blocked = cognition && cognition.State.PlayerMovementBlocked;
                bool forced = cognition && cognition.State.PlayerMoving && !AppleIdentity;
                Vector3 horizontal = forced ? (PanelOpen ? panelDirection : transform.forward) : transform.right * input.x + transform.forward * input.y;
                if (Locked || blocked) horizontal = Vector3.zero;
                if (motor.isGrounded && vertical < 0) vertical = -2;
                vertical += -20 * Time.deltaTime;
                bool stepping=horizontal.sqrMagnitude>.001f&&!AppleIdentity;
                float confidence=AppleIdentity?1:WalkingConfidence;
                float envelope=Mathf.SmoothStep(0,1,Mathf.InverseLerp(.25f,.85f,WalkedDistance))*(1-confidence);
                if(stepping){pushTime+=Time.deltaTime;stepPhase+=Time.deltaTime*Mathf.Lerp(6.4f,10.5f,confidence);}else pushTime=0;
                float footfall=Mathf.Sin(stepPhase)*.5f+.5f;
                float hesitation=1-learningHesitation*envelope*footfall*footfall;
                float push=Mathf.Lerp(Mathf.Lerp(.35f,1,Mathf.SmoothStep(0,1,pushTime/.28f)),1,confidence);
                float speed=moveSpeed*Mathf.Lerp(initialSpeedFraction,1,confidence)*hesitation*push;
                motor.Move((horizontal * speed + Vector3.up * vertical) * Time.deltaTime);
                float distance=Vector3.ProjectOnPlane(transform.position-before,Vector3.up).magnitude;
                if (!Locked&&!PanelOpen&&!AppleIdentity) WalkedDistance += distance;
                float sway=distance>.0001f&&!PanelOpen?envelope:0;
                learningYaw=Mathf.Lerp(learningYaw,(Mathf.Sin(stepPhase*.5f)*.8f+Mathf.Sin(stepPhase*.83f)*.2f)*learningYawAmplitude*sway,1-Mathf.Exp(-5*Time.deltaTime));
                learningBob=Mathf.Lerp(learningBob,Mathf.Sin(stepPhase)*learningBobAmplitude*sway,1-Mathf.Exp(-10*Time.deltaTime));
            }
            if(Carried||Locked||AppleIdentity){learningYaw=0;learningBob=0;}
            // Sway affects the view only, never steers the player's walking path.
            view.localRotation=Quaternion.Euler(pitch,learningYaw,0);
            view.localPosition=new Vector3(normalView.x,cameraHeight+learningBob,normalView.z);
            if (transform.position.y < -4) Teleport(spawn);
        }
        public void Teleport(Vector3 p) { motor.enabled = false; transform.position = p; motor.enabled = true; vertical = 0; }
    }
}
