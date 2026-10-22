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
        public bool PanelOpen { get; private set; }
        public bool CanInteractWithWorld => isActiveAndEnabled && !PanelOpen && closedFrame != Time.frameCount && Cursor.lockState == CursorLockMode.Locked;
        CharacterController motor;
        Vector3 spawn, normalView;
        Quaternion spawnRotation, panelRotation;
        float vertical, pitch, idleTime, stepPhase, pushTime, learningYaw, learningBob;
        float panelYaw, panelPitch;
        int closedFrame = -1;

        void Awake()
        {
            motor = GetComponent<CharacterController>();
            if (!view) view = GetComponentInChildren<Camera>().transform;
            spawn = transform.position; spawnRotation = transform.rotation; normalView = view.localPosition;
        }
        void OnEnable() => LockCursor(true);
        void OnDisable() => LockCursor(false);
        void OnApplicationFocus(bool focus) { if (!focus) LockCursor(false); }
        static void LockCursor(bool locked) { Cursor.lockState = locked ? CursorLockMode.Locked : CursorLockMode.None; Cursor.visible = !locked; }
        public void SetPanelOpen(bool open, float stop = .2f, float yaw = 2.5f, float idlePitch = .35f)
        {
            PanelOpen = open; closedFrame = open ? -1 : Time.frameCount;
            panelRotation = transform.rotation; idleTime = 0; panelYaw = yaw; panelPitch = idlePitch;
            LockCursor(!open);
        }
        public void Teleport(Vector3 position, Quaternion rotation)
        {
            bool active=motor.enabled; motor.enabled=false;
            transform.SetPositionAndRotation(position,rotation); motor.enabled=active;
            spawn=position; spawnRotation=rotation; panelRotation=rotation;
            vertical=pitch=learningYaw=learningBob=0;
        }
        void Update()
        {
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
                transform.rotation = panelRotation * Quaternion.Euler(0, Mathf.Sin(idleTime * .5f) * panelYaw * Mathf.Clamp01(idleTime * .5f), 0);
            }
            Vector2 input = controls && key != null ? new Vector2((key.dKey.isPressed ? 1 : 0) - (key.aKey.isPressed ? 1 : 0), (key.wKey.isPressed ? 1 : 0) - (key.sKey.isPressed ? 1 : 0)) : Vector2.zero;
            input = MovementLocked ? Vector2.zero : Vector2.ClampMagnitude(input, 1);
            var horizontal = transform.right * input.x + transform.forward * input.y;
            if (motor.isGrounded && vertical < 0) vertical = -2;
            vertical += -20 * Time.deltaTime;
            float confidence = WalkingConfidence;
            float envelope = Mathf.SmoothStep(0, 1, Mathf.InverseLerp(.25f, .85f, WalkedDistance)) * (1 - confidence);
            if (input.sqrMagnitude > .001f) { pushTime += Time.deltaTime; stepPhase += Time.deltaTime * Mathf.Lerp(6.4f, 10.5f, confidence); } else pushTime = 0;
            float footfall = Mathf.Sin(stepPhase) * .5f + .5f;
            float hesitation = 1 - learningHesitation * envelope * footfall * footfall;
            float push = Mathf.Lerp(Mathf.Lerp(.35f, 1, Mathf.SmoothStep(0, 1, pushTime / .28f)), 1, confidence);
            float speed = moveSpeed * Mathf.Lerp(initialSpeedFraction, 1, confidence) * hesitation * push;
            var before = transform.position;
            motor.Move((horizontal * speed + Vector3.up * vertical) * Time.deltaTime);
            float distance = Vector3.ProjectOnPlane(transform.position - before, Vector3.up).magnitude;
            if (controls && !MovementLocked && input.sqrMagnitude > .001f) WalkedDistance += distance;
            float sway = distance > .0001f && !PanelOpen && !MovementLocked ? envelope : 0;
            learningYaw = Mathf.Lerp(learningYaw, (Mathf.Sin(stepPhase * .5f) * .8f + Mathf.Sin(stepPhase * .83f) * .2f) * learningYawAmplitude * sway, 1 - Mathf.Exp(-5 * Time.deltaTime));
            learningBob = Mathf.Lerp(learningBob, Mathf.Sin(stepPhase) * learningBobAmplitude * sway, 1 - Mathf.Exp(-10 * Time.deltaTime));
            view.localRotation = Quaternion.Euler(pitch + (PanelOpen ? Mathf.Sin(idleTime * .37f) * panelPitch * Mathf.Clamp01(idleTime * .5f) : 0), learningYaw, 0);
            view.localPosition = normalView + Vector3.up * learningBob;
            if (transform.position.y < -5)
            {
                motor.enabled = false; transform.SetPositionAndRotation(spawn, spawnRotation); motor.enabled = true;
                vertical = pitch = learningYaw = learningBob = 0;
            }
        }
    }
}
