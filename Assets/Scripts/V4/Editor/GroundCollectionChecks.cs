using System;
using System.Collections;
using System.Reflection;
using UnityEngine;

namespace PuzzleApple.V4.Editor
{
    public static class GroundCollectionChecks
    {
        public static string Result { get; private set; }
        static void Require(bool value,string message)
        { if(!value){Result="FAIL: "+message;throw new InvalidOperationException(Result);} }
        public static void Run()
        {
            if(!Application.isPlaying)throw new InvalidOperationException("Fresh P4 Play required");
            Result="Running";
            var flow=UnityEngine.Object.FindFirstObjectByType<OpeningFlow>();
            flow.StartCoroutine(Check(flow));
        }
        static IEnumerator Check(OpeningFlow flow)
        {
            bool background=Application.runInBackground;Application.runInBackground=true;
            try
            {
                UnityEngine.Object.FindFirstObjectByType<WorldEntry>().EnterWorld();
                yield return new WaitForSecondsRealtime(2);
                Require(!flow.board.State.Knows("ground"),"ground not granted at entry");
                flow.board.Panel.SetOpen(false);yield return null;
                flow.player.Teleport(new Vector3(100,1,100),Quaternion.Euler(0,63,0));
                var pitch=typeof(FirstPersonController).GetField("pitch",BindingFlags.Instance|BindingFlags.NonPublic);
                pitch.SetValue(flow.player,80f);
                yield return null;
                Cursor.lockState=CursorLockMode.Locked;
                flow.player.view.localRotation=Quaternion.Euler(80,0,0);
                Physics.SyncTransforms();
                Require(flow.TryCollectGround()&&flow.Collecting,"ground raycast starts real collection");
                Require(!flow.board.State.Knows("ground")&&!flow.TryCollectGround(),"pending flight blocks duplicate acquisition");
                yield return new WaitForSecondsRealtime(flow.wordDisplayDuration+.2f);
                Require(flow.board.State.Knows("ground")&&!flow.player.MovementLocked,"flight grants ground and restores movement");
                flow.library.Select("ground");
                var photo=flow.library.SnapshotImage.texture;
                Require(photo,"ground retains first static snapshot");
                flow.library.Show(false);
                Cursor.lockState=CursorLockMode.Locked;
                Require(flow.TryCollectGround()&&!flow.Collecting&&!flow.player.MovementLocked,"known ground feedback does not recollect or freeze");
                flow.library.Select("ground");
                Require(flow.library.SnapshotImage.texture==photo,"repeat click keeps first snapshot");
                flow.library.Show(false);
                flow.board.Panel.SetOpen(true);
                Require(!flow.TryCollectGround(),"panel blocks ground interaction");
                Result="PASS: real ground raycast, deferred acquisition, first snapshot, repeated click, panel gating";
            }
            finally{Application.runInBackground=background;}
        }
    }
}
