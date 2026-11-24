using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using PuzzleApple.V3.Cognition;

namespace PuzzleApple.V3.Editor
{
    public static class PlayerBalanceChecks
    {
        static readonly BindingFlags Private=BindingFlags.NonPublic|BindingFlags.Static;
        static OpeningRoom Prepare(GameObject root)=>(OpeningRoom)typeof(MainRouteChecks).GetMethod("Prepare",Private).Invoke(null,new object[]{root});
        static void Step(OpeningRoom r,int count)=>typeof(MainRouteChecks).GetMethod("Step",Private).Invoke(null,new object[]{r,count,true});
        static CognitionState.Group Add(OpeningRoom r,string words)
        {r.State.TryAcquireSentence(Guid.NewGuid().ToString(),words);r.ApplySentences();return r.State.Groups.Last();}
        static void Remove(OpeningRoom r,CognitionState.Group g){r.State.ReturnToLibrary(g.Id,0,true);r.ApplySentences();}
        public static string Run()
        {
            if(Application.isPlaying)throw new InvalidOperationException("Edit mode only");
            var results=new List<string>();Action<bool,string> check=(pass,name)=>results.Add((pass?"PASS ":"FAIL ")+name);
            for(int scenario=0;scenario<4;scenario++)
            {
                var root=PrefabUtility.LoadPrefabContents(OpeningSetup.PrefabPath);
                try
                {
                    var r=Prepare(root);var mobile=r.playerBalance;var a=r.mainRoute.apple;
                    r.player.Teleport(new Vector3(0,.05f,19));r.player.transform.rotation=Quaternion.identity;
                    var identity=Add(r,"i equal equal");
                    check(identity.Effective&&mobile.Active&&!r.player.AppleIdentity,"balance identity is recognized independently of apple "+scenario);
                    if(scenario==3)Add(r,"negative apple");
                    var move=Add(r,"apple move equal");Step(r,30);
                    check(mobile.slots.PlaceFor(a),"nearest player balance reserves apple "+scenario);
                    if(scenario==1)
                    {
                        r.player.Teleport(new Vector3(3,.05f,17));r.ApplySentences();
                        check(mobile.slots.PlaceFor(a),"moving farther away does not change the chosen support");
                    }
                    if(scenario==2)
                    {
                        var before=a.transform.position;Remove(r,identity);
                        check(Vector3.Distance(before,a.transform.position)<.001f,"identity removal does not teleport in-flight fruit");
                        Step(r,1600);check(r.mainRoute.LeftWeight+r.mainRoute.RightWeight==1,"in-flight active sentence automatically redirects to fixed balance");
                        continue;
                    }
                    Step(r,1600);
                    check(mobile.Carries(a),"apple lands on player tray "+scenario+" pos="+a.transform.position);
                    check(!r.mainRoute.Solved&&r.mainRoute.LeftWeight+r.mainRoute.RightWeight==0,"mobile load does not press fixed puzzle "+scenario);
                    Remove(r,move);var local=r.player.transform.InverseTransformPoint(a.transform.position);
                    r.player.Teleport(r.player.transform.position+Vector3.back*.4f);r.player.transform.rotation=Quaternion.Euler(0,25,0);r.ApplySentences();
                    check(Vector3.Distance(local,r.player.transform.InverseTransformPoint(a.transform.position))<.01f,"load follows translation and turning without movement sentence "+scenario);
                    Add(r,"mirror apple");Step(r,10);var copy=r.Copies.ContainsKey(a)?r.Copies[a]:null;
                    check(copy&&Vector3.Distance(copy.transform.position,a.GetComponent<ObjectReflection>().Point(a,a.transform.position,r.mirrorView))<.02f,"on-player reflection uses player axis "+scenario);
                    if(scenario==3)Add(r,"apple move equal");
                    var origin=a.transform.position;var reflected=copy?copy.transform.position:Vector3.zero;
                    Remove(r,identity);
                    check(Vector3.Distance(origin,a.transform.position)<.001f&&(!copy||Vector3.Distance(reflected,copy.transform.position)<.001f),"detaching identity preserves both world poses "+scenario);
                    if(scenario==3)
                    {
                        Step(r,2400);check(r.mainRoute.LeftWeight==.25f&&r.mainRoute.RightWeight==.25f,"active mirrored core pair retargets fixed trays after identity removal");
                    }
                    else
                    {
                        r.player.Teleport(r.player.transform.position+Vector3.back);r.player.transform.rotation=Quaternion.Euler(0,80,0);Step(r,100);
                        check(Vector3.Distance(origin,a.transform.position)<.01f&&copy&&Vector3.Distance(reflected,copy.transform.position)<.01f,"without movement sentence fruit and frozen reflection stay suspended "+scenario);
                    }
                }
                finally{PrefabUtility.UnloadPrefabContents(root);}
            }
            return string.Join("\n",results);
        }
    }
}
