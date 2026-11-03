using UnityEngine;
using UnityEngine.InputSystem;

namespace PuzzleApple.V4
{
    [RequireComponent(typeof(CharacterController))]
    public sealed class FirstPersonController : MonoBehaviour
    {
        public Transform view;
        public float moveSpeed = 3.5f, mouseSensitivity = .09f;
        [Header("First steps")]
        public float learningDistance = 4.6f, learnMoveDistance = 2.3f;
        [Range(.05f, 1)] public float initialSpeedFraction = .26f;
        public float learningYawAmplitude = 2.8f, learningBobAmplitude = .025f;
        [Range(0, .6f)] public float learningHesitation = .38f;
        public float WalkedDistance { get; private set; }
        public float WalkingConfidence => Mathf.SmoothStep(0, 1, Mathf.InverseLerp(.15f, 1, WalkedDistance / learningDistance));
        public bool MovementLocked { get; set; }
        public bool CinematicLocked { get; set; }
        public bool PanelOpen { get; private set; }
        public bool CanInteractWithWorld => isActiveAndEnabled && !CinematicLocked && !PanelOpen && closedFrame != Time.frameCount && Cursor.lockState == CursorLockMode.Locked;
        CharacterController motor;
        Vector3 spawn, normalView;
        Vector3 defaultView, defaultCenter;
        float defaultHeight, defaultStepOffset;
        Quaternion spawnRotation, panelRotation;
        float vertical, pitch, idleTime, stepPhase, pushTime, learningYaw, learningBob;
        float panelSway, panelPitch;
        int closedFrame = -1;

        void Awake()
        {
            motor = GetComponent<CharacterController>();
            if (!view) view = GetComponentInChildren<Camera>().transform;
            spawn = transform.position; spawnRotation = transform.rotation; normalView = view.localPosition;
            defaultView=normalView;defaultCenter=motor.center;defaultHeight=motor.height;defaultStepOffset=motor.stepOffset;
        }
        public void SetEyeHeight(float height)
        {
            normalView=defaultView;
            if(height>0)normalView.y=height;
            view.localPosition=normalView;
            motor.height=height>0?height+.15f:defaultHeight;
            motor.center=height>0?new Vector3(defaultCenter.x,motor.height*.5f,defaultCenter.z):defaultCenter;
            motor.stepOffset=height>0?Mathf.Max(defaultStepOffset,.5f):defaultStepOffset;
        }
        void OnEnable() => LockCursor(true);
        void OnDisable() => LockCursor(false);
        void OnApplicationFocus(bool focus) { if (!focus) LockCursor(false); }
        static void LockCursor(bool locked) { Cursor.lockState = locked ? CursorLockMode.Locked : CursorLockMode.None; Cursor.visible = !locked; }
        public void SuppressInteractionThisFrame() => closedFrame = Time.frameCount;
        public void SetPanelOpen(bool open, float stop = .2f, float swayAngle = 2.5f, float idlePitch = .35f)
        {
            if (PanelOpen && !open) AdoptCurrentView();
            PanelOpen = open; closedFrame = open ? -1 : Time.frameCount;
            panelRotation = transform.rotation; idleTime = 0; panelSway = swayAngle; panelPitch = idlePitch;
            LockCursor(!open);
        }
        public void Teleport(Vector3 position, Quaternion rotation)
        {
            bool active=motor.enabled; motor.enabled=false;
            transform.SetPositionAndRotation(position,rotation); motor.enabled=active;
            spawn=position; spawnRotation=rotation; panelRotation=rotation;
            vertical=pitch=learningYaw=learningBob=0;
        }
        public void AdoptCurrentView()
        {
            var forward=view.forward;
            float yaw=Mathf.Atan2(forward.x,forward.z)*Mathf.Rad2Deg;
            pitch=-Mathf.Asin(Mathf.Clamp(forward.y,-1,1))*Mathf.Rad2Deg;
            transform.rotation=Quaternion.Euler(0,yaw,0);
            view.localRotation=Quaternion.Euler(pitch,0,0);
            learningYaw=learningBob=0;
            panelRotation=transform.rotation;idleTime=0;
        }
        void Update()
        {
            if (CinematicLocked) return;
            var key = Keyboard.current; var mouse = Mouse.current;
            if (key != null && key.escapeKey.wasPressedThisFrame) LockCursor(false);
            else if (!PanelOpen && mouse != null && mouse.leftButton.wasPressedThisFrame) LockCursor(true);
            bool controls = !PanelOpen && Cursor.lockState == CursorLockMode.Locked;
            if (controls && mouse != null)
            {
                var delta = mouse.delta.ReadValue() * mouseSensitivity;
                transform.Rotate(0, delta.x, 0); pitch = Mathf.Clamp(pitch - delta.y, -85, 85);
            }
            if (PanelOpen)
            {
                idleTime += Time.deltaTime;
                transform.rotation = panelRotation;
            }
            Vector2 input = controls && key != null ? new Vector2((key.dKey.isPressed ? 1 : 0) - (key.aKey.isPressed ? 1 : 0), (key.wKey.isPressed ? 1 : 0) - (key.sKey.isPressed ? 1 : 0)) : Vector2.zero;
            AdvanceMovement(input, controls, Time.deltaTime);
        }
        // Shared by live input and the editor-only first-steps recorder.
        internal void AdvanceMovement(Vector2 input, bool controls, float deltaTime)
        {
            input = MovementLocked ? Vector2.zero : Vector2.ClampMagnitude(input, 1);
            var horizontal = transform.right * input.x + transform.forward * input.y;
            if (motor.isGrounded && vertical < 0) vertical = -2;
            vertical += -20 * deltaTime;
            float speed = moveSpeed;
            var before = transform.position;
            motor.Move((horizontal * speed + Vector3.up * vertical) * deltaTime);
            float distance = Vector3.ProjectOnPlane(transform.position - before, Vector3.up).magnitude;
            if (controls && !MovementLocked && input.sqrMagnitude > .001f) WalkedDistance += distance;
            view.localRotation = Quaternion.Euler(pitch + (PanelOpen ? (Mathf.Sin(idleTime * .5f) * panelSway + Mathf.Sin(idleTime * .37f) * panelPitch) * Mathf.Clamp01(idleTime * .5f) : 0), 0, 0);
            view.localPosition = normalView;
            if (transform.position.y < -5)
            {
                motor.enabled = false; transform.SetPositionAndRotation(spawn, spawnRotation); motor.enabled = true;
                vertical = pitch = learningYaw = learningBob = 0;
            }
        }
    }
}
