using System;
using System.Collections.Generic;
using System.Linq;
using PuzzleApple.Cognition;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace PuzzleApple.Editor
{
    public static class ApplePuzzleSetup
    {
        public static void Configure()
        {
            if (Application.isPlaying || UnityEngine.SceneManagement.SceneManager.GetActiveScene().name != "TutorialLevel")
                throw new InvalidOperationException("Open TutorialLevel in Edit Mode.");
            const string folder = "Assets/Prefabs/Interaction";
            if (!AssetDatabase.IsValidFolder(folder)) AssetDatabase.CreateFolder("Assets/Prefabs", "Interaction");
            var source = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/ArtAssets-3D/AppleBitten.fbx").GetComponentInChildren<MeshFilter>();
            var mesh = UnityEngine.Object.Instantiate(source.sharedMesh);
            mesh.name = "AppleBitten split skin and flesh";
            var vertices = mesh.vertices;
            var rotation = source.transform.rotation;
            var center = mesh.bounds.center;
            float scale = .60f / mesh.bounds.size.x;
            for (int i = 0; i < vertices.Length; i++) vertices[i] = rotation * ((vertices[i] - center) * scale);
            mesh.vertices = vertices;
            var normals = mesh.normals;
            for (int i = 0; i < normals.Length; i++) normals[i] = rotation * normals[i];
            mesh.normals = normals;
            var skin = new List<int>(); var flesh = new List<int>();
            var triangles = mesh.triangles; var uv = mesh.uv;
            // This supplied FBX retains UVs on the skin; every newly cut face has zero UVs.
            for (int i = 0; i < triangles.Length; i += 3)
            {
                bool cut = uv[triangles[i]].sqrMagnitude < 1e-10f && uv[triangles[i+1]].sqrMagnitude < 1e-10f && uv[triangles[i+2]].sqrMagnitude < 1e-10f;
                var target = cut ? flesh : skin;
                target.Add(triangles[i]); target.Add(triangles[i+1]); target.Add(triangles[i+2]);
            }
            mesh.subMeshCount = 2; mesh.SetTriangles(skin, 0); mesh.SetTriangles(flesh, 1);
            mesh.RecalculateBounds(); mesh.RecalculateTangents();
            const string meshPath = "Assets/ArtAssets-3D/Generated/AppleBitten.asset";
            var existing = AssetDatabase.LoadAssetAtPath<Mesh>(meshPath);
            if (existing) { EditorUtility.CopySerialized(mesh, existing); UnityEngine.Object.DestroyImmediate(mesh); mesh = existing; }
            else AssetDatabase.CreateAsset(mesh, meshPath);
            // Landing uses this small mesh's actual support vertices, including at tilted poses.
            var meshSettings = new SerializedObject(mesh);
            meshSettings.FindProperty("m_IsReadable").boolValue = true;
            meshSettings.ApplyModifiedPropertiesWithoutUndo();
            var fleshMaterial = Material("AppleFlesh", new Color(.96f, .91f, .72f), .18f);
            var model = new GameObject("Bitten apple");
            model.AddComponent<MeshFilter>().sharedMesh = mesh;
            model.AddComponent<MeshRenderer>().sharedMaterials = new[] { AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/Apple.mat"), fleshMaterial };
            model.AddComponent<MeshCollider>().sharedMesh = mesh;
            var prefab = PrefabUtility.SaveAsPrefabAsset(model, folder + "/BittenApple.prefab");
            UnityEngine.Object.DestroyImmediate(model);
            var board = UnityEngine.Object.FindFirstObjectByType<CognitionBoard>();
            var apple = UnityEngine.Object.FindFirstObjectByType<CognitionApple>();
            var key = ConfigureKey();
            Assign(apple, "bittenPrefab", prefab); Assign(apple, "key", key);
            ConfigurePhysics(apple);
            if (!UnityEngine.Object.FindFirstObjectByType<CognitionDoor>())
            {
                var root = new GameObject("Exit door"); root.transform.position = new Vector3(-15, .04f, 0);
                var door = root.AddComponent<CognitionDoor>(); Assign(door,"board",board);
                AuthoredInteractableSetup.ConfigureDoor(door);
            }
            EditorSceneManager.MarkSceneDirty(apple.gameObject.scene); EditorSceneManager.SaveScene(apple.gameObject.scene); AssetDatabase.SaveAssets();
            Debug.Log("Apple puzzle configured: " + skin.Count/3 + " skin and " + flesh.Count/3 + " flesh triangles.");
        }
        public static CognitionKey ConfigureKey()
        {
            if (Application.isPlaying) throw new InvalidOperationException("Configure the key in Edit Mode.");
            var source = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/ArtAssets-3D/Key.fbx");
            if (!source) throw new InvalidOperationException("Missing authored Key.fbx.");
            var key = UnityEngine.Object.FindObjectsByType<CognitionKey>(FindObjectsInactive.Include, FindObjectsSortMode.None).FirstOrDefault();
            if (!key) key = new GameObject("Apple key").AddComponent<CognitionKey>();
            var root = key.gameObject;
            root.name = "Apple key";
            foreach (var collider in root.GetComponents<Collider>()) Undo.DestroyObjectImmediate(collider);
            if (root.GetComponent<MeshRenderer>()) Undo.DestroyObjectImmediate(root.GetComponent<MeshRenderer>());
            if (root.GetComponent<MeshFilter>()) Undo.DestroyObjectImmediate(root.GetComponent<MeshFilter>());
            foreach (Transform child in root.transform.Cast<Transform>().ToArray()) Undo.DestroyObjectImmediate(child.gameObject);
            root.transform.localScale = Vector3.one;
            root.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            var brass = Material("KeyBrass", new Color(.85f,.61f,.16f), .45f);
            brass.name = "KeyBrass"; brass.SetFloat("_Metallic", .6f); EditorUtility.SetDirty(brass);
            var model = (GameObject)PrefabUtility.InstantiatePrefab(source);
            model.name = "Key model"; model.transform.SetParent(root.transform, false);
            var renderers = model.GetComponentsInChildren<Renderer>();
            var bounds = renderers[0].bounds;
            foreach (var renderer in renderers) bounds.Encapsulate(renderer.bounds);
            model.transform.localScale *= .4f / Mathf.Max(bounds.size.x, bounds.size.y, bounds.size.z);
            bounds = renderers[0].bounds;
            foreach (var renderer in renderers) bounds.Encapsulate(renderer.bounds);
            model.transform.position -= bounds.center;
            foreach (var renderer in renderers) renderer.sharedMaterial = brass;
            foreach (var mesh in model.GetComponentsInChildren<MeshFilter>())
                mesh.gameObject.AddComponent<MeshCollider>().sharedMesh = mesh.sharedMesh;
            var focus = new GameObject("Interaction focus").transform;
            focus.SetParent(root.transform, false); focus.localPosition = new Vector3(.048f, -.145f, -.008f);
            var outward = new Vector3(-.55f, .70f, .45f).normalized;
            var facing = Vector3.ProjectOnPlane(new Vector3(-1, .3f, -1), outward).normalized;
            var settings = new SerializedObject(key);
            // Teeth penetrate the curved bite surface by about 8 cm; the bow remains exposed.
            settings.FindProperty("appleLocalPosition").vector3Value = new Vector3(-.13f, .07f, -.02f) + outward * .02f;
            settings.FindProperty("appleLocalEulerAngles").vector3Value = Quaternion.LookRotation(facing, -outward).eulerAngles;
            settings.FindProperty("interactionFocus").objectReferenceValue = focus;
            settings.FindProperty("board").objectReferenceValue = UnityEngine.Object.FindFirstObjectByType<CognitionBoard>();
            settings.ApplyModifiedPropertiesWithoutUndo();
            root.SetActive(false);
            var apple = UnityEngine.Object.FindFirstObjectByType<CognitionApple>();
            if (apple) Assign(apple, "key", key);
            EditorSceneManager.MarkSceneDirty(root.scene);
            return key;
        }
        public static void ConfigurePhysics(CognitionApple apple)
        {
            var go = apple.gameObject;
            // The original static mesh collider cannot participate in dynamic gravity.
            var mesh = go.GetComponent<MeshCollider>();
            if (mesh) Undo.DestroyObjectImmediate(mesh);
            var sphere = go.GetComponent<SphereCollider>();
            if (!sphere) sphere = Undo.AddComponent<SphereCollider>(go);
            var bounds = go.GetComponent<Renderer>().bounds;
            var scale = go.transform.lossyScale;
            sphere.center = go.transform.InverseTransformPoint(bounds.center);
            sphere.radius = Mathf.Max(bounds.extents.x, bounds.extents.y, bounds.extents.z) /
                Mathf.Max(Mathf.Abs(scale.x), Mathf.Abs(scale.y), Mathf.Abs(scale.z));
            var body = go.GetComponent<Rigidbody>();
            if (!body) body = Undo.AddComponent<Rigidbody>(go);
            body.mass = .18f; body.linearDamping = .3f; body.angularDamping = .6f;
            body.interpolation = RigidbodyInterpolation.Interpolate;
            body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            body.useGravity = false; body.isKinematic = true;
            var settings = new SerializedObject(apple);
            settings.FindProperty("moveSpeed").floatValue = .8f;
            settings.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(sphere); EditorUtility.SetDirty(body);
        }
        static Material Material(string name, Color color, float smoothness)
        {
            string path = "Assets/Materials/Tutorial/"+name+".mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (!m) { m = new Material(Shader.Find("Universal Render Pipeline/Lit")); AssetDatabase.CreateAsset(m,path); }
            m.color=color; m.SetFloat("_Smoothness",smoothness); EditorUtility.SetDirty(m); return m;
        }
        static void Assign(UnityEngine.Object target,string name,UnityEngine.Object value)
        { var so=new SerializedObject(target); so.FindProperty(name).objectReferenceValue=value; so.ApplyModifiedPropertiesWithoutUndo(); }
    }
}

