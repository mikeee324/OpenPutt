#if UNITY_EDITOR
using dev.mikeee324.OpenPutt;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public class ScoreboardBuildProcessor : IProcessSceneWithReport
{
    public int callbackOrder
    {
        get { return 0; }
    }

    public void OnProcessScene(Scene scene, BuildReport report)
    {
        OpenPutt openPutt = GameObject.FindObjectOfType<OpenPutt>();

        if (openPutt == null)
        {
            OpenPuttUtils.LogWarning("ScoreboardBuilder", "Could not find an OpenPutt prefab in the scene.. doing nothing");
            return;
        }

        if (openPutt.scoreboardManager == null)
        {
            OpenPuttUtils.LogError("ScoreboardBuilder", "OpenPutt prefab can't find scoreboard manager! Make sure all references are set up correctly!");
            return;
        }

        List<Scoreboard> scoreboards = new List<Scoreboard>();
        scoreboards.AddRange(openPutt.scoreboardManager.scoreboards);
        scoreboards.AddRange(openPutt.scoreboardManager.staticScoreboards);

        if (ShouldBuildScoreboards(openPutt.scoreboardManager, scoreboards))
        {
            BuildScoreboards(openPutt.scoreboardManager, scoreboards);
            OpenPuttUtils.LogWarning(openPutt.scoreboardManager, $"Scoreboards were populated because some rows or columns were missing. If you would like to avoid this click the 'Setup Scoreboards' button on the ScoreboardManager inspector window!");
        }
    }

    public static void BuildScoreboards(ScoreboardManager manager, List<Scoreboard> scoreboards, bool showProgressBar = false)
    {
        float totalWork = scoreboards.Count * manager.numberOfPlayersToDisplay;
        totalWork += scoreboards.Count * 2;
        float currentWork = 0;

        for (int scoreboardID = 0; scoreboardID < scoreboards.Count; scoreboardID++)
        {
            if (scoreboards[scoreboardID] == null)
            {
                OpenPuttUtils.LogError(manager, $"Scoreboard ID {scoreboardID} is null! Please make sure all scoreboards have been assigned properly!");
                continue;
            }

            EditorUtility.SetDirty(scoreboards[scoreboardID]);
            PrefabUtility.RecordPrefabInstancePropertyModifications(scoreboards[scoreboardID]);

            if (showProgressBar && EditorUtility.DisplayCancelableProgressBar("Scoreboard Setup", $"Clearing old rows from scoreboard ID {scoreboardID}({scoreboards[scoreboardID].name})", (currentWork++ / totalWork)))
            {
                EditorUtility.ClearProgressBar();
                return;
            }

            OpenPuttUtils.Log("ScoreboardBuildProcessor", $"Clearing old rows from scoreboard ID {scoreboardID}({scoreboards[scoreboardID].name})");

            while (scoreboards[scoreboardID].topRowPanel.transform.childCount > 0)
            {
                foreach (Transform t in scoreboards[scoreboardID].topRowPanel.transform)
                {
                    GameObject.DestroyImmediate(t.gameObject);
                }
            }

            while (scoreboards[scoreboardID].parRowPanel.transform.childCount > 0)
            {
                foreach (Transform t in scoreboards[scoreboardID].parRowPanel.transform)
                {
                    GameObject.DestroyImmediate(t.gameObject);
                }
            }

            while (scoreboards[scoreboardID].playerListCanvas.transform.childCount > 0)
            {
                foreach (Transform t in scoreboards[scoreboardID].playerListCanvas.transform)
                {
                    GameObject.DestroyImmediate(t.gameObject);
                }
            }

            scoreboards[scoreboardID].scoreboardRows = new ScoreboardPlayerRow[manager.numberOfPlayersToDisplay];

            if (showProgressBar && EditorUtility.DisplayCancelableProgressBar("Scoreboard Setup", $"Creating top rows for scoreboard ID {scoreboardID}({scoreboards[scoreboardID].name})", (currentWork++ / totalWork)))
            {
                EditorUtility.ClearProgressBar();
                return;
            }

            OpenPuttUtils.Log("ScoreboardBuildProcessor", $"Creating top rows for scoreboard ID {scoreboardID}({scoreboards[scoreboardID].name})");

            ScoreboardPlayerRow row = scoreboards[scoreboardID].CreateRow(0, scoreboards[scoreboardID].topRowPanel);
            row.rowType = ScoreboardPlayerRowType.Header;
            scoreboards[scoreboardID].topRowCanvas = row.GetComponent<Canvas>();

            row = scoreboards[scoreboardID].CreateRow(0, scoreboards[scoreboardID].parRowPanel);
            row.rowType = ScoreboardPlayerRowType.Par;
            scoreboards[scoreboardID].parRowCanvas = row.GetComponent<Canvas>();
        }

        for (int scoreboardID = 0; scoreboardID < scoreboards.Count; scoreboardID++)
        {
            if (scoreboards[scoreboardID] == null)
                continue;
            for (int playerID = 0; playerID < manager.numberOfPlayersToDisplay; playerID++)
            {
                if (showProgressBar && EditorUtility.DisplayCancelableProgressBar("Scoreboard Setup", $"Creating row {playerID} for scoreboard ID {scoreboardID}({scoreboards[scoreboardID].name})", (currentWork++ / totalWork)))
                {
                    EditorUtility.ClearProgressBar();
                    return;
                }

                ScoreboardPlayerRow row = scoreboards[scoreboardID].CreateRow(playerID);
                row.gameObject.SetActive(false);
                OpenPuttUtils.Log("ScoreboardBuildProcessor", $"Creating row {playerID} for scoreboard ID {scoreboardID}({scoreboards[scoreboardID].name})");
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

    public static bool ShouldBuildScoreboards(ScoreboardManager scoreboardManager, List<Scoreboard> allScoreboards)
    {
        int numberOfPlayersOnScoreboard = scoreboardManager.numberOfPlayersToDisplay;
        int numberOfColumnsOnScoreboard = scoreboardManager.NumberOfColumns;

        foreach (Scoreboard scoreboard in allScoreboards)
        {
            if (scoreboard.parRowCanvas == null || scoreboard.topRowCanvas == null)
            {
                return true;
            }

            ScoreboardPlayerRow parRow = scoreboard.parRowCanvas.GetComponent<ScoreboardPlayerRow>();
            ScoreboardPlayerRow topRow = scoreboard.topRowCanvas.GetComponent<ScoreboardPlayerRow>();

            if (parRow == null || parRow.columns.Length != numberOfColumnsOnScoreboard)
            {
                return true;
            }

            if (topRow == null || topRow.columns.Length != numberOfColumnsOnScoreboard)
            {
                return true;
            }

            Transform playerList = scoreboard.playerListCanvas.transform;

            if (playerList.childCount != numberOfPlayersOnScoreboard)
            {
                return true;
            }

            for (int i = 0; i < playerList.childCount; i++)
            {
                ScoreboardPlayerRow row = playerList.GetChild(i).GetComponent<ScoreboardPlayerRow>();
                if (row == null || row.columns.Length != numberOfColumnsOnScoreboard)
                {
                    return true;
                }
            }
        }

        return false;
    }
}
#endif