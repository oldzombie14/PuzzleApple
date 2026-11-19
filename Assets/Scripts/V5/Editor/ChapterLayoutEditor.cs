using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace PuzzleApple.V5.Editor
{
    [CustomEditor(typeof(ChapterLayoutSettings))]
    public sealed class ChapterLayoutEditor : UnityEditor.Editor
    {
        const string SettingsPath="Assets/GameData/V5/Resources/V5/ChapterLayout.asset";
        public override void OnInspectorGUI(){
            DrawDefaultInspector();
            EditorGUILayout.HelpBox("Apply updates the three Chapter I scenes. The arrival box keeps its six metre dimensions.",MessageType.Info);
            if(GUILayout.Button("Apply chapter layout"))Apply();
        }
        static Transform Find(Scene scene,string name)=>scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<Transform>(true)).First(t=>t.name==name);
        static ChapterRoom Room(Scene scene)=>scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<ChapterRoom>(true)).First();
        static void Delete(Scene scene,Func<string,bool> match){
            foreach(var t in scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<Transform>(true)).Where(t=>match(t.name)).ToArray())
                if(t)DestroyImmediate(t.gameObject);
        }
        static void Y(Transform t,float y){var p=t.position;p.y=y;t.position=p;}
        static void LocalZ(Transform t,float z){var p=t.localPosition;p.z=z;t.localPosition=p;}
        public static string Apply(){
            if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Apply in Edit mode.");
            var settings=AssetDatabase.LoadAssetAtPath<ChapterLayoutSettings>(SettingsPath);
            if(!settings){settings=CreateInstance<ChapterLayoutSettings>();AssetDatabase.CreateAsset(settings,SettingsPath);}
            var active=SceneManager.GetActiveScene();
            var scenes=new Scene[3];
            try{
                var names=new[]{"ChapterI","ChapterI_Garden","ChapterI_Vitruvian"};
                for(int i=0;i<3;i++){
                    var path="Assets/Scenes/V5/"+names[i]+".unity";
                    if(SceneManager.GetSceneByPath(path).isLoaded)throw new InvalidOperationException("Close Chapter scenes before applying layout; their unsaved edits must be preserved.");
                    scenes[i]=EditorSceneManager.OpenScene(path,OpenSceneMode.Additive);
                }
                var gallery=scenes[0];var garden=scenes[1];var vitruvian=scenes[2];
                var chapter=Room(gallery);var gardenRoom=Room(garden);var vitruvianRoom=Room(vitruvian);
                var content=Find(gallery,"ChapterI content");float oldScale=content.lossyScale.x;
                var corridor=Find(gallery,"Corridor");
                var prefab=PrefabUtility.GetOutermostPrefabInstanceRoot(corridor.gameObject);
                if(prefab)PrefabUtility.UnpackPrefabInstance(prefab,PrefabUnpackMode.Completely,InteractionMode.AutomatedAction);
                var paintingFrame=corridor.Find("frame");if(paintingFrame)DestroyImmediate(paintingFrame.gameObject);
                Delete(gallery,n=>n.StartsWith("Gallery entrance tread")||n.StartsWith("Vitruvian tread")||n=="Right painting"||n=="Gallery wall light");
                Delete(garden,n=>n.StartsWith("Garden entrance tread")||n=="Threshold blossoms"||n=="PortraitBouquet"||n=="Bouquet soft fill"||n=="Entry threshold");
                Delete(vitruvian,n=>n.StartsWith("Blank painting"));
                var fixture=Find(gallery,"Vitruvian Gallery wall light");
                chapter.paintingFixture=fixture;
                chapter.paintingSpot=fixture.GetComponentInChildren<Light>(true);
                chapter.paintingLens=fixture.GetComponentsInChildren<Renderer>(true).First(r=>r.sharedMaterial.HasProperty("_EmissionColor") && r.sharedMaterial.GetColor("_EmissionColor").maxColorComponent>0);

                // Shorten the room in its original local units before uniform scaling.
                var vFloor=vitruvianRoom.floor.transform;
                float shortening=vFloor.localScale.z-18;
                if(Mathf.Abs(shortening)>.001f){
                    var root=vitruvianRoom.transform;
                    foreach(Transform t in root){
                        if(t.name=="Floor"||t.name=="Ceiling"||t.name.StartsWith("Side wall")){
                            LocalZ(t,t.localPosition.z-shortening*.5f);var size=t.localScale;size.z-=shortening;t.localScale=size;
                        }else if(t.name=="Room fill"){
                            LocalZ(t,t.localPosition.z-shortening*.5f);var b=vitruvianRoom.roomFill.interior;var size=b.size;size.z-=shortening;b.size=size;vitruvianRoom.roomFill.interior=b;
                        }else if(t.name=="Figure wall"||t.name.StartsWith("Vitruvian_" )||t.name=="Vitruvian instruments")LocalZ(t,t.localPosition.z-shortening);
                        else if(t.name.StartsWith("Ceiling light")||t.name=="Sculpture spotlight")LocalZ(t,t.localPosition.z*18/(18+shortening));
                    }
                }
                // The sill is specified above the actual gallery floor, independently of scale.
                float currentSill=Find(gallery,"Left wall below frame").GetComponent<Renderer>().bounds.max.y;
                float targetBeforeScale=chapter.floor.bounds.max.y+settings.sillHeight*oldScale/settings.uniformScale;
                float dy=targetBeforeScale-currentSill;
                foreach(string n in new[]{"Living frame to ChapterI_Garden","Living frame to Vitruvian room","Vitruvian frame.001"})Find(gallery,n).position+=Vector3.up*dy;
                var nativeFrame=corridor.Find("frame.001");
                float frameShift=chapter.gardenThreshold.position.y+.003723f*oldScale-nativeFrame.position.y;
                nativeFrame.position+=Vector3.up*frameShift;
                var backing=corridor.Find("frame.002");if(backing)backing.position+=Vector3.up*frameShift;
                foreach(string n in new[]{"Left wall below frame","Vitruvian Left wall below frame"}){
                    var t=Find(gallery,n);var b=t.GetComponent<Renderer>().bounds;
                    Y(t,(b.min.y+targetBeforeScale)*.5f);var size=t.localScale;size.y=(targetBeforeScale-b.min.y)/oldScale;t.localScale=size;
                }
                foreach(string n in new[]{"Left frame lintel","Vitruvian Left frame lintel"}){
                    var t=Find(gallery,n);var b=t.GetComponent<Renderer>().bounds;
                    Y(t,t.position.y+dy*.5f);var size=t.localScale;size.y=(b.size.y-dy)/oldScale;t.localScale=size;
                }
                foreach(var scene in new[]{garden,vitruvian})foreach(var root in scene.GetRootGameObjects())root.transform.position+=Vector3.up*dy;
                Physics.SyncTransforms();
                float raise=targetBeforeScale-gardenRoom.floor.bounds.max.y;
                gardenRoom.floor.transform.position+=Vector3.up*raise;
                var fill=gardenRoom.roomFill;var bounds=fill.interior;
                var minimum=bounds.min;minimum.y+=raise/oldScale;bounds.SetMinMax(minimum,bounds.max);fill.interior=bounds;
                Y(gardenRoom.arrival,targetBeforeScale+.12f*oldScale);

                // Snapshot the objects belonging to the six metre arrival space.
                var preserved=new[]{chapter.box.transform,chapter.arrival,chapter.mirrors.originals[0],chapter.boxFill.transform,Find(gallery,"Downward illumination")};
                var positions=preserved.Select(t=>t.position).ToArray();
                var scales=preserved.Select(t=>t.lossyScale).ToArray();
                float oldCentre=chapter.arrival.position.x;
                float ratio=settings.uniformScale/oldScale;
                var pivot=new Vector3(4000,0,0);
                foreach(var scene in scenes)foreach(var root in scene.GetRootGameObjects()){
                    var t=root.transform;t.position=pivot+(t.position-pivot)*ratio;t.localScale*=ratio;
                }
                Physics.SyncTransforms();
                float centre=chapter.floor.bounds.max.x-3.1f;
                for(int i=0;i<preserved.Length;i++){
                    var t=preserved[i];t.position=positions[i]+Vector3.right*(centre-oldCentre);
                    var p=t.parent.lossyScale;t.localScale=new Vector3(scales[i].x/p.x,scales[i].y/p.y,scales[i].z/p.z);
                }
                chapter.box.foldHalfWidth=chapter.floor.bounds.extents.z-.12f;
                chapter.mirrors.foldedCentre=content.InverseTransformPoint(chapter.arrival.position).x;
                // The entry mirror remains full size; outlines are in gallery local units.
                chapter.mirrors.mirrorSize/=ratio;
                var figure=Find(vitruvian,"Vitruvian_Body_15k_Relaxed");
                if(!figure.GetComponent<Collider>())figure.gameObject.AddComponent<MeshCollider>().sharedMesh=figure.GetComponent<MeshFilter>().sharedMesh;
                foreach(var scene in scenes){
                    var room=Room(scene);room.layout=settings;
                    foreach(var edges in scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<OuterRoomEdges>(true)))edges.surfaces=edges.surfaces.Where(r=>r).ToArray();
                    room.artLights=room.artLights.Where(l=>l).ToArray();
                    EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);
                }
                EditorUtility.SetDirty(settings);AssetDatabase.SaveAssetIfDirty(settings);
                return "Applied Chapter layout: scale "+settings.uniformScale+", sill "+settings.sillHeight+", walk "+settings.walkingMultiplier;
            }finally{
                foreach(var scene in scenes)if(scene.IsValid()&&scene.isLoaded)EditorSceneManager.CloseScene(scene,true);
                if(active.IsValid())SceneManager.SetActiveScene(active);
            }
        }
    }
}
