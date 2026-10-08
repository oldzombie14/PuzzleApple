using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using PuzzleApple.V3.Cognition;
using Object=UnityEngine.Object;

namespace PuzzleApple.V3.Editor
{
    public static class ReflectionCollisionChecks
    {
        static void Check(List<string> results,bool value,string name)
        {results.Add((value?"PASS ":"FAIL ")+name);}
        static void Place(OpeningObject obj,Vector3 position)
        {obj.Stop();obj.Body.position=position;obj.transform.position=position;Physics.SyncTransforms();}
        public static string Run()
        {
            if(Application.isPlaying)throw new InvalidOperationException("Edit Mode only.");
            var results=new List<string>();var root=PrefabUtility.LoadPrefabContents(OpeningSetup.PrefabPath);
            try
            {
                var room=root.GetComponent<OpeningRoom>();var physics=root.scene.GetPhysicsScene();
                if(physics.Equals(Physics.defaultPhysicsScene))throw new InvalidOperationException("Isolated physics required.");
                foreach(var o in room.objects)o.Initialize();
                var apple=room.mainRoute.apple;var effect=apple.GetComponent<ObjectReflection>();
                apple.reflectionPlane.position=new Vector3(40,10,40);apple.reflectionPlane.rotation=Quaternion.identity;
                Place(apple,new Vector3(40,10,39));var copy=effect.Create(apple,room.mirrorView);
                Check(results,copy!=null,"unobstructed reflection creates a physical copy");
                if(!copy)return string.Join("\n",results);
                Check(results,!Physics.GetIgnoreCollision(apple.GetComponent<Collider>(),copy.GetComponent<Collider>()),"source/copy collisions are enabled");
                bool penetrated=false;Vector3 contact=Vector3.zero;
                for(int i=0;i<60;i++)
                {
                    contact=effect.MovePair(apple,room.mirrorView,Vector3.forward*.05f);physics.Simulate(.02f);
                    var a=apple.GetComponent<Collider>();var b=copy.GetComponent<Collider>();
                    penetrated|=Physics.ComputePenetration(a,a.transform.position,a.transform.rotation,b,b.transform.position,b.transform.rotation,out _,out var depth)&&depth>.001f;
                }
                Check(results,!penetrated&&apple.Body.position.z<40&&copy.Body.position.z>40,"approaching pair cannot overlap or cross the mirror plane");
                Check(results,contact.z<-.9f,"head-on pair contact supplies a reflection normal");
                Check(results,Vector3.Distance(copy.Body.position,effect.Point(apple,apple.Body.position,room.mirrorView))<.001f,"pair stays exactly reflected when blocked");
                Place(apple,new Vector3(40,10,39));Place(copy,new Vector3(40,10,41));
                var wall=GameObject.CreatePrimitive(PrimitiveType.Cube);SceneManager.MoveGameObjectToScene(wall,root.scene);
                wall.transform.position=new Vector3(40,10,42);wall.transform.localScale=new Vector3(3,3,.1f);Physics.SyncTransforms();
                for(int i=0;i<80;i++){contact=effect.MovePair(apple,room.mirrorView,Vector3.back*.05f);physics.Simulate(.02f);}
                Check(results,copy.Bounds.max.z<=41.951f&&apple.Body.position.z>38.2f,"copy-only wall stops both halves before penetration");
                Check(results,contact.z>.9f,"copy collision normal maps back to source motion");
                var old=apple.Body.position;
                effect.MovePair(apple,room.mirrorView,Vector3.forward*.1f);physics.Simulate(.02f);
                Check(results,apple.Body.position.z>old.z+.09f,"pair can move away from contact without sticking");
                effect.Remove();Place(apple,new Vector3(40,10,40));
                Check(results,effect.Create(apple,room.mirrorView)==null,"overlapping spawn is deferred rather than creating interpenetration");
                Object.DestroyImmediate(wall);
                Place(apple,new Vector3(40,10,39));
                var flags=BindingFlags.Instance|BindingFlags.NonPublic;
                typeof(CognitionBoard).GetField("<State>k__BackingField",flags).SetValue(room.board,new CognitionState(room.board.Catalog));
                room.State.TryAcquireSentence("check.core","negative apple");room.State.TryAcquireSentence("check.mirror","mirror apple");room.ApplySentences();
                Check(results,room.State.Groups.All(g=>g.Effective),"negative apple and mirror apple coexist as effective sentences");
                Check(results,room.objects.Where(o=>o.wordId=="apple").Concat(room.Copies.Values.Where(o=>o.wordId=="apple")).All(o=>o.GetComponent<AppleState>().IsCore),"both physical apples and reflected copies become cores");
                var state=apple.GetComponent<AppleState>();
                Check(results,state.wholeColliders.All(c=>!c.enabled)&&state.core.GetComponentsInChildren<Collider>().Length==3,"core uses three fitted colliders instead of original sphere");
                var coreCopy=room.Copies[apple];
                Place(apple,new Vector3(40,10,39.78f));Place(coreCopy,new Vector3(40,10,40.22f));
                var sentence=room.State.Groups.First(g=>g.Signal==CognitionSignal.AppleNegative);room.State.ReturnToLibrary(sentence.Id,0,true);room.ApplySentences();
                Check(results,!state.IsCore&&state.wholeColliders.All(c=>c.enabled)&&room.Copies.Values.All(o=>!o.GetComponent<AppleState>().IsCore),"removing negative sentence restores whole apples and copies");
                var ca=apple.GetComponent<Collider>();var cb=coreCopy.GetComponent<Collider>();
                Check(results,!Physics.ComputePenetration(ca,ca.transform.position,ca.transform.rotation,cb,cb.transform.position,cb.transform.rotation,out _,out _),"restoring wider apples separates a touching core pair without overlap");
                Check(results,room.mainRoute.passageSeal.parent.Find("Passage floor")==null&&room.mainRoute.passageSeal.parent.Find("Passage end")==null,"completion exit has no corridor floor or dead-end wall");
                Check(results,room.presentation.view.clearFlags==CameraClearFlags.SolidColor&&room.presentation.view.backgroundColor==Color.black,"exterior camera background is black");
                Check(results,!physics.Raycast(room.mainRoute.transform.TransformPoint(new Vector3(-19,1,0)),Vector3.down,out _,3,~0,QueryTriggerInteraction.Ignore),"outside exit has no support floor");
            }
            finally{PrefabUtility.UnloadPrefabContents(root);}
            return string.Join("\n",results);
        }
    }
}
