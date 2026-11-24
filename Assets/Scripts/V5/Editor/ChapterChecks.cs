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
        public static void RunBuildRoute()
        {
            Result = "Running";
            var entry = UnityEngine.Object.FindFirstObjectByType<WorldEntry>();
            entry.StartCoroutine(BuildRoute(entry));
        }
        public static void RunLightStates()
        {
            Result = "Running";
            var entry = UnityEngine.Object.FindFirstObjectByType<WorldEntry>();
            entry.StartCoroutine(BuildRoute(entry, true));
        }
        static IEnumerator BuildRoute(WorldEntry entry, bool checkStates = false)
        {
            var passage = entry.outdoor;
            var start = entry.gameObject.scene;
            Check(start.buildIndex == 0 && start.path == "Assets/Scenes/V5/P5.unity", "P5 is the build entry");
            entry.OpenWorld(-1);
            yield return Until(() => entry.room.Opened && !entry.Busy, "open intro");
            yield return WhiteAscentChecks.Leave(entry);
            var outdoor = passage.World;
            var worldMotion = outdoor.world.MotionOutput.StateSource<Outdoor.WorldSentenceEffects.MotionState>();
            var groundState = checkStates ? Copy(worldMotion.CaptureState()) : default;
            if (checkStates)
            {
                var world = passage.World;
                var emission = world.stateSource.GetComponents<SentenceEntity>().SelectMany(entity => entity.Outputs)
                    .Single(output => ReferenceEquals(output.Source, world));
                var dimmer = emission.StateSource<Outdoor.OutdoorWorld.LightState>();
                var original = dimmer.CaptureState();
                emission.Command(false);
                yield return Until(() => world.stateSource.Brightness < .8f && world.stateSource.Brightness > 0, "outdoor dimmer in flight");
                emission.Hold();
                var night = JsonUtility.FromJson<Outdoor.OutdoorWorld.LightState>(JsonUtility.ToJson(dimmer.CaptureState()));
                Check(dimmer.TryRestoreState(original) && dimmer.TryRestoreState(night), "restore sky and lamps");
                yield return null;
                Check(world.stateSource.Brightness == night.lamps.brightness && world.environment.NightBlend == night.nightBlend,
                    "held outdoor dimmer preserves both levels");
                emission.Command(true);
                yield return Until(() => !emission.Settling(true), "outdoor lighting resumes");
                var door = world.door.GetComponents<SentenceEntity>().SelectMany(entity => entity.Outputs)
                    .Single(output => ReferenceEquals(output.Source, world));
                var frame = door.StateSource<EmissionState>();
                var saved = frame.CaptureState();
                door.Hold();
                Check(frame.TryRestoreState(new EmissionState(.35f)) && door.Value == .35f && frame.TryRestoreState(saved),
                    "threshold frame restores independently from outdoor lamps");
            }
            yield return Cross(entry, passage.World.chapterFrame);
            yield return ChapterLessonChecks.Traverse(entry);
            yield return Until(() => passage.InChapter && !passage.Transitioning, "cross into Chapter I");
            foreach (var room in new[] { passage.Chapter, passage.Garden, passage.Vitruvian })
            {
                Check(room && room.gameObject.scene.isLoaded && room.gameObject.scene.buildIndex > 0 &&
                    room.gameObject.scene.path.StartsWith("Assets/Scenes/V5/"), "V5 additive chapter room loaded");
                var missingScripts = room.gameObject.scene.GetRootGameObjects()
                    .SelectMany(root => root.GetComponentsInChildren<MonoBehaviour>(true)).Any(script => !script);
                Check(!missingScripts, "all room scripts resolved in " + room.gameObject.scene.name);
            }
            if (checkStates)
            {
                var away = entry.player.transform.position;
                var underground = Copy(groundState);
                underground.groundY -= .35f;
                underground.clouds.travel += Vector3.forward * 7;
                underground.stars = Quaternion.Euler(0, 17, 0);
                Check(worldMotion.TryRestoreState(underground) && entry.player.transform.position == away &&
                    Mathf.Abs(outdoor.ground.transform.position.y - underground.groundY) < .001f &&
                    worldMotion.CaptureState().clouds.travel == underground.clouds.travel &&
                    Quaternion.Angle(worldMotion.CaptureState().stars, underground.stars) < .01f,
                    "inactive outdoor ground, cloud and star pose restore without carrying the chapter player");
                Check(worldMotion.TryRestoreState(groundState), "outdoor height restored before return");
                foreach (var room in new[] { passage.Chapter, passage.Garden, passage.Vitruvian })
                {
                    var position = room.MotionOutput.StateSource<ChapterRoom.MotionState>();
                    Outdoor.LightMotion created = null;
                    if (room.puzzle)
                    {
                        var lamp = room.puzzle.field.Spawn(room.puzzle.ball.transform.position + Vector3.right * 2,
                            0, InstanceShape.Sphere, room.GetComponent<WorldDropRegion>());
                        created = lamp.GetComponent<Outdoor.LightMotion>();
                    }
                    var rest = Copy(position.CaptureState());
                    var sample = Copy(rest);
                    if (room.lamp) sample.lamp.position += Vector3.up * .35f;
                    for (int i = 0; i < sample.paintingPhases.Length; i++) sample.paintingPhases[i] += .42f;
                    if (room.puzzle)
                    {
                        sample.orbit.angle = Mathf.Repeat(rest.orbit.angle + 47, 360);
                        int index = Array.FindIndex(sample.orbit.created.lamps, l => l.id == created.SpawnId);
                        Check(index >= 0, "chapter output includes the generated lamp");
                        sample.orbit.created.lamps[index].motion.pose.position += Vector3.up;
                    }
                    var previousCreated = created ? created.CaptureMotion().pose.position : default;
                    var previousBall = room.puzzle ? room.puzzle.ball.transform.position : default;
                    Check(position.TryRestoreState(sample) &&
                        (!room.lamp || room.lamp.position == sample.lamp.position) &&
                        (!room.puzzle || room.puzzle.ball.transform.position != previousBall &&
                            created.CaptureMotion().pose.position == previousCreated + Vector3.up) &&
                        position.CaptureState().paintingPhases.SequenceEqual(sample.paintingPhases) &&
                        position.TryRestoreState(rest), "chapter motion round trip in " + room.name);
                    room.HoldLight();
                    var original = room.CaptureState();
                    var levels = JsonUtility.FromJson<ChapterRoom.LightState>(JsonUtility.ToJson(original));
                    levels.room.brightness = .45f;
                    for (int i = 0; i < levels.fixtures.Length; i++) levels.fixtures[i].brightness = (i+1f)/(levels.fixtures.Length+1);
                    Check(room.TryRestoreState(levels), "restore chapter fixtures " + room.name);
                    yield return null;
                    Check(JsonUtility.ToJson(room.CaptureState()) == JsonUtility.ToJson(levels) && room.TryRestoreState(original),
                        "each held chapter fixture retains its brightness");
                }
                var puzzle = passage.Vitruvian.puzzle;
                puzzle.Hold();
                var originalPuzzle = puzzle.CaptureState();
                var mixed = new VitruvianPuzzle.LightState { source = new EmissionState(.27f), ball = new EmissionState(.83f) };
                Check(puzzle.TryRestoreState(mixed) && puzzle.source.Brightness == .27f && puzzle.ball.Brightness == .83f,
                    "Vitruvian source and ball keep independent brightness");
                mixed.ball.brightness = 2;
                mixed.source.brightness = 0;
                Check(!puzzle.TryRestoreState(mixed) && puzzle.source.Brightness == .27f && puzzle.TryRestoreState(originalPuzzle),
                    "bad ball snapshot does not partially restore the source");
            }
            yield return WalkToMirror(entry, passage.Chapter);
            yield return Until(() => passage.IsOutside && !passage.Transitioning, "return to outdoor");
            Result = checkStates ? "PASS: outdoor light, frame and sky, cross-region ground restore, chapter lamps, orbit and created lamp" : "PASS: P5, outdoor prefab, three V5 chapter scenes and return crossing";
        }
        static T Copy<T>(T state) where T : struct => JsonUtility.FromJson<T>(JsonUtility.ToJson(state));
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
            yield return Until(()=>passage.InLesson && passage.Transitioning && e.blackBackground.color.a>.02f && e.blackBackground.color.a<.94f,"the teaching chamber fades in after loading");
            yield return ChapterLessonChecks.Traverse(e);
            yield return Until(()=>passage.InChapter&&!passage.Transitioning,"picture frame arrival");
            var chapter=passage.Chapter;
            Check(!chapter.box.Opened&&state.Knows("mirror"),"arrives closed with the mirror vocabulary");
            var rim=chapter.mirrors.originals[0].Find("Frame left").GetComponent<Renderer>().sharedMaterial;
            Check(rim.GetColor("_BaseColor").r>.95f && rim.GetColor("_EmissionColor").r>.5f,"the arrival rim is visible inside the dark box");
            state.TryAcquireWord("chapter.drop.light",e.board.Catalog.Word("light"));
            yield return CheckDropRegion(e);
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
                yield return ChapterLessonChecks.Traverse(e);
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
            yield return ChapterLessonChecks.Traverse(e);
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
            yield return CheckDropRegion(e);
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
        static IEnumerator CheckDropRegion(WorldEntry e)
        {
            var pass=e.outdoor;var region=pass.DropRegion;var panel=e.board.Panel;
            Check(region && !region.allowInstantiation,"chapter placement disabled by default");
            yield return Until(()=>!e.Busy && !e.chamberLight.Collecting && !e.sentenceRuntime.presentation.Playing,
                "finish presentation before dragging in "+region.gameObject.scene.name);
            panel.SetOpen(true);
            yield return Until(()=>panel.FullyOpen,"drop panel finished opening");
            Vector2 point=new Vector2(.84f*Screen.width,.53f*Screen.height);
            Check(!panel.InputBlocked,"panel input available in "+region.gameObject.scene.name);
            Check(!RectTransformUtility.RectangleContainsScreenPoint(panel.ContentRoot,point),"drop point lies in the world");
            Check(e.board.State.Knows("light"),"light is available for placement");
            Check(!pass.TryWorldDrop(e.board,"light",point),"disabled region ignores drop");
            try{
                region.allowInstantiation=true;
                Check(pass.TryWorldDrop(e.board,"light",point) && pass.Picker.Open,"chapter region can opt in");
                pass.Picker.Select(InstanceShape.Sphere);
                Check(!pass.Picker.Open,"chapter choice handled");
            }finally{region.allowInstantiation=false;panel.SetOpen(false);}
        }
        internal static IEnumerator WalkToMirror(WorldEntry e,ChapterRoom chapter)
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
