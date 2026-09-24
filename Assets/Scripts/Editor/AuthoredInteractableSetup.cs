using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace PuzzleApple.Editor
{
    // Reuses imported meshes directly; no generated substitute geometry.
    public static class AuthoredInteractableSetup
    {
        public static BoxCollider ConfigureMirror(Transform anchor)
        {
            var source = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/ArtAssets-3D/Mirror.fbx");
            if (!source) throw new InvalidOperationException("Mirror.fbx is required.");
            var oldChildren = anchor.Cast<Transform>().Select(t => t.gameObject).ToArray();
            var model = InstantiateModel(source, anchor);
            model.name = "Mirror model";
            Fit(model, 1.87f, anchor.position, false);
            var glass = model.GetComponentsInChildren<MeshRenderer>().Single(r => r.name == "镜面");
            var white = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/GalleryWall.mat");
            var glassMaterial = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/Tutorial/MirrorSurface.mat");
            if (!glassMaterial) throw new InvalidOperationException("MirrorSurface.mat is required.");
            foreach (var renderer in model.GetComponentsInChildren<MeshRenderer>()) renderer.sharedMaterial = renderer == glass ? glassMaterial : white;
            var mirror = glass.gameObject.AddComponent<PlanarMirror>();
            var so = new SerializedObject(mirror);
            so.FindProperty("sourceCamera").objectReferenceValue = Camera.main;
            var point = glass.bounds.center + Vector3.forward * glass.bounds.extents.z;
            so.FindProperty("localSurfacePoint").vector3Value = glass.transform.InverseTransformPoint(point);
            so.FindProperty("localSurfaceNormal").vector3Value = glass.transform.InverseTransformDirection(Vector3.forward);
            so.ApplyModifiedPropertiesWithoutUndo();
            var target = anchor.GetComponent<BoxCollider>();
            if (!target) target = anchor.gameObject.AddComponent<BoxCollider>();
            target.isTrigger = true;
            target.center = anchor.InverseTransformPoint(point + Vector3.forward * .012f);
            target.size = new Vector3(glass.bounds.size.x, glass.bounds.size.y, .02f);
            foreach (var child in oldChildren) UnityEngine.Object.DestroyImmediate(child);
            return target;
        }
        public static void ConfigureDoor(CognitionDoor door)
        {
            var source = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/ArtAssets-3D/Door.fbx");
            if (!source) throw new InvalidOperationException("Door.fbx is required.");
            var oldChildren = door.transform.Cast<Transform>().Select(t => t.gameObject).ToArray();
            door.transform.rotation = Quaternion.identity;
            door.name = "Exit door";
            var model = InstantiateModel(source, door.transform); model.name = "Door model";
            Fit(model, 3.2f, door.transform.position, true);
            var renderers = model.GetComponentsInChildren<MeshRenderer>();
            var leaf = renderers.Single(r => r.name == "立方体.001");
            var blue = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/Tutorial/DoorSoftBlue.mat");
            foreach (var renderer in renderers)
            {
                renderer.sharedMaterial = blue;
                renderer.gameObject.AddComponent<MeshCollider>().sharedMesh = renderer.GetComponent<MeshFilter>().sharedMesh;
            }
            var hinge = new GameObject("Hinge").transform;
            hinge.SetParent(door.transform, false);
            hinge.position = new Vector3(leaf.bounds.center.x, leaf.bounds.min.y, leaf.bounds.max.z);
            leaf.transform.SetParent(hinge, true);
            var so = new SerializedObject(door); so.FindProperty("hinge").objectReferenceValue = hinge; so.ApplyModifiedPropertiesWithoutUndo();
            foreach (var child in oldChildren) UnityEngine.Object.DestroyImmediate(child);
        }
        public static void ReplaceSceneModels()
        {
            if (Application.isPlaying) throw new InvalidOperationException("Use Edit Mode.");
            var tutorial = UnityEngine.Object.FindFirstObjectByType<OpeningTutorial>();
            var mirrorAnchor = tutorial.transform.Find("Mirror");
            var target = ConfigureMirror(mirrorAnchor);
            var data = new SerializedObject(tutorial); data.FindProperty("mirrorTarget").objectReferenceValue = target;
            data.ApplyModifiedPropertiesWithoutUndo();
            var oldProbe = tutorial.transform.Find("Corridor reflection");
            if (oldProbe) UnityEngine.Object.DestroyImmediate(oldProbe.gameObject);
            ConfigureDoor(UnityEngine.Object.FindFirstObjectByType<CognitionDoor>());
            var player = UnityEngine.Object.FindFirstObjectByType<FirstPersonController>();
            var settings = new SerializedObject(player);
            settings.FindProperty("moveSpeed").floatValue = 3.5f;
            settings.FindProperty("initialSpeedFraction").floatValue = .26f;
            settings.FindProperty("steadyFirstStepDistance").floatValue = .25f;
            settings.FindProperty("learningSwayFadeInDistance").floatValue = .6f;
            settings.FindProperty("learningHesitation").floatValue = .38f;
            settings.FindProperty("learningBobAmplitude").floatValue = .025f;
            settings.ApplyModifiedPropertiesWithoutUndo();
            EditorSceneManager.MarkSceneDirty(tutorial.gameObject.scene); EditorSceneManager.SaveScene(tutorial.gameObject.scene);
            AssetDatabase.SaveAssets();
        }
        static GameObject InstantiateModel(GameObject source, Transform parent)
        {
            var go = (GameObject)PrefabUtility.InstantiatePrefab(source);
            PrefabUtility.UnpackPrefabInstance(go, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            go.transform.SetParent(parent, true); return go;
        }
        static void Fit(GameObject go, float height, Vector3 destination, bool bottom)
        {
            var renderers=go.GetComponentsInChildren<Renderer>();var bounds=renderers[0].bounds;
            foreach(var r in renderers)bounds.Encapsulate(r.bounds);
            go.transform.localScale *= height / bounds.size.y;
            bounds=renderers[0].bounds;foreach(var r in renderers)bounds.Encapsulate(r.bounds);
            var reference=bounds.center;if(bottom)reference.y=bounds.min.y;
            go.transform.position += destination-reference;
        }
    }
}
