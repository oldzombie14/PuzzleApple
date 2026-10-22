using System;
using System.Collections;
using System.Linq;
using UnityEngine;

namespace PuzzleApple.V4.Editor
{
    public static class GatheringChecks
    {
        public static string Result { get; private set; }
        static void Require(bool condition,string message)
        { if(!condition) { Result="FAIL: "+message; throw new InvalidOperationException(Result); } }
        public static void Run()
        {
            Result="Running";
            var flow=UnityEngine.Object.FindFirstObjectByType<OpeningFlow>(); flow.StartCoroutine(Check(flow));
        }
        static void Place(OpeningLight lamp,Vector3 p)
        { lamp.GetComponent<Rigidbody>().position=p; lamp.transform.position=p; }
        static IEnumerator Check(OpeningFlow flow)
        {
            bool background=Application.runInBackground; Application.runInBackground=true;
            try
            {
                UnityEngine.Object.FindFirstObjectByType<WorldEntry>().EnterWorld(); yield return new WaitForSeconds(2);
                flow.player.enabled=false; flow.lightField.enabled=false;
                var effects=flow.effects.lights; var all=effects.Lamps.ToArray(); var pair=all.Take(2).ToArray();
                foreach(var lamp in all.Skip(2)) lamp.gameObject.SetActive(false);
                var center=flow.player.transform.position+new Vector3(0,2,10);
                Place(pair[0],center+Vector3.right*2); Place(pair[1],center+Vector3.right*2.1f); Physics.SyncTransforms();
                Require(effects.Nearest(center,pair[1])==pair[1],"near tie retains previous target");
                Place(pair[0],center+Vector3.right*.5f); Physics.SyncTransforms();
                Require(effects.Nearest(center,pair[1])==pair[0],"significantly nearer target takes over");
                Place(pair[0],center-Vector3.right*3); Place(pair[1],center+Vector3.right*3); Physics.SyncTransforms();
                var s=flow.board.State; s.TryAcquireSentence("gather.cluster","light light move");
                yield return new WaitForSeconds(3);
                float distance=Vector3.Distance(pair[0].transform.position,pair[1].transform.position);
                Require(distance>effects.spacing && distance<3,"cluster approaches and retains spacing");
                var positions=pair.Select(x=>x.transform.position).ToArray(); yield return new WaitForSeconds(.5f);
                Require(pair.Select((x,i)=>Vector3.Distance(x.transform.position,positions[i])<.06f).All(x=>x),"cluster settles instead of pushing forever");
                s.ClearWorkspace(); var d=flow.door; d.Reveal(flow.player.view);
                d.GetComponent<Rigidbody>().position=new Vector3(center.x,5,center.z); d.transform.position=new Vector3(center.x,5,center.z);
                foreach(var lamp in pair) lamp.gameObject.SetActive(false);
                s.TryAcquireSentence("gather.world","world door move");
                yield return new WaitForSeconds(3);
                Require(flow.lightField.ground.bounds.max.y>4.5f && flow.effects.world.Waiting,"world follows door and waits");
                Require(flow.player.transform.position.y>4.5f,"player carried with raised ground");
                Result="PASS: nearest-target hysteresis, clustering spacing and settling, world-to-door ascent with player";
            }
            finally {Application.runInBackground=background;}
        }
    }
}
