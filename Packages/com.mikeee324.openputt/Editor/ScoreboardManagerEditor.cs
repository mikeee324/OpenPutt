using mikeee324.OpenPutt;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

[CustomEditor(typeof(ScoreboardManager))]
public class ScoreboardManagerEditor : Editor
{
    private ScoreboardManager manager => (ScoreboardManager)target;
    public override void OnInspectorGUI()
    {

        if (GUILayout.Button("Clear Scoreboard Rows"))
        {
            float totalWork = manager.scoreboards.Length * manager.numberOfPlayersToDisplay;
            totalWork += manager.scoreboards.Length * 2;
            float currentWork = 0;

            for (int scoreboardID = 0; scoreboardID < manager.scoreboards.Length; scoreboardID++)
            {
                if (manager.scoreboards[scoreboardID] == null)
                {
                    Utils.LogError(manager, $"Scoreboard ID {scoreboardID} is null! Please make sure all scoreboards have been assigned properly!");
                    continue;
                }

                EditorUtility.SetDirty(manager.scoreboards[scoreboardID]);
                PrefabUtility.RecordPrefabInstancePropertyModifications(manager.scoreboards[scoreboardID]);

                if (EditorUtility.DisplayCancelableProgressBar("Scoreboard Row Clear", $"Clearing old rows from scoreboard ID {scoreboardID}({manager.scoreboards[scoreboardID].name})", (currentWork++ / totalWork)))
                {
                    EditorUtility.ClearProgressBar();
                    return;
                }

                while (manager.scoreboards[scoreboardID].topRowPanel.transform.childCount > 0)
                {
                    foreach (Transform t in manager.scoreboards[scoreboardID].topRowPanel.transform)
                    {
                        DestroyImmediate(t.gameObject);
                    }
                }
                while (manager.scoreboards[scoreboardID].parRowPanel.transform.childCount > 0)
                {
                    foreach (Transform t in manager.scoreboards[scoreboardID].parRowPanel.transform)
                    {
                        DestroyImmediate(t.gameObject);
                    }
                }
                while (manager.scoreboards[scoreboardID].playerListCanvas.transform.childCount > 0)
                {
                    foreach (Transform t in manager.scoreboards[scoreboardID].playerListCanvas.transform)
                    {
                        DestroyImmediate(t.gameObject);
                    }
                }
                manager.scoreboards[scoreboardID].scoreboardRows = new ScoreboardPlayerRow[manager.numberOfPlayersToDisplay];
            }

            EditorUtility.DisplayProgressBar("Scoreboard Row Clear", "Saving the scene", 1);
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());
            EditorUtility.ClearProgressBar();
        }

        if (GUILayout.Button("Setup Scoreboards"))
        {
            ScoreboardBuildProcessor.BuildScoreboards(manager, showProgressBar: true);
        }

        // Show default inspector property editor
        DrawDefaultInspector();
    }
}