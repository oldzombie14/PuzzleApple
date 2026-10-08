using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using PuzzleApple.V3.Cognition;

namespace PuzzleApple.V3.Editor
{
    public static class TrayArrivalChecks
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
            var results=new List<string>();
            Action<bool,string> check=(pass,name)=>results.Add((pass?"PASS ":"FAIL ")+name);
            for(int scenario=0;scenario<8;scenario++)
            {
                var root=PrefabUtility.LoadPrefabContents(OpeningSetup.PrefabPath);
                try
                {
                    var r=Prepare(root);var route=r.mainRoute;var apple=route.apple;
                    if(scenario<2)
                    {
                        Add(r,"negative apple");var move=Add(r,"apple move equal");Step(r,1100);
                        var place=route.trayPlaces.OccupiedPlace(apple);
                        check(place,"core occupies actual tray before removing sentence "+scenario);
                        Remove(r,move);Step(r,120);var position=apple.transform.position;
                        check(route.LeftWeight+route.RightWeight==.25f,"core remains weighed without move sentence "+scenario);
                        if(scenario==0)
                        {
                            r.player.Teleport(new Vector3(1.5f,.05f,20));Add(r,"i equal apple");Add(r,"i move equal");Step(r,800);
                            check(route.LeftWeight==.25f&&route.RightWeight==.25f&&route.InsufficientAttempts==1&&!route.Solved,"core plus player core triggers insufficient-weight balance");
                        }
                        else
                        {
                            Add(r,"apple move equal");Step(r,1);
                            check(Vector3.Distance(position,apple.transform.position)<.03f,"reissuing arrival does not jump or relaunch core");
                            Step(r,120);check(route.trayPlaces.OccupiedPlace(apple)==place&&Vector3.Distance(position,apple.transform.position)<.04f,"reissued core stays on same tray");
                        }
                    }
                    else if(scenario<6)
                    {
                        bool core=scenario%2==1;if(core)Add(r,"negative apple");
                        var tray=route.trays[1].position;
                        apple.Stop();apple.transform.position=new Vector3(tray.x,.045f,tray.z+(scenario>=4?.25f:0));apple.Body.position=apple.transform.position;
                        Physics.SyncTransforms();Add(r,"apple move equal");Step(r,1000);
                        check(route.LeftWeight+route.RightWeight==(core?.25f:1f),"arrival escapes below tray, core="+core+" offset="+(scenario>=4)+" final="+apple.transform.position);
                    }
                    else if(scenario==6)
                    {
                        Add(r,"negative apple");var move=Add(r,"apple move equal");Step(r,1100);Remove(r,move);
                        apple.transform.position+=Vector3.forward*.23f;apple.Body.position=apple.transform.position;Physics.SyncTransforms();Step(r,100);
                        check(route.LeftWeight+route.RightWeight==.25f,"off-centre core is still weighed by tray sensor");
                        var before=apple.transform.position;Add(r,"apple move equal");Step(r,150);
                        check(Vector3.Distance(before,apple.transform.position)<.025f,"off-centre resident is not re-centred or relaunched on reissue");
                        apple.transform.position+=Vector3.forward;apple.Body.position=apple.transform.position;Physics.SyncTransforms();r.mainRoute.RefreshTrayOccupancy();
                        check(route.Weight(0)+route.Weight(1)==0&&!route.trayPlaces.OccupiedPlace(apple),"physical departure releases weight and occupancy immediately");
                    }
                    else
                    {
                        Add(r,"negative apple");var tray=route.trays[1].position;
                        apple.Stop();apple.transform.position=new Vector3(tray.x,.17f,tray.z+.2f);apple.Body.position=apple.transform.position;Physics.SyncTransforms();
                        check(route.Weight(1)==0,"core beneath pan is not counted as a load");
                        Add(r,"mirror apple");Add(r,"apple move equal");Step(r,1300);
                        check(route.LeftWeight==.25f&&route.RightWeight==.25f&&route.InsufficientAttempts==1,"mirrored low approach reaches both trays while staying paired");
                    }
                }
                finally{PrefabUtility.UnloadPrefabContents(root);}
            }
            return string.Join("\n",results);
        }
    }
}
