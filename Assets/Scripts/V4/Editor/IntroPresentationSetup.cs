using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace PuzzleApple.V4.Editor
{
    // Incremental setup: preserves existing hand-edited menus, panel layout and scene objects.
    public static class IntroPresentationSetup
    {
        public static void Apply()
        {
            var scene=UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            if(Application.isPlaying||scene.path!="Assets/Scenes/V4/P4.unity")throw new System.InvalidOperationException("Open P4 in Edit mode.");
            var importer=(TextureImporter)AssetImporter.GetAtPath("Assets/Textures/V4/SentenceSparkle.png");
            importer.alphaIsTransparency=true;importer.mipmapEnabled=false;importer.maxTextureSize=256;
            importer.textureCompression=TextureImporterCompression.Uncompressed;importer.SaveAndReimport();
            var sparkle=Material("SentenceSparkle");sparkle.mainTexture=AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Textures/V4/SentenceSparkle.png");
            var ripple=Material("SentenceRipple");
            const string prefabPath="Assets/Prefabs/V4/UI/SentenceSparkle.prefab";
            var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            if(!prefab)
            {
                var go=new GameObject("Sentence sparkle",typeof(ParticleSystem));go.layer=31;
                var ps=go.GetComponent<ParticleSystem>();ps.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);
                var main=ps.main;main.loop=false;main.playOnAwake=false;main.duration=.8f;main.startLifetime=new ParticleSystem.MinMaxCurve(.5f,.8f);
                main.startSpeed=new ParticleSystem.MinMaxCurve(3,10);main.startSize=new ParticleSystem.MinMaxCurve(34,60);
                main.startColor=Color.white;main.simulationSpace=ParticleSystemSimulationSpace.Local;main.useUnscaledTime=true;
                var emission=ps.emission;emission.rateOverTime=0;emission.SetBursts(new[]{new ParticleSystem.Burst(0,6)});
                var shape=ps.shape;shape.shapeType=ParticleSystemShapeType.Box;shape.scale=new Vector3(150,40,0);
                var color=ps.colorOverLifetime;color.enabled=true;
                var gradient=new Gradient();gradient.SetKeys(new[]{new GradientColorKey(Color.white,0),new GradientColorKey(Color.white,1)},
                    new[]{new GradientAlphaKey(0,0),new GradientAlphaKey(1,.15f),new GradientAlphaKey(.8f,.5f),new GradientAlphaKey(0,1)});color.color=gradient;
                var size=ps.sizeOverLifetime;size.enabled=true;size.size=new ParticleSystem.MinMaxCurve(1,new AnimationCurve(new Keyframe(0,.25f),new Keyframe(.2f,1),new Keyframe(1,.15f)));
                var renderer=ps.GetComponent<ParticleSystemRenderer>();renderer.sharedMaterial=sparkle;
                renderer.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;renderer.receiveShadows=false;
                prefab=PrefabUtility.SaveAsPrefabAsset(go,prefabPath);Object.DestroyImmediate(go);
            }
            var effects=Object.FindFirstObjectByType<SentenceEffectManager>();
            var feedback=effects.GetComponent<SentenceFeedback>();if(!feedback)feedback=effects.gameObject.AddComponent<SentenceFeedback>();
            feedback.board=effects.board;feedback.sparklePrefab=prefab.GetComponent<ParticleSystem>();feedback.rippleMaterial=ripple;
            var presentation=effects.GetComponent<IntroPresentation>();if(!presentation)presentation=effects.gameObject.AddComponent<IntroPresentation>();
            presentation.effects=effects;presentation.player=Object.FindFirstObjectByType<FirstPersonController>();presentation.feedback=feedback;
            effects.presentation=presentation;
            var camera=presentation.player.view.GetComponent<Camera>();camera.cullingMask&=~(1<<feedback.effectLayer);
            var library=Object.FindFirstObjectByType<WordLibrary>();library.moveMemory=Sequence();
            foreach(var obj in new Object[]{effects,feedback,presentation,camera,library,sparkle,ripple})
            {EditorUtility.SetDirty(obj);if(PrefabUtility.IsPartOfPrefabInstance(obj))PrefabUtility.RecordPrefabInstancePropertyModifications(obj);}
            AssetDatabase.SaveAssets();EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);
        }
        static Material Material(string name)
        {
            string path="Assets/Materials/V4/"+name+".mat";
            var material=AssetDatabase.LoadAssetAtPath<Material>(path);
            if(!material){material=new Material(Shader.Find("PuzzleApple/V4/SentenceParticle"));AssetDatabase.CreateAsset(material,path);}
            return material;
        }
        public static SnapshotSequence Sequence()
        {
            const string path="Assets/GameData/V4/Opening/MoveMemory.asset";
            var clip=AssetDatabase.LoadAssetAtPath<SnapshotSequence>(path);
            if(!clip){clip=ScriptableObject.CreateInstance<SnapshotSequence>();AssetDatabase.CreateAsset(clip,path);}
            return clip;
        }
    }
}
