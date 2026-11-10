using System;
using System.Collections;
using System.Reflection;
using UnityEngine;
namespace PuzzleApple.V5.Editor
{
    public static class EntryTransitionChecks
    {
        public static string Result {get;private set;}
        public static float MaxFrame {get;private set;}
        static void Check(bool ok,string message){if(!ok){Result="FAIL: "+message;throw new Exception(Result);}}
        static IEnumerator Until(Func<bool> ready){float end=Time.realtimeSinceStartup+25;while(!ready()&&Time.realtimeSinceStartup<end)yield return null;Check(ready(),"setup timeout");}
        public static void Run(){Result="Running";var e=UnityEngine.Object.FindFirstObjectByType<WorldEntry>();e.StartCoroutine(Exercise(e));}
        static IEnumerator Exercise(WorldEntry e)
        {
            bool bg=Application.runInBackground;Application.runInBackground=true;
            var p=e.player;bool enabled=p.enabled;
            var step=typeof(FirstPersonController).GetMethod("AdvanceMovement",BindingFlags.Instance|BindingFlags.NonPublic);
            try
            {
                if(!e.Entered){e.EnterWorld();yield return Until(()=>e.Entered);}
                if(!e.room.Opened){e.OpenWorld(-1);yield return Until(()=>e.room.Opened&&!e.Busy);}
                e.board.Panel.SetOpen(false);p.enabled=false;
                var j=e.journey;var entrance=UnityEngine.Object.FindFirstObjectByType<WallPassage>();MaxFrame=0;
                for(int pass=0;pass<2;pass++)
                {
                    p.Teleport(j.Root.TransformPoint(new Vector3(0,-entrance.sillHeight+.05f,-1.3f)),j.Root.rotation);
                    for(int n=0;n<10;n++){step.Invoke(p,new object[]{Vector2.zero,true,1f/60,false});yield return null;}
                    Check(!j.BeyondChamber&&!j.ViewBeyondChamber,"return restores chamber");
                    bool sawFade=false,sawExposure=false,sawHidden=false,sawReveal=false,sawLightTail=false;float end=Time.realtimeSinceStartup+4;
                    int frames=0;
                    while(Time.realtimeSinceStartup<end)
                    {
                        step.Invoke(p,new object[]{frames<70?Vector2.up:Vector2.zero,true,1f/60,frames==0});frames++;
                        yield return new WaitForEndOfFrame();
                        MaxFrame=Mathf.Max(MaxFrame,Time.unscaledDeltaTime);
                        float alpha=j.EntryFadeAlpha;
                        sawFade|=alpha>.03f;
                        Check(alpha<=.3f,"scene stays visible without white flash");
                        sawExposure|=j.EntryAdaptation>.9f;
                        sawReveal|=j.InCorridor&&j.EntrySceneVisibility>.05f&&j.EntrySceneVisibility<.95f;
                        sawLightTail|=j.InCorridor&&j.EntrySceneVisibility==1&&j.EntryFadeAlpha>.05f;
                        var output=UnityEngine.Object.FindFirstObjectByType<ReticleBackground>();
                        Check(Mathf.Abs(output.WorldVisibility-j.EntrySceneVisibility)<.001f,"actual world image follows scene reveal");
                        if(j.ViewBeyondChamber)sawHidden=true;
                        Check(RenderSettings.fog,"fog variant stays enabled");
                    }
                    Check(sawFade&&sawExposure&&sawHidden&&sawReveal&&sawLightTail&&j.EntrySceneVisibility==1&&j.EntryFadeAlpha==0&&j.EntryAdaptation==0,"jump entry renders exposure rise and gentle recovery");
                    Check(j.InCorridor&&j.Root.InverseTransformPoint(p.transform.position).z>.5f,"player traverses entry without being stuck");
                }
                Result="PASS: two physical jump entries, whole world fades in before light haze recedes, complete exposure recovery, stable fog variant. Max frame "+(MaxFrame*1000).ToString("F1")+" ms";
            }
            finally{p.enabled=enabled;p.AdoptCurrentView();Application.runInBackground=bg;}
        }
    }
}
