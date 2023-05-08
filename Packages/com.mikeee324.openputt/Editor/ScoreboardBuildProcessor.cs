#if UNITY_EDITOR
using mikeee324.OpenPutt;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public class ScoreboardBuildProcessor : IProcessSceneWithReport
{
    public int callbackOrder { get { return 0; } }

    public void OnProcessScene(Scene scene, BuildReport report)
    {
        OpenPutt openPutt = GameObject.FindObjectOfType<OpenPutt>();

        if (openPutt == null)
        {
            Utils.LogWarning("ScoreboardBuilder", "Could not find an OpenPutt prefab in the scene.. doing nothing");
            return;
        }

        if (openPutt.scoreboardManager == null)
        {
            Utils.LogError("ScoreboardBuilder", "OpenPutt prefab can't find scoreboard manager! Make sure all references are set up correctly!");
            return;
        }

        int numberOfPlayersOnScoreboard = openPutt.scoreboardManager.numberOfPlayersToDisplay;
        int numberOfColumnsOnScoreboard = openPutt.scoreboardManager.NumberOfColumns;

        bool needsToBuildScoreboards = false;

        foreach (Scoreboard scoreboard in openPutt.scoreboardManager.scoreboards)
        {
            if (scoreboard.parRowCanvas == null || scoreboard.topRowCanvas == null)
            {
                needsToBuildScoreboards = true;
                break;
            }

            ScoreboardPlayerRow parRow = scoreboard.parRowCanvas.GetComponent<ScoreboardPlayerRow>();
            ScoreboardPlayerRow topRow = scoreboard.topRowCanvas.GetComponent<ScoreboardPlayerRow>();

            if (parRow == null || parRow.columns.Length != numberOfColumnsOnScoreboard)
            {
                needsToBuildScoreboards = true;
                break;
            }
            if (topRow == null || topRow.columns.Length != numberOfColumnsOnScoreboard)
            {
                needsToBuildScoreboards = true;
                break;
            }

            Transform playerList = scoreboard.playerListCanvas.transform;

            if (playerList.childCount != numberOfPlayersOnScoreboard)
            {
                needsToBuildScoreboards = true;
                break;
            }

            for (int i = 0; i < playerList.childCount; i++)
            {
                ScoreboardPlayerRow row = playerList.GetChild(i).GetComponent<ScoreboardPlayerRow>();
                if (row == null || row.columns.Length != numberOfColumnsOnScoreboard)
                {
                    needsToBuildScoreboards = true;
                    break;
                }
            }

            if (needsToBuildScoreboards)
                break;
        }


        foreach (Scoreboard scoreboard in openPutt.scoreboardManager.staticScoreboards)
        {
            if (scoreboard.parRowCanvas == null || scoreboard.topRowCanvas == null)
            {
                needsToBuildScoreboards = true;
                break;
            }

            ScoreboardPlayerRow parRow = scoreboard.parRowCanvas.GetComponent<ScoreboardPlayerRow>();
            ScoreboardPlayerRow topRow = scoreboard.topRowCanvas.GetComponent<ScoreboardPlayerRow>();

            if (parRow == null || parRow.columns.Length != numberOfColumnsOnScoreboard)
            {
                needsToBuildScoreboards = true;
                break;
            }
            if (topRow == null || topRow.columns.Length != numberOfColumnsOnScoreboard)
            {
                needsToBuildScoreboards = true;
                break;
            }

            Transform playerList = scoreboard.playerListCanvas.transform;

            if (playerList.childCount != numberOfPlayersOnScoreboard)
            {
                needsToBuildScoreboards = true;
                break;
            }

            for (int i = 0; i < playerList.childCount; i++)
            {
                ScoreboardPlayerRow row = playerList.GetChild(i).GetComponent<ScoreboardPlayerRow>();
                if (row == null || row.columns.Length != numberOfColumnsOnScoreboard)
                {
                    needsToBuildScoreboards = true;
                    break;
                }
            }

            if (needsToBuildScoreboards)
                break;
        }

        if (needsToBuildScoreboards)
        {
            BuildScoreboards(openPutt.scoreboardManager);
            BuildStaticScoreboards(openPutt.scoreboardManager);
            Utils.LogWarning(openPutt.scoreboardManager, $"Scoreboards were populated because some rows or columns were missing. If you would like to avoid this click the 'Setup Scoreboards' button on the ScoreboardManager inspector window!");
        }
    }

    public static void BuildScoreboards(ScoreboardManager manager, bool showProgressBar = false)
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

            if (showProgressBar && EditorUtility.DisplayCancelableProgressBar("Scoreboard Setup", $"Clearing old rows from scoreboard ID {scoreboardID}({manager.scoreboards[scoreboardID].name})", (currentWork++ / totalWork)))
            {
                EditorUtility.ClearProgressBar();
                return;
            }
            Utils.Log("ScoreboardBuildProcessor", $"Clearing old rows from scoreboard ID {scoreboardID}({manager.scoreboards[scoreboardID].name})");

            while (manager.scoreboards[scoreboardID].topRowPanel.transform.childCount > 0)
            {
                foreach (Transform t in manager.scoreboards[scoreboardID].topRowPanel.transform)
                {
                    GameObject.DestroyImmediate(t.gameObject);
                }
            }
            while (manager.scoreboards[scoreboardID].parRowPanel.transform.childCount > 0)
            {
                foreach (Transform t in manager.scoreboards[scoreboardID].parRowPanel.transform)
                {
                    GameObject.DestroyImmediate(t.gameObject);
                }
            }
            while (manager.scoreboards[scoreboardID].playerListCanvas.transform.childCount > 0)
            {
                foreach (Transform t in manager.scoreboards[scoreboardID].playerListCanvas.transform)
                {
                    GameObject.DestroyImmediate(t.gameObject);
                }
            }
            manager.scoreboards[scoreboardID].scoreboardRows = new ScoreboardPlayerRow[manager.numberOfPlayersToDisplay];
        }

        for (int scoreboardID = 0; scoreboardID < manager.scoreboards.Length; scoreboardID++)
        {
            if (manager.scoreboards[scoreboardID] == null)
                continue;
            if (showProgressBar && EditorUtility.DisplayCancelableProgressBar("Scoreboard Setup", $"Creating top rows for scoreboard ID {scoreboardID}({manager.scoreboards[scoreboardID].name})", (currentWork++ / totalWork)))
            {
                EditorUtility.ClearProgressBar();
                return;
            }
            Utils.Log("ScoreboardBuildProcessor", $"Creating top rows for scoreboard ID {scoreboardID}({manager.scoreboards[scoreboardID].name})");

            ScoreboardPlayerRow row = manager.scoreboards[scoreboardID].CreateRow(0, manager.scoreboards[scoreboardID].topRowPanel);
            row.rowType = ScoreboardPlayerRowType.Header;
            manager.scoreboards[scoreboardID].topRowCanvas = row.GetComponent<Canvas>();

            row = manager.scoreboards[scoreboardID].CreateRow(0, manager.scoreboards[scoreboardID].parRowPanel);
            row.rowType = ScoreboardPlayerRowType.Par;
            manager.scoreboards[scoreboardID].parRowCanvas = row.GetComponent<Canvas>();
        }

        for (int playerID = 0; playerID < manager.numberOfPlayersToDisplay; playerID++)
        {
            for (int scoreboardID = 0; scoreboardID < manager.scoreboards.Length; scoreboardID++)
            {
                if (manager.scoreboards[scoreboardID] == null)
                    continue;
                if (showProgressBar && EditorUtility.DisplayCancelableProgressBar("Scoreboard Setup", $"Creating row {playerID} for scoreboard ID {scoreboardID}({manager.scoreboards[scoreboardID].name})", (currentWork++ / totalWork)))
                {
                    EditorUtility.ClearProgressBar();
                    return;
                }
                ScoreboardPlayerRow row = manager.scoreboards[scoreboardID].CreateRow(playerID);
                row.gameObject.SetActive(false);
                Utils.Log("ScoreboardBuildProcessor", $"Creating row {playerID} for scoreboard ID {scoreboardID}({manager.scoreboards[scoreboardID].name})");
            }
        }

        if (showProgressBar)
        {
            EditorUtility.DisplayProgressBar("Scoreboard Setup", "Saving the scene", 1);
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());
            EditorUtility.ClearProgressBar();
        }
    }

    public static void BuildStaticScoreboards(ScoreboardManager manager, bool showProgressBar = false)
    {
        float totalWork = manager.staticScoreboards.Length * manager.numberOfPlayersToDisplay;
        totalWork += manager.staticScoreboards.Length * 2;
        float currentWork = 0;

        for (int scoreboardID = 0; scoreboardID < manager.staticScoreboards.Length; scoreboardID++)
        {
            if (manager.staticScoreboards[scoreboardID] == null)
            {
                Utils.LogError(manager, $"Scoreboard ID {scoreboardID} is null! Please make sure all scoreboards have been assigned properly!");
                continue;
            }

            EditorUtility.SetDirty(manager.staticScoreboards[scoreboardID]);
            PrefabUtility.RecordPrefabInstancePropertyModifications(manager.staticScoreboards[scoreboardID]);

            if (showProgressBar && EditorUtility.DisplayCancelableProgressBar("Scoreboard Setup", $"Clearing old rows from scoreboard ID {scoreboardID}({manager.staticScoreboards[scoreboardID].name})", (currentWork++ / totalWork)))
            {
                EditorUtility.ClearProgressBar();
                return;
            }
            Utils.Log("ScoreboardBuildProcessor", $"Clearing old rows from scoreboard ID {scoreboardID}({manager.staticScoreboards[scoreboardID].name})");

            while (manager.staticScoreboards[scoreboardID].topRowPanel.transform.childCount > 0)
            {
                foreach (Transform t in manager.staticScoreboards[scoreboardID].topRowPanel.transform)
                {
                    GameObject.DestroyImmediate(t.gameObject);
                }
            }
            while (manager.staticScoreboards[scoreboardID].parRowPanel.transform.childCount > 0)
            {
                foreach (Transform t in manager.staticScoreboards[scoreboardID].parRowPanel.transform)
                {
                    GameObject.DestroyImmediate(t.gameObject);
                }
            }
            while (manager.staticScoreboards[scoreboardID].playerListCanvas.transform.childCount > 0)
            {
                foreach (Transform t in manager.staticScoreboards[scoreboardID].playerListCanvas.transform)
                {
                    GameObject.DestroyImmediate(t.gameObject);
                }
            }
            manager.staticScoreboards[scoreboardID].scoreboardRows = new ScoreboardPlayerRow[manager.numberOfPlayersToDisplay];
        }

        for (int scoreboardID = 0; scoreboardID < manager.staticScoreboards.Length; scoreboardID++)
        {
            if (manager.staticScoreboards[scoreboardID] == null)
                continue;
            if (showProgressBar && EditorUtility.DisplayCancelableProgressBar("Scoreboard Setup", $"Creating top rows for scoreboard ID {scoreboardID}({manager.staticScoreboards[scoreboardID].name})", (currentWork++ / totalWork)))
            {
                EditorUtility.ClearProgressBar();
                return;
            }
            Utils.Log("ScoreboardBuildProcessor", $"Creating top rows for scoreboard ID {scoreboardID}({manager.staticScoreboards[scoreboardID].name})");

            ScoreboardPlayerRow row = manager.staticScoreboards[scoreboardID].CreateRow(0, manager.staticScoreboards[scoreboardID].topRowPanel);
            row.rowType = ScoreboardPlayerRowType.Header;
            manager.staticScoreboards[scoreboardID].topRowCanvas = row.GetComponent<Canvas>();

            row = manager.staticScoreboards[scoreboardID].CreateRow(0, manager.staticScoreboards[scoreboardID].parRowPanel);
            row.rowType = ScoreboardPlayerRowType.Par;
            manager.staticScoreboards[scoreboardID].parRowCanvas = row.GetComponent<Canvas>();
        }

        for (int playerID = 0; playerID < manager.numberOfPlayersToDisplay; playerID++)
        {
            for (int scoreboardID = 0; scoreboardID < manager.staticScoreboards.Length; scoreboardID++)
            {
                if (manager.staticScoreboards[scoreboardID] == null)
                    continue;
                if (showProgressBar && EditorUtility.DisplayCancelableProgressBar("Scoreboard Setup", $"Creating row {playerID} for scoreboard ID {scoreboardID}({manager.staticScoreboards[scoreboardID].name})", (currentWork++ / totalWork)))
                {
                    EditorUtility.ClearProgressBar();
                    return;
                }
                ScoreboardPlayerRow row = manager.staticScoreboards[scoreboardID].CreateRow(playerID);
                row.gameObject.SetActive(false);
                Utils.Log("ScoreboardBuildProcessor", $"Creating row {playerID} for scoreboard ID {scoreboardID}({manager.staticScoreboards[scoreboardID].name})");
            }
        }

        if (showProgressBar)
        {
            EditorUtility.DisplayProgressBar("Scoreboard Setup", "Saving the scene", 1);
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());
            EditorUtility.ClearProgressBar();
        }
    }
}
#endif