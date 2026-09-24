using System.Collections;
using PuzzleApple.Cognition;
using UnityEngine;

namespace PuzzleApple
{
    [DefaultExecutionOrder(-200)]
    public sealed class OpeningTutorial : MonoBehaviour
    {
        public enum Phase { LookingForMirror, FocusingMirror, ReadingI, ReturningForward, LearningToWalk, DiscoveringMove, Complete }
        public const string SelfAcquisition = "tutorial.mirror.self";
        public const string MoveAcquisition = "tutorial.walk.move";
        [SerializeField] FirstPersonController player;
        [SerializeField] Camera view;
        [SerializeField] CognitionBoard board;
        [SerializeField] CognitionWorldInteraction presentation;
        [SerializeField] BoxCollider mirrorTarget;
        [SerializeField] Transform recoveryEnd;
        [SerializeField, Min(.1f)] float gazeDuration = 2;
        [SerializeField, Min(.1f)] float gazeDistance = 3;
        [SerializeField, Range(20, 90)] float mirrorFieldOfView = 48;
        [SerializeField, Min(.1f)] float focusDuration = .65f;
        [SerializeField, Min(0)] float mirrorHold = .45f;
        [SerializeField, Min(.1f)] float returnDuration = .8f;
        [SerializeField, Range(.1f, 1)] float moveAcquisitionProgress = .5f;
        Vector3 startPosition, recoveryDirection;
        float recoveryLength, originalFov, forwardYaw, furthestDistance;
        public Phase CurrentPhase { get; private set; }
        public float GazeElapsed { get; private set; }
        public float RecoveryProgress => recoveryLength > 0 ? Mathf.Clamp01(furthestDistance / recoveryLength) : 0;
        public bool MirrorInSight { get; private set; }

        void Awake()
        {
            if (!player || !view || !board || !presentation || !mirrorTarget || !recoveryEnd)
            {
                Debug.LogError("Opening tutorial requires player, camera, board, presenter, mirror and recovery endpoint.", this);
                enabled = false;
                return;
            }
            startPosition = player.transform.position;
            Vector3 route = Vector3.ProjectOnPlane(recoveryEnd.position - startPosition, Vector3.up);
            recoveryLength = route.magnitude;
            recoveryDirection = route.normalized;
            originalFov = view.fieldOfView;
            forwardYaw = player.transform.eulerAngles.y;
            player.SetMovementLocked(true);
            player.SetLearningProgress(0);
            board.Panel.InputBlocked = true;
        }
        void Update()
        {
            if (CurrentPhase == Phase.LookingForMirror)
            {
                MirrorInSight = IsLookingAtMirror();
                GazeElapsed = MirrorInSight ? GazeElapsed + Time.deltaTime : 0;
                if (GazeElapsed >= gazeDuration) StartCoroutine(DiscoverSelf());
            }
            else if ((CurrentPhase == Phase.LearningToWalk || CurrentPhase == Phase.DiscoveringMove)
                && ((!board.Panel.IsOpen && player.CanInteractWithWorld) || (board.Panel.IsOpen && player.ForcedWalking)))
            {
                // Maximum forward progress: waiting, wall pushing and backtracking cannot farm or undo recovery.
                furthestDistance = Mathf.Max(furthestDistance, Vector3.Dot(player.transform.position - startPosition, recoveryDirection));
                player.SetLearningProgress(RecoveryProgress);
                if (CurrentPhase == Phase.LearningToWalk)
                {
                    if (RecoveryProgress >= moveAcquisitionProgress && !board.State.HasAcquired(MoveAcquisition)
                        && !presentation.IsPresenting) StartCoroutine(DiscoverMove());
                    else if (RecoveryProgress >= 1 && board.State.HasAcquired(MoveAcquisition)) CurrentPhase = Phase.Complete;
                }
            }
        }
        public bool IsLookingAtMirror()
        {
            if (board.Panel.IsOpen || Cursor.lockState != CursorLockMode.Locked || !player.CanInteractWithWorld) return false;
            Ray ray = view.ViewportPointToRay(new Vector3(.5f, .5f));
            if (!mirrorTarget.Raycast(ray, out var mirrorHit, gazeDistance)) return false;
            // Explicitly test the trigger, then reject solid occluders in front of it.
            return !Physics.Raycast(ray, out var obstruction, gazeDistance, ~0, QueryTriggerInteraction.Ignore)
                || obstruction.distance >= mirrorHit.distance;
        }
        IEnumerator DiscoverSelf()
        {
            CurrentPhase = Phase.FocusingMirror;
            MirrorInSight = false;
            player.SetPresentationLocked(true);
            Vector3 direction = mirrorTarget.bounds.center - view.transform.position;
            Quaternion look = Quaternion.LookRotation(direction);
            float pitch = Mathf.DeltaAngle(0, look.eulerAngles.x);
            yield return LookTo(look.eulerAngles.y, pitch, mirrorFieldOfView, focusDuration);
            yield return new WaitForSeconds(mirrorHold);
            yield return presentation.PresentWord(SelfAcquisition, board.Catalog.Word("i"));
            CurrentPhase = Phase.ReadingI;
            player.SetPresentationLocked(false);
            board.Panel.InputBlocked = false;
            board.Panel.SetOpen(true);
            while (board.Panel.IsOpen) yield return null;
            CurrentPhase = Phase.ReturningForward;
            board.Panel.InputBlocked = true;
            player.SetPresentationLocked(true);
            yield return LookTo(forwardYaw, 0, originalFov, returnDuration);
            player.SetPresentationLocked(false);
            player.SetMovementLocked(false);
            board.Panel.InputBlocked = false;
            CurrentPhase = Phase.LearningToWalk;
        }
        IEnumerator DiscoverMove()
        {
            CurrentPhase = Phase.DiscoveringMove;
            // Recognition happens mid-stride; the overlay does not own locomotion or the camera.
            yield return presentation.PresentWord(MoveAcquisition, board.Catalog.Word("move"), acquireOnStart: true);
            CurrentPhase = RecoveryProgress >= 1 ? Phase.Complete : Phase.LearningToWalk;
        }
        IEnumerator LookTo(float yaw, float pitch, float fov, float duration)
        {
            float fromYaw = player.transform.eulerAngles.y;
            float fromPitch = player.ViewPitch;
            float fromFov = view.fieldOfView;
            for (float elapsed = 0; elapsed < duration; elapsed += Time.deltaTime)
            {
                float t = Mathf.SmoothStep(0, 1, elapsed / duration);
                player.SetViewPose(Mathf.LerpAngle(fromYaw, yaw, t), Mathf.Lerp(fromPitch, pitch, t));
                view.fieldOfView = Mathf.Lerp(fromFov, fov, t);
                yield return null;
            }
            player.SetViewPose(yaw, pitch);
            view.fieldOfView = fov;
        }
        void OnDisable()
        {
            StopAllCoroutines();
            if (player) { player.SetMovementLocked(false); player.SetPresentationLocked(false); player.SetLearningProgress(1); }
            if (board && board.Panel) board.Panel.InputBlocked = false;
            if (view && originalFov > 0) view.fieldOfView = originalFov;
        }
    }
}
