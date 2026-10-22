using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace PuzzleApple.V4.Editor
{
    [CustomEditor(typeof(CloudLightCookie))]
    public sealed class CloudLightCookieEditor : UnityEditor.Editor
    {
        bool references;
        public override void OnInspectorGUI()
        {
            var cloud = (CloudLightCookie)target;
            serializedObject.Update();
            EditorGUILayout.LabelField("云层",EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(serializedObject.FindProperty("cloudsEnabled"),new GUIContent("显示云与云影"));
            if (cloud.cloudPlane)
            {
                var plane = cloud.cloudPlane.transform;
                EditorGUI.BeginChangeCheck();
                float height = EditorGUILayout.FloatField(new GUIContent("离地高度（米）","直接修改真实云平面的世界坐标 Y；当前地面位于 Y=0。"),plane.position.y);
                if (EditorGUI.EndChangeCheck())
                {
                    Undo.RecordObject(plane,"Change cloud height");
                    var position=plane.position; position.y=height; plane.position=position;
                    if (!Application.isPlaying) EditorSceneManager.MarkSceneDirty(plane.gameObject.scene);
                }
            }
            EditorGUILayout.PropertyField(serializedObject.FindProperty("cookieSize"),new GUIContent("贴图尺寸（米）","调大可增大云图案、减少重复；不改变云层高度。"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("wind"),new GUIContent("移动速度（米/秒）"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("directionDelay"),new GUIContent("跟随方向延迟（秒）"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("directionSmoothing"),new GUIContent("转向平滑时间"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("initialOffset"),new GUIContent("图案偏移"));
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("全局雾气",EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(serializedObject.FindProperty("fogEnabled"),new GUIContent("启用雾气"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("fogDensity"),new GUIContent("雾气浓度"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("fogColor"),new GUIContent("雾气颜色"));
            references=EditorGUILayout.Foldout(references,"资源引用",true);
            if (references)
                foreach (var name in new[]{"sun","sky","cloudPlane","cloudMask","player"})
                    EditorGUILayout.PropertyField(serializedObject.FindProperty(name));
            serializedObject.ApplyModifiedProperties();
            cloud.UpdateProjection();
        }
    }
}
