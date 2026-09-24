using UnityEngine;
using UnityEngine.InputSystem;
using PuzzleApple.Cognition;

namespace PuzzleApple
{
    [RequireComponent(typeof(CharacterController))]
    public sealed class FirstPersonController : MonoBehaviour
    {
        [SerializeField] Transform view;
        [SerializeField] CognitionBoard cognition;
        [SerializeField, Min(0)] float moveSpeed = 3.5f;
        [SerializeField, Min(0)] float mouseSensitivity = 0.09f;
        [SerializeField, Range(1, 89)] float pitchLimit = 85;
        [SerializeField] float gravity = -20;
        [SerializeField] float rescueHeight = -5;
        [Header("Learning to walk")]
        [SerializeField, Range(.01f, 1)] float initialSpeedFraction = .26f;
        [SerializeField, Range(0, 8)] float learningYawAmplitude = 2.8f;
        [SerializeField, Min(0)] float steadyFirstStepDistance = .25f;
        [SerializeField, Min(.01f)] float learningSwayFadeInDistance = .6f;
        [SerializeField, Range(0, .6f)] float learningHesitation = .38f;
        [SerializeField, Range(0, .08f)] float learningBobAmplitude = .025f;
        CharacterController motor;
        Vector3 spawn;
        Quaternion spawnRotation;
        float pitch, verticalSpeed;
        Vector3 horizontalVelocity, panelEntryVelocity;
        bool panelOpen;
        bool presentationLocked;
        int panelClosedFrame = -1;
        float stopDuration, stopElapsed, panelYawLimit, panelYaw;
        float idleElapsed, idleYawPhase, idlePitchPhase, idleEntryPitch, idlePitchLimit;
        readonly System.Random idleRandom = new System.Random();
        Quaternion panelEntryRotation;
        bool movementLocked;
        float learningProgress = 1, learningDistance, learningYaw;
        float learningBob;
        float learningStepPhase, learningPushTime;
        Vector3 viewRestPosition;
        Vector3 panelTravelDirection;
        bool wasForcedWalking;

        public bool MovementLocked => movementLocked;
        public float LearningProgress => learningProgress;
        public float ViewPitch => pitch;
        public float LearningYaw => learningYaw;
        public float LearningBob => learningBob;
        public float NormalMoveSpeed => moveSpeed;
        public bool CognitionMovementBlocked => cognition && cognition.State != null && cognition.State.PlayerMovementBlocked;
        public bool ForcedWalking => cognition && cognition.State != null && cognition.State.PlayerMoving && !CognitionMovementBlocked;
        float WalkingConfidence => Mathf.SmoothStep(0, 1, Mathf.InverseLerp(.15f, 1, learningProgress));
        float LearningEnvelope => Mathf.SmoothStep(0, 1, Mathf.InverseLerp(steadyFirstStepDistance,
            steadyFirstStepDistance + learningSwayFadeInDistance, learningDistance)) * (1 - WalkingConfidence);
        public void SetMovementLocked(bool locked)
        {
            movementLocked = locked;
            if (locked) horizontalVelocity = panelEntryVelocity = Vector3.zero;
        }
        public void SetLearningProgress(float progress) => learningProgress = Mathf.Clamp01(progress);
        public void SetViewPose(float yaw, float lookPitch)
        {
            transform.rotation = Quaternion.Euler(0, yaw, 0);
            pitch = Mathf.Clamp(lookPitch, -pitchLimit, pitchLimit);
            learningYaw = 0;
            learningBob = 0;
            view.localPosition = viewRestPosition;
            view.localRotation = Quaternion.Euler(pitch, 0, 0);
        }

        public bool PanelOpen => panelOpen;
        // Future scene-item interaction must check this before processing clicks/raycasts.
        public bool CanInteractWithWorld => !panelOpen && !presentationLocked && Time.frameCount != panelClosedFrame;
        public void SetPresentationLocked(bool locked)
        {
            presentationLocked = locked;
            if (locked)
            {
                horizontalVelocity = Vector3.zero;
                learningBob = 0;
                view.localPosition = viewRestPosition;
            }
        }
        public Vector3 HorizontalVelocity => horizontalVelocity;

        public void SetPanelOpen(bool open, float stopTime = 0.2f, float yawLimit = 2.5f, float pitchAmplitude = 0.35f)
        {
            if (panelOpen == open) return;
            panelOpen = open;
            if (!open) panelClosedFrame = Time.frameCount;
            if (open)
            {
                // Capture travel before the panel's idle look starts rotating the view.
                panelTravelDirection = horizontalVelocity.sqrMagnitude > .001f ? horizontalVelocity.normalized : transform.forward;
                // Carry the visible yaw into the panel's idle base without a camera jump.
                transform.Rotate(0, learningYaw, 0);
                learningYaw = 0;
                learningBob = 0;
                view.localPosition = viewRestPosition;
                view.localRotation = Quaternion.Euler(pitch, 0, 0);
                panelEntryVelocity = horizontalVelocity;
                stopDuration = Mathf.Max(0, stopTime);
                stopElapsed = panelYaw = 0;
                panelYawLimit = Mathf.Max(0, yawLimit);
                idlePitchLimit = Mathf.Max(0, pitchAmplitude);
                panelEntryRotation = transform.rotation;
                BeginIdleLook();
            }
            LockCursor(!open);
        }

        void BeginIdleLook()
        {
            idleElapsed = 0;
            idleEntryPitch = pitch;
            idleYawPhase = (float)idleRandom.NextDouble() * Mathf.PI * 2;
            idlePitchPhase = (float)idleRandom.NextDouble() * Mathf.PI * 2;
        }

        void UpdateIdleLook(float deltaTime)
        {
            idleElapsed += deltaTime;
            float t = Mathf.Clamp01(idleElapsed / 2f);
            float fade = t * t * t * (t * (6 * t - 15) + 10);
            // Continuous, differently paced waves avoid stop-start glances and a fixed oval loop.
            // Random phases vary each opening without introducing abrupt random targets.
            panelYaw = panelYawLimit * fade *
                (0.78f * Mathf.Sin(idleElapsed * 0.48f + idleYawPhase) +
                 0.22f * Mathf.Sin(idleElapsed * 0.83f + idlePitchPhase));
            float pitchOffset = idlePitchLimit * fade *
                (0.65f * Mathf.Sin(idleElapsed * 0.67f + idlePitchPhase) +
                 0.35f * Mathf.Sin(idleElapsed * 1.07f + idleYawPhase));
            transform.rotation = Quaternion.AngleAxis(panelYaw, Vector3.up) * panelEntryRotation;
            // Keep pitch synchronized for a seamless handoff when the panel closes.
            pitch = Mathf.Clamp(idleEntryPitch + pitchOffset, -pitchLimit, pitchLimit);
            view.localRotation = Quaternion.Euler(pitch, 0, 0);
        }

        void Awake()
        {
            motor = GetComponent<CharacterController>();
            if (!cognition) cognition = FindFirstObjectByType<CognitionBoard>();
            if (!view) view = GetComponentInChildren<Camera>().transform;
            viewRestPosition = view.localPosition;
            spawn = transform.position;
            spawnRotation = transform.rotation;
        }
        void OnEnable() => LockCursor(!panelOpen);
        void OnDisable() => LockCursor(false);
        void OnApplicationFocus(bool focused) { if (!focused) LockCursor(false); }
        static void LockCursor(bool locked)
        {
            Cursor.lockState = locked ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible = !locked;
        }
        void Update()
        {
            var keyboard = Keyboard.current;
            var mouse = Mouse.current;
            if (keyboard != null && keyboard.escapeKey.wasPressedThisFrame) LockCursor(false);
            else if (!panelOpen && !presentationLocked && mouse != null && mouse.leftButton.wasPressedThisFrame) LockCursor(true);
            Vector2 input = Vector2.zero;
            if (!panelOpen && !presentationLocked && Cursor.lockState == CursorLockMode.Locked)
            {
                if (mouse != null)
                {
                    var delta = mouse.delta.ReadValue() * mouseSensitivity;
                    transform.Rotate(0, delta.x, 0);
                    pitch = Mathf.Clamp(pitch - delta.y, -pitchLimit, pitchLimit);
                    view.localRotation = Quaternion.Euler(pitch, 0, 0);
                }
                if (keyboard != null && !movementLocked)
                    input = new Vector2((keyboard.dKey.isPressed ? 1 : 0) - (keyboard.aKey.isPressed ? 1 : 0),
                        (keyboard.wKey.isPressed ? 1 : 0) - (keyboard.sKey.isPressed ? 1 : 0));
            }
            input = Vector2.ClampMagnitude(input, 1);
            bool forcedWalking = ForcedWalking;
            bool cognitionBlocked = CognitionMovementBlocked;
            if (cognitionBlocked || (wasForcedWalking && !forcedWalking && panelOpen))
            {
                panelEntryVelocity = Vector3.zero;
                stopElapsed = stopDuration;
            }
            wasForcedWalking = forcedWalking;
            if (motor.isGrounded && verticalSpeed < 0) verticalSpeed = -2;
            verticalSpeed += gravity * Time.deltaTime;
            if (panelOpen && !forcedWalking)
            {
                stopElapsed += Time.deltaTime;
                horizontalVelocity = panelEntryVelocity * (stopDuration > 0 ? 1 - Mathf.Clamp01(stopElapsed / stopDuration) : 0);
            }
            else
            {
                bool stepping = (forcedWalking || input.sqrMagnitude > .001f) && !movementLocked && !presentationLocked && !cognitionBlocked;
                if (stepping)
                {
                    learningPushTime += Time.deltaTime;
                    learningStepPhase += Time.deltaTime * Mathf.Lerp(6.4f, 10.5f, WalkingConfidence);
                }
                else learningPushTime = 0;
                // Short pushes separated by a small hesitation, then a gradual transition to steady walking.
                float footfall = Mathf.Sin(learningStepPhase) * .5f + .5f;
                float hesitation = 1 - learningHesitation * LearningEnvelope * footfall * footfall;
                float push = Mathf.Lerp(Mathf.Lerp(.35f, 1, Mathf.SmoothStep(0, 1, learningPushTime / .28f)), 1, WalkingConfidence);
                Vector3 travel = forcedWalking ? (panelOpen ? panelTravelDirection : transform.forward)
                    : transform.right * input.x + transform.forward * input.y;
                horizontalVelocity = travel * moveSpeed *
                    Mathf.Lerp(initialSpeedFraction, 1, WalkingConfidence) * hesitation * push;
            }
            if (panelOpen && !presentationLocked) UpdateIdleLook(Time.deltaTime);
            if (movementLocked || presentationLocked || cognitionBlocked) horizontalVelocity = Vector3.zero;
            var velocity = horizontalVelocity;
            velocity.y = verticalSpeed;
            Vector3 before = transform.position;
            Vector3 displacement = velocity * Time.deltaTime;
            motor.Move(displacement);
            float distance = Vector3.ProjectOnPlane(transform.position - before, Vector3.up).magnitude;
            if (!panelOpen && !presentationLocked)
            {
                learningDistance += distance;
                // First steps are straight. Then tentative alternating corrections fade with mastery.
                float envelope = LearningEnvelope;
                float target = distance > .0001f ? (Mathf.Sin(learningStepPhase * .5f) * .8f + Mathf.Sin(learningStepPhase * .83f) * .2f) * learningYawAmplitude * envelope : 0;
                learningYaw = Mathf.Lerp(learningYaw, target, 1 - Mathf.Exp(-5 * Time.deltaTime));
                float bobTarget = distance > .0001f ? Mathf.Sin(learningStepPhase) * learningBobAmplitude * envelope : 0;
                learningBob = Mathf.Lerp(learningBob, bobTarget, 1 - Mathf.Exp(-10 * Time.deltaTime));
                view.localPosition = viewRestPosition + Vector3.up * learningBob;
                view.localRotation = Quaternion.Euler(pitch, learningYaw, 0);
            }
            if (transform.position.y < rescueHeight) Respawn();
        }
        public void Respawn()
        {
            motor.enabled = false;
            transform.SetPositionAndRotation(spawn, spawnRotation);
            view.localRotation = Quaternion.identity;
            pitch = verticalSpeed = 0;
            learningBob = 0;
            view.localPosition = viewRestPosition;
            horizontalVelocity = panelEntryVelocity = Vector3.zero;
            panelEntryRotation = spawnRotation;
            panelYaw = 0;
            if (panelOpen) BeginIdleLook();
            motor.enabled = true;
        }
    }
}
