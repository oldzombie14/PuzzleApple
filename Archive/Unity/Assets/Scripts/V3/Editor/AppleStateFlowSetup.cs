using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Playables;
using UnityEngine.Timeline;
using Object=UnityEngine.Object;

namespace PuzzleApple.V3.Editor
{
    // Author native assets once; no runtime UI or particle construction.
    public static class AppleStateFlowSetup
    {
        const string Data="Assets/GameData/V3/Opening/";
        public static string Apply()
        {
            if(Application.isPlaying)throw new InvalidOperationException("Edit mode only.");
            var room=Object.FindFirstObjectByType<OpeningRoom>();
            if(!room||room.gameObject.scene.path!="Assets/Scenes/V3/P3.unity")throw new InvalidOperationException("Open P3.");
            var stripe=CreateStripe();
            var whole=AssetDatabase.LoadAssetAtPath<AnalysisRecipe>(Data+"AppleAnalysis.asset");
            whole.requiredAppleForm=AnalysisRecipe.AppleForm.Whole;EditorUtility.SetDirty(whole);
            var core=AssetDatabase.LoadAssetAtPath<AnalysisRecipe>(Data+"AppleCoreAnalysis.asset");
            if(!core){core=ScriptableObject.CreateInstance<AnalysisRecipe>();AssetDatabase.CreateAsset(core,Data+"AppleCoreAnalysis.asset");}
            core.input=whole.input;core.requiredAppleForm=AnalysisRecipe.AppleForm.Core;
            core.outputs=new Cognition.WordDefinition[0];core.previewSymbols=new[]{room.board.Catalog.Word("red").Symbol,stripe};EditorUtility.SetDirty(core);
            const string timelinePath="Assets/Animations/V3/InsufficientWeight.playable";
            var timeline=AssetDatabase.LoadAssetAtPath<TimelineAsset>(timelinePath);
            if(!timeline){timeline=ScriptableObject.CreateInstance<TimelineAsset>();AssetDatabase.CreateAsset(timeline,timelinePath);}
            timeline.durationMode=TimelineAsset.DurationMode.FixedLength;timeline.fixedDuration=2.8;EditorUtility.SetDirty(timeline);
            var material=PaperMaterial();var mesh=PaperMesh();
            var root=PrefabUtility.LoadPrefabContents(OpeningSetup.PrefabPath);
            try{Configure(root.GetComponent<OpeningRoom>(),whole,core,timeline,material,mesh);PrefabUtility.SaveAsPrefabAsset(root,OpeningSetup.PrefabPath);}
            finally{PrefabUtility.UnloadPrefabContents(root);}
            Configure(room,whole,core,timeline,material,mesh);
            AssetDatabase.SaveAssets();EditorSceneManager.SaveScene(room.gameObject.scene);
            return "Authored core analysis, vertical symbol, button close-up and confetti.";
        }
        static Sprite CreateStripe()
        {
            // Extend the existing geometric symbol family precisely: 640 square,
            // 84-pixel grey stroke, flat ends, transparent padding matching red.png.
            const string path="Assets/Sprites/V3/vertical.png";
            var texture=new Texture2D(640,640,TextureFormat.RGBA32,false);var pixels=new Color32[640*640];
            for(int y=11;y<629;y++)for(int x=278;x<362;x++)pixels[y*640+x]=new Color32(222,222,222,255);
            texture.SetPixels32(pixels);texture.Apply();System.IO.File.WriteAllBytes(path,texture.EncodeToPNG());Object.DestroyImmediate(texture);
            AssetDatabase.ImportAsset(path);
            var importer=(TextureImporter)AssetImporter.GetAtPath(path);importer.textureType=TextureImporterType.Sprite;
            importer.spriteImportMode=SpriteImportMode.Single;importer.alphaIsTransparency=true;importer.mipmapEnabled=false;
            importer.textureCompression=TextureImporterCompression.Uncompressed;importer.spritePixelsPerUnit=100;importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }
        static Material PaperMaterial()
        {
            const string path="Assets/Materials/V3/Opening/Confetti.mat";
            var m=AssetDatabase.LoadAssetAtPath<Material>(path);
            if(!m){m=new Material(Shader.Find("Universal Render Pipeline/Particles/Unlit"));AssetDatabase.CreateAsset(m,path);}
            m.SetColor("_BaseColor",Color.white);m.SetFloat("_Cull",0);EditorUtility.SetDirty(m);return m;
        }
        static Mesh PaperMesh()
        {
            const string path="Assets/ArtAssets-3D/V3/Generated/Confetti.asset";
            var mesh=AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if(mesh)return mesh;
            mesh=new Mesh{name="Rectangular paper"};mesh.vertices=new[]{new Vector3(-.5f,-.3f,0),new Vector3(.5f,-.3f,0),new Vector3(.5f,.3f,0),new Vector3(-.5f,.3f,0)};
            mesh.triangles=new[]{0,1,2,0,2,3};mesh.uv=new[]{Vector2.zero,Vector2.right,Vector2.one,Vector2.up};mesh.RecalculateNormals();mesh.RecalculateBounds();
            AssetDatabase.CreateAsset(mesh,path);return mesh;
        }
        static void Configure(OpeningRoom room,AnalysisRecipe whole,AnalysisRecipe core,TimelineAsset timeline,Material material,Mesh mesh)
        {
            room.analyzer.recipes=new[]{whole,core};EditorUtility.SetDirty(room.analyzer);
            var route=room.mainRoute;route.minimumTotalWeight=2;
            if(!route.insufficientWeight)
            {
                var g=Object.Instantiate(route.completion.gameObject,route.transform);g.name="Insufficient weight feedback";
                route.insufficientWeight=g.GetComponent<CutscenePlayer>();
            }
            var cut=route.insufficientWeight;cut.completed=new UnityEvent();
            foreach(var binding in cut.director.playableAsset.outputs)cut.director.ClearGenericBinding(binding.sourceObject);
            cut.director.playableAsset=timeline;cut.director.playOnAwake=false;cut.director.extrapolationMode=DirectorWrapMode.Hold;
            cut.shot.transform.position=route.pressurePlate.position+new Vector3(1.9f,.70f,-1.4f);
            cut.shot.transform.LookAt(route.pressurePlate.position+new Vector3(.35f,.02f,-.35f));
            cut.shot.fieldOfView=43;cut.shot.clearFlags=CameraClearFlags.SolidColor;cut.shot.backgroundColor=Color.black;
            cut.overlay.gameObject.SetActive(false);cut.shot.enabled=false;
            route.completion.shot.clearFlags=CameraClearFlags.SolidColor;route.completion.shot.backgroundColor=Color.black;
            if(!route.celebration)
            {
                var g=new GameObject("Completion paper confetti");g.transform.SetParent(room.presentation.view.transform,false);
                route.celebration=g.AddComponent<ParticleSystem>();
            }
            ConfigurePaper(route.celebration,material,mesh);
            EditorUtility.SetDirty(route);EditorUtility.SetDirty(cut);EditorUtility.SetDirty(cut.director);EditorUtility.SetDirty(cut.shot);
        }
        static void ConfigurePaper(ParticleSystem particles,Material material,Mesh mesh)
        {
            particles.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);
            particles.transform.localPosition=Vector3.zero;particles.transform.localRotation=Quaternion.identity;
            var main=particles.main;main.playOnAwake=false;main.loop=false;main.duration=.25f;
            var emission=particles.emission;emission.enabled=false;emission.SetBursts(new ParticleSystem.Burst[0]);
            particles.GetComponent<ParticleSystemRenderer>().enabled=false;
            foreach(bool left in new[]{true,false})
            {
                string name=left?"Upper left burst":"Upper right burst";
                var child=particles.transform.Find(name);
                if(!child){child=new GameObject(name,typeof(ParticleSystem)).transform;child.SetParent(particles.transform,false);}
                ConfigurePaperEmitter(child.GetComponent<ParticleSystem>(),left,material,mesh);
            }
        }
        static void ConfigurePaperEmitter(ParticleSystem particles,bool left,Material material,Mesh mesh)
        {
            particles.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);
            particles.transform.localPosition=new Vector3(left?-1.8f:1.8f,.95f,1.5f);particles.transform.localRotation=Quaternion.identity;
            var main=particles.main;main.playOnAwake=false;main.loop=false;main.duration=.25f;main.maxParticles=24;
            main.simulationSpace=ParticleSystemSimulationSpace.Local;main.startLifetime=new ParticleSystem.MinMaxCurve(1.7f,2.6f);
            main.startSpeed=0;main.startSize=new ParticleSystem.MinMaxCurve(.025f,.045f);main.startRotation3D=true;
            main.startRotationX=new ParticleSystem.MinMaxCurve(0,6.28f);main.startRotationY=new ParticleSystem.MinMaxCurve(0,6.28f);main.startRotationZ=new ParticleSystem.MinMaxCurve(0,6.28f);
            var gradient=new Gradient();gradient.SetKeys(new[]{new GradientColorKey(new Color(.85f,.18f,.15f),0),new GradientColorKey(new Color(1,.80f,.38f),.33f),new GradientColorKey(new Color(.96f,.94f,.87f),.67f),new GradientColorKey(new Color(.65f,.8f,.78f),1)},new[]{new GradientAlphaKey(1,0),new GradientAlphaKey(1,1)});
            var colors=new ParticleSystem.MinMaxGradient(gradient);colors.mode=ParticleSystemGradientMode.RandomColor;main.startColor=colors;
            var emission=particles.emission;emission.enabled=true;emission.rateOverTime=0;
            emission.SetBursts(new[]{new ParticleSystem.Burst(0,12),new ParticleSystem.Burst(.12f,12)});
            var shape=particles.shape;shape.enabled=true;shape.shapeType=ParticleSystemShapeType.Sphere;shape.radius=.1f;
            var velocity=particles.velocityOverLifetime;velocity.enabled=true;velocity.space=ParticleSystemSimulationSpace.Local;
            velocity.x=left?new ParticleSystem.MinMaxCurve(1.1f,2f):new ParticleSystem.MinMaxCurve(-2f,-1.1f);
            velocity.y=new ParticleSystem.MinMaxCurve(-.75f,.1f);velocity.z=new ParticleSystem.MinMaxCurve(-.08f,.12f);
            var force=particles.forceOverLifetime;force.enabled=true;force.space=ParticleSystemSimulationSpace.Local;force.y=-1f;
            var rotation=particles.rotationOverLifetime;rotation.enabled=true;rotation.separateAxes=true;
            rotation.x=new ParticleSystem.MinMaxCurve(-4,4);rotation.y=new ParticleSystem.MinMaxCurve(-3,3);rotation.z=new ParticleSystem.MinMaxCurve(-2,2);
            var renderer=particles.GetComponent<ParticleSystemRenderer>();renderer.enabled=true;renderer.renderMode=ParticleSystemRenderMode.Mesh;renderer.mesh=mesh;renderer.sharedMaterial=material;
            renderer.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;renderer.receiveShadows=false;
        }
    }
}
