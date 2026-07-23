#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using System.Linq;
using UnityEditor.SceneManagement;
using UnityEditor.PackageManager.UI;

namespace dev.mikeee324.OpenPutt
{
    public class OpenPuttMainMenu : EditorWindow
    {
        GameObject openPuttPrefab = null;
        GameObject openPuttCoursePrefab = null;
        GameObject openPuttScoreboardPositionerPrefab = null;
        Texture openPuttLogo = null;

        Vector2 scrollPosition = Vector2.zero;
        int selectedTab = 0;
        bool showChangelog = false;
        bool samplesImported = true;

        [MenuItem("OpenPutt/Open Setup Helper")]
        public static void ShowWindow()
        {
            var window = GetWindow(typeof(OpenPuttMainMenu));
            window.titleContent.text = "OpenPutt Setup";
            window.minSize = new Vector2(360, 420);
        }

        private void OnEnable()
        {
            samplesImported = AreSamplesImported();
        }

        private void OnFocus()
        {
            // Refresh in case the samples were imported/removed via the Package Manager while open.
            samplesImported = AreSamplesImported();
        }

        private static bool AreSamplesImported()
        {
            var assembly = typeof(OpenPuttMainMenu).Assembly;
            var packageInfo = UnityEditor.PackageManager.PackageInfo.FindForAssembly(assembly);
            if (packageInfo == null)
                return true; // can't tell - don't nag the user

            foreach (Sample sample in Sample.FindByPackage(packageInfo.name, packageInfo.version))
                if (!sample.isImported)
                    return false;
            return true;
        }

        private void CenteredLabel(string text, int fontSize = -1, Color textColor = default, bool wordWrap = true)
        {
            GUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            GUIStyle style = new GUIStyle(EditorStyles.boldLabel);
            if (fontSize != -1) style.fontSize = fontSize;
            if (textColor != default) style.normal.textColor = textColor;
            style.wordWrap = wordWrap;
            style.alignment = TextAnchor.MiddleCenter;
            style.margin = new RectOffset(0, 0, 0, 0);
            style.padding = new RectOffset(0, 0, 0, 0);
            GUILayout.Label(text, style, GUILayout.ExpandWidth(true));
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();
        }

        /// <summary>A non-bold, word wrapped paragraph used for section descriptions.</summary>
        private void Description(string text)
        {
            GUIStyle style = new GUIStyle(EditorStyles.label) { wordWrap = true };
            GUILayout.Label(text, style);
        }

        private void SectionHeader(string title)
        {
            GUILayout.Space(2);
            GUIStyle style = new GUIStyle(EditorStyles.boldLabel) { fontSize = 13 };
            GUILayout.Label(title, style);
            HorizontalLine();
            GUILayout.Space(2);
        }

        private void HorizontalLine()
        {
            Rect rect = EditorGUILayout.GetControlRect(false, 1f);
            EditorGUI.DrawRect(rect, new Color(0.5f, 0.5f, 0.5f, 0.4f));
        }

        /// <summary>Draws the title/subtitle with the OpenPutt logo on each side (the right one mirrored).</summary>
        private void DrawTitleWithLogos(string title, string subtitle)
        {
            const float logoSize = 40f;

            GUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();

            if (openPuttLogo != null)
            {
                Rect left = GUILayoutUtility.GetRect(logoSize, logoSize, GUILayout.Width(logoSize), GUILayout.Height(logoSize));
                GUI.DrawTexture(left, openPuttLogo, ScaleMode.ScaleToFit);
            }

            GUILayout.Space(8);
            GUILayout.BeginVertical(GUILayout.Height(logoSize));
            GUILayout.FlexibleSpace();
            CenteredLabel(title, 20);
            GUILayout.Space(2);
            CenteredLabel(subtitle, 11, new Color(0.6f, 0.6f, 0.6f));
            GUILayout.FlexibleSpace();
            GUILayout.EndVertical();
            GUILayout.Space(8);

            if (openPuttLogo != null)
            {
                Rect right = GUILayoutUtility.GetRect(logoSize, logoSize, GUILayout.Width(logoSize), GUILayout.Height(logoSize));
                // Mirror horizontally around the rect's centre while keeping aspect (ScaleToFit).
                Matrix4x4 previous = GUI.matrix;
                GUIUtility.ScaleAroundPivot(new Vector2(-1f, 1f), right.center);
                GUI.DrawTexture(right, openPuttLogo, ScaleMode.ScaleToFit);
                GUI.matrix = previous;
            }

            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();
        }

        /// <summary>Builds a tab label with an item count and an optional error/warning badge icon.</summary>
        private GUIContent MakeTabContent(string title, int count, bool error, bool warning)
        {
            string label = title + " (" + count + ")";
            if (error)
                return new GUIContent(label, EditorGUIUtility.IconContent("console.erroricon.sml").image);
            if (warning)
                return new GUIContent(label, EditorGUIUtility.IconContent("console.warnicon.sml").image);
            return new GUIContent(label);
        }

        private void OnGUI()
        {
            if (openPuttPrefab == null)
                openPuttPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Packages/com.mikeee324.openputt/Runtime/Prefabs/OpenPutt.prefab");

            if (openPuttCoursePrefab == null)
                openPuttCoursePrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Packages/com.mikeee324.openputt/Runtime/Prefabs/Components/OpenPuttCourse.prefab");

            if (openPuttScoreboardPositionerPrefab == null)
                openPuttScoreboardPositionerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Packages/com.mikeee324.openputt/Runtime/Prefabs/UI/ScoreboardPositioner.prefab");

            if (openPuttLogo == null)
                openPuttLogo = AssetDatabase.LoadAssetAtPath<Texture>("Packages/com.mikeee324.openputt/Runtime/Extras/OpenPuttLogo.png");

            var firstOpenPutt = FindObjectsOfType<OpenPutt>().FirstOrDefault();

            if (firstOpenPutt == null)
            {
                DrawMissingOpenPutt();
                return;
            }

            // ----- Header (always visible) -----
            GUILayout.Space(4);
            DrawTitleWithLogos("OpenPutt", firstOpenPutt.CurrentVersion + "  •  by mikeee324");
            GUILayout.Space(4);

            OpenPutt openPutt = EditorGUILayout.ObjectField("OpenPutt", firstOpenPutt, typeof(OpenPutt), true) as OpenPutt;

            var scoreboardManager = openPutt.transform.GetComponentInChildren<ScoreboardManager>();
            SerializedObject serializedOpenPutt = new SerializedObject(openPutt);
            SerializedObject scoreboardManagerSerialized = scoreboardManager != null ? new SerializedObject(scoreboardManager) : null;

            // ----- Per-tab metrics (drive the tab counts + error/warning badges) -----
            SerializedProperty coursesProp = serializedOpenPutt.FindProperty("courses");
            List<Object> distinctCourses = new List<Object>();
            bool coursesHaveDuplicates = false;
            for (int i = 0; i < coursesProp.arraySize; i++)
            {
                Object course = coursesProp.GetArrayElementAtIndex(i).objectReferenceValue;
                if (!course)
                    continue;
                if (distinctCourses.Contains(course))
                    coursesHaveDuplicates = true;
                else
                    distinctCourses.Add(course);
            }
            int courseCount = distinctCourses.Count;

            List<Scoreboard> scoreboards = new List<Scoreboard>();
            int positionCount = 0;
            bool needsRebuild = false;
            bool scoreboardsMissing = scoreboardManager == null;
            if (scoreboardManagerSerialized != null)
            {
                scoreboards.AddRange(scoreboardManager.scoreboards);
                scoreboards.AddRange(scoreboardManager.staticScoreboards);
                // Guard against null scoreboards (ShouldBuildScoreboards would throw) - treat as needing a rebuild.
                needsRebuild = scoreboards.Any(s => s == null) || ScoreboardBuildProcessor.ShouldBuildScoreboards(scoreboardManager, scoreboards);

                SerializedProperty positionsProp = scoreboardManagerSerialized.FindProperty("scoreboardPositions");
                for (int i = 0; i < positionsProp.arraySize; i++)
                    if (positionsProp.GetArrayElementAtIndex(i).objectReferenceValue)
                        positionCount++;
            }

            // ----- Tabs with counts + error/warning badges -----
            GUIContent[] tabs =
            {
                new GUIContent("Home"),
                MakeTabContent("Courses", courseCount, courseCount == 0, coursesHaveDuplicates),
                MakeTabContent("Scoreboards", positionCount, scoreboardsMissing, needsRebuild)
            };

            GUILayout.Space(6);
            selectedTab = GUILayout.Toolbar(selectedTab, tabs, GUILayout.Height(24));
            GUILayout.Space(6);

            scrollPosition = GUILayout.BeginScrollView(scrollPosition, false, false);
            EditorGUI.BeginChangeCheck();

            switch (selectedTab)
            {
                case 1:
                    DrawCoursesTab(openPutt, serializedOpenPutt, courseCount, coursesHaveDuplicates);
                    break;
                case 2:
                    DrawScoreboardsTab(openPutt, scoreboardManager, scoreboardManagerSerialized, scoreboards, needsRebuild);
                    break;
                default:
                    DrawHomeTab(openPutt);
                    break;
            }

            bool changed = EditorGUI.EndChangeCheck();
            GUILayout.EndScrollView();

            if (changed)
            {
                if (scoreboardManagerSerialized != null) scoreboardManagerSerialized.ApplyModifiedProperties();
                serializedOpenPutt.ApplyModifiedProperties();
                // Force-record overrides so inline edits persist when OpenPutt is nested in another prefab.
                if (scoreboardManager != null) PrefabUtility.RecordPrefabInstancePropertyModifications(scoreboardManager);
                PrefabUtility.RecordPrefabInstancePropertyModifications(openPutt);
                EditorSceneManager.MarkSceneDirty(openPutt.gameObject.scene);
            }
            else if (Event.current.type == EventType.ExecuteCommand && (Event.current.commandName == "UndoRedoPerformed" || Event.current.commandName == "SoftDelete"))
            {
                if (scoreboardManagerSerialized != null) scoreboardManagerSerialized.UpdateIfRequiredOrScript();
                serializedOpenPutt.UpdateIfRequiredOrScript();
                EditorSceneManager.MarkSceneDirty(openPutt.gameObject.scene);
            }
        }

        private void DrawMissingOpenPutt()
        {
            GUILayout.Space(24);
            DrawTitleWithLogos("OpenPutt", "by mikeee324");
            GUILayout.Space(18);

            EditorGUILayout.HelpBox("OpenPutt isn't in the scene yet. Click the button below to add it.", MessageType.Info);
            GUILayout.Space(6);

            if (GUILayout.Button("Add OpenPutt to the scene", GUILayout.Height(40)))
            {
                GameObject openPuttObj = PrefabUtility.InstantiatePrefab(openPuttPrefab) as GameObject;
                Undo.RegisterCreatedObjectUndo(openPuttObj, "Create OpenPutt Prefab");
                OpenPuttUtils.Log("OpenPutt Setup", "Added OpenPutt to the scene", "cyan");
                Repaint();
            }
        }

        private void DrawCoursesTab(OpenPutt openPutt, SerializedObject serializedOpenPutt, int courseCount, bool hasDuplicates)
        {
            SerializedProperty coursesProp = serializedOpenPutt.FindProperty("courses");

            SectionHeader("Courses");
            Description("All of the courses in your world, in the order they'll be played. Each course has a start pad and a hole collider - drag those into position in the scene.");
            GUILayout.Space(6);

            if (GUILayout.Button("+ Add New Course", GUILayout.Height(26)))
            {
                Transform holes = openPutt.transform.Find("Holes");

                GameObject newCourse = PrefabUtility.InstantiatePrefab(openPuttCoursePrefab, holes) as GameObject;
                Undo.RegisterCreatedObjectUndo(newCourse, "Create OpenPutt Course");
                Transform cameraTransform = SceneView.lastActiveSceneView.camera.transform;
                newCourse.transform.position = cameraTransform.position + cameraTransform.forward * 3f;

                Selection.activeGameObject = newCourse;

                coursesProp.InsertArrayElementAtIndex(coursesProp.arraySize);
                coursesProp.GetArrayElementAtIndex(coursesProp.arraySize - 1).objectReferenceValue = newCourse;

                OpenPuttUtils.Log("OpenPutt Setup", $"Added new course '{newCourse.name}'", "cyan");

                // Apply + force-record the override immediately. When OpenPutt is nested inside
                // another prefab this is required for the array change to persist, and it avoids
                // mutating the serialized array mid-layout (which aborts the rest of OnGUI).
                serializedOpenPutt.ApplyModifiedProperties();
                PrefabUtility.RecordPrefabInstancePropertyModifications(openPutt);
                EditorSceneManager.MarkSceneDirty(openPutt.gameObject.scene);
                Repaint();
                GUIUtility.ExitGUI();
            }

            GUILayout.Space(6);

            if (courseCount == 0)
                EditorGUILayout.HelpBox("You need to add at least 1 course. OpenPutt won't work until you add one.", MessageType.Error);
            if (hasDuplicates)
                EditorGUILayout.HelpBox("The same course is listed more than once. Duplicates are removed automatically when you enter play mode or build.", MessageType.Warning);

            GUILayout.Space(4);

            // Null/duplicate course references are NOT stripped here on purpose - doing it every
            // OnGUI frame deletes empty slots before you can drag a hand-placed course into them.
            // Cleanup happens on entering play mode and at build time (see OpenPuttCourseCleanup).
            //
            // Unity's built-in "+" on the list copies the previous entry (a duplicate reference).
            // Our custom "+ Add New Course" button bails out with ExitGUI, so any growth here came
            // from the built-in "+" - null the new slot(s) so you get an empty entry to fill in.
            int coursesSizeBefore = coursesProp.arraySize;
            EditorGUILayout.PropertyField(coursesProp, true);
            for (int i = coursesSizeBefore; i < coursesProp.arraySize; i++)
                coursesProp.GetArrayElementAtIndex(i).objectReferenceValue = null;
        }

        private void DrawScoreboardsTab(OpenPutt openPutt, ScoreboardManager scoreboardManager, SerializedObject scoreboardManagerSerialized, List<Scoreboard> scoreboards, bool needsRebuild)
        {
            if (scoreboardManager == null || scoreboardManagerSerialized == null)
            {
                EditorGUILayout.HelpBox("Couldn't find a ScoreboardManager under OpenPutt. Make sure all references are set up correctly.", MessageType.Error);
                return;
            }

            SerializedProperty scoreboardPositions = scoreboardManagerSerialized.FindProperty("scoreboardPositions");

            SectionHeader("Scoreboard Rebuild");
            if (needsRebuild)
            {
                EditorGUILayout.HelpBox("The scoreboards need rebuilding. You've added or removed courses, so the scoreboards need updating to match. If you don't, they won't work properly and can crash.", MessageType.Warning);
                if (GUILayout.Button("Rebuild Scoreboards", GUILayout.Height(26)))
                {
                    ScoreboardBuildProcessor.BuildScoreboards(scoreboardManager, scoreboards, true);
                    GUIUtility.ExitGUI();
                }
            }
            else
            {
                int neededCourseColumns = Mathf.Max(0, scoreboardManager.NumberOfColumns - 2);
                int builtCourseColumns = neededCourseColumns;
                foreach (Scoreboard scoreboard in scoreboards)
                {
                    if (scoreboard == null || scoreboard.parRowCanvas == null)
                        continue;
                    ScoreboardPlayerRow parRow = scoreboard.parRowCanvas.GetComponent<ScoreboardPlayerRow>();
                    if (parRow != null)
                    {
                        builtCourseColumns = Mathf.Max(0, parRow.columns.Length - 2);
                        break;
                    }
                }

                EditorGUILayout.HelpBox($"Scoreboards have {builtCourseColumns}/{neededCourseColumns} course columns set up.", MessageType.Info);

                // No rebuild is strictly required, but expose the button anyway so you can force a
                // rebuild after changing the scoreboard prefabs (e.g. canvas/layout tweaks that don't
                // change the course count and so don't trip needsRebuild).
                if (GUILayout.Button("Rebuild Scoreboards", GUILayout.Height(26)))
                {
                    ScoreboardBuildProcessor.BuildScoreboards(scoreboardManager, scoreboards, true);
                    GUIUtility.ExitGUI();
                }
            }

            GUILayout.Space(10);
            SectionHeader("Scoreboard Positions");
            Description("Where scoreboards are displayed in your world. Add more with the button below, then move each one into place in the scene. The blue arrow on a positioner's gizmo shows which way that scoreboard will face.");
            GUILayout.Space(6);

            if (GUILayout.Button("+ Add New Scoreboard Position", GUILayout.Height(26)))
            {
                Transform positionsRoot = scoreboardManager.transform.Find("ScoreboardPositions");

                GameObject newScoreboardPosition = PrefabUtility.InstantiatePrefab(openPuttScoreboardPositionerPrefab, positionsRoot) as GameObject;
                newScoreboardPosition.GetComponent<ScoreboardPositioner>().manager = scoreboardManager;
                Undo.RegisterCreatedObjectUndo(newScoreboardPosition, "Create OpenPutt Scoreboard Positioner");
                Transform cameraTransform = SceneView.lastActiveSceneView.camera.transform;
                newScoreboardPosition.transform.position = cameraTransform.position + cameraTransform.forward * 3f;

                Selection.activeGameObject = newScoreboardPosition;

                scoreboardPositions.InsertArrayElementAtIndex(scoreboardPositions.arraySize);
                scoreboardPositions.GetArrayElementAtIndex(scoreboardPositions.arraySize - 1).objectReferenceValue = newScoreboardPosition;

                OpenPuttUtils.Log("OpenPutt Setup", $"Added new scoreboard position '{newScoreboardPosition.name}'", "cyan");

                scoreboardManagerSerialized.ApplyModifiedProperties();
                PrefabUtility.RecordPrefabInstancePropertyModifications(scoreboardManager);
                EditorSceneManager.MarkSceneDirty(openPutt.gameObject.scene);
                Repaint();
                GUIUtility.ExitGUI();
            }

            GUILayout.Space(4);
            EditorGUILayout.PropertyField(scoreboardPositions, true);
        }

        private void DrawHomeTab(OpenPutt openPutt)
        {
            SectionHeader("About");
            Description("This window is your hub for setting up OpenPutt in your world. Use the tabs to:");
            Description("•  Courses - add and order your holes");
            Description("•  Scoreboards - rebuild and place them");

            GUILayout.Space(12);
            SectionHeader("Updates");

            // Resolve the update state when the Home tab is shown. This is throttled - it only
            // hits GitHub if we haven't checked in the last few hours, otherwise it uses the cache.
            OpenPuttVersionCheck.AutoCheck(openPutt.CurrentVersion, Repaint);

            switch (OpenPuttVersionCheck.CurrentState)
            {
                case OpenPuttVersionCheck.State.Checking:
                    EditorGUILayout.HelpBox("Checking GitHub for the latest release...", MessageType.None);
                    break;
                case OpenPuttVersionCheck.State.UpToDate:
                    EditorGUILayout.HelpBox($"You're on the latest version ({openPutt.CurrentVersion}).", MessageType.Info);
                    break;
                case OpenPuttVersionCheck.State.UpdateAvailable:
                    EditorGUILayout.HelpBox($"Update available: {OpenPuttVersionCheck.LatestVersion} (you have {openPutt.CurrentVersion}).", MessageType.Warning);
                    if (GUILayout.Button("Open Releases Page", GUILayout.Height(26)))
                        Application.OpenURL(OpenPuttVersionCheck.ReleasesPage);
                    break;
                case OpenPuttVersionCheck.State.Failed:
                    EditorGUILayout.HelpBox("Couldn't reach GitHub to check for updates. Check your connection and try again.", MessageType.None);
                    break;
            }

            using (new EditorGUI.DisabledScope(OpenPuttVersionCheck.CurrentState == OpenPuttVersionCheck.State.Checking))
            {
                if (GUILayout.Button("Check for Updates"))
                    OpenPuttVersionCheck.CheckForUpdates(openPutt.CurrentVersion, Repaint);
            }

            if (!string.IsNullOrEmpty(OpenPuttVersionCheck.Changelog))
            {
                GUILayout.Space(4);
                showChangelog = EditorGUILayout.Foldout(showChangelog, "What's New", true);
                if (showChangelog)
                {
                    GUIStyle changelogStyle = new GUIStyle(EditorStyles.label) { wordWrap = true };
                    EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                    GUILayout.Label(OpenPuttVersionCheck.Changelog, changelogStyle);
                    EditorGUILayout.EndVertical();
                }
            }

            GUILayout.Space(12);
            SectionHeader("Links");
            if (GUILayout.Button("GitHub Page", GUILayout.Height(30)))
                Application.OpenURL("https://github.com/mikeee324/OpenPutt");
            GUILayout.Space(4);

            Color previousBackground = GUI.backgroundColor;
            if (!samplesImported)
                GUI.backgroundColor = new Color(0.5f, 0.9f, 0.5f); // highlight to nudge first-time setup
            if (GUILayout.Button(samplesImported ? "Import Samples" : "Import Samples (recommended)", GUILayout.Height(30)))
            {
                ImportSamples();
                samplesImported = AreSamplesImported();
            }
            GUI.backgroundColor = previousBackground;
        }

        [MenuItem("OpenPutt/Import Samples")]
        public static void ImportSamples()
        {
            var assembly = typeof(OpenPuttMainMenu).Assembly;
            var packageInfo = UnityEditor.PackageManager.PackageInfo.FindForAssembly(assembly);
            foreach (Sample sample in Sample.FindByPackage(packageInfo.name, packageInfo.version))
                sample.Import();

            EditorUtility.DisplayDialog("Sample Import Finished", "The samples have been imported into your project.\r\nYou can find them in the Assets/Samples/OpenPutt folder.", "OK");
        }
    }
}
#endif
