using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace PuzzleApple.V3.Editor
{
    // Explicit look migration. The result uses editable native lights, materials and volumes.
    public static class MainHallLook
    {
        static T Effect<T>(VolumeProfile profile) where T:VolumeComponent
        {
            profile.components.RemoveAll(c=>!c);
            if(!profile.TryGet<T>(out var effect))effect=profile.Add<T>(false);
            // VolumeProfile.Add creates a transient ScriptableObject; persist it as a subasset.
            if(!AssetDatabase.Contains(effect))AssetDatabase.AddObjectToAsset(effect,profile);
            EditorUtility.SetDirty(effect);EditorUtility.SetDirty(profile);return effect;
        }
        static Material Material(string name,Material source,Color color,float metal,float smooth)
        {
            string path="Assets/Materials/V3/Opening/"+name+".mat";
            var m=AssetDatabase.LoadAssetAtPath<Material>(path);
            if(!m){m=new Material(source);m.name=name;AssetDatabase.CreateAsset(m,path);}
            m.SetColor("_BaseColor",color);m.SetFloat("_Metallic",metal);m.SetFloat("_Smoothness",smooth);
            EditorUtility.SetDirty(m);return m;
        }
        public static string Apply()
        {
            if(Application.isPlaying)throw new System.InvalidOperationException("Exit Play first.");
            var root=GameObject.Find("Opening Prototype");var room=root.GetComponent<OpeningRoom>();var route=room.mainRoute;
            int layer=LayerMask.NameToLayer("V3 Hall Volumes");
            if(layer<0)
            {
                var tags=new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
                var layers=tags.FindProperty("layers");
                for(int i=8;i<32;i++)if(string.IsNullOrEmpty(layers.GetArrayElementAtIndex(i).stringValue)){layer=i;layers.GetArrayElementAtIndex(i).stringValue="V3 Hall Volumes";break;}
                if(layer<0)throw new System.InvalidOperationException("No free layer for hall volumes.");
                tags.ApplyModifiedPropertiesWithoutUndo();
            }
            var volume=route.transform.Find("Hall exposure volume").GetComponent<Volume>();volume.gameObject.layer=layer;volume.enabled=false;
            // The hall keeps its low ambient baseline. CorridorAmbient supplies
            // the per-pixel bridge from the bright opening across the corridor.
            var lightingRegion=volume.GetComponent<BoxCollider>();lightingRegion.size=new Vector3(36,16,20);
            var profile=volume.sharedProfile;
            var neutral=AssetDatabase.LoadAssetAtPath<VolumeProfile>("Assets/Settings/V3/NeutralViewProfile.asset");
            if(!neutral){neutral=ScriptableObject.CreateInstance<VolumeProfile>();AssetDatabase.CreateAsset(neutral,"Assets/Settings/V3/NeutralViewProfile.asset");}
            Effect<Tonemapping>(neutral).mode.Override(TonemappingMode.None);
            Effect<Bloom>(neutral).intensity.Override(0);Effect<Vignette>(neutral).intensity.Override(0);
            var baseline=route.transform.Find("Neutral camera baseline");
            var baselineVolume=baseline?baseline.GetComponent<Volume>():new GameObject("Neutral camera baseline",typeof(Volume)).GetComponent<Volume>();
            baselineVolume.transform.SetParent(route.transform,false);baselineVolume.gameObject.layer=layer;
            baselineVolume.isGlobal=true;baselineVolume.priority=-10;baselineVolume.sharedProfile=neutral;
            var grade=Effect<ColorAdjustments>(profile);grade.postExposure.Override(0);grade.saturation.Override(0);grade.contrast.Override(0);
            Effect<Tonemapping>(profile).mode.Override(TonemappingMode.None);
            Effect<Bloom>(profile).intensity.Override(0);
            var vignette=Effect<Vignette>(profile);vignette.intensity.Override(0);vignette.smoothness.Override(.65f);
            // Outside the local hall volume the pipeline defaults are neutral, preserving the opening.
            foreach(var camera in new[]{room.presentation.view,room.mirrorView.reflectedCamera})
            {
                var data=camera.GetComponent<UniversalAdditionalCameraData>();data.renderPostProcessing=true;
                data.volumeLayerMask=1<<layer;data.volumeTrigger=camera.transform;EditorUtility.SetDirty(data);
                PrefabUtility.RecordPrefabInstancePropertyModifications(data);
            }
            foreach(var camera in room.GetComponentsInChildren<OpeningSnapshotCamera>(true))
            {var data=camera.GetComponent<UniversalAdditionalCameraData>();if(data)data.volumeLayerMask|=1<<layer;}
            var brass=Material("HallBrass",AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/V3/BalanceBrass.mat"),new Color(.67f,.53f,.31f),.88f,.53f);
            var wall=Material("HallPlaster",AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/General/GalleryWall.mat"),new Color(.86f,.86f,.86f),0,.2f);
            var floor=Material("HallStone",AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/General/GalleryFloor.mat"),wall.GetColor("_BaseColor"),0,.64f);
            floor.SetFloat("_EnvironmentReflections",0);floor.EnableKeyword("_ENVIRONMENTREFLECTIONS_OFF");
            var corridorWall=AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/V3/Opening/CorridorPlaster.mat");
            foreach(var renderer in route.balance.GetComponentsInChildren<Renderer>(true))renderer.sharedMaterial=brass;
            foreach(var renderer in route.transform.Find("Gallery and corridor").GetComponentsInChildren<Renderer>())
            {
                if(!renderer.name.StartsWith("主房")&&!renderer.name.StartsWith("走廊"))continue;
                renderer.sharedMaterials=renderer.sharedMaterials.Select(m=>renderer.name.StartsWith("走廊")&&corridorWall?corridorWall:m.name=="GalleryFloor"||m==floor?floor:wall).ToArray();
            }
            foreach(var renderer in route.transform.Find("V3 balance passage").GetComponentsInChildren<Renderer>())renderer.sharedMaterial=wall;
            var key=route.transform.Find("Balance spotlight").GetComponent<Light>();key.intensity=230;key.range=16;key.spotAngle=38;key.innerSpotAngle=10;
            key.transform.localPosition=new Vector3(-8,11.7f,0);key.transform.LookAt(route.transform.TransformPoint(new Vector3(-8,0,0)));key.shadowStrength=.7f;
            key.color=Color.white;key.shadowBias=.025f;key.shadowNormalBias=.12f;
            ConfigureShaft(route,key);
            var fill=route.transform.Find("Gallery dim fill").GetComponent<Light>();fill.intensity=.35f;fill.range=16;
            var corridor=route.transform.Find("Corridor light").GetComponent<Light>();
            // The corridor's per-pixel indirect light joins the two room baselines.
            // This point light is only a small local fill, not the transition itself.
            corridor.transform.position=new Vector3(0,1.9f,5.5f);corridor.intensity=.05f;corridor.range=15;corridor.color=Color.white;
            var bounce=route.transform.Find("Doorway bounced light");
            var bounceLight=bounce?bounce.GetComponent<Light>():new GameObject("Doorway bounced light",typeof(Light)).GetComponent<Light>();
            bounceLight.transform.SetParent(route.transform,false);bounceLight.transform.localPosition=new Vector3(1.8f,2.7f,0);
            bounceLight.type=LightType.Point;bounceLight.color=Color.white;bounceLight.intensity=.65f;bounceLight.range=9;bounceLight.shadows=LightShadows.None;
            for(int i=0;i<2;i++)
            {
                var wash=Light(route.transform,"Rear wall wash "+(i+1),new Vector3(-15,8,i==0?-6:6),new Vector3(-17.9f,3.5f,i==0?-5:5));
                wash.intensity=0;wash.range=12;wash.spotAngle=105;wash.innerSpotAngle=15;wash.color=Color.white;wash.shadows=LightShadows.None;
            }
            var rim=Light(route.transform,"Balance edge light",new Vector3(-10,4,2),new Vector3(-8,1.3f,0));
            rim.intensity=9;rim.range=7;rim.spotAngle=58;rim.innerSpotAngle=15;rim.color=Color.white;rim.shadows=LightShadows.Soft;
            var softFill=Light(route.transform,"Exhibit soft fill",new Vector3(-4,4,-2),new Vector3(-8,.8f,0));
            softFill.intensity=65;softFill.range=10;softFill.spotAngle=45;softFill.innerSpotAngle=8;softFill.color=Color.white;softFill.shadows=LightShadows.None;
            var existing=route.transform.Find("Gallery reflection");
            var probe=existing?existing.GetComponent<ReflectionProbe>():new GameObject("Gallery reflection",typeof(ReflectionProbe)).GetComponent<ReflectionProbe>();
            probe.transform.SetParent(route.transform,false);probe.transform.localPosition=new Vector3(-8,1.4f,0);probe.transform.rotation=Quaternion.identity;
            probe.mode=ReflectionProbeMode.Custom;probe.boxProjection=true;probe.size=new Vector3(18,12,20);probe.center=new Vector3(0,4.6f,0);
            probe.intensity=1.4f;probe.blendDistance=1;probe.importance=2;probe.resolution=256;probe.clearFlags=ReflectionProbeClearFlags.SolidColor;
            probe.backgroundColor=new Color(.08f,.08f,.08f);probe.nearClipPlane=.1f;probe.farClipPlane=40;
            probe.customBakedTexture=AssetDatabase.LoadAssetAtPath<Cubemap>("Assets/ArtAssets-3D/V3/Generated/MainHallReflection.exr");
            foreach(var renderer in root.GetComponentsInChildren<Renderer>(true))
            {
                if(!renderer.sharedMaterials.Any(m=>m&&m.shader.name=="Universal Render Pipeline/Lit"))continue;
                var ambient=renderer.GetComponent<LocalAmbientProbe>();if(!ambient)ambient=renderer.gameObject.AddComponent<LocalAmbientProbe>();
                ambient.roomVolume=lightingRegion;ambient.followRoomLighting=!renderer.transform.IsChildOf(route.gateFrame);
                ambient.Apply();EditorUtility.SetDirty(ambient);
            }
            AssetDatabase.SaveAssets();PrefabUtility.ApplyPrefabInstance(root,InteractionMode.AutomatedAction);EditorSceneManager.SaveScene(root.scene);
            return "Hall materials, lighting and persistent volume configured.";
        }
        static void ConfigureShaft(MainRoute route,Light key)
        {
            const float length=11.64f;
            float radius=Mathf.Tan(key.spotAngle*.5f*Mathf.Deg2Rad)*length;
            var shader=Shader.Find("PuzzleApple/V3/Exhibit Light Shaft");
            if(!shader)throw new System.InvalidOperationException("Import ExhibitLightShaft.shader first.");
            const string path="Assets/Materials/V3/Opening/ExhibitLightShaft.mat";
            var material=AssetDatabase.LoadAssetAtPath<Material>(path);
            if(!material){material=new Material(shader);AssetDatabase.CreateAsset(material,path);}
            material.SetColor("_BeamColor",Color.white*.32f);material.SetFloat("_Density",.11f);
            material.SetFloat("_Radius",radius);material.SetFloat("_SourceRadius",.16f);EditorUtility.SetDirty(material);
            var old=route.transform.Find("Exhibit light shaft");var shaft=old?old.gameObject:GameObject.CreatePrimitive(PrimitiveType.Cube);
            shaft.name="Exhibit light shaft";shaft.transform.SetParent(route.transform,false);
            shaft.transform.SetPositionAndRotation(key.transform.position+key.transform.forward*length*.5f,key.transform.rotation);
            shaft.transform.localScale=new Vector3(radius*2,radius*2,length);
            if(shaft.GetComponent<Collider>())Object.DestroyImmediate(shaft.GetComponent<Collider>());
            var renderer=shaft.GetComponent<MeshRenderer>();renderer.sharedMaterial=material;
            renderer.shadowCastingMode=ShadowCastingMode.Off;renderer.receiveShadows=false;
            renderer.lightProbeUsage=LightProbeUsage.Off;renderer.reflectionProbeUsage=ReflectionProbeUsage.Off;
            var source=route.transform.Find("Ceiling light aperture");var disk=source?source.gameObject:GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            disk.name="Ceiling light aperture";disk.transform.SetParent(route.transform,false);disk.transform.localPosition=new Vector3(-8,11.78f,0);
            disk.transform.localScale=new Vector3(.5f,.012f,.5f);if(disk.GetComponent<Collider>())Object.DestroyImmediate(disk.GetComponent<Collider>());
            const string sourcePath="Assets/Materials/V3/Opening/ExhibitAperture.mat";
            var white=AssetDatabase.LoadAssetAtPath<Material>(sourcePath);
            if(!white){white=new Material(Shader.Find("Universal Render Pipeline/Unlit"));AssetDatabase.CreateAsset(white,sourcePath);}
            white.SetColor("_BaseColor",Color.white);EditorUtility.SetDirty(white);
            disk.GetComponent<Renderer>().sharedMaterial=white;disk.GetComponent<Renderer>().shadowCastingMode=ShadowCastingMode.Off;
            foreach(var camera in route.room.GetComponentsInChildren<Camera>(true))
            {var data=camera.GetComponent<UniversalAdditionalCameraData>();if(data)data.requiresDepthOption=CameraOverrideOption.On;}
        }
        static Light Light(Transform parent,string name,Vector3 position,Vector3 target)
        {
            var old=parent.Find(name);var light=old?old.GetComponent<Light>():new GameObject(name,typeof(Light)).GetComponent<Light>();
            light.transform.SetParent(parent,false);light.transform.localPosition=position;light.transform.LookAt(parent.TransformPoint(target));light.type=LightType.Spot;return light;
        }
    }
}
