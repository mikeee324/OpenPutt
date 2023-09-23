#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using System.Linq;
using UnityEditor.SceneManagement;
using Varneon.VUdon.ArrayExtensions;

namespace mikeee324.OpenPutt
{
    public class OpenPuttMainMenu : EditorWindow
    {
        GameObject openPuttPrefab = null;
        GameObject openPuttCoursePrefab = null;
        GameObject openPuttScoreboardPositionerPrefab = null;

        Vector2 scrollPosition = Vector2.zero;

        [MenuItem("OpenPutt/Open OpenPutt Setup Helper...")]
        public static void ShowWindow()
        {
            var window = GetWindow(typeof(OpenPuttMainMenu));
            window.titleContent.text = "OpenPutt Setup";
        }

        private void CenteredLabel(string text, int fontSize = -1, Color textColor = default, bool wordWrap = true)
        {
            GUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            GUIStyle style = new GUIStyle(EditorStyles.boldLabel);
            if (fontSize != -1) style.fontSize = fontSize;
            if (textColor != default) style.normal.textColor = textColor;
            style.wordWrap = wordWrap;
            GUILayout.Label(text, style, GUILayout.ExpandWidth(true));
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();
        }

        private void Label(string text, int fontSize = -1, Color textColor = default, bool wordWrap = true)
        {
            GUIStyle style = new GUIStyle(EditorStyles.boldLabel);
            if (fontSize != -1) style.fontSize = fontSize;
            if (textColor != default) style.normal.textColor = textColor;
            style.wordWrap = wordWrap;
            GUILayout.Label(text, style, GUILayout.ExpandWidth(true));
        }

        private void OnGUI()
        {
            if (openPuttPrefab == null)
                openPuttPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Packages/com.mikeee324.openputt/Runtime/Prefabs/OpenPutt.prefab");

            if (openPuttCoursePrefab == null)
                openPuttCoursePrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Packages/com.mikeee324.openputt/Runtime/Prefabs/Components/OpenPuttCourse.prefab");

            if (openPuttScoreboardPositionerPrefab == null)
                openPuttScoreboardPositionerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Packages/com.mikeee324.openputt/Runtime/Prefabs/UI/ScoreboardPositioner.prefab");

            var firstOpenPutt = FindObjectsOfType<OpenPutt>().FirstOrDefault();

            if (firstOpenPutt == null)
            {
                CenteredLabel("OpenPutt", 20);
                CenteredLabel("by mikeee324", 12);

                GUILayout.Space(10);

                CenteredLabel("OpenPutt is not in the scene, click the button below to add it in", -1, Color.red);

                if (GUILayout.Button("Add OpenPutt to the scene", GUILayout.Height(40)))
                {
                    GameObject openPuttObj = PrefabUtility.InstantiatePrefab(openPuttPrefab) as GameObject;
                    Undo.RegisterCreatedObjectUndo(openPuttObj, "Create OpenPutt Prefab");
                    Repaint();
                }
                return;
            }

            CenteredLabel("OpenPutt " + firstOpenPutt.CurrentVersion, 20);
            CenteredLabel("by mikeee324", 12);

            GUILayout.Space(10);

            OpenPutt openPutt = EditorGUILayout.ObjectField("OpenPutt", firstOpenPutt, typeof(OpenPutt), true) as OpenPutt;

            scrollPosition = GUILayout.BeginScrollView(scrollPosition, false, false);

            SerializedObject serializedOpenPutt = new SerializedObject(openPutt);
            SerializedProperty coursesProp = serializedOpenPutt.FindProperty("courses");

            GUILayout.Space(10);

            EditorGUI.BeginChangeCheck();

            CenteredLabel("Courses", 14);
            Label("This is a list of all of the courses in your world. These need to be listed in the order you want them to be played.", -1, default, true);
            Label("Each course has a start pad and a collider for the hole, move these into position manually.", -1, default, true);

            if (GUILayout.Button("+ Add New Course"))
            {
                Transform holes = openPutt.transform.Find("Holes");

                GameObject newCourse = PrefabUtility.InstantiatePrefab(openPuttCoursePrefab, holes) as GameObject;
                Undo.RegisterCreatedObjectUndo(newCourse, "Create OpenPutt Course");
                Transform cameraTransform = SceneView.lastActiveSceneView.camera.transform;
                newCourse.transform.position = cameraTransform.position + cameraTransform.forward * 3f;

                Selection.activeGameObject = newCourse;

                coursesProp.InsertArrayElementAtIndex(coursesProp.arraySize);
                coursesProp.GetArrayElementAtIndex(coursesProp.arraySize - 1).objectReferenceValue = newCourse;
            }

            // Remove any null course references
            Object[] allCourses = new Object[0];
            for (int i = 0; i < coursesProp.arraySize; i++)
                if (coursesProp.GetArrayElementAtIndex(i).objectReferenceValue && !allCourses.Contains(coursesProp.GetArrayElementAtIndex(i).objectReferenceValue))
                    allCourses = allCourses.Add(coursesProp.GetArrayElementAtIndex(i).objectReferenceValue);

            if (allCourses.Length > 0 && coursesProp.arraySize != allCourses.Length)
            {
                coursesProp.ClearArray();
                foreach (Object course in allCourses)
                {
                    coursesProp.InsertArrayElementAtIndex(coursesProp.arraySize);
                    coursesProp.GetArrayElementAtIndex(coursesProp.arraySize - 1).objectReferenceValue = course;
                }
                serializedOpenPutt.ApplyModifiedProperties();
            }


            bool hasCourses = false;
            for (int i = 0; i < coursesProp.arraySize; i++)
                if (coursesProp.GetArrayElementAtIndex(i).objectReferenceValue)
                    hasCourses = true;
            if (!hasCourses)
            {
                CenteredLabel("You need to add at least 1 course!", 13, Color.red);
                CenteredLabel("OpenPutt will not work until you add one!", 13, Color.red);
            }


            EditorGUILayout.PropertyField(coursesProp, true);

            GUILayout.Space(30);
            CenteredLabel("Scoreboard Setup", 14);

            var scoreboardManager = openPutt.transform.GetComponentInChildren<ScoreboardManager>();
            var scoreboardManagerSerialized = new SerializedObject(scoreboardManager);

            SerializedProperty scoreboardPostions = scoreboardManagerSerialized.FindProperty("scoreboardPositions");

            List<Scoreboard> scoreboards = new List<Scoreboard>();
            scoreboards.AddRange(openPutt.scoreboardManager.scoreboards);
            scoreboards.AddRange(openPutt.scoreboardManager.staticScoreboards);

            if (ScoreboardBuildProcessor.ShouldBuildScoreboards(scoreboardManager, scoreboards))
            {
                CenteredLabel("The scoreboards need rebuilding!", 13, Color.red);
                Label("This needs to be done as you have added or removed courses and the scoreboards need to be updated to reflect this. If you don't do this the scoreboards won't work properly and can crash.");

                if (GUILayout.Button("Rebuild Scoreboards"))
                {
                    ScoreboardBuildProcessor.BuildScoreboards(scoreboardManager, scoreboards, true);
                }
            }

            Label("This is a list of all the scoreboard positioners in your world. These describe where scoreboards will be displayed in your world and you can add more with the button below.");

            if (GUILayout.Button("+ Add New Scoreboard Position"))
            {
                Transform positionsRoot = scoreboardManager.transform.Find("ScoreboardPositions");

                GameObject newScoreboardPosition = PrefabUtility.InstantiatePrefab(openPuttScoreboardPositionerPrefab, positionsRoot) as GameObject;
                newScoreboardPosition.GetComponent<ScoreboardPositioner>().manager = scoreboardManager;
                Undo.RegisterCreatedObjectUndo(newScoreboardPosition, "Create OpenPutt Scoreboard Positioner");
                Transform cameraTransform = SceneView.lastActiveSceneView.camera.transform;
                newScoreboardPosition.transform.position = cameraTransform.position + cameraTransform.forward * 3f;

                Selection.activeGameObject = newScoreboardPosition;

                scoreboardPostions.InsertArrayElementAtIndex(scoreboardPostions.arraySize);
                scoreboardPostions.GetArrayElementAtIndex(scoreboardPostions.arraySize - 1).objectReferenceValue = newScoreboardPosition;
            }
            EditorGUILayout.PropertyField(scoreboardPostions, true);


            GUILayout.Space(30);
            if (GUILayout.Button("Github Page", GUILayout.Height(40f)))
                Application.OpenURL("https://github.com/mikeee324/OpenPutt");

            GUILayout.EndScrollView();

            if (EditorGUI.EndChangeCheck())
            {
                scoreboardManagerSerialized.ApplyModifiedProperties();
                serializedOpenPutt.ApplyModifiedProperties();
                EditorSceneManager.MarkSceneDirty(openPutt.gameObject.scene);
            }
            else if (Event.current.type == EventType.ExecuteCommand && (Event.current.commandName == "UndoRedoPerformed" || Event.current.commandName == "SoftDelete"))
            {
                scoreboardManagerSerialized.UpdateIfRequiredOrScript();
                serializedOpenPutt.UpdateIfRequiredOrScript();
                EditorSceneManager.MarkSceneDirty(openPutt.gameObject.scene);
            }
        }
    }
}
#endif