using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using PuzzleApple.Cognition;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace PuzzleApple.Editor
{
    // Editor-only fixtures: no test vocabulary or controls are included in the player.
    public sealed class CognitionPolishChecks
    {
        public static string Result { get; private set; }
        CognitionBoard board;
        int checks;
        public static void Run()
        {
            if (!Application.isPlaying) throw new InvalidOperationException("Run in a fresh Play session.");
            var test = new CognitionPolishChecks { board = UnityEngine.Object.FindFirstObjectByType<CognitionBoard>() };
            Result = "Running"; Application.runInBackground = true;
            EditorWindow.GetWindow(Type.GetType("UnityEditor.GameView,UnityEditor")).Focus();
            test.board.StartCoroutine(test.Check());
        }
        IEnumerator Check()
        {
            UnityEngine.Object.FindFirstObjectByType<OpeningTutorial>().enabled = false;
            board.Panel.InputBlocked = false; board.Panel.SetOpen(true);
            board.State.TryAcquireWord("polish/self", board.Catalog.Word("i"));
            board.State.TryAcquireWord("polish/move", board.Catalog.Word("move"));
            var self = board.State.Groups.Single(g => g.Words[0].Meaning == "i");
            var move = board.State.Groups.Single(g => g.Words[0].Meaning == "move");
            yield return new WaitForSeconds(1.2f);
            Preview(move, self, false, false);
            yield return new WaitForSeconds(.15f);
            CheckPreview(self, false);
            System.IO.Directory.CreateDirectory("Temp/PuzzleChecks");
            ScreenCapture.CaptureScreenshot("Temp/PuzzleChecks/polish-dot-append.png");
            yield return new WaitForSeconds(.2f); board.CancelDrag();
            Preview(self, move, true, false);
            yield return new WaitForSeconds(.15f); CheckPreview(move, true); board.CancelDrag();
            board.State.TryAcquireSentence("polish/tail", "consume apple");
            var tail = board.State.Groups.Single(g => g.Words.Count == 2);
            yield return new WaitForSeconds(1.2f);
            Preview(tail, self, false, true);
            yield return new WaitForSeconds(.15f); CheckPreview(self, false);
            ScreenCapture.CaptureScreenshot("Temp/PuzzleChecks/polish-dot-whole.png");
            yield return new WaitForSeconds(.2f); board.CancelDrag();
            board.State.Join(tail.Id, self.Id, false);
            yield return new WaitForSeconds(1.2f);
            var square = Group(self).GetComponentsInChildren<Image>().Single(i => i.name == "Square").rectTransform;
            var first = Group(self).GetComponentsInChildren<Text>().First(t => t.text == "I");
            float squareRight = square.TransformPoint(new Vector3(square.rect.xMax, square.rect.center.y)).x;
            Require((Ink(first).xMin - squareRight) / board.Surface.lossyScale.x > 28, "sentence square has clear spacing");
            ScreenCapture.CaptureScreenshot("Temp/PuzzleChecks/polish-sentence.png");
            yield return new WaitForSeconds(.2f);
            board.Panel.SetOpen(false);

            var apple = UnityEngine.Object.FindFirstObjectByType<CognitionApple>();
            var body = apple.GetComponent<Rigidbody>();
            var wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            wall.name = "Polish check wall (Play only)";
            wall.transform.position = new Vector3(-12, 2, 4);
            wall.transform.localScale = new Vector3(.2f, 4, 4);
            GameObject cornerWall = null;
            try
            {
                body.position = new Vector3(-10, 2, 4);
                Physics.SyncTransforms();
                board.State.TryAcquireSentence("polish/float", "apple move");
                var floating = board.State.Groups.Single(g => g.Signal == CognitionSignal.AppleMove);
                yield return new WaitForFixedUpdate(); yield return new WaitForFixedUpdate();
                typeof(CognitionApple).GetField("direction", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(apple, Vector3.left);
                var start = apple.Center;
                yield return new WaitForSeconds(.5f);
                Require(Vector3.Distance(start, apple.Center) > .32f, "floating speed exceeds old speed");
                yield return new WaitForSeconds(3);
                Require(Mathf.Abs(apple.Center.z - start.z) > .65f, "head-on collision turns into wall sliding");
                Require(apple.Center.x > wall.GetComponent<Collider>().bounds.max.x, "apple stays on the near side of wall");
                var along = apple.Center;
                yield return new WaitForSeconds(.5f);
                Require(Vector3.Distance(along, apple.Center) > .3f, "apple keeps moving after contacting wall");
                cornerWall = GameObject.CreatePrimitive(PrimitiveType.Cube);
                cornerWall.name = "Polish check corner (Play only)";
                cornerWall.transform.position = new Vector3(-10, 2, 6);
                cornerWall.transform.localScale = new Vector3(4, 4, .2f);
                body.position = new Vector3(-11.45f, 2, 5.45f);
                body.linearVelocity = Vector3.zero;
                Physics.SyncTransforms();
                typeof(CognitionApple).GetField("direction", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(apple, new Vector3(-1, 0, 1).normalized);
                var cornerStart = apple.Center;
                yield return new WaitForSeconds(2.5f);
                Require(Vector3.Distance(cornerStart, apple.Center) > .5f, "apple escapes an inside corner");
                Require(apple.Center.x > -11.9f && apple.Center.z < 5.9f, "corner deflection stays inside both walls");
                var detached = board.State.Detach(floating.Id, floating.Words[1].Id);
                float height = apple.Center.y;
                yield return new WaitForSeconds(.4f);
                Require(body.useGravity && !body.isKinematic && apple.Center.y < height - .35f && body.linearVelocity.y < -2,
                    "breaking move produces accelerating gravity fall");
                yield return new WaitForSeconds(1.4f);
                Require(apple.GetComponent<Collider>().bounds.min.y > -.03f && apple.Center.y < .5f, "apple lands on floor without passing through");
                board.State.Join(detached.Id, floating.Id, false);
                yield return new WaitForSeconds(.4f);
                Require(apple.IsMoving && !body.useGravity && apple.Center.y > .38f, "reforming move lifts apple from landed position");
                board.State.TryAcquireSentence("polish/second-float", "apple move");
                board.State.Detach(floating.Id, floating.Words[1].Id);
                yield return new WaitForSeconds(.1f);
                Require(apple.IsMoving && !body.useGravity, "another valid move sentence sustains floating");
                var remaining = board.State.Groups.Single(g => g.Signal == CognitionSignal.AppleMove);
                board.State.Detach(remaining.Id, remaining.Words[1].Id);
                yield return new WaitForSeconds(.1f);
                Require(!apple.IsMoving && body.useGravity, "removing last move restores gravity");
            }
            finally { UnityEngine.Object.Destroy(wall); if (cornerWall) UnityEngine.Object.Destroy(cornerWall); }
            Result = "PASS: " + checks + " polish assertions"; Debug.Log(Result);
        }
        RectTransform Group(CognitionState.Group group) => (RectTransform)board.Surface.Find("Group " + group.Id);
        void Preview(CognitionState.Group source, CognitionState.Group target, bool prepend, bool whole)
        {
            var sourceRect = Group(source); var targetRect = Group(target);
            var grab = RectTransformUtility.WorldToScreenPoint(null, sourceRect.TransformPoint(new Vector3(whole ? 10 : 16, -28)));
            float x = prepend ? -8 : targetRect.rect.width + 8;
            // Whole groups snap by their text edges, with the pointer at the sentence handle.
            if (whole && !prepend) x -= 38;
            var end = RectTransformUtility.WorldToScreenPoint(null, targetRect.TransformPoint(new Vector3(x, -28)));
            board.BeginDrag(source.Id, source.Words[0].Id, whole, grab); board.MoveDrag(end);
        }
        void CheckPreview(CognitionState.Group target, bool prepend)
        {
            var dot = board.Surface.GetComponentInChildren<CognitionSnapDot>();
            Require(dot && dot.gameObject.activeInHierarchy, "snap circle is visible");
            var ghost = board.Surface.Find("Dragging chalk");
            var targetInk = Union(Group(target).GetComponentsInChildren<Text>());
            var sourceInk = Union(ghost.GetComponentsInChildren<Text>());
            Require(Group(target).GetComponentsInChildren<Text>().All(t => !t.canvasRenderer.cull && t.canvasRenderer.GetInheritedAlpha() > .9f),
                "target text remains visible throughout snap preview");
            var center = dot.rectTransform.TransformPoint(dot.rectTransform.rect.center);
            float middle = prepend ? (sourceInk.xMax + targetInk.xMin) * .5f : (targetInk.xMax + sourceInk.xMin) * .5f;
            Require(Mathf.Abs(center.x - middle) < 1, "circle is centered in visible glyph gap");
            Require(Mathf.Abs(center.y - targetInk.center.y) < 1 && Mathf.Abs(center.y - sourceInk.center.y) < 1,
                "circle and both visible word centers align vertically");
            Require(dot.color == Group(target).GetComponentInChildren<Text>().color, "circle shares chalk color");
        }
        static Rect Union(Text[] texts)
        {
            var bounds = Ink(texts[0]);
            foreach (var text in texts.Skip(1)) { var ink = Ink(text); bounds = Rect.MinMaxRect(Mathf.Min(bounds.xMin, ink.xMin), Mathf.Min(bounds.yMin, ink.yMin), Mathf.Max(bounds.xMax, ink.xMax), Mathf.Max(bounds.yMax, ink.yMax)); }
            return bounds;
        }
        static Rect Ink(Text text)
        {
            var generator = text.cachedTextGenerator;
            generator.Populate(text.text, text.GetGenerationSettings(text.rectTransform.rect.size));
            var vertices = generator.verts;
            Vector2 min = Vector2.positiveInfinity, max = Vector2.negativeInfinity;
            for (int q = 0; q + 3 < vertices.Count; q += 4)
            {
                if (Mathf.Approximately(vertices[q].position.y, vertices[q + 2].position.y)) continue;
                for (int i = 0; i < 4; i++)
                {
                    Vector2 p = text.rectTransform.TransformPoint(vertices[q + i].position / text.pixelsPerUnit);
                    min = Vector2.Min(min, p); max = Vector2.Max(max, p);
                }
            }
            return Rect.MinMaxRect(min.x, min.y, max.x, max.y);
        }
        void Require(bool value, string message)
        { if (!value) { Result = "FAIL: " + message; throw new Exception(Result); } checks++; }
    }
}
