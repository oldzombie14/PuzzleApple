using System.Linq;
using UnityEditor;
using UnityEngine;

namespace PuzzleApple.V3.Editor
{
    // Author semantic material ownership explicitly instead of inheriting wall materials
    // from imported/legacy geometry. Existing assets remain the editable source of truth.
    public static class OpeningMaterials
    {
        static Material Get(string name,Material source)
        {
            string path="Assets/Materials/V3/Opening/"+name+".mat";
            var material=AssetDatabase.LoadAssetAtPath<Material>(path);
            if(!material){material=new Material(source){name=name};AssetDatabase.CreateAsset(material,path);}
            return material;
        }
        public static void Assign(OpeningRoom room)
        {
            var frames=room.doorwayMirror.GetComponentsInChildren<Renderer>()
                .Where(r=>!r.GetComponent<PlanarMirror>()).ToArray();
            var frame=Get("MirrorFrame",frames.First().sharedMaterial);
            foreach(var renderer in frames)renderer.sharedMaterials=renderer.sharedMaterials.Select(m=>frame).ToArray();
            if(!room.mainRoute)return;
            var leaves=room.mainRoute.doorLeaves.SelectMany(t=>t.GetComponentsInChildren<Renderer>()).ToArray();
            var door=Get("Door",leaves.First().sharedMaterial);
            foreach(var renderer in leaves)renderer.sharedMaterials=renderer.sharedMaterials.Select(m=>door).ToArray();
            foreach(var ambient in room.mainRoute.gateFrame.GetComponentsInChildren<LocalAmbientProbe>())
                ambient.followRoomLighting=false;
        }
    }
}
