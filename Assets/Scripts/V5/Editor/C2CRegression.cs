using System;
using System.Collections.Generic;
using System.Reflection;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace PuzzleApple.V5.Editor
{
    // Run each existing acceptance coroutine in a fresh Play without saving the user's scene.
    [InitializeOnLoad]
    public static class C2CRegression
    {
        const string Key="PuzzleApple.C2CRegression.";
        static double deadline;
        static Type current;
        static UnityEngine.InputSystem.InputSettings originalInput,testInput;
        static C2CRegression(){EditorApplication.update+=Tick;EditorApplication.playModeStateChanged+=Changed;}
        public static string Status=>SessionState.GetString(Key+"results","")+"\n"+SessionState.GetString(Key+"current","");
        public static void Start(string[] checks,bool period=true){
            if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Start from Edit mode.");
            SessionState.SetString(Key+"queue",string.Join(";",checks));SessionState.SetString(Key+"results","");
            SessionState.SetBool(Key+"period",period);SessionState.SetBool(Key+"active",true);Next();
        }
        public static void Stop(){SessionState.SetBool(Key+"active",false);RestoreInput();EditorApplication.isPlaying=false;}
        static void Next(){
            var queue=SessionState.GetString(Key+"queue","").Split(new[]{';'},StringSplitOptions.RemoveEmptyEntries);
            if(queue.Length==0){SessionState.SetString(Key+"current","Complete");SessionState.SetBool(Key+"active",false);return;}
            SessionState.SetString(Key+"current",queue[0]);SessionState.SetString(Key+"queue",string.Join(";",queue,1,queue.Length-1));
            current=null;EditorApplication.isPlaying=true;
        }
        static void Changed(PlayModeStateChange state){
            if(!SessionState.GetBool(Key+"active",false))return;
            if(state==PlayModeStateChange.EnteredPlayMode){deadline=EditorApplication.timeSinceStartup+240;EditorApplication.delayCall+=Run;}
        }
        static void Run(){
            try{
                Application.runInBackground=true;
                originalInput=UnityEngine.InputSystem.InputSystem.settings;
                testInput=UnityEngine.Object.Instantiate(originalInput);
                testInput.backgroundBehavior=UnityEngine.InputSystem.InputSettings.BackgroundBehavior.IgnoreFocus;
                testInput.editorInputBehaviorInPlayMode=UnityEngine.InputSystem.InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
                UnityEngine.InputSystem.InputSystem.settings=testInput;
                var e=UnityEngine.Object.FindFirstObjectByType<WorldEntry>();
                e.board.SetPeriodMode(SessionState.GetBool(Key+"period",true));
                var name=SessionState.GetString(Key+"current","").Split('.');
                current=typeof(C2CRegression).Assembly.GetType("PuzzleApple.V5.Editor."+name[0],true);
                var run=current.GetMethod(name.Length>1?name[1]:"Run",BindingFlags.Public|BindingFlags.Static);
                run.Invoke(null,run.GetParameters().Select(p=>p.DefaultValue).ToArray());
            }catch(Exception ex){Finish("FAIL: "+ex.GetBaseException().Message);}
        }
        static void Tick(){
            if(!SessionState.GetBool(Key+"active",false))return;
            if(!EditorApplication.isPlayingOrWillChangePlaymode && current==null){Next();return;}
            if(!EditorApplication.isPlaying||current==null)return;
            string result=current.GetProperty("Result",BindingFlags.Public|BindingFlags.Static)?.GetValue(null) as string;
            if(result!=null&&(result.StartsWith("PASS")||result.StartsWith("FAIL")))Finish(result);
            else if(EditorApplication.timeSinceStartup>deadline)Finish("FAIL: acceptance timeout");
        }
        static void Finish(string result){
            var name=SessionState.GetString(Key+"current","");current=null;
            RestoreInput();
            SessionState.SetString(Key+"results",SessionState.GetString(Key+"results","")+name+": "+result+"\n");
            EditorApplication.isPlaying=false;
        }
        static void RestoreInput(){
            if(originalInput)UnityEngine.InputSystem.InputSystem.settings=originalInput;
            if(testInput)UnityEngine.Object.DestroyImmediate(testInput);testInput=null;originalInput=null;
        }
    }
}
