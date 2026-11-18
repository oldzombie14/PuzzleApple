using System;
using System.Collections;
using System.Linq;
using UnityEngine;

namespace PuzzleApple.V5.Editor
{
    public static class ChapterChecks
    {
        public static string Result {get;private set;}
        static void Check(bool passed,string label)
        {
            if(passed)return;
            Result="FAIL: "+label;
            throw new InvalidOperationException(Result);
        }
        static IEnumerator Until(Func<bool> done,string label)
        {
            float end=Time.realtimeSinceStartup+45;
            while(!done()&&Time.realtimeSinceStartup<end)yield return null;
            Check(done(),label);
        }
        public static void Run(bool mirrorFirst=false)
        {
            var e=UnityEngine.Object.FindFirstObjectByType<WorldEntry>();
            Check(Application.isPlaying&&e&&!e.room.Opened,"fresh P5 Play");
            Result="Running";
            e.StartCoroutine(Route(e,mirrorFirst));
        }
        internal static IEnumerator Cross(WorldEntry e,Transform frame)
        {
            e.player.Teleport(frame.TransformPoint(new Vector3(0,.24f,-.65f)),frame.rotation);
            yield return null;
            yield return null;
            e.player.GetComponent<CharacterController>().Move(frame.forward*1.3f);
            yield return null;
        }
        static IEnumerator Route(WorldEntry e,bool mirrorFirst)
        {
            var passage=e.outdoor;
            var state=e.board.State;
            e.OpenWorld(-1);
            yield return Until(()=>e.room.Opened&&!e.Busy,"initial open");
            e.board.Panel.SetOpen(false);
            var j=e.journey;
            yield return WhiteAscentChecks.Leave(e);
            yield return Cross(e,passage.World.door.transform);
            yield return Until(()=>!passage.IsOutside&&!passage.Transitioning,"original door returns indoors");
            var voice=passage.World.Audio.GetComponents<AudioSource>().First(a=>a.clip);
            Check(voice.isPlaying && voice.volume<passage.World.music.volume,"return music fades across scene switch");
            yield return Until(()=>!voice.isPlaying,"indoor silence");
            Check(j.enabled&&!passage.InChapter,"indoor restored");
            yield return WhiteAscentChecks.Revisit(e);
            yield return Until(()=>passage.IsOutside&&!passage.Transitioning,"second fall");
            var frame=passage.World.chapterFrame;
            yield return Cross(e,frame);
            yield return Until(()=>passage.IsOutside && passage.Transitioning && e.blackBackground.color.a>.06f && e.blackBackground.color.a<.98f,"outdoor picture fades before the switch");
            Check(voice.isPlaying && voice.volume<passage.World.music.volume,"outdoor music is fading at the frame");
            yield return Until(()=>passage.InChapter && passage.Transitioning && e.blackBackground.color.a>.02f && e.blackBackground.color.a<.94f,"the gallery fades in after loading");
            yield return Until(()=>passage.InChapter&&!passage.Transitioning,"picture frame arrival");
            var chapter=passage.Chapter;
            Check(!chapter.box.Opened&&!state.Knows("mirror"),"starts closed");
            var rim=chapter.mirrors.originals[0].Find("Frame left").GetComponent<Renderer>().sharedMaterial;
            Check(rim.GetColor("_BaseColor").r>.95f && rim.GetColor("_EmissionColor").r>.5f,"the arrival rim is visible inside the dark box");
            state.TryAcquireWord("chapter.drop.light",e.board.Catalog.Word("light"));
            CheckDropRegion(e);
            bool outdoorLight=passage.World.stateSource.TargetOn;
            state.TryAcquireSentence("chapter.check.off","negative light");
            yield return Until(()=>chapter.bulb.intensity<.001f&&!state.Groups.Any(g=>g.RuleId=="light.off"),"off retires");
            Check(RenderSettings.ambientLight==UnityEngine.Object.FindFirstObjectByType<RoomLighting>(FindObjectsInactive.Include).closedAmbient,"intro closed ambient");
            var ink=new MaterialPropertyBlock();
            foreach(var side in chapter.box.GetComponent<FoldingInterior>().surfaces)
            {
                side.renderer.GetPropertyBlock(ink);
                Check(ink.GetVector("_ContactSeams").sqrMagnitude>3.9f && ink.GetFloat("_ClosedWeight")>.99f,"four visible closed creases on "+side.renderer.name);
            }
            Check(passage.World.stateSource.TargetOn==outdoorLight,"light command stops at portal");
            if(mirrorFirst)
            {
                yield return CollectMirror(e,chapter);
                Check(chapter.box.Progress==0,"collecting mirror leaves box closed");
                yield return WalkToMirror(e,chapter);
                yield return Until(()=>passage.IsOutside,"mirror returns from closed box");
                yield return Cross(e,frame);
                yield return Until(()=>passage.InChapter&&!passage.Transitioning,"return to closed box");
            }
            state.TryAcquireSentence("chapter.check.move","light move");
            Check(!chapter.lamp&&!chapter.emitter,"chapter has no cube lamp");
            state.TryAcquireSentence("chapter.check.open","positive world");
            yield return Until(()=>chapter.box.Progress>.1f,"chapter opening");
            Check(chapter.box.movingColliders.All(c=>c.enabled),"live folding collision");
            yield return Until(()=>chapter.box.Opened&&!e.Busy,"unfold without mirror");
            var shell=chapter.roomFill.GetComponent<OuterRoomEdges>();
            var ground=chapter.floor.GetComponent<Renderer>();
            Check(shell&&shell.surfaces.Contains(ground),"gallery floor belongs to outlined room");
            ground.GetPropertyBlock(ink);
            var min=ink.GetVector("_InkRoomMin");
            var max=ink.GetVector("_InkRoomMax");
            Check(max.x>min.x&&max.y>min.y&&max.z>min.z,"gallery has a complete inner boundary");
            foreach(var surface in shell.surfaces)
            {
                surface.GetPropertyBlock(ink);
                Check(ink.GetFloat("_UseInkRoomBounds")==1 && ink.GetVector("_InkRoomMin")==min &&
                    ink.GetVector("_InkRoomMax")==max && surface.sharedMaterial==ground.sharedMaterial,
                    "shared gallery ink on "+surface.name);
            }
            yield return WalkToMirror(e,chapter);
            yield return Until(()=>passage.IsOutside,"return to outdoor frame");
            yield return Cross(e,frame);
            yield return Until(()=>passage.InChapter&&!passage.Transitioning,"revisit chapter");
            Check(chapter.box.Opened&&chapter.bulb.intensity<.001f,"revisit preserves chapter");
            if(!mirrorFirst)yield return CollectMirror(e,chapter);
            Check(state.Groups.Count==0 && state.Knows("move"),"chapter crossings clear sentences and retain words");
            yield return new WaitForFixedUpdate();
            state.TryAcquireSentence("chapter.check.on","positive light");
            yield return Until(()=>chapter.bulb.intensity>0,"light can switch back on");
            e.player.Teleport(chapter.arrival.position,chapter.arrival.rotation);
            yield return null;
            Check(rim.GetColor("_BaseColor").r<.1f,"the fully open gallery mirror has a black rim");
            e.RequestExit();
            yield return Until(()=>chapter.box.Progress<.64f && chapter.box.Progress>.28f,"the entrance folds back around the mirror");
            var middle=rim.GetColor("_BaseColor").r;
            Check(middle>.2f && middle<.9f,"the frame brightens continuously during closing");
            yield return Until(()=>e.DialogReady,"world exit confirmation");
            Check(chapter.box.Progress==0 && rim.GetColor("_BaseColor").r>.95f,"closed mirror is white again");
            e.CancelExit();e.OpenWorld(-1);
            yield return Until(()=>e.room.Opened&&!e.Busy,"portable box reopens");
            Check(chapter.box.Opened&&state.Knows("mirror"),"chapter retained after cancelling exit");
            var portablePanels=e.room.GetComponent<FoldingInterior>();
            foreach(var surface in portablePanels.surfaces)
            {
                surface.renderer.GetPropertyBlock(ink);
                Check(ink.GetFloat("_PanelOpacity")==0,"portable panels stay sunk after returning to the gallery");
            }
            yield return CheckGarden(e);
            Result="PASS";
        }
        static IEnumerator CheckGarden(WorldEntry e)
        {
            var passage=e.outdoor;
            var chapter=passage.Chapter;
            var garden=passage.Garden;
            var gate=chapter.gardenThreshold;
            var p=e.player;
            var state=e.board.State;
            yield return Until(()=>!state.Groups.Any(g=>g.RuleId=="light.on"),"previous light command retired");
            var lens=chapter.paintingLens.bounds.center;
            p.Teleport(lens+Vector3.down*13+Vector3.back*4,Quaternion.identity);
            p.view.LookAt(lens);p.AdoptCurrentView();Cursor.lockState=CursorLockMode.Locked;
            yield return null;
            Check(e.chamberLight.TryCollect()&&e.chamberLight.KnownFeedbackWord=="light","painting lamp light interaction");
            var galleryFill=chapter.roomFill;
            Check(galleryFill.style==e.journey.LightStyle,"shared Intro and gallery lighting style");
            float litFill=galleryFill.Fill;
            state.TryAcquireSentence("garden.gallery.off","negative light");
            yield return Until(()=>chapter.paintingSpot.intensity==0,"painting spotlight fades out");
            yield return Until(()=>galleryFill.Fill<litFill && RenderSettings.ambientLight==galleryFill.FillColor,"gallery bounce follows its real lights");
            Check(!e.chamberLight.LookingAtLight()&&garden.bulb.intensity>0,"unlit painting lamp and independent garden");
            p.Teleport(new Vector3(gate.position.x,.1f,gate.position.z+4.2f),Quaternion.LookRotation(-gate.forward));
            var walk=typeof(FirstPersonController).GetMethod("AdvanceMovement",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic);
            for(int i=0;i<165;i++)walk.Invoke(p,new object[]{Vector2.up,true,1f/120,false});
            Check(passage.CurrentRoom==chapter,"gallery steps stop below the frame");
            for(int i=0;i<185;i++)walk.Invoke(p,new object[]{Vector2.up,true,1f/120,i==0});
            Check(passage.CurrentRoom==garden,"jump over the frame into the garden");
            CheckDropRegion(e);
            var start=garden.lamp.position;
            yield return Until(()=>Mathf.Abs(garden.lamp.position.y-start.y)>.04f,"garden cube floats");
            state.TryAcquireSentence("garden.cube.off","negative light");
            yield return Until(()=>garden.bulb.intensity==0,"garden light switches off");
            state.TryAcquireSentence("garden.cube.on","positive light");
            yield return Until(()=>garden.bulb.intensity>0,"garden light switches on");
            Check(chapter.paintingSpot.intensity==0,"garden command stays in garden");
            state.TryAcquireSentence("garden.cube.floor","light move floor",out var floorCommand);
            yield return Until(()=>state.Find(floorCommand.Id)==null,"garden floor command retires");
            var parked=garden.lamp.position;yield return new WaitForSeconds(.28f);
            Check(Vector3.Distance(parked,garden.lamp.position)<.015f,"garden cube holds its floor position");
            for(int i=0;i<330;i++)walk.Invoke(p,new object[]{Vector2.down,true,1f/120,i==175});
            Check(passage.CurrentRoom==chapter,"walk back into gallery");
            yield return Until(()=>!state.Groups.Any(g=>g.RuleId=="light.on"),"garden command retired");
            state.TryAcquireSentence("garden.gallery.on","positive light");
            yield return Until(()=>chapter.paintingSpot.intensity>0,"painting light switches on");
        }
        static void CheckDropRegion(WorldEntry e)
        {
            var pass=e.outdoor;var region=pass.DropRegion;var panel=e.board.Panel;
            Check(region && !region.allowInstantiation,"chapter placement disabled by default");
            panel.SetOpen(true);
            Vector2 point=new Vector2(.84f*Screen.width,.53f*Screen.height);
            Check(!pass.TryWorldDrop(e.board,"light",point),"disabled region ignores drop");
            try{
                region.allowInstantiation=true;
                Check(pass.TryWorldDrop(e.board,"light",point) && pass.Picker.Open,"chapter region can opt in");
                pass.Picker.Select(InstanceShape.Sphere);
                Check(!pass.Picker.Open,"chapter choice handled");
            }finally{region.allowInstantiation=false;panel.SetOpen(false);}
        }
        static IEnumerator WalkToMirror(WorldEntry e,ChapterRoom chapter)
        {
            var towardMirror=Vector3.right;
            var approach=chapter.mirror.bounds.center-towardMirror*1.4f;
            approach.y=chapter.floor.bounds.max.y+.08f;
            e.player.Teleport(approach,Quaternion.LookRotation(towardMirror));
            yield return null;yield return null;
            e.player.GetComponent<CharacterController>().Move(towardMirror*1.4f);
            yield return null;
        }
        static IEnumerator CollectMirror(WorldEntry e,ChapterRoom chapter)
        {
            e.player.Teleport(chapter.mirror.bounds.center+Vector3.left*2-Vector3.up,Quaternion.LookRotation(Vector3.right));
            e.player.view.LookAt(chapter.mirror.bounds.center);e.player.AdoptCurrentView();
            Cursor.lockState=CursorLockMode.Locked;
            yield return null;
            Check(e.chamberLight.TryCollect(),"mirror raycast in dark");
            yield return Until(()=>e.board.State.Knows("mirror"),"mirror enters library");
        }
    }
}
