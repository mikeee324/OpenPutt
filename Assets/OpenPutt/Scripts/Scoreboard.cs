using System;
using TMPro;
using UdonSharp;
using UnityEngine;
using UnityEngine.UI;
using VRC.SDK3.Components;
using VRC.SDKBase;

namespace mikeee324.OpenPutt
{
    public enum ScoreboardVisibility
    {
        AlwaysVisible,
        NearbyAndCourseFinished,
        NearbyOnly,
        Hidden,
    }

    public enum ScoreboardView
    {
        Scoreboard,
        Info,
        Settings
    }

    [UdonBehaviourSyncMode(BehaviourSyncMode.None)]
    public class Scoreboard : UdonSharpBehaviour
    {
        [Header("This is a scoreboard that you can drag into the scene to display player scores. Make sure the ScoreboardManager has a reference to all scoreboards!")]
        [Header("External References")]
        [Tooltip("This is needed to receive refresh events and give access to the player info")]
        public ScoreboardManager manager;

        [Header("Internal References (All are required to be set)")]
        public RectTransform scoreboardHeader;
        public RectTransform settingsPanelHeader;
        public RectTransform settingsPanel;
        public RectTransform infoPanelHeader;
        public RectTransform infoPanel;
        public RectTransform parRowPanel;
        public RectTransform topRowPanel;
        public RectTransform playerListCanvas;
        public GameObject rowPrefab;
        public GameObject columnPrefab;
        public Image scoreboardTabBackground;
        public Image scoreboardTimerTabBackground;
        public Image infoTabBackground;
        public Image settingsTabBackground;

        public Slider clubPowerSlider;
        public TextMeshProUGUI clubPowerValueLabel;
        public Slider sfxVolumeSlider;
        public TextMeshProUGUI sfxVolumeValueLabel;
        public Slider bgmVolumeSlider;
        public TextMeshProUGUI bgmVolumeValueLabel;
        public Slider worldVolumeSlider;
        public TextMeshProUGUI worldVolumeValueLabel;
        public Slider ballRColorSlider;
        public Slider ballGColorSlider;
        public Slider ballBColorSlider;
        public Image ballColorPreview;

        public Image verticalHitsCheckbox;
        public Image isPlayingCheckbox;
        public Image leftHandModeCheckbox;
        public Image enableBigShaftCheckbox;
        public Image courseReplaysCheckbox;
        public Image showAllPlayersCheckbox;
        public Material checkboxOn;
        public Material checkboxOff;
        public Button resetButton;
        public Button resetConfirmButton;
        public Button resetCancelButton;

        [Space, Header("Settings")]
        public ScoreboardVisibility scoreboardVisiblility = ScoreboardVisibility.AlwaysVisible;
        [Tooltip("How close the player needs to be to this scoreboard if one of the 'nearby' settings are used above")]
        public float nearbyMaxRadius = 10f;
        [Tooltip("The maximum number of players this scoreobard can display (0=Show all players, -1=Auto)")]
        private int numberOfPlayersToDisplay = -1;
        [Tooltip("Defines which course this scoreboard is attached to. Used to toggle visibility when the player finishes a course")]
        public int attachedToCourse = -1;
        [Tooltip("Toggles whether to hide people who haven't started playing yet (On by default)")]
        public bool hideInactivePlayers = true;
        [Header("Background Colours")]
        public Color nameBackground1 = Color.black;
        public Color scoreBackground1 = Color.black;
        public Color totalBackground1 = Color.black;
        [Space]
        public Color nameBackground2 = Color.black;
        public Color scoreBackground2 = Color.black;
        public Color totalBackground2 = Color.black;
        [Space]
        public Color currentCourseBackground = Color.black;
        public Color underParBackground = Color.green;
        public Color overParBackground = Color.red;
        [Header("Text Colours")]
        public Color text = Color.white;
        public Color currentCourseText = Color.white;
        public Color underParText = Color.white;
        public Color overParText = Color.white;

        [Space, Header("Sizing")]
        public float nameColumnWidth = 0.35f;
        public float totalColumnWidth = 0.2f;
        public float columnPadding = 0.005f;
        public float rowPadding = 0.01f;
        public float rowHeight = 0.15f;

        private bool initializedUI = false;
        private bool refreshWaitingForInit = false;
        private float speedrunUpdateTimer = -1f;
        private ScoreboardView _currentScoreboardView = ScoreboardView.Settings;
        public ScoreboardView CurrentScoreboardView
        {
            get => _currentScoreboardView;
            set
            {
                if (_currentScoreboardView != value)
                {
                    speedrunUpdateTimer = -1f;
                    switch (value)
                    {
                        case ScoreboardView.Scoreboard:
                            settingsPanel.gameObject.SetActive(false);
                            settingsPanelHeader.gameObject.SetActive(false);
                            infoPanel.gameObject.SetActive(false);
                            infoPanelHeader.gameObject.SetActive(false);
                            topRowPanel.gameObject.SetActive(true);
                            parRowPanel.gameObject.SetActive(true);
                            playerListCanvas.gameObject.SetActive(true);
                            if (manager != null && manager.speedGolfMode)
                                speedrunUpdateTimer = 0;
                            break;
                        case ScoreboardView.Info:
                            settingsPanel.gameObject.SetActive(false);
                            settingsPanelHeader.gameObject.SetActive(false);
                            infoPanel.gameObject.SetActive(true);
                            infoPanelHeader.gameObject.SetActive(true);
                            topRowPanel.gameObject.SetActive(false);
                            parRowPanel.gameObject.SetActive(false);
                            playerListCanvas.gameObject.SetActive(false);
                            break;
                        case ScoreboardView.Settings:
                            settingsPanel.gameObject.SetActive(true);
                            settingsPanelHeader.gameObject.SetActive(true);
                            infoPanel.gameObject.SetActive(false);
                            infoPanelHeader.gameObject.SetActive(false);
                            topRowPanel.gameObject.SetActive(false);
                            parRowPanel.gameObject.SetActive(false);
                            playerListCanvas.gameObject.SetActive(false);

                            RefreshSettingsMenu();

                            OnResetCancel();
                            break;
                    }
                }
                _currentScoreboardView = value;

                // Update Tab Background Colours
                Color defaultBackground = GetComponent<Image>().color;
                scoreboardTabBackground.color = CurrentScoreboardView == ScoreboardView.Scoreboard && (manager == null || !manager.speedGolfMode) ? currentCourseBackground : defaultBackground;
                scoreboardTimerTabBackground.color = CurrentScoreboardView == ScoreboardView.Scoreboard && (manager != null && manager.speedGolfMode) ? currentCourseBackground : defaultBackground;
                infoTabBackground.color = CurrentScoreboardView == ScoreboardView.Info ? currentCourseBackground : defaultBackground;
                settingsTabBackground.color = CurrentScoreboardView == ScoreboardView.Settings ? currentCourseBackground : defaultBackground;
            }
        }
        private int NumberOfPlayersToDisplay
        {
            get
            {
                if (manager != null)
                    return manager.showAllPlayers ? 0 : numberOfPlayersToDisplay;

                return numberOfPlayersToDisplay;
            }
        }

        private PlayerManager[] VisibleScoreboardPlayers
        {
            get
            {
                PlayerManager[] allPlayers = manager.openPutt.GetPlayers(noSort: false, hideInactivePlayers);

                if (allPlayers == null)
                    return new PlayerManager[0];

                if (NumberOfPlayersToDisplay <= 0)
                    return allPlayers;

                // Default to the top of the list
                int myPosition = 0;
                for (int i = 0; i < allPlayers.Length; i++)
                {
                    if (allPlayers[i].Owner == Networking.LocalPlayer)
                    {
                        // Found the local player
                        myPosition = i;
                        break;
                    }
                }

                // Try to work out where we need to slice the array
                int startPos = myPosition - (int)Math.Ceiling(NumberOfPlayersToDisplay / 2d);
                int endPos = myPosition + (int)Math.Floor(NumberOfPlayersToDisplay / 2d);

                // If the player is near the top or bottom shift the positions so we still fill the board up
                startPos -= endPos >= allPlayers.Length ? endPos - allPlayers.Length : 0;
                endPos += startPos < 0 ? 0 - startPos : 0;

                // Make sure we never go out of bounds
                if (startPos < 0)
                    startPos = 0;
                if (endPos >= allPlayers.Length)
                    endPos = allPlayers.Length;

                // Create the array for the scoreboard
                PlayerManager[] players = new PlayerManager[endPos - startPos];
                Array.Copy(allPlayers, startPos, players, 0, endPos - startPos);

                return players;
            }
        }

        void Start()
        {
            if (manager == null || manager.openPutt == null)
            {
                Utils.LogError(this, "Missing references to manager or OpenPutt! Disabling this scoreboard.");
                gameObject.SetActive(false);
                return;
            }

            CurrentScoreboardView = ScoreboardView.Scoreboard;

            // Make sure that the scoreboard has basically no thickness so the laser pointer works properly
            RectTransform rectTransform = GetComponent<RectTransform>();
            if (rectTransform != null)
            {
                Vector3 scoreboardScale = rectTransform.localScale;
                if (scoreboardScale.z != 0.01f)
                    rectTransform.localScale = new Vector3(scoreboardScale.x, scoreboardScale.y, 0.01f);
            }

            CreateAllRows();
        }

        private void Update()
        {
            if (CurrentScoreboardView == ScoreboardView.Settings)
            {
                // Update setting preview/value UI elements if the settings panel is visible
                UpdateBallColorPreview();

                clubPowerValueLabel.text = String.Format("{0:F2}x", clubPowerSlider.value);
                sfxVolumeValueLabel.text = String.Format("{0:P0}", sfxVolumeSlider.value);
                bgmVolumeValueLabel.text = String.Format("{0:P0}", bgmVolumeSlider.value);
                worldVolumeValueLabel.text = String.Format("{0:P0}", worldVolumeSlider.value);
            }
            else if (speedrunUpdateTimer != -1f)
            {
                speedrunUpdateTimer += Time.deltaTime;
                if (speedrunUpdateTimer > manager.maxRefreshInterval)
                {
                    RefreshScoreboard();
                    speedrunUpdateTimer = 0f;
                }
            }
        }

        public void RefreshScoreboard(bool shouldScrollToLocalPlayer = false)
        {
            if (!initializedUI)
            {
                Utils.Log(this, "Can't refresh scoreboard as UI hasn't finished initializing yet!");
                refreshWaitingForInit = true;
                return;
            }

            float totalheightOfScrollViewport = 1f;

            RectTransform scoreboardPanel = GetComponent<RectTransform>();
            if (scoreboardPanel != null && scoreboardHeader != null && topRowPanel != null && parRowPanel != null)
            {
                // Get the total height of the player canvas view
                totalheightOfScrollViewport = scoreboardPanel.sizeDelta.y - scoreboardHeader.sizeDelta.y - topRowPanel.sizeDelta.y - parRowPanel.sizeDelta.y;
            }

            // Update size of canvas so scrollviews work
            float totalHeightOfAllRows = (rowHeight + rowPadding) * VisibleScoreboardPlayers.Length;
            playerListCanvas.GetComponent<RectTransform>().sizeDelta = new Vector2(playerListCanvas.GetComponent<RectTransform>().sizeDelta.x, totalHeightOfAllRows);

            if (numberOfPlayersToDisplay == -1)
            {
                // Default to showing all players in case we can't work out how many to display
                numberOfPlayersToDisplay = 0;

                if (scoreboardPanel != null && scoreboardHeader != null && topRowPanel != null && parRowPanel != null)
                {
                    // Work out how many rows we can fit in that height
                    numberOfPlayersToDisplay = (int)Math.Floor(totalheightOfScrollViewport / (rowHeight + rowPadding));
                    if ((rowHeight + rowPadding) * numberOfPlayersToDisplay > totalheightOfScrollViewport)
                        numberOfPlayersToDisplay--;

                    Utils.Log(this, $"This scoreboard can display {numberOfPlayersToDisplay} players at once. ViewportHeight:{totalheightOfScrollViewport} Height:{(rowHeight + rowPadding) * numberOfPlayersToDisplay} RowHeight:{rowHeight + rowPadding}");
                }
            }

            PlayerManager[] activePlayers = VisibleScoreboardPlayers;

            if (playerListCanvas.transform.childCount != manager.openPutt.MaxPlayerCount)
            {
                Utils.LogWarning(this, $"Scoreboard tried to refresh with an incorrect number of player rows! Current={playerListCanvas.transform.childCount} Target={manager.openPutt.MaxPlayerCount}");
                CreateAllRows();
            }

            // Toggle scrollview if the players list is taller than the viewport
            ScrollRect scrollRect = playerListCanvas.transform.parent.GetComponent<ScrollRect>();
            scrollRect.enabled = (rowHeight + rowPadding) * activePlayers.Length > totalheightOfScrollViewport;

            // If scrollview is disabled scroll to top
            if (!scrollRect.enabled)
                SnapTo(null);

            // Toggle raycast target depending on scrollRect state
            Image scrollPanel = playerListCanvas.transform.parent.GetComponent<Image>();
            if (scrollPanel != null)
                scrollPanel.raycastTarget = scrollRect.enabled;

            UpdateParRow();
            for (int position = 0; position < manager.openPutt.MaxPlayerCount; position++)
            {
                if (position < activePlayers.Length && activePlayers[position].Owner != null)
                {
                    UpdatePlayerRow(activePlayers[position], position);

                    // If this is the local players row, and we need to scroll to their position
                    if (scrollRect != null && scrollRect.enabled && shouldScrollToLocalPlayer && activePlayers[position].Owner == Networking.LocalPlayer)
                    {
                        // Find the row transform
                        RectTransform newRow = playerListCanvas.GetChild(position).GetComponent<RectTransform>();
                        if (newRow != null)
                            SnapTo(newRow); // Snap the ScrollView to this position so the local player is in view
                    }
                }
                else
                {
                    UpdatePlayerRow(null, position);
                }
            }

            if (CurrentScoreboardView == ScoreboardView.Settings)
                RefreshSettingsMenu();

            if (CurrentScoreboardView == ScoreboardView.Scoreboard)
                speedrunUpdateTimer = activePlayers.Length > 0 && manager != null && manager.speedGolfMode ? 0 : -1;
        }

        private void UpdatePlayerRow(PlayerManager player, int position)
        {
            if (position >= playerListCanvas.transform.childCount)
            {
                Utils.LogError(this, $"Updating row at position {position} for player {player.Owner.displayName} cannot be performed as the player canvas only has {playerListCanvas.transform.childCount} children!");
                return;
            }

            GameObject newRow = playerListCanvas.GetChild(position).gameObject;
            newRow.SetActive(player != null);

            if (player == null) return;

            RectTransform rowRect = newRow.GetComponent<RectTransform>();

            if (rowRect.transform.childCount != manager.openPutt.courses.Length + 2)
            {
                Utils.Log(this, $"This row({rowRect.name}-{position}) appears to have the incorrect number of columns on it! Should have {manager.openPutt.courses.Length + 2} but this row has {rowRect.transform.childCount}");
                return;
            }

            // Generate data for all columns
            for (int col = 0; col < rowRect.transform.childCount; col++)
            {
                RectTransform column = rowRect.GetChild(col).GetComponent<RectTransform>();
                UnityEngine.UI.Image columnBackground = column.GetComponent<UnityEngine.UI.Image>();
                TextMeshProUGUI tmp = rowRect.GetChild(col).transform.GetChild(0).GetComponent<TextMeshProUGUI>();
                if (tmp != null)
                {
                    if (col == 0)
                    {
                        tmp.text = player.Owner.displayName;
                        tmp.color = text;
                        columnBackground.color = position % 2 == 0 ? nameBackground1 : nameBackground2;
                    }
                    else if (col == rowRect.transform.childCount - 1)
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

                        bool playerIsAbovePar = false;
                        bool playerIsBelowPar = false;

                        if (manager.speedGolfMode)
                        {
                            tmp.text = TimeSpan.FromMilliseconds(player.PlayerTotalTime).ToString(@"m\:ss");
                            playerIsAbovePar = player.PlayerTotalTime > 0 && player.PlayerTotalTime > (manager.openPutt.TotalParTime);
                            playerIsBelowPar = finishedAllCourses && player.PlayerTotalTime > 0 && player.PlayerTotalTime < (manager.openPutt.TotalParTime);
                        }
                        else
                        {
                            tmp.text = $"{player.PlayerTotalScore}";
                            playerIsAbovePar = player.PlayerTotalScore > 0 && player.PlayerTotalScore > manager.openPutt.TotalParScore;
                            playerIsBelowPar = finishedAllCourses && player.PlayerTotalScore > 0 && player.PlayerTotalScore < manager.openPutt.TotalParScore;
                        }

                        if (playerIsAbovePar)
                        {
                            tmp.color = overParText;
                            columnBackground.color = overParBackground;
                        }
                        else if (playerIsBelowPar)
                        {
                            tmp.color = underParText;
                            columnBackground.color = underParBackground;
                        }
                        else
                        {
                            tmp.color = text;
                            columnBackground.color = position % 2 == 0 ? totalBackground1 : totalBackground2;
                        }
                    }
                    else if (col > 0 || col < rowRect.transform.childCount - 1)
                    {
                        if (col - 1 < player.courseStates.Length)
                        {
                            CourseState courseState = player.courseStates[col - 1];
                            int holeScore = player.courseScores[col - 1];

                            columnBackground.color = position % 2 == 0 ? scoreBackground1 : scoreBackground2;

                            bool playerIsAbovePar = false;
                            bool playerIsBelowPar = false;

                            if (manager.speedGolfMode)
                            {
                                double timeOnThisCourse = player.courseTimes[col - 1];

                                // If the player is playing this course right now, then the stored value is the time when they started the course
                                if (courseState == CourseState.Playing)
                                    timeOnThisCourse = Networking.GetServerTimeInMilliseconds() - timeOnThisCourse;

                                tmp.text = TimeSpan.FromMilliseconds(timeOnThisCourse).ToString(@"m\:ss");
                                playerIsAbovePar = timeOnThisCourse > (manager.openPutt.courses[col - 1].parTimeMillis);
                                playerIsBelowPar = courseState == CourseState.Completed && player.PlayerTotalTime > 0 && timeOnThisCourse < (manager.openPutt.courses[col - 1].parTimeMillis);
                            }
                            else
                            {
                                tmp.text = $"{holeScore}";
                                playerIsAbovePar = holeScore > 0 && holeScore > manager.openPutt.courses[col - 1].parScore;
                                playerIsBelowPar = courseState == CourseState.Completed && holeScore > 0 && holeScore < manager.openPutt.courses[col - 1].parScore;
                            }

                            if (courseState == CourseState.Playing)
                            {
                                tmp.color = currentCourseText;
                                columnBackground.color = currentCourseBackground;
                                // If we are in slow update mode we can't display a score as it will not be up to date
                                if (player.Owner != Networking.LocalPlayer && manager.openPutt.playerSyncType != PlayerSyncType.All)
                                    tmp.text = "-";
                            }
                            else if (courseState == CourseState.NotStarted)
                            {
                                tmp.text = "-";
                            }
                            else if (playerIsAbovePar)
                            {
                                tmp.color = overParText;
                                columnBackground.color = overParBackground;
                            }
                            else if (playerIsBelowPar)
                            {
                                tmp.color = underParText;
                                columnBackground.color = underParBackground;
                            }
                            else
                            {
                                tmp.color = text;
                            }
                        }
                    }
                }
            }
        }

        private void UpdateParRow()
        {
            RectTransform rowRect = parRowPanel.GetChild(0).gameObject.GetComponent<RectTransform>();
            rowRect.gameObject.SetActive(true);

            int totalPar = 0;

            // Generate data for all columns
            int columnCount = manager.openPutt.courses.Length + 2;
            for (int col = 0; col < columnCount; col++)
            {
                CourseManager course = null;
                int parScore = 0;
                if (col >= 1 && col < columnCount - 1)
                {
                    course = manager.openPutt.courses[col - 1];
                    if (manager.speedGolfMode)
                    {
                        parScore = course.parTimeMillis;
                        totalPar += course.parTimeMillis;
                    }
                    else
                    {
                        parScore = course.parScore;
                        totalPar += course.parScore;
                    }
                }

                rowRect.GetChild(col).GetComponent<UnityEngine.UI.Image>().color = nameBackground2;

                TextMeshProUGUI tmp = rowRect.GetChild(col).transform.GetChild(0).GetComponent<TextMeshProUGUI>();
                if (tmp != null)
                {
                    if (col == 0)
                    {
                        tmp.text = "Par";
                    }
                    else if (col == columnCount - 1)
                    {
                        if (manager.speedGolfMode)
                            tmp.text = TimeSpan.FromMilliseconds(totalPar).ToString(@"m\:ss");
                        else
                            tmp.text = $"{totalPar}";
                    }
                    else
                    {
                        if (manager.speedGolfMode)
                            tmp.text = TimeSpan.FromMilliseconds(parScore).ToString(@"m\:ss");
                        else
                            tmp.text = $"{parScore}";
                    }

                    tmp.color = text;
                }
            }
        }

        private void UpdateTopRow()
        {
            RectTransform rowRect = topRowPanel.GetChild(0).gameObject.GetComponent<RectTransform>();
            rowRect.gameObject.SetActive(true);

            int totalPar = 0;

            // Generate data for all columns
            int columnCount = manager.openPutt.courses.Length + 2;
            for (int col = 0; col < columnCount; col++)
            {
                CourseManager course = null;
                int parScore = 0;
                if (col >= 1 && col < columnCount - 1)
                {
                    course = manager.openPutt.courses[col - 1];

                    parScore = course.parScore;
                    totalPar += course.parScore;
                }

                rowRect.GetChild(col).GetComponent<UnityEngine.UI.Image>().color = nameBackground2;

                TextMeshProUGUI tmp = rowRect.GetChild(col).transform.GetChild(0).GetComponent<TextMeshProUGUI>();
                if (tmp != null)
                {
                    if (col == 0)
                    {
                        tmp.text = "#";
                    }
                    else if (col == columnCount - 1)
                    {
                        tmp.text = $"";
                    }
                    else
                    {
                        tmp.text = $"{col}";
                    }

                    tmp.color = text;
                }
            }
        }

        private void CreateAllRows()
        {
            initializedUI = false;

            for (int i = 0; i < playerListCanvas.transform.childCount; i++)
                GameObject.Destroy(playerListCanvas.GetChild(i));

            CreateRow(0, topRowPanel);
            UpdateTopRow();
            CreateRow(0, parRowPanel);
            UpdateParRow();

            for (int position = 0; position < manager.openPutt.MaxPlayerCount; position++)
                CreateRow(position, playerListCanvas);

            initializedUI = true;

            if (refreshWaitingForInit)
            {
                RefreshScoreboard();
                refreshWaitingForInit = false;
            }
        }

        private void CreateRow(int position, RectTransform parent)
        {
            int scoreboardColumnCount = manager.openPutt.courses.Length + 2; // + Name + Total
            RectTransform newRow = Instantiate(rowPrefab).GetComponent<RectTransform>();
            newRow.name = $"Player {position}";

            RectTransform scoreboardPanel = GetComponent<RectTransform>();

            float widthForEachHole = scoreboardPanel.sizeDelta.x - nameColumnWidth - totalColumnWidth - (columnPadding * (scoreboardColumnCount - 1));
            widthForEachHole = widthForEachHole / (scoreboardColumnCount - 2);

            float columnXOffset = 0f;
            for (int col = 0; col < scoreboardColumnCount; col++)
            {
                RectTransform rect = Instantiate(columnPrefab).GetComponent<RectTransform>();
                rect.SetParent(newRow, false);
                rect.anchoredPosition = new Vector3(columnXOffset, 0f);

                if (col == 0)
                {
                    rect.sizeDelta = new Vector2(nameColumnWidth, rowHeight);
                }
                else if (col == scoreboardColumnCount - 1)
                {
                    rect.sizeDelta = new Vector2(totalColumnWidth, rowHeight);
                }
                else
                {
                    rect.sizeDelta = new Vector2(widthForEachHole, rowHeight);
                }

                rect.GetChild(0).GetComponent<RectTransform>().sizeDelta = rect.sizeDelta;

                columnXOffset += rect.sizeDelta.x + columnPadding;
            }

            // Position this row in the list
            newRow.SetParent(parent, false);
            newRow.anchoredPosition = new Vector3(0f, -(rowHeight + rowPadding) * position);

            newRow.gameObject.SetActive(false);
        }

        /// <summary>
        /// Updates all UI elements on the settings screen so they match the current state of the game
        /// </summary>
        public void RefreshSettingsMenu()
        {
            if (manager == null || manager.openPutt == null || manager.openPutt.LocalPlayerManager == null)
                return;

            PlayerManager playerManager = manager.openPutt.LocalPlayerManager;

            clubPowerSlider.value = playerManager.golfClub.forceMultiplier;
            clubPowerValueLabel.text = String.Format("{0:F2}x", clubPowerSlider.value);

            sfxVolumeSlider.value = playerManager.openPutt.SFXController.Volume;
            sfxVolumeValueLabel.text = String.Format("{0:P0}", sfxVolumeSlider.value);

            // Just use the first audio source volume
            foreach (AudioSource audioSource in manager.openPutt.BGMAudioSources)
            {
                if (audioSource == null) continue;
                bgmVolumeSlider.value = audioSource.volume;
                bgmVolumeValueLabel.text = String.Format("{0:P0}", bgmVolumeSlider.value);
                break;
            }
            bgmVolumeSlider.transform.parent.gameObject.SetActive(manager.openPutt.BGMAudioSources.Length > 0);

            // Just use the first audio source volume
            foreach (AudioSource audioSource in manager.openPutt.WorldAudioSources)
            {
                if (audioSource == null) continue;
                worldVolumeSlider.value = audioSource.volume;
                worldVolumeValueLabel.text = String.Format("{0:P0}", worldVolumeSlider.value);
                break;
            }
            worldVolumeSlider.transform.parent.gameObject.SetActive(manager.openPutt.WorldAudioSources.Length > 0);

            Color color = playerManager.BallColor;

            ballRColorSlider.value = color.r;
            ballGColorSlider.value = color.g;
            ballBColorSlider.value = color.b;

            ballColorPreview.color = color;

            courseReplaysCheckbox.material = manager.openPutt.replayableCourses ? checkboxOn : checkboxOff;

            verticalHitsCheckbox.material = playerManager.openPutt.enableVerticalHits ? checkboxOn : checkboxOff;
            isPlayingCheckbox.material = playerManager.isPlaying ? checkboxOff : checkboxOn;
            leftHandModeCheckbox.material = playerManager.IsInLeftHandedMode ? checkboxOn : checkboxOff;
            enableBigShaftCheckbox.material = playerManager.golfClub.enableBigShaft ? checkboxOn : checkboxOff;
            showAllPlayersCheckbox.material = manager.showAllPlayers ? checkboxOn : checkboxOff;
        }

        public void UpdateBallColorPreview()
        {
            if (manager == null || manager.openPutt == null || manager.openPutt.LocalPlayerManager == null)
                return;

            PlayerManager playerManager = manager.openPutt.LocalPlayerManager;
            ballColorPreview.color = playerManager.BallColor;
        }

        public void ToggleAllPlayers()
        {
            if (manager != null)
            {
                manager.showAllPlayers = !manager.showAllPlayers;
                manager.RequestRefresh();
            }
        }

        public void OnClubPowerChanged()
        {
            if (manager == null || manager.openPutt == null || manager.openPutt.LocalPlayerManager == null)
                return;

            PlayerManager playerManager = manager.openPutt.LocalPlayerManager;
            playerManager.golfClub.forceMultiplier = clubPowerSlider.value;

            manager.RequestRefresh();
        }

        public void OnClubPowerReset()
        {
            if (manager == null || manager.openPutt == null || manager.openPutt.LocalPlayerManager == null)
                return;

            PlayerManager playerManager = manager.openPutt.LocalPlayerManager;
            playerManager.golfClub.forceMultiplier = 1f;

            RefreshSettingsMenu();

            manager.RequestRefresh();
        }

        public void OnSFXVolumeChanged()
        {
            if (manager == null || manager.openPutt == null || manager.openPutt.LocalPlayerManager == null)
                return;

            manager.openPutt.SFXController.Volume = sfxVolumeSlider.value;

            manager.RequestRefresh();
        }

        public void OnSFXVolumeReset()
        {
            if (manager == null || manager.openPutt == null || manager.openPutt.LocalPlayerManager == null)
                return;

            manager.openPutt.SFXController.Volume = 1f;

            RefreshSettingsMenu();

            manager.RequestRefresh();
        }

        public void OnBGMVolumeChanged()
        {
            if (manager == null || manager.openPutt == null || manager.openPutt.LocalPlayerManager == null)
                return;

            foreach (AudioSource audioSource in manager.openPutt.BGMAudioSources)
                audioSource.volume = bgmVolumeSlider.value;

            manager.RequestRefresh();
        }

        public void OnBGMVolumeReset()
        {
            if (manager == null || manager.openPutt == null || manager.openPutt.LocalPlayerManager == null)
                return;

            foreach (AudioSource audioSource in manager.openPutt.BGMAudioSources)
                audioSource.volume = 1f;

            RefreshSettingsMenu();

            manager.RequestRefresh();
        }

        public void OnWorldVolumeChanged()
        {
            if (manager == null || manager.openPutt == null || manager.openPutt.LocalPlayerManager == null)
                return;

            foreach (AudioSource audioSource in manager.openPutt.WorldAudioSources)
                audioSource.volume = worldVolumeSlider.value;

            manager.RequestRefresh();
        }

        public void OnWorldVolumeReset()
        {
            if (manager == null || manager.openPutt == null || manager.openPutt.LocalPlayerManager == null)
                return;

            foreach (AudioSource audioSource in manager.openPutt.WorldAudioSources)
                audioSource.volume = 1f;

            RefreshSettingsMenu();

            manager.RequestRefresh();
        }

        public void OnBallColorChanged()
        {
            if (manager == null || manager.openPutt == null || manager.openPutt.LocalPlayerManager == null)
                return;

            PlayerManager playerManager = manager.openPutt.LocalPlayerManager;

            // Set the new ball colour
            playerManager.BallColor = new Color(ballRColorSlider.value, ballGColorSlider.value, ballBColorSlider.value);

            manager.RequestRefresh();

            // Tell other players about the change straight away if possible
            if (manager.openPutt.playerSyncType < PlayerSyncType.FinishOnly)
                playerManager.RequestSerialization();
        }

        public void OnBallColorReset()
        {
            if (manager == null || manager.openPutt == null || manager.openPutt.LocalPlayerManager == null)
                return;

            PlayerManager playerManager = manager.openPutt.LocalPlayerManager;

            playerManager.BallColor = new Color(UnityEngine.Random.Range(0, 1f), UnityEngine.Random.Range(0, 1f), UnityEngine.Random.Range(0, 1f));

            RefreshSettingsMenu();

            manager.RequestRefresh();

            // Tell other players about the change straight away if possible
            if (manager.openPutt.playerSyncType < PlayerSyncType.FinishOnly)
                playerManager.RequestSerialization();
        }

        public void OnToggleVerticalHits()
        {
            if (manager == null || manager.openPutt == null || manager.openPutt.LocalPlayerManager == null)
                return;

            if (Networking.LocalPlayer.isMaster)
            {
                PlayerManager playerManager = manager.openPutt.LocalPlayerManager;

                playerManager.openPutt.enableVerticalHits = !playerManager.openPutt.enableVerticalHits;

                manager.openPutt.RequestSerialization();

                RefreshSettingsMenu();

                manager.RequestRefresh();
            }
        }

        public void OnTogglePlayerManager()
        {
            if (manager == null || manager.openPutt == null || manager.openPutt.LocalPlayerManager == null)
                return;

            PlayerManager playerManager = manager.openPutt.LocalPlayerManager;
            playerManager.isPlaying = !playerManager.isPlaying;

            if (playerManager.isPlaying)
            {
                playerManager.openPutt.leftShoulderPickup.gameObject.SetActive(true);
                playerManager.openPutt.rightShoulderPickup.gameObject.SetActive(true);
            }
            else
            {
                // Drop the shoulder objects and set them back to Vector3.zero
                GameObject attachedObject = playerManager.openPutt.leftShoulderPickup.ObjectToAttach;
                VRCPickup pickup = null;
                if (attachedObject != null)
                {
                    pickup = attachedObject.GetComponent<VRCPickup>();
                    if (pickup != null)
                        pickup.Drop();

                    attachedObject.transform.localPosition = Vector3.zero;
                }
                attachedObject = playerManager.openPutt.rightShoulderPickup.ObjectToAttach;
                if (attachedObject != null)
                {
                    pickup = attachedObject.GetComponent<VRCPickup>();
                    if (pickup != null)
                        pickup.Drop();

                    attachedObject.transform.localPosition = Vector3.zero;
                }

                //Drop the BodyMountedObjects
                pickup = playerManager.openPutt.leftShoulderPickup.gameObject.GetComponent<VRCPickup>();
                if (pickup != null)
                    pickup.Drop();
                pickup = playerManager.openPutt.rightShoulderPickup.gameObject.GetComponent<VRCPickup>();
                if (pickup != null)
                    pickup.Drop();

                // Disable the pickups
                playerManager.openPutt.leftShoulderPickup.gameObject.SetActive(false);
                playerManager.openPutt.rightShoulderPickup.gameObject.SetActive(false);
            }

            RefreshSettingsMenu();

            manager.RequestRefresh();
        }

        public void OnToggleUnlimitedShaftSize()
        {
            if (manager == null || manager.openPutt == null || manager.openPutt.LocalPlayerManager == null)
                return;

            PlayerManager playerManager = manager.openPutt.LocalPlayerManager;
            playerManager.golfClub.enableBigShaft = !playerManager.golfClub.enableBigShaft;

            RefreshSettingsMenu();

            manager.RequestRefresh();
        }

        public void OnToggleLeftHand()
        {
            if (manager == null || manager.openPutt == null || manager.openPutt.LocalPlayerManager == null)
                return;

            PlayerManager playerManager = manager.openPutt.LocalPlayerManager;
            playerManager.IsInLeftHandedMode = !playerManager.IsInLeftHandedMode;

            RefreshSettingsMenu();

            manager.RequestRefresh();
        }

        public void OnToggleExperimental()
        {
            if (manager == null || manager.openPutt == null || manager.openPutt.LocalPlayerManager == null)
                return;

            PlayerManager playerManager = manager.openPutt.LocalPlayerManager;
            playerManager.golfClub.putter.experimentalCollisionDetection = !playerManager.golfClub.putter.experimentalCollisionDetection;

            RefreshSettingsMenu();

            manager.RequestRefresh();
        }

        public void OnToggleCourseReplays()
        {
            if (manager == null || manager.openPutt == null || manager.openPutt.LocalPlayerManager == null)
                return;

            if (Networking.LocalPlayer.isMaster)
            {
                manager.openPutt.replayableCourses = !manager.openPutt.replayableCourses;

                manager.openPutt.RequestSerialization();

                RefreshSettingsMenu();

                manager.RequestRefresh();
            }
        }

        public void OnToggleSettings()
        {
            if (CurrentScoreboardView == ScoreboardView.Settings)
                CurrentScoreboardView = ScoreboardView.Scoreboard;
            else
                CurrentScoreboardView = ScoreboardView.Settings;
        }

        public void OnToggleInfo()
        {
            if (CurrentScoreboardView == ScoreboardView.Info)
                CurrentScoreboardView = ScoreboardView.Scoreboard;
            else
                CurrentScoreboardView = ScoreboardView.Info;
        }

        public void OnToggleTimerMode()
        {
            if (manager == null) return;

            manager.speedGolfMode = true;
            speedrunUpdateTimer = 0;
            CurrentScoreboardView = ScoreboardView.Scoreboard;
            RefreshScoreboard();
        }

        public void OnToggleAllPlayers()
        {
            if (manager == null) return;

            manager.showAllPlayers = !manager.showAllPlayers;
            RefreshScoreboard();
        }

        public void OnShowScoreboard()
        {
            if (manager == null) return;

            manager.speedGolfMode = false;
            speedrunUpdateTimer = -1;
            CurrentScoreboardView = ScoreboardView.Scoreboard;
            RefreshScoreboard();
        }

        public void OnResetClick()
        {
            resetButton.gameObject.SetActive(false);
            resetConfirmButton.gameObject.SetActive(true);
            resetCancelButton.gameObject.SetActive(true);
        }

        public void OnResetConfirm()
        {
            if (manager == null || manager.openPutt == null) return;

            PlayerManager pm = manager.openPutt.LocalPlayerManager;
            if (pm != null)
            {
                pm.ResetPlayerScores();
                if (pm.golfClub != null)
                {
                    if (pm.golfClub.pickup != null)
                        pm.golfClub.pickup.Drop();
                    pm.golfClub.transform.localPosition = Vector3.zero;
                }
                if (pm.golfBall != null)
                {
                    if (pm.golfBall.GetComponent<VRCPickup>() != null)
                        pm.golfBall.GetComponent<VRCPickup>().Drop();
                    pm.golfBall.transform.localPosition = Vector3.zero;
                    pm.golfBall.BallIsMoving = false;
                }
                pm.RequestSync();
                Utils.Log(this, "Player reset their scores");
            }

            manager.RequestRefresh();

            OnResetCancel();

            OnToggleSettings();
        }

        public void OnResetCancel()
        {
            resetButton.gameObject.SetActive(true);
            resetConfirmButton.gameObject.SetActive(false);
            resetCancelButton.gameObject.SetActive(false);
        }

        public void SnapTo(RectTransform target)
        {
            Canvas.ForceUpdateCanvases();

            ScrollRect scrollRect = playerListCanvas.transform.parent.GetComponent<ScrollRect>();
            if (target == null)
                scrollRect.content.anchoredPosition = Vector2.zero;
            else
                scrollRect.content.anchoredPosition =
                        (Vector2)scrollRect.transform.InverseTransformPoint(scrollRect.transform.position)
                        - (Vector2)scrollRect.transform.InverseTransformPoint(target.position);
        }
    }
}