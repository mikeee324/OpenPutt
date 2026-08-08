#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace dev.mikeee324.OpenPutt
{
    /// <summary>
    /// Keeps the OpenPutt courses list clean without fighting manual edits in the setup window.
    /// Null/duplicate course references are only stripped when entering play mode or building, so
    /// while editing you can freely add empty slots and drag hand-placed courses into them.
    /// </summary>
    [InitializeOnLoad]
    public static class OpenPuttCourseCleanup
    {
        static OpenPuttCourseCleanup()
        {
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
        }

        private static void OnPlayModeStateChanged(PlayModeStateChange state)
        {
            // Clean up just before play starts so the runtime never sees null/duplicate courses.
            if (state != PlayModeStateChange.ExitingEditMode)
                return;

            foreach (OpenPutt openPutt in Object.FindObjectsOfType<OpenPutt>())
                StripInvalidCourses(openPutt, recordSceneOverride: true);
        }

        /// <summary>
        /// Returns the filtered course list with nulls and duplicates removed, order preserved.
        /// </summary>
        public static CourseManager[] GetValidCourses(CourseManager[] courses)
        {
            if (courses == null)
                return new CourseManager[0];

            List<CourseManager> valid = new List<CourseManager>(courses.Length);
            foreach (CourseManager course in courses)
                if (course != null && !valid.Contains(course))
                    valid.Add(course);

            return valid.ToArray();
        }

        /// <summary>
        /// Strips null/duplicate courses from an OpenPutt in the current scene. When
        /// recordSceneOverride is true the change is written as an instance override (safe when
        /// OpenPutt is nested inside another prefab) and the scene is marked dirty for a manual
        /// save - nothing is ever applied to a prefab asset. Returns true if anything changed.
        /// </summary>
        public static bool StripInvalidCourses(OpenPutt openPutt, bool recordSceneOverride)
        {
            if (openPutt == null)
                return false;

            CourseManager[] valid = GetValidCourses(openPutt.courses);
            if (openPutt.courses != null && valid.Length == openPutt.courses.Length)
                return false; // already clean, don't touch anything

            int removed = (openPutt.courses != null ? openPutt.courses.Length : 0) - valid.Length;
            OpenPuttUtils.Log("OpenPutt Cleanup", $"Removed {removed} null/duplicate course reference(s) from '{openPutt.name}'", "cyan");

            if (recordSceneOverride)
            {
                SerializedObject so = new SerializedObject(openPutt);
                SerializedProperty coursesProp = so.FindProperty("courses");
                coursesProp.ClearArray();
                for (int i = 0; i < valid.Length; i++)
                {
                    coursesProp.InsertArrayElementAtIndex(i);
                    coursesProp.GetArrayElementAtIndex(i).objectReferenceValue = valid[i];
                }
                so.ApplyModifiedProperties();
                PrefabUtility.RecordPrefabInstancePropertyModifications(openPutt);
                EditorSceneManager.MarkSceneDirty(openPutt.gameObject.scene);
            }
            else
            {
                // Build-time temporary scene - direct assignment is fine and never persists.
                openPutt.courses = valid;
            }

            return true;
        }
    }

    /// <summary>
    /// Strips null/duplicate courses from the temporary scene used for a build. This only affects
    /// the built/uploaded world, never the saved scene.
    /// </summary>
    public class OpenPuttCourseBuildProcessor : IProcessSceneWithReport
    {
        public int callbackOrder => 0;

        public void OnProcessScene(Scene scene, BuildReport report)
        {
            foreach (OpenPutt openPutt in Object.FindObjectsOfType<OpenPutt>())
                OpenPuttCourseCleanup.StripInvalidCourses(openPutt, recordSceneOverride: false);
        }
    }
}
#endif
