using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using UnityEngine;

namespace PuzzleApple.V5.Editor
{
    public static class PassageChecks
    {
        public static string Result {get;private set;}
        static void Check(bool value,string text){if(!value){Result="FAIL: "+text;throw new Exception(Result);}}
        static IEnumerator Until(Func<bool> condition){float limit=Time.realtimeSinceStartup+25;while(!condition()&&Time.realtimeSinceStartup<limit)yield return null;Check(condition(),"timeout");}
        static readonly MethodInfo Step=typeof(FirstPersonController).GetMethod("AdvanceMovement",BindingFlags.Instance|BindingFlags.NonPublic);
        static void Walk(FirstPersonController p,int count,bool jump=false)
        {for(int i=0;i<count;i++)Step.Invoke(p,new object[]{Vector2.up,true,1f/120,jump&&i==0});}
        public static void Run(){Result="Running";var e=UnityEngine.Object.FindFirstObjectByType<WorldEntry>();e.StartCoroutine(Run(e));}
        static IEnumerator Run(WorldEntry e)
        {
            bool background=Application.runInBackground;Application.runInBackground=true;
            var p=e.player;var source=UnityEngine.Object.FindFirstObjectByType<WallPassage>();var journey=e.journey;
            var renderers=source.walls.Select(w=>w.GetComponent<Renderer>()).ToArray();var colliders=source.walls.Select(w=>w.GetComponent<Collider>()).ToArray();
            var renderStates=renderers.Select(r=>r.enabled).ToArray();var collisionStates=colliders.Select(c=>c.enabled).ToArray();
            GameObject testWall=null,testPassage=null,testJourney=null;
            bool isolated=false; var lighting=UnityEngine.Object.FindFirstObjectByType<RoomLighting>();
            try
            {
                e.EnterWorld();yield return Until(()=>e.Entered);e.OpenWorld(-1);yield return Until(()=>e.room.Opened&&!e.Busy);
                e.board.Panel.SetOpen(false);p.Teleport(new Vector3(0,.08f,0),Quaternion.identity);yield return null;
                Check(e.chamberLight.reticle.activeSelf&&!e.chamberLight.hoverEye.gameObject.activeSelf,"X on ordinary surface");
                p.view.LookAt(e.chamberLight.transform);p.AdoptCurrentView();yield return null;
                Check(e.chamberLight.hoverEye.gameObject.activeSelf&&!e.chamberLight.reticle.activeSelf,"eye on collectible light");
                e.chamberLight.SetOn(false);yield return Until(()=>e.chamberLight.Brightness==0);yield return null;
                Check(e.chamberLight.reticle.activeSelf&&!e.chamberLight.hoverEye.gameObject.activeSelf&&!e.chamberLight.TryCollect(),"dark lamp uses X and cannot collect");
                e.board.Panel.SetOpen(true);yield return null;Check(!e.chamberLight.reticle.activeSelf&&!e.chamberLight.hoverEye.gameObject.activeSelf,"panel hides world cursor");
                e.board.Panel.SetOpen(false);e.chamberLight.SetOn(true);yield return Until(()=>e.chamberLight.Brightness==1);yield return null;
                p.view.LookAt(e.chamberLight.transform);p.AdoptCurrentView();yield return null;
                Check(e.chamberLight.TryCollect(),"collection starts");Check(!e.chamberLight.reticle.activeSelf&&!e.chamberLight.hoverEye.gameObject.activeSelf,"collection hides cursor");
                yield return Until(()=>!e.chamberLight.Collecting);yield return null;Check(e.chamberLight.hoverEye.color.a<.4f,"known-word eye subdued");
                source.enabled=false;source.Entrance.parent.gameObject.SetActive(false);journey.enabled=false;journey.Root.gameObject.SetActive(false);p.journey=null;p.enabled=false;isolated=true;
                foreach(var r in renderers)r.enabled=false;foreach(var c in colliders)c.enabled=false;
                for(int wall=0;wall<4;wall++)
                {
                    var original=source.walls[wall];testWall=GameObject.CreatePrimitive(PrimitiveType.Cube);testWall.name="Temporary passage test wall";
                    testWall.transform.SetPositionAndRotation(original.position,original.rotation);testWall.transform.localScale=original.lossyScale;testWall.GetComponent<Renderer>().sharedMaterial=renderers[wall].sharedMaterial;
                    testJourney=new GameObject("Temporary passage test journey");var j=testJourney.AddComponent<PassageJourney>();j.enabled=false;j.entry=e;j.whitePath=journey.whitePath;j.blackSurround=journey.blackSurround;j.roomMaterial=journey.roomMaterial;j.lampMaterial=journey.lampMaterial;
                    testPassage=new GameObject("Temporary passage test");var h=testPassage.AddComponent<WallPassage>();h.lamp=e.chamberLight;h.journey=j;h.walls=new[]{testWall.transform};h.width=source.width;h.height=source.height;h.sillHeight=source.sillHeight;h.edgeMaterial=source.edgeMaterial;
                    yield return null;yield return null;
                    e.journey=j;lighting.journey=j;j.enabled=true;
                    Check(Mathf.Abs(h.Entrance.localPosition.x)<.001f,"centred opening on wall "+wall);
                    var path=j.Root.Find("White path");var floor=j.Root.Find("Room floor");float thickness=Mathf.Min(original.lossyScale.x,original.lossyScale.z);
                    var lintel=j.Root.Find("Door lintel");
                    Check(Mathf.Abs(j.Door.localScale.x-h.width)<.0001f&&Mathf.Abs(j.Door.localScale.y-h.height)<.0001f,"door matches entrance width and height");
                    Check(Mathf.Abs(path.localScale.x-j.Door.localScale.x)<.0001f&&Mathf.Abs(j.Door.localPosition.y-j.Door.localScale.y*.5f)<.0001f,"door matches path width and floor height");
                    Check(Mathf.Abs(lintel.localPosition.y-lintel.localScale.y*.5f-h.height)<.0001f,"destination opening matches door height");
                    var sill=h.Entrance.parent.Find("Wall below opening");
                    var sillEnd=sill.TransformPoint(new Vector3(0,.5f,.5f));
                    var pathStart=path.TransformPoint(new Vector3(0,.5f,-.5f));
                    Check(Vector3.Distance(sillEnd,pathStart)<.0001f,"sill and path meet at a single level edge");
                    Check(Mathf.Abs(path.localPosition.z+path.localScale.z*.5f-(floor.localPosition.z-floor.localScale.z*.5f))<.0001f,"floors meet without overlap");
                    foreach(bool on in new[]{true,false})
                    {
                        e.chamberLight.SetOn(on);yield return Until(()=>Mathf.Approximately(e.chamberLight.Brightness,on?1:0));yield return null;
                        Check(testWall.GetComponent<Renderer>().enabled==on,"light conceals passage and darkness reveals it");
                        var origin=h.Entrance.TransformPoint(new Vector3(0,-h.sillHeight+.08f,-1.3f));
                        p.Teleport(origin,h.Entrance.rotation);Physics.SyncTransforms();Walk(p,180);
                        Check(h.Entrance.InverseTransformPoint(p.transform.position).z<0,"raised sill requires jump");
                        p.Teleport(origin,h.Entrance.rotation);Physics.SyncTransforms();for(int n=0;n<20;n++)Step.Invoke(p,new object[]{Vector2.zero,true,1f/120,false});Walk(p,110,true);
                        Check(h.Entrance.InverseTransformPoint(p.transform.position).z>.35f,"normal jump enters wall "+wall+" lit="+on);
                        p.transform.rotation=h.Entrance.rotation*Quaternion.Euler(0,180,0);Walk(p,200);yield return new WaitForEndOfFrame();
                        Check(h.Entrance.InverseTransformPoint(p.transform.position).z<-.3f&&!j.BeyondChamber,"return crosses visual cover without collision");
                    }
                    foreach(float z in new[]{-.19f,.01f,.3f,3f,j.pathLength-.8f})foreach(float yaw in new[]{-65f,0f,65f})
                    {
                        p.Teleport(j.Root.TransformPoint(new Vector3(0,.08f,z)),j.Root.rotation);
                        p.view.rotation=j.Root.rotation*Quaternion.Euler(0,180+yaw,0);p.AdoptCurrentView();
                        yield return Until(()=>j.ViewBeyondChamber);
                        yield return new WaitForEndOfFrame();yield return new WaitForEndOfFrame();
                        Check(j.BeyondChamber&&!testWall.GetComponent<Renderer>().enabled,"corridor sees the opening through the chamber wall");
                        Check(ExteriorIsBlack(p.view.GetComponent<Camera>(),h),"exterior stays black outside the opening on wall "+wall+" z="+z+" yaw="+yaw);
                    }
                    p.Teleport(j.Root.TransformPoint(new Vector3(0,-h.sillHeight+.08f,-1)),j.Root.rotation);yield return new WaitForEndOfFrame();
                    Check(!testWall.GetComponent<Renderer>().forceRenderingOff,"return restores original wall rendering");
                    UnityEngine.Object.Destroy(testPassage);UnityEngine.Object.Destroy(testJourney);UnityEngine.Object.Destroy(testWall);yield return null;
                }
                Result="PASS: four centred enlarged entries; lit cover and open dark doorway; jump and physical return; open return aperture; black exterior GPU samples near threshold, middle and door at three angles; restored chamber; collection cursor states";
            }
            finally
            {
                if(testPassage)UnityEngine.Object.Destroy(testPassage);if(testJourney)UnityEngine.Object.Destroy(testJourney);if(testWall)UnityEngine.Object.Destroy(testWall);
                if(isolated){for(int i=0;i<renderers.Length;i++){renderers[i].enabled=renderStates[i];colliders[i].enabled=collisionStates[i];}source.Entrance.parent.gameObject.SetActive(true);source.enabled=true;journey.Root.gameObject.SetActive(true);journey.enabled=true;}
                e.journey=journey;lighting.journey=journey;p.journey=journey;p.Teleport(new Vector3(0,.08f,0),Quaternion.identity);p.enabled=true;journey.RefreshCollisionExclusions();Application.runInBackground=background;
            }
        }
        static bool ExteriorIsBlack(Camera camera,WallPassage passage)
        {
            var frame=camera.targetTexture;var previous=RenderTexture.active;
            var pixels=new Texture2D(frame.width,frame.height,TextureFormat.RGB24,false);
            try
            {
                RenderTexture.active=frame;pixels.ReadPixels(new Rect(0,0,frame.width,frame.height),0,0);pixels.Apply();
                Vector3 origin=passage.Entrance.InverseTransformPoint(camera.transform.position);
                float thickness=Mathf.Min(passage.walls[0].lossyScale.x,passage.walls[0].lossyScale.z);
                for(int y=frame.height*3/5;y<frame.height-2;y+=13)for(int x=2;x<frame.width-2;x+=13)
                {
                    // A wide-FOV oblique view near the door also includes forward rays.
                    // Only return-facing rays outside the aperture should see black.

                    var ray=camera.ViewportPointToRay(new Vector3((x+.5f)/frame.width,(y+.5f)/frame.height,0));

                    Vector3 direction=passage.Entrance.InverseTransformDirection(ray.direction);
                    if(direction.z>=-.001f)continue;

                    var atWall=origin+direction*((-thickness*.5f-origin.z)/direction.z);
                    if(Mathf.Abs(atWall.x)<passage.width*.5f+.2f&&atWall.y>-.2f&&atWall.y<passage.height+.2f)continue;
                    // The return outline sits on the corridor side of the thick wall.
                    var atOutline=origin+direction*((thickness*.5f+.07f-origin.z)/direction.z);
                    if(Mathf.Abs(atOutline.x)<passage.width*.5f+.2f&&atOutline.y>-.2f&&atOutline.y<passage.height+.2f)continue;
                    var c=pixels.GetPixel(x,y);
                    if(Mathf.Max(c.r,Mathf.Max(c.g,c.b))>.02f)
                    {
                        Debug.LogWarning("Exterior sample "+c+" at inner wall "+atWall+" outer outline "+atOutline);
                        return false;
                    }
                }
                return true;
            }
            finally{RenderTexture.active=previous;UnityEngine.Object.Destroy(pixels);}
        }
    }
}
