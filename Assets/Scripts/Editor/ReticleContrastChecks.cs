using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using PuzzleApple.Cognition;
using UnityEngine;
using UnityEngine.UI;

namespace PuzzleApple.Editor
{
    // Explicit Play Mode GPU check: the same icon spans four different world backgrounds.
    public static class ReticleContrastChecks
    {
        public static string Result { get; private set; }
        public static void Run()
        {
            if (!Application.isPlaying) throw new InvalidOperationException("Run in Play Mode.");
            if (Result == "Running") return;
            Result = "Running";
            UnityEngine.Object.FindFirstObjectByType<CognitionBoard>().StartCoroutine(Check());
        }
        static IEnumerator Check()
        {
            var board = UnityEngine.Object.FindFirstObjectByType<CognitionBoard>();
            var world = UnityEngine.Object.FindFirstObjectByType<CognitionWorldInteraction>();
            var hud = board.transform.Find("Cognition HUD").gameObject;
            var cross = hud.transform.Find("Temporary X reticle").gameObject;
            var eye = hud.transform.Find("Collect eye").gameObject;
            bool panelOpen = board.Panel.IsOpen;
            var objects = new List<UnityEngine.Object>();
            Texture2D shot = null;
            try
            {
                board.Panel.SetOpen(false);
                world.enabled = false;
                hud.transform.Find("Collected apple forbidden").gameObject.SetActive(false);
                hud.SetActive(true); cross.SetActive(true); eye.SetActive(false);
                for (int row = 0; row < 2; row++)
                    for (int col = 0; col < 2; col++)
                    {
                        var quad = GameObject.CreatePrimitive(PrimitiveType.Quad); objects.Add(quad);
                        quad.name = "Temporary reticle contrast verification";
                        quad.transform.SetParent(Camera.main.transform,false);
                        quad.transform.localPosition = new Vector3(col == 0 ? -.5f : .5f,row == 0 ? -.5f : .5f,1);
                        var material = new Material(Shader.Find("Universal Render Pipeline/Unlit")); objects.Add(material);
                        // bottom-left white, top-left black; the right side is the reverse.
                        material.SetColor("_BaseColor",row == col ? Color.white : Color.black);
                        quad.GetComponent<Renderer>().sharedMaterial = material;
                        UnityEngine.Object.Destroy(quad.GetComponent<Collider>());
                    }
                yield return new WaitForSecondsRealtime(.35f);
                yield return new WaitForEndOfFrame();
                shot = ScreenCapture.CaptureScreenshotAsTexture();
                CheckInk(shot,-5,5,true,"X on upper-left dark background");
                CheckInk(shot,5,5,false,"X on upper-right light background");
                CheckInk(shot,-5,-5,false,"X on lower-left light background");
                CheckInk(shot,5,-5,true,"X on lower-right dark background");
                Save(shot,"reticle-four-backgrounds.png");
                UnityEngine.Object.Destroy(shot); shot=null;
                cross.SetActive(false); eye.SetActive(true);
                yield return new WaitForEndOfFrame();
                shot = ScreenCapture.CaptureScreenshotAsTexture();
                float eyeScale = ((RectTransform)eye.transform).rect.width / 300f;
                CheckInk(shot,-28*eyeScale,-10*eyeScale,false,"eye over light background");
                CheckInk(shot,28*eyeScale,-10*eyeScale,true,"eye over dark background");
                CheckInk(shot,-80,-10,true,"transparent PNG margin preserves light background");
                CheckInk(shot,80,-10,false,"transparent PNG margin preserves dark background");
                Save(shot,"eye-split-background.png");
                Result="PASS: 8 reticle/eye GPU pixel checks";
                Debug.Log(Result);
            }
            finally
            {
                if (shot) UnityEngine.Object.Destroy(shot);
                foreach (var item in objects) if(item) UnityEngine.Object.Destroy(item);
                world.enabled=true; board.Panel.SetOpen(panelOpen);
            }
        }
        static void CheckInk(Texture2D shot,float offsetX,float offsetY,bool white,string label)
        {
            float scale = UnityEngine.Object.FindFirstObjectByType<CognitionBoard>().GetComponent<Canvas>().scaleFactor;
            Color pixel = shot.GetPixel(Mathf.RoundToInt(shot.width*.5f+offsetX*scale),Mathf.RoundToInt(shot.height*.5f+offsetY*scale));
            if (white ? pixel.r < .85f || pixel.g < .85f || pixel.b < .85f : pixel.r > .15f || pixel.g > .15f || pixel.b > .15f)
            { Result="FAIL: "+label+" pixel="+pixel; throw new Exception(Result); }
        }
        static void Save(Texture2D shot,string file)
        {
            const string folder="Temp/CognitionVerification";
            Directory.CreateDirectory(folder); File.WriteAllBytes(Path.Combine(folder,file),shot.EncodeToPNG());
        }
    }
}
