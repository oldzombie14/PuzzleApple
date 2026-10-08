using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using PuzzleApple.V3.Cognition;
using UnityEditor;
using UnityEngine;

namespace PuzzleApple.V3.Editor
{
    public static class MainRouteChecks
    {
        static readonly BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic;
        static void Invoke(object o,string method)=>o.GetType().GetMethod(method,Private).Invoke(o,null);
        static void Check(List<string> results,bool condition,string name)=>results.Add((condition?"PASS ":"FAIL ")+name);
        static CognitionState.Group Sentence(OpeningRoom r,string text)
        {r.State.TryAcquireSentence(Guid.NewGuid().ToString(),text);return r.State.Groups.Last();}
        static void Remove(OpeningRoom r,CognitionState.Group g)=>r.State.ReturnToLibrary(g.Id,0,true);
        static OpeningRoom Prepare(GameObject root)
        {
            var r=root.GetComponent<OpeningRoom>();
            typeof(CognitionBoard).GetField("<State>k__BackingField",Private).SetValue(r.board,new CognitionState(r.board.Catalog));
            typeof(V3Presentation).GetField("<Ready>k__BackingField",Private).SetValue(r.presentation,true);
            foreach(var o in r.objects)o.Initialize();Invoke(r.player,"Awake");r.mainRoute.Initialize();return r;
        }
        static void Step(OpeningRoom r,int frames,bool carry=false)
        {
            var physics=r.gameObject.scene.GetPhysicsScene();
            if(physics.Equals(Physics.defaultPhysicsScene))throw new InvalidOperationException("Checks require isolated preview physics.");
            for(int i=0;i<frames;i++)
            {
                r.ApplySentences();Invoke(r,"FixedUpdate");
                if(carry&&r.player.Carried)
                {
                    r.player.MoveToSupport(.02f);
                }
                physics.Simulate(.02f);r.mainRoute.Tick(.02f,i*.02f);
            }
        }
        [MenuItem("PuzzleApple/V3/Checks/Main route (Edit Mode)")]
        public static string Run()
        {
            if(Application.isPlaying)throw new InvalidOperationException("Edit Mode only.");
            var results=new List<string>();
            for(int scenario=0;scenario<15;scenario++)
            {
                var root=PrefabUtility.LoadPrefabContents(OpeningSetup.PrefabPath);
                try
                {
                    var r=Prepare(root);var m=r.mainRoute;var physics=root.scene.GetPhysicsScene();
                    if(scenario==0)
                    {
                        Check(results,Mathf.Abs(m.gateFrame.position.z-12)<.01f&&Mathf.Abs(m.balance.position.z-22)<.01f,"corridor and gallery aligned behind opening");
                        Check(results,root.GetComponentsInChildren<FirstPersonController>().Length==1&&!root.GetComponentsInChildren<V3World>().Any(),"single player and no active legacy controller");
                        Check(results,m.balance.position.y<.2f&&m.pressurePlate.GetComponent<Renderer>().bounds.max.y<.2f,"low plate replaces raised pedestal");
                        var doorSurfaces=m.doorLeaves.SelectMany(t=>t.GetComponentsInChildren<Renderer>()).ToArray();
                        var doorMaterial=doorSurfaces[0].sharedMaterial;
                        Check(results,doorSurfaces.All(s=>s.sharedMaterials.All(material=>material==doorMaterial))&&
                            r.doorwayMirror.GetComponentsInChildren<Renderer>().All(s=>!s.sharedMaterials.Contains(doorMaterial)),
                            "door leaves share one material asset, separate from mirror frame and glass");
                        Check(results,doorSurfaces.All(s=>s.GetComponent<LocalAmbientProbe>()&&!s.GetComponent<LocalAmbientProbe>().followRoomLighting),
                            "both door leaves retain corridor lighting regardless of hall boundary precision");
                        Check(results,r.secretSeal.activeSelf&&r.secretRoomVolume.bounds.center.z<0,"hidden room stays sealed and untouched");
                        Physics.SyncTransforms();bool floors=true;
                        for(float z=4.4f;z<30;z+=1)floors&=physics.Raycast(new Vector3(1.1f,1,z),Vector3.down,out var floor,1.2f,~0,QueryTriggerInteraction.Ignore);
                        Check(results,floors,"continuous walkable floor from mirror passage through gallery");
                        for(int i=0;i<120;i++)m.Tick(.02f,0);
                        Physics.SyncTransforms();Check(results,!m.GateOpen&&physics.Raycast(new Vector3(0,1.2f,10),Vector3.forward,out var gate,4,~0,QueryTriggerInteraction.Ignore)&&
                            gate.collider==m.gateBarrier.GetComponent<Collider>(),"natural bright phase blocks interaction through its narrow gap");
                        Check(results,m.idleGateTravel*2+.03f<r.player.GetComponent<CharacterController>().radius*2,"natural opening is visibly too narrow to pass");
                        Check(results,m.galleryLighting&&m.galleryLighting.Amount==0&&m.galleryLighting.lights.All(l=>l.intensity==0)&&m.galleryLighting.fixtures.All(f=>!f.enabled),"natural opening hides hall lights and luminous fixtures");
                        Check(results,m.indicator.sharedMaterial.shader.name=="Universal Render Pipeline/Unlit","indicator uses flat unlit black and white material");
                        for(int i=0;i<120;i++)m.Tick(.02f,2);
                        Physics.SyncTransforms();Check(results,!m.GateOpen&&physics.CapsuleCast(new Vector3(0,.4f,10),new Vector3(0,1.2f,10),.25f,Vector3.forward,out gate,4,~0,QueryTriggerInteraction.Ignore),"unlit gate closes physically");
                        r.player.Teleport(new Vector3(0,.05f,11.4f));for(int i=0;i<120;i++)m.Tick(.02f,0);
                        for(int i=0;i<60;i++)r.player.GetComponent<CharacterController>().Move(Vector3.forward*.04f);
                        Check(results,!m.GateOpen&&r.player.transform.position.z<12,"walking into the natural cycle cannot trigger safety opening or cross");
                        r.player.Teleport(r.playerSpawn.position);
                        var positive=Sentence(r,"positive door");m.Tick(.02f,2);Physics.SyncTransforms();
                        Check(results,!m.GateOpen&&physics.Raycast(new Vector3(0,1.2f,10),Vector3.forward,out gate,4,~0,QueryTriggerInteraction.Ignore),"positive opening still blocks until fully open");
                        Check(results,m.galleryLighting.Amount==0,"hall stays dark during positive opening travel");
                        for(int i=0;i<120;i++)m.Tick(.02f,2);Physics.SyncTransforms();
                        Check(results,m.GateOpen&&m.LampLit(2)&&!physics.Raycast(new Vector3(0,1.2f,10),Vector3.forward,out gate,4,~0,QueryTriggerInteraction.Ignore),"positive fully opens both passage and interaction ray");
                        Check(results,!physics.Raycast(new Vector3(1.4f,1.2f,10),Vector3.forward,out gate,3,~0,QueryTriggerInteraction.Ignore),"positive retracts previously retained door edges completely");
                        Check(results,m.galleryLighting.Amount>.99f&&m.galleryLighting.fixtures.All(f=>f.enabled),"fully open gate fades hall illumination on");
                        r.player.Teleport(new Vector3(0,.05f,12));Remove(r,positive);m.Tick(.02f,2);
                        Check(results,m.gateBarrier.WaitingForClearance&&!m.GateOpen,"relocking protects only the player already crossing");
                        r.player.GetComponent<CharacterController>().Move(Vector3.forward);m.Tick(.02f,2);
                        Check(results,!m.gateBarrier.WaitingForClearance&&m.gateBarrier.GetComponent<Collider>().enabled,"protection expires immediately after crossing clears");
                        r.player.Teleport(r.playerSpawn.position);
                        var negative=Sentence(r,"negative door");for(int i=0;i<120;i++)m.Tick(.02f,0);
                        Check(results,m.galleryLighting.Revealed&&m.galleryLighting.Amount>.99f,"hall illumination stays on after the door closes again");
                        Check(results,!m.GateOpen&&!m.LampLit(0),"negative fixes both gate and lamp");Remove(r,negative);
                        Step(r,150);Check(results,!m.Solved&&m.LeftWeight==0&&m.RightWeight==0,"empty balanced trays cannot press button");
                        var move=Sentence(r,"apple move equal");Step(r,1000);
                        Check(results,!m.Solved&&m.LeftWeight+m.RightWeight==1,"destination movement lands one apple without solving; "+m.apple.transform.position);
                        var p=m.apple.transform.position;Remove(r,move);Step(r,50);Check(results,Vector3.Distance(p,m.apple.transform.position)<.06f,"removing movement holds landed apple");
                        Sentence(r,"mirror apple");Step(r,200);
                        Check(results,m.Solved&&m.LeftWeight==1&&m.RightWeight==1,"mirroring a landed apple balances both loaded trays");
                        m.apple.GetComponent<ObjectReflection>().Sync(m.apple,r.mirrorView,false);
                        Check(results,Mathf.Abs(m.apple.Bounds.min.y-r.Copies[m.apple].Bounds.min.y)<.005f,"balanced apples have equal visible base heights");
                        Check(results,m.pressurePlate.localPosition.y<.015f&&m.passageSeal.localPosition.y>5,"loaded balance depresses plate and raises exit seal");
                    }
                    else if(scenario==1)
                    {
                        Sentence(r,"mirror apple");Sentence(r,"apple move equal");Step(r,1200);
                        Check(results,m.Solved&&m.LeftWeight==1&&m.RightWeight==1,"mirror first then destination movement solves");
                        Check(results,r.Copies.Count==r.objects.Count(o=>o.wordId=="apple")&&Vector3.Distance(r.Copies[m.apple].transform.position,m.apple.ReflectPoint(m.apple.transform.position))<.08f,"all named apples mirror; hall copy stays symmetric about balance");
                    }
                    else if(scenario==2)
                    {
                        var move=Sentence(r,"apple move equal");Step(r,1000);Remove(r,move);r.ApplySentences();
                        r.player.Teleport(new Vector3(1.5f,.05f,20));
                        Sentence(r,"i equal apple");Sentence(r,"i move equal");Step(r,650,true);
                        Check(results,m.Solved&&m.LeftWeight==1&&m.RightWeight==1,"player as apple travels to free tray and solves; "+r.player.transform.position);
                        Check(results,r.player.AppleIdentity&&r.player.Carried,"explicit player destination retains apple identity");
                        var reset=Sentence(r,"i i");Check(results,reset.Conflicted,"later self reset cannot override earlier identity");
                    }
                    else if(scenario==3)
                    {
                        Sentence(r,"apple move equal");Step(r,100);
                        var mirror=Sentence(r,"mirror apple");Step(r,25);
                        var p=m.apple.Body.position;var v=m.apple.Body.linearVelocity;
                        Remove(r,mirror);r.ApplySentences();
                        Check(results,Vector3.Distance(p,m.apple.Body.position)<.0001f&&Vector3.Distance(v,m.apple.Body.linearVelocity)<.0001f&&!r.Copies.ContainsKey(m.apple),"removing apple reflection preserves in-flight pose and velocity");
                        Step(r,50);Check(results,Vector3.Distance(p,m.apple.Body.position)>.05f&&!m.apple.AtHome(),"original continues its existing destination movement");
                        Sentence(r,"mirror move");var reflected=Sentence(r,"mirror mirror");Step(r,30);
                        p=r.doorwayMirror.Body.position;Remove(r,reflected);r.ApplySentences();
                        Check(results,Vector3.Distance(p,r.doorwayMirror.Body.position)<.0001f,"same effect removal contract preserves moving mirror pose");
                        Sentence(r,"red analyzer");r.ApplySentences();var color=r.transformTargets.Single(t=>t.wordId=="analyzer");
                        Check(results,color.IsRed,"analyzer supports red through reusable color component");
                        r.State.ClearWorkspace();r.ApplySentences();
                        Check(results,r.State.Groups.Count==0&&r.State.Knows("apple")&&r.State.Knows("analyzer")&&!color.IsRed&&r.Copies.Count==0,"clear removes sentences and temporary effects but preserves vocabulary");
                    }
                    else if(scenario<6)
                    {
                        r.player.Teleport(new Vector3(1.5f,.05f,20));
                        CognitionState.Group identity;
                        if(scenario==4){identity=Sentence(r,"i equal apple");Sentence(r,"apple move equal");}
                        else{Sentence(r,"apple move equal");Step(r,80);identity=Sentence(r,"i equal apple");}
                        r.ApplySentences();
                        Check(results,r.player.Carried&&r.player.SentenceTravel,"apple instruction includes player identity, order "+scenario);
                        Step(r,40,true);var pos=r.player.transform.position;var applePos=m.apple.transform.position;
                        Remove(r,identity);r.ApplySentences();
                        Check(results,!r.player.Carried&&!r.player.SentenceTravel&&Vector3.Distance(pos,r.player.transform.position)<.0001f&&Vector3.Distance(applePos,m.apple.transform.position)<.0001f,
                            "identity removal detaches player without resetting either recipient, order "+scenario);
                        Sentence(r,"i equal apple");Step(r,950,true);
                        Check(results,m.Solved&&m.LeftWeight==1&&m.RightWeight==1&&r.player.Carried,
                            "one apple movement sentence carries player and fruit to separate trays and solves, order "+scenario);
                    }
                    else if(scenario==6)
                    {
                        r.player.Teleport(new Vector3(1.5f,.05f,20));
                        var identity=Sentence(r,"i equal apple");var direct=Sentence(r,"i move equal");Step(r,650,true);
                        var owned=m.trayPlaces.OccupiedPlace(r.player);
                        Check(results,owned,"landed player has a physical tray occupancy marker");
                        Remove(r,direct);Step(r,20);
                        Check(results,m.trayPlaces.OccupiedPlace(r.player)==owned,"removing movement preserves occupied tray");
                        var collective=Sentence(r,"apple move equal");r.ApplySentences();
                        Check(results,m.trayPlaces.PlaceFor(r.player)==owned&&Vector3.Distance(r.player.CarryTarget,owned.position)<.001f,
                            "switching self movement to apple movement retains player's original tray");
                        Check(results,m.trayPlaces.PlaceFor(m.apple)&&m.trayPlaces.PlaceFor(m.apple)!=owned,"ground apple reserves only the other tray");
                        Step(r,950,true);Check(results,m.Solved&&m.trayPlaces.OccupiedPlace(r.player)==owned,"exact reported sequence solves without swapping player sides");
                        var fruitPlace=m.trayPlaces.OccupiedPlace(m.apple);
                        Remove(r,collective);Sentence(r,"i move equal");r.ApplySentences();
                        Check(results,m.trayPlaces.PlaceFor(r.player)==owned&&m.trayPlaces.OccupiedPlace(m.apple)==fruitPlace,
                            "reverse sentence switch preserves both physical occupants");
                        r.State.ClearWorkspace();r.ApplySentences();
                        Check(results,m.trayPlaces.OccupiedPlace(r.player)==owned&&m.trayPlaces.OccupiedPlace(m.apple)==fruitPlace,
                            "clearing sentences and identity does not erase physical occupancy");
                        r.player.Teleport(new Vector3(2,.05f,20));r.ApplySentences();
                        Check(results,!m.trayPlaces.OccupiedPlace(r.player),"physically leaving releases player's occupied marker");
                        Sentence(r,"i equal apple");Sentence(r,"apple move equal");r.ApplySentences();
                        Check(results,m.trayPlaces.PlaceFor(r.player)==owned&&m.trayPlaces.PlaceFor(m.apple)==fruitPlace,
                            "new arrival reserves released tray without evicting landed apple");
                    }
                    else if(scenario<11)
                    {
                        // Both names and both establishment orders must reach the same player motor.
                        bool alias=scenario>=9,identityFirst=scenario%2==1;
                        CognitionState.Group identity=null;
                        if(identityFirst)identity=Sentence(r,"i equal apple");
                        var move=Sentence(r,alias?"apple move":"i move");r.ApplySentences();
                        if(!identityFirst){identity=Sentence(r,"i equal apple");r.ApplySentences();}
                        Check(results,r.player.SentenceForward&&!r.player.SentenceTravel&&!r.player.Carried,
                            "untargeted movement resolves to player forward motor, name/order "+scenario);
                        var position=r.player.transform.position;
                        Remove(r,identity);r.ApplySentences();
                        Check(results,r.player.SentenceForward==!alias&&Vector3.Distance(position,r.player.transform.position)<.0001f,
                            "identity removal detaches only alias instruction without resetting pose, "+scenario);
                        Sentence(r,"i equal apple");r.ApplySentences();
                        Check(results,r.player.SentenceForward,"identity reattachment resumes forward motor, "+scenario);
                        Remove(r,move);r.ApplySentences();
                        Check(results,!r.player.SentenceForward&&!r.player.SentenceTravel,
                            "dismantling movement clears motor command, "+scenario);
                        Sentence(r,alias?"apple move equal":"i move equal");r.ApplySentences();
                        Check(results,!r.player.SentenceForward&&r.player.SentenceTravel&&r.player.Carried,
                            "explicit destination replaces forward policy, "+scenario);
                    }
                    else
                    {
                        var starts=new[]{new Vector3(1.5f,.05f,20),new Vector3(-1.5f,.05f,20),new Vector3(2,.05f,24),new Vector3(-2,.05f,24)};
                        r.player.Teleport(starts[scenario-11]);Sentence(r,"i equal apple");Sentence(r,"i move equal");Step(r,650,true);
                        var slot=m.trayPlaces.PlaceFor(r.player);
                        Check(results,slot&&Vector3.ProjectOnPlane(r.player.transform.position-slot.position,Vector3.up).magnitude<.035f,
                            "native player motor reaches tray centre from approach "+scenario);
                        Sentence(r,"apple move equal");Step(r,1100,true);
                        Check(results,m.Solved&&m.LeftWeight==1&&m.RightWeight==1&&m.trayPlaces.PlaceFor(r.player)==slot,
                            "keeping self move then adding apple move balances without conflict or displacement, "+scenario);
                    }
                }
                finally{PrefabUtility.UnloadPrefabContents(root);}
            }
            return string.Join("\n",results);
        }
    }
}
