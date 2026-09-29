using System;
using System.Collections;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

namespace PuzzleApple.General.Editor
{
    public static class BasicControllerChecks
    {
        public static string Result { get; private set; }
        public static void Run()
        {
            if (!Application.isPlaying) throw new InvalidOperationException("Use a fresh V3 Play session.");
            var player = UnityEngine.Object.FindFirstObjectByType<BasicFirstPersonController>();
            if (!player) throw new InvalidOperationException("Open the V3 sandbox.");
            Result = "Running";
            Application.runInBackground = true;
            EditorWindow.GetWindow(Type.GetType("UnityEditor.GameView,UnityEditor")).Focus();
            player.StartCoroutine(Check(player));
        }
        static IEnumerator Check(BasicFirstPersonController player)
        {
            var original = InputSystem.settings;
            var settings = UnityEngine.Object.Instantiate(original);
            settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            InputSystem.settings = settings;
            GameObject wall = null;
            int checks = 0;
            void Require(bool value, string message)
            { if (!value) { Result = "FAIL: " + message; throw new Exception(Result); } checks++; }
            try
            {
                Require(UnityEngine.Object.FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                    .Where(c => c.GetType().Namespace != null && c.GetType().Namespace.StartsWith("PuzzleApple"))
                    .All(c => c is BasicFirstPersonController || c is PlanarMirror), "no prototype gameplay scripts in V3");
                var motor = player.GetComponent<CharacterController>();
                motor.enabled = false; player.transform.SetPositionAndRotation(new Vector3(-4,.08f,3), Quaternion.identity); motor.enabled = true;
                var start = player.transform.position;
                for (float t = 0; t < .5f; t += Time.deltaTime)
                { Keys(Key.W); yield return null; }
                Keys();
                Require(player.transform.position.z > start.z + 1.2f, "W moves immediately at ordinary speed");
                Require(Mathf.Abs(player.transform.position.y - start.y) < .3f, "floor collision holds player");
                wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
                wall.transform.position = player.transform.position + new Vector3(0, 1, 1);
                wall.transform.localScale = new Vector3(3, 3, .1f);
                Physics.SyncTransforms();
                for (float t = 0; t < .6f; t += Time.deltaTime)
                { Keys(Key.W); yield return null; }
                Keys();
                Require(player.transform.position.z < wall.transform.position.z - .1f, "solid wall blocks movement");
                motor.enabled = false; player.transform.position = new Vector3(0,-6,0); motor.enabled = true;
                yield return null; yield return null;
                Require(player.transform.position.y > -1 && player.transform.position.x > 8, "fall rescue returns to authored spawn");
                Keys(Key.Tab); yield return null; Keys(); yield return null;
                Require(UnityEngine.Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None).Length == 0, "Tab does not create a cognition panel");
                Result = "PASS: " + checks + " basic controller assertions";
                Debug.Log(Result);
            }
            finally
            {
                Keys();
                InputSystem.settings = original;
                UnityEngine.Object.Destroy(settings);
                if (wall) UnityEngine.Object.Destroy(wall);
            }
        }
        static void Keys(params Key[] keys)
        {
            Cursor.lockState = CursorLockMode.Locked;
            if (!Keyboard.current.enabled) InputSystem.EnableDevice(Keyboard.current);
            InputSystem.QueueStateEvent(Keyboard.current, new KeyboardState(keys));
        }
    }
}
