using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using PuzzleApple.V2.Cognition;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace PuzzleApple.V2.Editor
{
    // Editor-only fixtures: no test vocabulary or controls are included in the player.
    public sealed class SymbolPresentationChecks
    {
        public static string Result { get; private set; }
        CognitionBoard board;
        int checks;
        public static void Run()
        {
            if (!Application.isPlaying) throw new InvalidOperationException("Run in a fresh Play session.");
            var test = new SymbolPresentationChecks { board = UnityEngine.Object.FindFirstObjectByType<CognitionBoard>() };
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
            ScreenCapture.CaptureScreenshot("Temp/PuzzleChecks/symbols-dot-append.png");
            yield return new WaitForSeconds(.2f); board.CancelDrag();
            Preview(self, move, true, false);
            yield return new WaitForSeconds(.15f); CheckPreview(move, true); board.CancelDrag();
            board.State.TryAcquireSentence("polish/tail", "consume apple");
            var tail = board.State.Groups.Single(g => g.Words.Count == 2);
            yield return new WaitForSeconds(1.2f);
            Preview(tail, self, false, true);
            yield return new WaitForSeconds(.15f); CheckPreview(self, false);
            ScreenCapture.CaptureScreenshot("Temp/PuzzleChecks/symbols-dot-whole.png");
            yield return new WaitForSeconds(.2f); board.CancelDrag();
            board.State.Join(tail.Id, self.Id, false);
            yield return new WaitForSeconds(1.2f);
            var square = Group(self).GetComponentsInChildren<Image>().Single(i => i.name == "Square").rectTransform;
            var first = Group(self).GetComponentsInChildren<Image>().Where(i => i.sprite).ToArray().First(t => t.name == "Word " + self.Words[0].Id);
            float squareRight = square.TransformPoint(new Vector3(square.rect.xMax, square.rect.center.y)).x;
            Require((Ink(first).xMin - squareRight) / board.Surface.lossyScale.x > 28, "sentence square has clear spacing");
            ScreenCapture.CaptureScreenshot("Temp/PuzzleChecks/symbols-sentence.png");
            yield return new WaitForSeconds(.2f);
            board.State.TryAcquireSentence("symbols/rewrite", "consume I apple");
            var rewriting = board.State.Groups.Last();
            yield return new WaitForSeconds(.3f);
            board.State.Swap(rewriting.Id, rewriting.Words[0].Id, rewriting.Words[1].Id);
            var erase = Group(rewriting).Find("Previous chalk - erase right to left");
            Require(erase && erase.GetComponentsInChildren<Image>().First(i => i.name == "Sentence handle").color.a == 0,
                "rewrite keeps the invisible sentence hit area transparent");
            Require(Group(rewriting).GetComponentsInChildren<Text>().Length == 0, "rewriting uses only sprite vocabulary");
            yield return new WaitForSeconds(1.1f);
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
            Result = "PASS: " + checks + " symbol layout and physics assertions"; Debug.Log(Result);
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
            var targetInk = Union(Group(target).GetComponentsInChildren<Image>().Where(i => i.sprite).ToArray());
            var sourceInk = Union(ghost.GetComponentsInChildren<Image>().Where(i => i.sprite).ToArray());
            Require(Group(target).GetComponentsInChildren<Image>().Where(i => i.sprite).ToArray().All(t => !t.canvasRenderer.cull && t.canvasRenderer.GetInheritedAlpha() > .9f),
                "target text remains visible throughout snap preview");
            var center = dot.rectTransform.TransformPoint(dot.rectTransform.rect.center);
            float middle = prepend ? (sourceInk.xMax + targetInk.xMin) * .5f : (targetInk.xMax + sourceInk.xMin) * .5f;
            Require(Mathf.Abs(center.x - middle) < 1, "circle is centered in visible glyph gap");
            Require(Mathf.Abs(center.y - targetInk.center.y) < 1 && Mathf.Abs(center.y - sourceInk.center.y) < 1,
                "circle and both visible word centers align vertically");
            Require(dot.color == Group(target).GetComponentsInChildren<Image>().First(i => i.sprite).color, "circle shares chalk color");
        }
        static Rect Union(Image[] texts)
        {
            var bounds = Ink(texts[0]);
            foreach (var text in texts.Skip(1)) { var ink = Ink(text); bounds = Rect.MinMaxRect(Mathf.Min(bounds.xMin, ink.xMin), Mathf.Min(bounds.yMin, ink.yMin), Mathf.Max(bounds.xMax, ink.xMax), Mathf.Max(bounds.yMax, ink.yMax)); }
            return bounds;
        }
        static Rect Ink(Image image)
        {
            // Inspect actual generated UI geometry, not the layout helper's prediction.
            Canvas.ForceUpdateCanvases();
            var mesh = image.canvasRenderer.GetMesh();
            var points = mesh.vertices.Select(v => (Vector2)image.rectTransform.TransformPoint(v)).ToArray();
            if (points.Length == 0) throw new Exception("Symbol has no visible UI geometry.");
            return Rect.MinMaxRect(points.Min(p => p.x), points.Min(p => p.y), points.Max(p => p.x), points.Max(p => p.y));
        }
        void Require(bool value, string message)
        { if (!value) { Result = "FAIL: " + message; throw new Exception(Result); } checks++; }
    }
}
