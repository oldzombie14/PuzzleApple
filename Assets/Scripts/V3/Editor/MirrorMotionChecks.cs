using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using PuzzleApple.V3.Cognition;

namespace PuzzleApple.V3.Editor
{
    public static class MirrorMotionChecks
    {
        public static string Run()
        {
            var results=new List<string>();
            var flags=BindingFlags.Instance|BindingFlags.NonPublic;
            var root=PrefabUtility.LoadPrefabContents(OpeningSetup.PrefabPath);
            try
            {
                var r=root.GetComponent<OpeningRoom>();
                typeof(CognitionBoard).GetField("<State>k__BackingField",flags).SetValue(r.board,new CognitionState(r.board.Catalog));
                typeof(V3Presentation).GetField("<Ready>k__BackingField",flags).SetValue(r.presentation,true);
                foreach(var o in r.objects)o.Initialize();
                var mirror=r.doorwayMirror;mirror.transform.position=new Vector3(100,4,100);mirror.Body.position=mirror.transform.position;
                r.State.TryAcquireSentence("check.mirror.move","mirror move");r.ApplySentences();
                var motions=(System.Collections.IDictionary)typeof(OpeningRoom).GetField("motion",flags).GetValue(r);
                var motion=motions[mirror.wordId];var direction=motion.GetType().GetField("direction");
                var incoming=new Vector3(1,0,.6f).normalized;direction.SetValue(motion,incoming);
                var tick=typeof(OpeningRoom).GetMethod("FixedUpdate",flags);
                // The timer no longer exists; free space outside old bounds must not turn it.
                tick.Invoke(r,null);
                Check(results,Vector3.Dot(mirror.Body.linearVelocity.normalized,incoming)>.9999f,"no timer or virtual-bound turn in free space");
                // Exercise the production direction consumer with known manifold normals.
                var normals=(Vector3[])typeof(OpeningObject).GetField("contactNormals",flags).GetValue(mirror);
                var count=typeof(OpeningObject).GetField("contactCount",flags);
                normals[0]=Vector3.left;count.SetValue(mirror,1);tick.Invoke(r,null);
                var outgoing=mirror.Body.linearVelocity.normalized;
                Check(results,Vector3.Dot(outgoing,new Vector3(-1,0,.6f).normalized)>.9999f,"oblique contact preserves tangential direction");
                count.SetValue(mirror,1);tick.Invoke(r,null);
                Check(results,Vector3.Dot(outgoing,mirror.Body.linearVelocity.normalized)>.9999f,"persistent contact does not reverse outgoing motion");
                direction.SetValue(motion,Vector3.right);count.SetValue(mirror,1);tick.Invoke(r,null);
                Check(results,Vector3.Dot(mirror.Body.linearVelocity.normalized,Vector3.left)>.9999f,"head-on contact returns along incoming path");
                direction.SetValue(motion,incoming);normals[1]=Vector3.back;count.SetValue(mirror,2);tick.Invoke(r,null);
                Check(results,Vector3.Dot(mirror.Body.linearVelocity.normalized,-incoming)>.9999f,"corner resolves both incoming contact planes");
                Check(results,Mathf.Abs(mirror.Body.linearVelocity.magnitude-r.movementSpeed)<.0001f,"reflection retains configured speed");
                r.State.ReturnToLibrary(r.State.Groups.Last().Id,0,true);r.ApplySentences();
                Check(results,mirror.Body.isKinematic,"removing move still stops mirror");
                var apple=r.mainRoute.apple;
                apple.transform.position=new Vector3(80,4,100);apple.Body.position=apple.transform.position;
                r.State.TryAcquireSentence("check.apple.move","apple move");r.ApplySentences();
                motion=motions[apple.wordId];direction=motion.GetType().GetField("direction");direction.SetValue(motion,incoming);
                for(int i=0;i<150;i++)tick.Invoke(r,null);
                Check(results,Vector3.Dot(apple.Body.linearVelocity.normalized,incoming)>.9999f,"apple keeps direction in free space outside old bounds");
                normals=(Vector3[])typeof(OpeningObject).GetField("contactNormals",flags).GetValue(apple);
                normals[0]=Vector3.left;count.SetValue(apple,1);tick.Invoke(r,null);
                outgoing=apple.Body.linearVelocity.normalized;
                Check(results,Vector3.Dot(outgoing,Vector3.Reflect(incoming,Vector3.left))>.9999f,"apple oblique collision reflects with correct angle");
                count.SetValue(apple,1);tick.Invoke(r,null);
                Check(results,Vector3.Dot(outgoing,apple.Body.linearVelocity.normalized)>.9999f,"apple does not turn again on outgoing contact");
                Check(results,Mathf.Abs(apple.Body.linearVelocity.magnitude-r.appleMovementSpeed)<.0001f&&r.appleMovementSpeed>r.movementSpeed,"apple uses faster speed while mirror speed stays unchanged");
            }
            finally{PrefabUtility.UnloadPrefabContents(root);}
            return string.Join("\n",results);
        }
        static void Check(List<string> results,bool pass,string name)
        {results.Add((pass?"PASS ":"FAIL ")+name);}
    }
}
