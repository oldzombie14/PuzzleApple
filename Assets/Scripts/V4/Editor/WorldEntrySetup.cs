using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace PuzzleApple.V4.Editor
{
    public static class WorldEntrySetup
    {
        public static void Apply()
        {
            var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            if (scene.path != "Assets/Scenes/V4/P4.unity") throw new System.InvalidOperationException("Open P4 first.");
            var ground = Material("DayGrass", "Universal Render Pipeline/Lit");
            ground.shader = Shader.Find("Universal Render Pipeline/Lit");
            ground.SetColor("_BaseColor", new Color(.39f,.47f,.32f));
            ground.SetFloat("_Smoothness", 0);
            GameObject.Find("Ground").GetComponent<Renderer>().sharedMaterial = ground;
            var sky = Material("DaySky", "PuzzleApple/V4/DaySky");
            RenderSettings.skybox = sky;
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(.66f,.72f,.76f);
            RenderSettings.ambientEquatorColor = new Color(.59f,.63f,.58f);
            RenderSettings.ambientGroundColor = new Color(.34f,.38f,.29f);
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogColor = new Color(.80f,.84f,.82f);
            RenderSettings.fogDensity = .0018f;
            var light = GameObject.Find("Directional Light").GetComponent<Light>();
            light.transform.rotation = Quaternion.Euler(52,-28,0);
            light.color = new Color(1,.97f,.89f); light.intensity = 1.15f;
            light.shadows = LightShadows.Soft; RenderSettings.sun = light;
            sky.SetVector("_SunDirection", -light.transform.forward);
            var plane = GameObject.Find("Cloud Layer");
            if (!plane)
            {
                plane = GameObject.CreatePrimitive(PrimitiveType.Quad);
                plane.name = "Cloud Layer";
                Object.DestroyImmediate(plane.GetComponent<Collider>());
                plane.transform.position = new Vector3(0,450,0);
                plane.transform.rotation = Quaternion.Euler(90,0,0);
                plane.transform.localScale = new Vector3(12000,12000,1);
            }
            var cloud = plane.GetComponent<CloudLightCookie>();
            if (!cloud) cloud = plane.AddComponent<CloudLightCookie>();
            var previousCloud = light.GetComponent<CloudLightCookie>();
            if (previousCloud)
            {
                cloud.cookieSize = previousCloud.cookieSize;
                cloud.wind = previousCloud.wind;
                cloud.initialOffset = previousCloud.initialOffset;
                cloud.fogEnabled = previousCloud.fogEnabled;
                cloud.fogDensity = previousCloud.fogDensity;
                cloud.fogColor = previousCloud.fogColor;
                Object.DestroyImmediate(previousCloud);
            }
            cloud.cloudPlane = plane.GetComponent<Renderer>();
            cloud.cloudPlane.sharedMaterial = Material("CloudPlane","PuzzleApple/V4/CloudPlane");
            cloud.cloudPlane.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            cloud.cloudPlane.receiveShadows = false;
            cloud.sun = light; cloud.sky = sky; cloud.cloudMask = CloudMaskAuthoring.GetOrCreate();
            cloud.UpdateProjection();
            Camera.main.clearFlags = CameraClearFlags.Skybox;
            Camera.main.farClipPlane = 8000;
            var picture = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/V4/WorldPicture.mat");
            // Hard luminance steps created grey contours around otherwise smooth white clouds.
            picture.SetFloat("_Strength", 0);
            EditorUtility.SetDirty(picture);
            var existing = GameObject.Find("World Entry");
            if (existing) Object.DestroyImmediate(existing);
            var root = new GameObject("World Entry", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster), typeof(WorldEntry));
            var canvas = root.GetComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 100;
            var scaler = root.GetComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920,1080); scaler.matchWidthOrHeight = .5f;
            var background = Image("Black", root.transform, Vector2.zero, Vector2.zero);
            background.color = Color.black; background.rectTransform.anchorMin = Vector2.zero;
            background.rectTransform.anchorMax = Vector2.one; background.rectTransform.sizeDelta = Vector2.zero;
            var enter = Button("Enter World", root.transform, new Vector2(300,160), Vector2.zero);
            Symbol("Positive", enter.transform, new Vector2(-66,0), 104);
            Symbol("Apple", enter.transform, new Vector2(66,0), 104);
            var exit = Button("Exit", root.transform, new Vector2(100,100), new Vector2(-76,-76));
            var exitRect = (RectTransform)exit.transform; exitRect.anchorMin = exitRect.anchorMax = Vector2.one;
            Symbol("Negative", exit.transform, Vector2.zero, 54);
            var entry = root.GetComponent<WorldEntry>(); entry.enterButton = enter; entry.exitButton = exit;
            entry.blackBackground = background;
            if (!Object.FindFirstObjectByType<EventSystem>())
            {
                var events = new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
                events.GetComponent<InputSystemUIInputModule>().AssignDefaultActions();
            }
            if (!AssetDatabase.IsValidFolder("Assets/Prefabs/V4")) AssetDatabase.CreateFolder("Assets/Prefabs", "V4");
            PrefabUtility.SaveAsPrefabAssetAndConnect(root, "Assets/Prefabs/V4/WorldEntry.prefab", InteractionMode.AutomatedAction);
            EditorUtility.SetDirty(ground); EditorUtility.SetDirty(sky);
            AssetDatabase.SaveAssets(); EditorSceneManager.SaveScene(scene);
        }

        static Material Material(string name, string shader)
        {
            string path = "Assets/Materials/V4/" + name + ".mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (!material) { material = new Material(Shader.Find(shader)); AssetDatabase.CreateAsset(material,path); }
            return material;
        }
        static Image Image(string name, Transform parent, Vector2 size, Vector2 position)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image)); go.transform.SetParent(parent,false);
            var image = go.GetComponent<Image>(); image.rectTransform.sizeDelta = size; image.rectTransform.anchoredPosition = position; return image;
        }
        static Button Button(string name, Transform parent, Vector2 size, Vector2 position)
        {
            var image = Image(name,parent,size,position); image.color = Color.clear;
            var button = image.gameObject.AddComponent<SymbolButton>();
            var colors = button.colors; colors.normalColor = new Color(.65f,.65f,.65f,1);
            colors.highlightedColor = Color.white;
            colors.selectedColor = colors.highlightedColor; colors.pressedColor = new Color(.48f,.48f,.48f,1);
            colors.fadeDuration = .12f; button.colors = colors; button.targetGraphic = image;
            return button;
        }
        static void Symbol(string name, Transform parent, Vector2 position, float size)
        {
            var image = Image(name,parent,new Vector2(size,size),position);
            image.sprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/V3/"+name+".png");
            image.preserveAspect = true; image.raycastTarget = false;
        }
    }
}
