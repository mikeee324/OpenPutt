
using mikeee324.OpenPutt;
using System;
using TMPro;
using UdonSharp;
using UnityEngine;
using VRC.SDKBase;

[UdonBehaviourSyncMode(BehaviourSyncMode.None), DefaultExecutionOrder(101)]
public class ScoreboardPlayerColumn : UdonSharpBehaviour
{

    public ScoreboardPlayerRow scoreboardRow;
    public UnityEngine.UI.Image colBackground;
    public TextMeshProUGUI colText;

    private Scoreboard scoreboard => scoreboardRow.scoreboard;
    private ScoreboardManager scoreboardManager => scoreboard.manager;
    private int rowIndex => scoreboardRow.CurrentPosition;
    private bool isEvenRow => rowIndex % 2 == 0;
    private int columnIndex => transform.GetSiblingIndex();
    private OpenPutt openPutt => scoreboardManager.openPutt;
    private int col => columnIndex;

    private void UpdateNameColumn(PlayerManager player)
    {
        if (player == null || player.Owner == null || player.Owner.displayName == null)
            return;

        SetText(player.Owner.displayName);
        SetTextColour(scoreboardManager.text);
        SetBackgroundColour(isEvenRow ? scoreboardManager.nameBackground1 : scoreboardManager.nameBackground2);
    }

    private void UpdateTotalScoreColumn(PlayerManager player)
    {
        bool playerIsAbovePar = false;
        bool playerIsBelowPar = false;

        if (player == null)
        {
            SetText("-");
        }
        else
        {
            // Render the last column - This is usually the "Total" column
            bool finishedAllCourses = true;
            foreach (CourseState courseState in player.courseStates)
            {
                // TODO: Maybe count skipped courses as completed too?
                if (courseState != CourseState.Completed)
                {
                    finishedAllCourses = false;
                    break;
                }
            }

            if (scoreboardManager.SpeedGolfMode)
            {
                int currentPlayerTime = player.PlayerTotalTime;
                if (currentPlayerTime == 999999)
                {
                    SetText("-");
                    playerIsAbovePar = false;
                    playerIsBelowPar = false;
                }
                else
                {
                    int totalParTime = openPutt.TotalParTime;
                    SetText(TimeSpan.FromMilliseconds(currentPlayerTime).ToString(@"m\:ss"));
                    playerIsAbovePar = currentPlayerTime > 0 && currentPlayerTime > totalParTime;
                    playerIsBelowPar = finishedAllCourses && currentPlayerTime > 0 && currentPlayerTime < totalParTime;
                }
            }
            else
            {
                int currentPlayerScore = player.PlayerTotalScore;
                if (currentPlayerScore == 999999)
                {
                    SetText("-");
                    playerIsAbovePar = false;
                    playerIsBelowPar = false;
                }
                else
                {
                    int totalParScore = openPutt.TotalParScore;
                    SetText($"{currentPlayerScore}");
                    playerIsAbovePar = currentPlayerScore > 0 && currentPlayerScore > totalParScore;
                    playerIsBelowPar = finishedAllCourses && currentPlayerScore < totalParScore;
                }
            }
        }

        if (playerIsAbovePar)
        {
            SetTextColour(scoreboardManager.overParText);
            SetBackgroundColour(scoreboardManager.overParBackground);
        }
        else if (playerIsBelowPar)
        {
            SetTextColour(scoreboardManager.underParText);
            SetBackgroundColour(scoreboardManager.underParBackground);
        }
        else
        {
            SetTextColour(scoreboardManager.text);
            SetBackgroundColour(isEvenRow ? scoreboardManager.totalBackground1 : scoreboardManager.totalBackground2);
        }
    }

    private void UpdateScoreColumn(PlayerManager player)
    {
        Color newBGColour = isEvenRow ? scoreboardManager.scoreBackground1 : scoreboardManager.scoreBackground2;

        if (player == null)
        {
            SetText("-");
            SetTextColour(scoreboardManager.text);
        }
        else
        {
            if ((col - 1) < 0 || (col - 1) >= player.courseStates.Length)
                return;

            //  if (col - 1 < player.courseStates.Length)

            CourseState courseState = player.courseStates[col - 1];
            int holeScore = player.courseScores[col - 1];

            bool playerIsAbovePar;
            bool playerIsBelowPar;

            CourseManager course = openPutt.courses[col - 1];

            bool courseIsDrivingRange = openPutt != null && course != null && course.drivingRangeMode;

            if (courseIsDrivingRange)
            {
                playerIsAbovePar = false;
                playerIsBelowPar = false;

                if (courseState == CourseState.Playing)
                    SetText("-");
                else if (courseState == CourseState.Completed)
                    SetText($"{holeScore}m");
            }
            else if (scoreboardManager.SpeedGolfMode)
            {
                double timeOnThisCourse = player.courseTimes[col - 1];

                // If the player is playing this course right now, then the stored value is the time when they started the course
                if (courseState == CourseState.Playing)
                    timeOnThisCourse = Networking.GetServerTimeInMilliseconds() - timeOnThisCourse;

                SetText(TimeSpan.FromMilliseconds(timeOnThisCourse).ToString(@"m\:ss"));
                playerIsAbovePar = timeOnThisCourse > (course.parTimeMillis);
                playerIsBelowPar = courseState == CourseState.Completed && player.PlayerTotalTime > 0 && timeOnThisCourse < (course.parTimeMillis);
            }
            else
            {
                SetText($"{holeScore}");
                playerIsAbovePar = holeScore > 0 && holeScore > course.parScore;
                playerIsBelowPar = courseState == CourseState.Completed && holeScore < course.parScore;
            }

            if (courseState == CourseState.Playing)
            {
                SetTextColour(scoreboardManager.currentCourseText);
                newBGColour = scoreboardManager.currentCourseBackground;
                // If we are in slow update mode we can't display a score as it will not be up to date
                if (player.Owner != Networking.LocalPlayer && openPutt.playerSyncType != PlayerSyncType.All)
                    SetText("-");
            }
            else if (courseState == CourseState.NotStarted)
            {
                SetText("-");
            }
            else if (playerIsAbovePar)
            {
                SetTextColour(scoreboardManager.overParText);
                newBGColour = scoreboardManager.overParBackground;
            }
            else if (playerIsBelowPar)
            {
                SetTextColour(scoreboardManager.underParText);
                newBGColour = scoreboardManager.underParBackground;
            }
            else
            {
                SetTextColour(scoreboardManager.text);
            }
        }

        SetBackgroundColour(newBGColour);
    }

    public void Refresh(PlayerManager player)
    {
        switch (scoreboardRow.rowType)
        {
            case ScoreboardPlayerRowType.Normal:
                if (player != null)
                {
                    if (columnIndex == 0)
                    {
                        UpdateNameColumn(player);
                    }
                    else if (columnIndex == scoreboardRow.NumberOfColumns - 1)
                    {
                        UpdateTotalScoreColumn(player);
                    }
                    else if (columnIndex > 0 || columnIndex < scoreboardRow.NumberOfColumns - 2)
                    {
                        UpdateScoreColumn(player);
                    }
                }
                break;
            case ScoreboardPlayerRowType.Par:
                if (openPutt != null && openPutt.courses.Length > 0)
                {
                    if (columnIndex == 0)
                    {
                        SetText("Par");
                    }
                    else if (columnIndex == scoreboardRow.NumberOfColumns - 1)
                    {
                        if (scoreboardManager.SpeedGolfMode)
                            SetText(TimeSpan.FromMilliseconds(openPutt.TotalParTime).ToString(@"m\:ss"));
                        else
                            SetText($"{openPutt.TotalParScore}");
                    }
                    else if (columnIndex > 0 || columnIndex < scoreboardRow.NumberOfColumns - 2)
                    {
                        CourseManager course = openPutt.courses[columnIndex - 1];
                        if (scoreboardManager.SpeedGolfMode)
                            SetText(TimeSpan.FromMilliseconds(course.parTime).ToString(@"m\:ss"));
                        else
                            SetText($"{course.parScore}");
                    }
                    SetTextColour(scoreboardManager.text);
                    SetBackgroundColour(scoreboardManager.nameBackground2);
                }
                break;
            case ScoreboardPlayerRowType.Header:
                if (openPutt != null && openPutt.courses.Length > 0)
                {
                    if (columnIndex == 0)
                    {
                        SetText("#");
                    }
                    else if (columnIndex == scoreboardRow.NumberOfColumns - 1)
                    {
                        SetText("");
                    }
                    else if (columnIndex > 0 || columnIndex < scoreboardRow.NumberOfColumns - 2)
                    {
                        CourseManager course = openPutt.courses[columnIndex - 1];

                        string newText = $"{col}";
                        if (course != null && course.scoreboardShortName != null && course.scoreboardShortName.Length > 0)
                            newText = course.scoreboardShortName;

                        SetText(newText);
                    }
                    SetTextColour(scoreboardManager.text);
                    SetBackgroundColour(scoreboardManager.nameBackground2);
                }
                break;
        }
    }


    public void SetBackgroundColour(Color colour)
    {
        if (colBackground.color != colour)
        {
            colBackground.color = colour;
        }
    }

    public void SetText(string newText)
    {
        if (colText.text != newText)
        {
            colText.text = newText;
        }
    }

    public void SetTextColour(Color colour)
    {
        if (colText.color != colour)
        {
            colText.color = colour;
        }
    }
}
