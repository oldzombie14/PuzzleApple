using System;
using System.Collections;
using System.Linq;
using UnityEngine;
using PuzzleApple.V5.Outdoor;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

namespace PuzzleApple.V5.Editor
{
    public static class WorldDropChecks
    {
        public static string Result {get;private set;}
        static void Check(bool ok,string message){if(ok)return;Result="FAIL: "+message;throw new Exception(Result);}
        public static void Run(){
            var e=UnityEngine.Object.FindFirstObjectByType<WorldEntry>();
            Result="Running";e.StartCoroutine(Exercise(e));
        }
        static IEnumerator Exercise(WorldEntry e){
            var keyboard=InputSystem.AddDevice<Keyboard>();var mouse=InputSystem.AddDevice<Mouse>();
            try{
            e.OpenWorld(-1);yield return ChargeDoorChecks.Wait(()=>e.room.Opened&&!e.Busy,"unfold");
            var s=e.board.State;s.TryAcquireWord("drop.check.light",e.board.Catalog.Word("light"));
            var pass=e.outdoor;var board=e.board;
            Vector2 point=new Vector2(Screen.width*.79f,Screen.height*.53f);
            board.Panel.SetOpen(true);Check(!pass.TryWorldDrop(board,"light",point),"intro rejects drop");
            yield return WhiteAscentChecks.Leave(e);
            var w=pass.World;var p=e.player;
            e.sentenceRuntime.presentation.enabledShots=false;
            p.Teleport(w.exit.position+Vector3.forward*10,Quaternion.identity);p.MovementLocked=true;
            board.Panel.SetOpen(true);yield return new WaitForSecondsRealtime(.4f);
            var field=w.field;var region=pass.DropRegion;
            var source=field.Spawn(w.exit.position+Vector3.up*4,.2f);
            Check(source && !source.GetComponent<PlayerInstance>(),"authored/transfer lamp has no recovery marker");
            Check(!pass.TryWorldDrop(board,"door",point),"other nouns rejected");
            foreach(var shape in new[]{InstanceShape.Sphere,InstanceShape.Cube,InstanceShape.Octahedron}){
                Check(pass.TryWorldDrop(board,"light",point),"accepted drop");
                var picker=pass.Picker;
                int count=field.PlayerCount;
                var choice=board.Panel.ContentRoot.GetComponentInParent<Canvas>().rootCanvas.transform.Find("Shape selection/Shape choices");
                Check(choice && choice.Cast<Transform>().Count(t=>t.GetComponent<Button>())==3 && field.PlayerCount==count,"choice before spawning");
                if(shape==InstanceShape.Sphere)choice.Find(shape.ToString()).GetComponent<Button>().onClick.Invoke();
                else{
                    keyboard.MakeCurrent();InputSystem.QueueStateEvent(keyboard,new KeyboardState(shape==InstanceShape.Cube?Key.Digit2:Key.Digit3));
                    yield return null;yield return null;
                    InputSystem.QueueStateEvent(keyboard,new KeyboardState());yield return null;
                }
                Check(!picker.Open && field.PlayerCount==count+1,"choice spawns exactly once");
                var instance=field.ActiveLamps.Select(l=>l.GetComponent<PlayerInstance>()).First(l=>l && l.Shape==shape);
                var col=instance.GetComponent<Collider>();
                Check(shape==InstanceShape.Sphere?col is SphereCollider:shape==InstanceShape.Cube?col is BoxCollider:col is MeshCollider mc && mc.convex,"matching mesh collider");
                yield return null;
                point.x-=Screen.width*.09f;
            }
            var nativeCount=field.ActiveCount;
            bool choseSphere=false;pass.Picker.Show(board.Panel,point,region.shapes,shape=>choseSphere=shape==InstanceShape.Sphere);
            keyboard.MakeCurrent();InputSystem.QueueStateEvent(keyboard,new KeyboardState(Key.Digit1));yield return null;yield return null;
            InputSystem.QueueStateEvent(keyboard,new KeyboardState());yield return null;
            Check(choseSphere,"key 1 selects the circle");
            var pickerUI=pass.Picker;
            foreach(var corner in new[]{new Vector2(0,0),new Vector2(Screen.width,0),new Vector2(0,Screen.height),new Vector2(Screen.width,Screen.height)}){
                pickerUI.Show(board.Panel,corner,region.shapes,_=>{});
                yield return new WaitForSecondsRealtime(.20f);
                var corners=new Vector3[4];pickerUI.Bounds.GetWorldCorners(corners);
                Check(corners.All(v=>v.x>=-1 && v.y>=-1 && v.x<=Screen.width+1 && v.y<=Screen.height+1),"picker clamped at "+corner);
                pickerUI.Bounds.parent.GetComponent<Button>().onClick.Invoke();
                Check(!pickerUI.Open,"outside click cancels");
            }
            pass.TryWorldDrop(board,"light",point);
            var cell=pickerUI.Bounds.GetChild(0).GetComponent<Button>();
            var pointer=new PointerEventData(EventSystem.current);
            ExecuteEvents.Execute(cell.gameObject,pointer,ExecuteEvents.pointerExitHandler);
            yield return new WaitForSecondsRealtime(.18f);var normal=cell.targetGraphic.canvasRenderer.GetColor();
            ExecuteEvents.Execute(cell.gameObject,pointer,ExecuteEvents.pointerEnterHandler);
            yield return new WaitForSecondsRealtime(.18f);
            Check(cell.targetGraphic.canvasRenderer.GetColor().r>normal.r+.1f,"hover brightens cell");
            keyboard.MakeCurrent();InputSystem.QueueStateEvent(keyboard,new KeyboardState(Key.Escape));
            yield return null;yield return null;
            InputSystem.QueueStateEvent(keyboard,new KeyboardState());yield return null;
            Check(!pickerUI.Open && board.Panel.IsOpen,"Escape cancels only picker (picker="+pickerUI.Open+", panel="+board.Panel.IsOpen+", keyboard="+keyboard.enabled+")");
            pass.TryWorldDrop(board,"light",point);
            mouse.MakeCurrent();InputSystem.QueueStateEvent(mouse,new MouseState{position=point}.WithButton(MouseButton.Right));
            yield return null;yield return null;
            InputSystem.QueueStateEvent(mouse,new MouseState{position=point});yield return null;
            Check(!pickerUI.Open && field.PlayerCount==3,"right click cancels without recovery");
            Check(field.ActiveCount==nativeCount && board.Panel.IsOpen,"cancel preserves panel and lamps");
            int limit=region.maxInstances;region.maxInstances=field.PlayerCount;
            pass.TryWorldDrop(board,"light",point);pass.Picker.Select(InstanceShape.Cube);
            Check(field.ActiveCount==nativeCount,"capacity refuses placement");region.maxInstances=limit;
            var created=field.ActiveLamps.Where(l=>l.GetComponent<PlayerInstance>()).ToArray();
            s.TryAcquireSentence("drop.move","light move");yield return new WaitForSeconds(.35f);
            Check(created.All(l=>l.GetComponent<LightMotion>().Moving),"all shapes move");
            s.TryAcquireSentence("drop.bright","light light");s.TryAcquireSentence("drop.pause","negative move",out var freeze);
            var held=created.Select(l=>l.transform.position).ToArray();yield return new WaitForSecondsRealtime(.25f);
            Check(created.Select((l,i)=>Vector3.Distance(l.transform.position,held[i])<.03f).All(v=>v),"freeze applies to all shapes");
            s.ReturnToLibrary(freeze.Id,0,true);yield return null;
            board.Panel.SetOpen(false);
            region.allowInstantiation=false;Check(!region.Accepts("light"),"space switch disables placement");region.allowInstantiation=true;
            var recovery=pass.GetComponent<InstanceRecovery>();
            var eye=p.view;var aimedLight=created[0].GetComponent<Rigidbody>();var originalSpot=aimedLight.position;
            aimedLight.position=eye.position+eye.forward*2;Physics.SyncTransforms();
            Cursor.lockState=CursorLockMode.Locked;
            yield return null;yield return new WaitForEndOfFrame();
            var recoveryIcon=e.chamberLight.hoverEye.transform.parent.Find("Recover light");
            Check(recoveryIcon && recoveryIcon.gameObject.activeSelf,"actual sightline shows recovery reticle");
            var authored=source.GetComponent<Rigidbody>();var authoredSpot=authored.position;
            aimedLight.position=originalSpot;authored.position=eye.position+eye.forward*2;Physics.SyncTransforms();
            yield return null;yield return new WaitForEndOfFrame();
            Check(!recoveryIcon.gameObject.activeSelf,"authored lamp has no recovery reticle");
            authored.position=authoredSpot;Physics.SyncTransforms();
            var target=created[0].GetComponent<PlayerInstance>();
            s.TryAcquireSentence("drop.off","negative light",out var off);yield return null;yield return null;
            recovery.Advance(target,true,true,false,.04f);
            recovery.Advance(target,false,false,true,.06f);
            Check(target.Recovering && field.PlayerCount==2,"short click recovers only aimed instance");
            yield return ChargeDoorChecks.Wait(()=>s.Find(off.Id)==null,"off light completes through recovery");
            s.TryAcquireSentence("drop.on","positive light",out var on);
            yield return ChargeDoorChecks.Wait(()=>s.Find(on.Id)==null && w.stateSource.IsOn,"remaining shapes relight");
            target=created[1].GetComponent<PlayerInstance>();
            recovery.Advance(target,true,true,false,1.2f);recovery.Advance(target,false,false,true,.1f);
            Check(!target.Recovering && field.PlayerCount==2,"holding on an instance never clears");
            recovery.Advance(null,true,true,false,.4f);Check(recovery.Progress>0,"empty hold shows progress");
            recovery.Advance(target,false,true,false,.7f);Check(recovery.Progress==0 && field.PlayerCount==2,"aiming cancels hold");
            recovery.Advance(null,true,true,false,.3f);recovery.Advance(null,false,false,true,.1f);
            Check(recovery.Progress==0 && field.PlayerCount==2,"release resets");
            recovery.Advance(null,true,true,false,1.1f);
            yield return new WaitForSeconds(.85f);
            Check(field.PlayerCount==0 && field.ActiveCount==1 && source && !target,"staggered recovery retains original");
            recovery.Advance(null,true,true,false,.4f);Check(recovery.Progress==0,"no empty progress");
            Result="PASS: scoped drops, three native selections, meshes, cap, cancel, motion and freeze, individual and group recovery, retention and command completion";
            }finally{InputSystem.RemoveDevice(keyboard);InputSystem.RemoveDevice(mouse);}
        }
    }
}
