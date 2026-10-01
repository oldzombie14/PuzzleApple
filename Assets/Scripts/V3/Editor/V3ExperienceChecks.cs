using System;
using System.Collections;
using System.Linq;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.UI;
using PuzzleApple.V3.Cognition;

namespace PuzzleApple.V3.Editor
{
    public static class V3ExperienceChecks
    {
        public static string Status="Not started";
        static V3World lastWorld;
        static int passed;
        public static void Begin()
        {
            var w=UnityEngine.Object.FindFirstObjectByType<V3World>();
            if(!Application.isPlaying||w.presentation.Ready)throw new Exception("Start in fresh Play before opening finishes");
            if(lastWorld==w)return;lastWorld=w;Status="Running";passed=0;Application.runInBackground=true;
            w.StartCoroutine(Guard(Run(w)));
        }
        static void Check(bool ok,string name){if(!ok)throw new Exception(name);passed++;}
        static IEnumerator Guard(IEnumerator routine)
        {
            try
            {
                while(true)
                {
                    bool more;object value;
                    try{more=routine.MoveNext();value=more?routine.Current:null;}
                    catch(Exception e){Status="FAIL experience after "+passed+": "+e;Debug.LogError(Status);yield break;}
                    if(!more){Status="PASS experience: "+passed;Debug.Log(Status);yield break;}
                    yield return value;
                }
            }
            finally{(routine as IDisposable)?.Dispose();}
        }
        public static void Capture(string name)
        {
            var type=typeof(UnityEditor.Editor).Assembly.GetType("UnityEditor.GameView");
            var window=Resources.FindObjectsOfTypeAll(type).Cast<UnityEditor.EditorWindow>().First();
            var rt=new RenderTexture((int)window.position.width,(int)window.position.height,24);rt.Create();
            var old=RenderTexture.active;Texture2D image=null;
            try
            {
                var method=typeof(UnityEditorInternal.InternalEditorUtility).GetMethod("CaptureEditorWindow",System.Reflection.BindingFlags.Static|System.Reflection.BindingFlags.Public|System.Reflection.BindingFlags.NonPublic);
                if(!(bool)method.Invoke(null,new object[]{window,rt}))throw new Exception("Window capture failed");
                RenderTexture.active=rt;image=new Texture2D(rt.width,rt.height,TextureFormat.RGB24,false);image.ReadPixels(new Rect(0,0,rt.width,rt.height),0,0);image.Apply();
                System.IO.Directory.CreateDirectory("Temp/V3ModelImport");System.IO.File.WriteAllBytes("Temp/V3ModelImport/"+name+".png",image.EncodeToPNG());
            }
            finally{RenderTexture.active=old;rt.Release();UnityEngine.Object.Destroy(rt);if(image)UnityEngine.Object.Destroy(image);}
        }
        static IEnumerator Run(V3World w)
        {
            var original=InputSystem.settings;var temporary=UnityEngine.Object.Instantiate(original);
            var initialRotation=w.player.transform.rotation;float sensitivity=w.player.mouseSensitivity;
            temporary.backgroundBehavior=InputSettings.BackgroundBehavior.IgnoreFocus;
            temporary.editorInputBehaviorInPlayMode=InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            InputSystem.settings=temporary;
            try
            {
                while(Time.time<2.5f)yield return null;
                Check(!w.presentation.Ready&&!w.State.Knows("i"),"No premature word before opening");Capture("v3-soft-opening");
                while(!w.State.Knows("i"))yield return null;
                Check(!w.presentation.Ready,"Self appears while eyes are still opening");
                Check(w.State.Knows("i")&&w.State.Groups.Count==0,"Acquisition enters vocabulary without spawning workspace token");
                yield return new WaitForSeconds(1);
                var symbol=w.presentation.GetComponentsInChildren<CenteredSymbolImage>().Single(i=>i.name=="Acquired word");
                Check(symbol.color.a>.9f&&symbol.GetComponent<Outline>(),"Word remains visible with contrast outline");Capture("v3-word-reveal");
                while(!w.presentation.Ready)yield return null;
                yield return new WaitForSeconds(w.presentation.wordDisplayDuration);
                Check(w.board.Panel.IsOpen,"First self acquisition automatically opens Tab after reveal");
                w.player.mouseSensitivity=0;w.board.Panel.SetOpen(false);w.player.transform.rotation=initialRotation;yield return null;
                float deadline=Time.time+20;float maxSway=0;float collectedAt=-1;float startX=w.player.transform.position.x;
                while(w.player.WalkedDistance<4.8f&&Time.time<deadline)
                {
                    Cursor.lockState=CursorLockMode.Locked;
                    if(!Keyboard.current.enabled)InputSystem.EnableDevice(Keyboard.current);
                    InputSystem.QueueStateEvent(Keyboard.current,new KeyboardState(Key.W));
                    yield return null;
                    maxSway=Mathf.Max(maxSway,Mathf.Abs(Mathf.DeltaAngle(0,w.player.view.localEulerAngles.y)));
                    if(w.State.Knows("move")&&collectedAt<0)collectedAt=w.player.WalkedDistance;
                }
                InputSystem.QueueStateEvent(Keyboard.current,new KeyboardState());yield return null;
                Check(w.player.WalkedDistance>=4.6f,"Actual keyboard walks through learning distance");
                Check(maxSway>.2f,"First steps contain visible sway");
                Check(collectedAt>=2.3f&&collectedAt<2.7f,"Move awarded halfway through learning");
                Check(w.player.WalkingConfidence>.99f,"Steady walking restored after learning");
                Check(Mathf.Abs(w.player.transform.position.z)<.1f&&w.player.transform.position.x<startX,"Sway does not steer route");
                foreach(var word in w.board.Catalog.Words)w.Learn(word.Id);
                w.board.Panel.SetOpen(true);yield return new WaitForSeconds(.4f);
                Check(w.library.GetComponentsInChildren<InputField>(true).Length==0,"No note field");
                Check(w.library.Scroll.vertical&&w.library.Scroll.verticalScrollbar,"Permanent scrolling word column");
                var rect=(RectTransform)w.board.Panel.ContentRoot.parent;var parent=(RectTransform)rect.parent;
                Check(Mathf.Abs(rect.rect.width/parent.rect.width-1f/3)<.01f,"Panel stays one third wide");
                var sentence=V3Checks.Sentence(w.State,w.board.Catalog,"i equal apple");yield return null;
                var group=(RectTransform)w.board.Surface.Find("Group "+sentence.Id);var position=group.anchoredPosition;
                w.library.Select("apple");yield return null;
                Check(w.library.IsOpen&&!w.board.Surface.gameObject.activeSelf&&sentence.Effective,"Snapshot preserves active sentence");
                Check(w.library.SnapshotImage.texture&&w.library.SnapshotImage.material.shader.name=="PuzzleApple/V3/MemoryPrint","Snapshot uses print shader");
                w.library.GetComponentsInChildren<Button>(true).Single(b=>b.name=="Close memory").onClick.Invoke();yield return null;
                Check(!w.library.IsOpen&&w.board.Surface.gameObject.activeSelf&&Vector2.Distance(position,group.anchoredPosition)<1,"Close X preserves workspace layout");
                int count=w.State.Groups.Count;w.library.BeginLibraryDrag(w.board.Catalog.Word("i"),Vector2.zero);w.library.EndLibraryDrag(Vector2.zero);
                Check(w.State.Groups.Count==count,"Cancelled library drag leaves no token");
                var middle=group.GetComponentsInChildren<CognitionDragHandle>().First(h=>h.WordId==sentence.Words[1].Id);
                ExecuteEvents.Execute(middle.gameObject,new PointerEventData(EventSystem.current){button=PointerEventData.InputButton.Right},ExecuteEvents.pointerDownHandler);yield return null;
                Check(!w.State.HasEffect(CognitionSignal.EqualApple)&&w.State.Groups.Count==2&&w.State.Knows("equal"),"Right click removes middle word, preserves two runs and knowledge");
                float before=w.library.Scroll.content.anchoredPosition.y;
                w.library.Scroll.OnScroll(new PointerEventData(EventSystem.current){scrollDelta=new Vector2(0,-5)});yield return null;
                Check(w.library.Scroll.content.anchoredPosition.y>before,"Wheel scroll moves vocabulary");
                Check(UnityEditorInternal.InternalEditorUtility.HasFullscreenCamera(),"Display output remains valid");
            }
            finally
            {
                if(Keyboard.current!=null)InputSystem.QueueStateEvent(Keyboard.current,new KeyboardState());
                if(w)w.player.mouseSensitivity=sensitivity;
                InputSystem.settings=original;UnityEngine.Object.Destroy(temporary);
            }
        }
    }
}
