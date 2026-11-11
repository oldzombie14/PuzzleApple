using System;
using System.Collections;
using UnityEngine;

namespace PuzzleApple.V5.Editor
{
    public static class FoldingInteriorChecks
    {
        public static string Result {get;private set;}
        static void Check(bool value,string message){if(!value){Result="FAIL: "+message;throw new Exception(Result);}}
        public static void Run(){Result="Running";var e=UnityEngine.Object.FindFirstObjectByType<WorldEntry>();e.StartCoroutine(Run(e));}
        static IEnumerator Until(Func<bool> predicate)
        {float end=Time.realtimeSinceStartup+20;while(!predicate()&&Time.realtimeSinceStartup<end)yield return null;Check(predicate(),"timeout");}
        static Color Centre(Camera camera)
        {
            var previous=RenderTexture.active;var image=new Texture2D(1,1,TextureFormat.RGBA32,false,true);
            try{RenderTexture.active=camera.targetTexture;image.ReadPixels(new Rect(camera.targetTexture.width/2,camera.targetTexture.height/2,1,1),0,0);image.Apply();return image.GetPixel(0,0);}
            finally{RenderTexture.active=previous;UnityEngine.Object.Destroy(image);}
        }
        static void Surfaces(FoldingInterior interior,float expected)
        {
            Check(interior.surfaces.Length==6,"all six interior surfaces configured");
            var block=new MaterialPropertyBlock();
            foreach(var surface in interior.surfaces)
            {
                surface.renderer.GetPropertyBlock(block);
                Check(Mathf.Abs(block.GetFloat("_ClosedWeight")-expected)<.001f,"surface handoff weight");
                Check(((Vector3)block.GetVector("_InteriorNormal")-surface.inwardNormal).sqrMagnitude<.0001f,"local inward direction retained");
            }
        }
        static IEnumerator Run(WorldEntry e)
        {
            bool background=Application.runInBackground;Application.runInBackground=true;
            var rig=UnityEngine.Object.FindFirstObjectByType<RoomLighting>();
            var lights=UnityEngine.Object.FindObjectsByType<Light>(FindObjectsSortMode.None);
            var enabled=Array.ConvertAll(lights,l=>l.enabled);bool rigEnabled=rig.enabled;Color ambient=RenderSettings.ambientLight;
            try
            {
                var interior=e.room.GetComponent<FoldingInterior>();
                e.EnterWorld();yield return Until(()=>e.Entered);e.board.Panel.SetOpen(false);yield return new WaitForSeconds(.4f);yield return new WaitForEndOfFrame();
                Surfaces(interior,1);
                var camera=e.player.view.GetComponent<Camera>();var initial=Centre(camera);
                Check(initial.r<.2f&&initial.r>.001f&&Mathf.Abs(initial.r-initial.g)<.01f,"closed neutral dark surface rendered");
                rig.enabled=false;foreach(var l in lights)l.enabled=false;RenderSettings.ambientLight=Color.white;
                yield return new WaitForEndOfFrame();yield return new WaitForEndOfFrame();
                var changed=Centre(camera);Check(Mathf.Abs(initial.r-changed.r)<.01f,"closed inner surface ignores external lighting changes");
                rig.enabled=rigEnabled;RenderSettings.ambientLight=ambient;for(int i=0;i<lights.Length;i++)lights[i].enabled=enabled[i];
                var room=e.room;
                float floorHeight=room.floorPanel.position.y;
                float floorAboveRoot=floorHeight-room.transform.position.y;
                // Save both ends of each real hinge in the coordinate systems of its two meshes.
                var seams=new System.Collections.Generic.List<Action>();
                Transform lid=null;
                foreach(var panel in room.panels)
                {
                    bool wall=panel.hinge.parent==room.transform;
                    if(!wall)lid=panel.hinge;
                    var childMesh=Array.Find(interior.surfaces,s=>s.renderer.transform.parent==panel.hinge).renderer.transform;
                    var parentMesh=wall?room.floorPanel:Array.Find(interior.surfaces,s=>s.renderer.transform.parent==panel.hinge.parent).renderer.transform;
                    var axis=panel.hinge.TransformDirection(panel.openEuler.normalized);
                    var vertices=childMesh.GetComponent<MeshFilter>().sharedMesh.vertices;
                    var ends=new System.Collections.Generic.HashSet<Vector3>();
                    foreach(var v in vertices)
                    {
                        Vector3 point=childMesh.TransformPoint(v);
                        if(Vector3.Cross(point-panel.hinge.position,axis).sqrMagnitude>1e-7f||!ends.Add(v))continue;
                        var mate=parentMesh.InverseTransformPoint(point);
                        Check(Array.Exists(parentMesh.GetComponent<MeshFilter>().sharedMesh.vertices,
                            other=>(parentMesh.TransformVector(other-mate)).sqrMagnitude<1e-7f),"matching vertex on adjacent panel");
                        seams.Add(()=>Check((childMesh.TransformPoint(v)-parentMesh.TransformPoint(mate)).sqrMagnitude<1e-7f,"hinge endpoints stay joined"));
                    }
                    Check(ends.Count==2,"two distinct mesh corners on hinge axis");
                }
                e.OpenWorld(-1);bool intermediate=false,seenFlatFloor=false,lidLed=false,seenFade=false,seenBackFold=false;
                while(e.Busy)
                {
                    foreach(var check in seams)check();
                    if(Quaternion.Angle(lid.localRotation,Quaternion.identity)>1&&
                        Array.TrueForAll(room.panels,p=>p.hinge==lid||Quaternion.Angle(p.hinge.localRotation,Quaternion.identity)<.05f))lidLed=true;
                    intermediate|=interior.ClosedWeight>0&&interior.ClosedWeight<1;
                    if(room.FloorFinishProgress==0)Check(room.PanelCoverage==1,"panels stay solid until flat");
                    if(room.PanelCoverage>0 && room.PanelCoverage<1)
                    {
                        seenFade=true;
                        Check(Mathf.Abs(room.floorPanel.position.y-floorHeight)<.001f,"fade finishes before sinking");
                    }
                    if(room.FloorFinishProgress<=.5f)
                        Check(Mathf.Abs(room.floorPanel.position.y-floorHeight)<.001f,"floor stays at its closed height throughout the unfold");
                    if(room.FloorFinishProgress>0)
                    {
                        if(room.PanelCoverage<.6f && room.PanelCoverage>.1f)
                        {
                            foreach(var panel in room.panels)
                            {
                                if(panel.hinge==lid)
                                {
                                    Check(Quaternion.Angle(lid.localRotation,Quaternion.Euler(panel.openEuler))<.1f,"lid inherits wall tilt without doubling it");
                                    continue;
                                }
                                var relative=Quaternion.Inverse(Quaternion.Euler(panel.openEuler))*panel.hinge.localRotation;
                                relative.ToAngleAxis(out float angle,out Vector3 axis);
                                Check(Mathf.Abs(angle-11*(1-room.PanelCoverage))<.1f && Vector3.Dot(axis,panel.openEuler.normalized)>.99f,"walls approach eleven degrees on their original hinges");
                            }
                            seenBackFold=true;
                        }
                        seenFlatFloor=true;
                    }
                    yield return null;
                }
                Check(seenFade && seenBackFold && room.PanelCoverage==0,"sheet folds back through the floor");
                Check(lidLed,"roof opens before any wall turns");
                Check(seenFlatFloor&&Mathf.Abs(room.floorPanel.position.y-floorHeight+room.sinkDepth)<.001f,"only the final phase submerges the floor");
                yield return new WaitForEndOfFrame();Check(intermediate&&e.room.Opened,"smooth handoff during opening");Surfaces(interior,0);
                foreach(bool second in new[]{false,true})
                {
                    var j=e.journey;e.player.Teleport(j.Root.TransformPoint(new Vector3(0,.08f,second?j.pathLength+3:3)),j.Root.rotation);
                    yield return null;yield return null;Check(j.BeyondChamber&&j.InSecondRoom==second,"test space selected");
                    e.RequestExit();
                    yield return Until(()=>room.FloorFinishProgress<.5f);
                    float exposedHeight=room.transform.position.y+floorAboveRoot;
                    Check(room.FloorFinishProgress>0&&Mathf.Abs(room.floorPanel.position.y-exposedHeight)<.001f,"floor rises before refolding");
                    foreach(var panel in room.panels)
                        if(panel.hinge!=lid)Check(Quaternion.Angle(panel.hinge.localRotation,Quaternion.Euler(panel.openEuler))>1,"walls resurface from the back-folded pose");
                    while(!e.DialogReady){foreach(var check in seams)check();yield return null;}
                    yield return new WaitForEndOfFrame();
                    Check(e.portableClosure.Isolated&&e.player.MovementLocked,"portable close isolates environment and locks position");Surfaces(interior,1);
                    foreach(var surface in interior.surfaces)Check(!surface.renderer.forceRenderingOff,"box surfaces remain visible");
                    e.CancelExit();e.OpenWorld(-1);yield return Until(()=>e.room.Opened&&!e.Busy);yield return new WaitForEndOfFrame();
                    Check(!e.portableClosure.Isolated,"reopen restores environment");Surfaces(interior,0);
                }
                Result="PASS: six inward surfaces, dark neutral GPU pixel, external-light independence, smooth handoff, corridor and second-room closure/cancel/reopen";
            }
            finally
            {
                rig.enabled=rigEnabled;for(int i=0;i<lights.Length;i++)if(lights[i])lights[i].enabled=enabled[i];Application.runInBackground=background;
            }
        }
    }
}
