using UnityEngine;
using UnityEngine.InputSystem;

namespace PuzzleApple
{
    [RequireComponent(typeof(CharacterController))]
    public sealed class FirstPersonController : MonoBehaviour
    {
        [SerializeField] Transform view;
        [SerializeField, Min(0)] float moveSpeed = 3.5f;
        [SerializeField, Min(0)] float mouseSensitivity = 0.09f;
        [SerializeField, Range(1, 89)] float pitchLimit = 85;
        [SerializeField] float gravity = -20;
        [SerializeField] float rescueHeight = -5;
        CharacterController motor;
        Vector3 spawn;
        Quaternion spawnRotation;
        float pitch, verticalSpeed;

        void Awake()
        {
            motor = GetComponent<CharacterController>();
            if (!view) view = GetComponentInChildren<Camera>().transform;
            spawn = transform.position;
            spawnRotation = transform.rotation;
        }
        void OnEnable() => LockCursor(true);
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
            else if (mouse != null && mouse.leftButton.wasPressedThisFrame) LockCursor(true);
            Vector2 input = Vector2.zero;
            if (Cursor.lockState == CursorLockMode.Locked)
            {
                if (mouse != null)
                {
                    var delta = mouse.delta.ReadValue() * mouseSensitivity;
                    transform.Rotate(0, delta.x, 0);
                    pitch = Mathf.Clamp(pitch - delta.y, -pitchLimit, pitchLimit);
                    view.localRotation = Quaternion.Euler(pitch, 0, 0);
                }
                if (keyboard != null)
                    input = new Vector2((keyboard.dKey.isPressed ? 1 : 0) - (keyboard.aKey.isPressed ? 1 : 0),
                        (keyboard.wKey.isPressed ? 1 : 0) - (keyboard.sKey.isPressed ? 1 : 0));
            }
            input = Vector2.ClampMagnitude(input, 1);
            if (motor.isGrounded && verticalSpeed < 0) verticalSpeed = -2;
            verticalSpeed += gravity * Time.deltaTime;
            var velocity = (transform.right * input.x + transform.forward * input.y) * moveSpeed;
            velocity.y = verticalSpeed;
            motor.Move(velocity * Time.deltaTime);
            if (transform.position.y < rescueHeight) Respawn();
        }
        public void Respawn()
        {
            motor.enabled = false;
            transform.SetPositionAndRotation(spawn, spawnRotation);
            view.localRotation = Quaternion.identity;
            pitch = verticalSpeed = 0;
            motor.enabled = true;
        }
    }
}
