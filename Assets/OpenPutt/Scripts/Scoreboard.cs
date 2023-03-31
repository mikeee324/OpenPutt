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
        public Canvas myCanvas;
        public RectTransform scoreboardHeader;
        public RectTransform settingsPanelHeader;
        public RectTransform settingsPanel;
        public RectTransform infoPanelHeader;
        public RectTransform infoPanel;
        public RectTransform parRowPanel;
        public RectTransform topRowPanel;
        public Canvas parRowCanvas;
        public Canvas topRowCanvas;
        public Canvas playerListCanvas;
        public GameObject rowPrefab;
        public GameObject columnPrefab;
        public GraphicRaycaster raycaster;
        public Image scoreboardTabBackground;
        public Image scoreboardTimerTabBackground;
        public Image infoTabBackground;
        public Image settingsTabBackground;
        public Image scoreboardBackground;

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
        [Tooltip("Defines which course this scoreboard is attached to. Used to toggle visibility when the player finishes a course")]
        public int attachedToCourse = -1;

        [Space, Header("Sizing")]
        public float nameColumnWidth = 0.35f;
        public float totalColumnWidth = 0.2f;
        public float columnPadding = 0.005f;
        public float rowPadding = 0.01f;
        public float rowHeight = 0.15f;

        private bool initializedUI = false;
        private float totalHeightOfScrollViewport = 0f;
        private ScoreboardView _currentScoreboardView = ScoreboardView.Settings;

        public int NumberOfColumns => manager != null && manager.openPutt != null ? manager.openPutt.courses.Length + 2 : 0;
        [HideInInspector]
        public int MaxVisibleRowCount = 12;

        private Canvas[] scoreboardRows = new Canvas[0];
        private UnityEngine.UI.Image[] scoreboardFieldBackgrounds = new Image[0];
        private TextMeshProUGUI[] scoreboardFieldLabels = new TextMeshProUGUI[0];
        public ScoreboardView CurrentScoreboardView
        {
            get => _currentScoreboardView;
            set
            {
                if (_currentScoreboardView != value)
                {
                    switch (value)
                    {
                        case ScoreboardView.Scoreboard:
                            settingsPanel.gameObject.SetActive(false);
                            settingsPanelHeader.gameObject.SetActive(false);
                            infoPanel.gameObject.SetActive(false);
                            infoPanelHeader.gameObject.SetActive(false);
                            topRowCanvas.enabled = true;
                            parRowCanvas.enabled = true;
                            playerListCanvas.enabled = true;
                            break;
                        case ScoreboardView.Info:
                            settingsPanel.gameObject.SetActive(false);
                            settingsPanelHeader.gameObject.SetActive(false);
                            infoPanel.gameObject.SetActive(true);
                            infoPanelHeader.gameObject.SetActive(true);
                            topRowCanvas.enabled = false;
                            parRowCanvas.enabled = false;
                            playerListCanvas.enabled = false;
                            break;
                        case ScoreboardView.Settings:
                            settingsPanel.gameObject.SetActive(true);
                            settingsPanelHeader.gameObject.SetActive(true);
                            infoPanel.gameObject.SetActive(false);
                            infoPanelHeader.gameObject.SetActive(false);
                            topRowCanvas.enabled = false;
                            parRowCanvas.enabled = false;
                            playerListCanvas.enabled = false;

                            RefreshSettingsMenu();

                            OnResetCancel();
                            break;
                    }
                }
                _currentScoreboardView = value;

                UpdateTabColours();
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

            InitUI();
        }

        /// <summary>
        /// Updates a field on the scoreboard
        /// </summary>
        /// <param name="fieldToUpdate">The ID of the field to update - Goes left-to-right and top-to-bottom</param>
        /// <param name="player">The PlayerManager that this row belongs to</param>
        /// <param name="columnText">The new text of the column</param>
        /// <param name="columnTextColor">The new color of the text in this column</param>
        /// <param name="columnBGColor">The new background colour of this column</param>
        /// <returns></returns>
        public bool UpdateField(int fieldToUpdate, PlayerManager player, string columnText, Color columnTextColor, Color columnBGColor)
        {
            if (manager == null || manager.openPutt == null) return false;

            int row = fieldToUpdate <= 0 ? 0 : fieldToUpdate / NumberOfColumns;
            //int col = fieldToUpdate <= 0 ? 0 : fieldToUpdate % NumberOfColumns;

            // It's out of bounds
            if (row >= playerListCanvas.transform.childCount)
                return false;

            Canvas playerRow = scoreboardRows[row];
            UnityEngine.UI.Image columnBackground = scoreboardFieldBackgrounds[fieldToUpdate];
            TextMeshProUGUI tmp = scoreboardFieldLabels[fieldToUpdate];

            if (player == null || columnBackground == null || tmp == null)
            {
                playerRow.enabled = false;
                return false;
            }

            playerRow.enabled = true;

            string oldColumnText = tmp.text;
            Color oldColumnTextColor = tmp.color;
            Color oldColumnBGColor = columnBackground.color;

            if (oldColumnText != columnText)
                tmp.text = columnText;
            if (oldColumnTextColor != columnTextColor)
                tmp.color = columnTextColor;
            if (oldColumnBGColor != columnBGColor)
                columnBackground.color = columnBGColor;

            return true;
        }

        private void UpdateParRow()
        {
            RectTransform rowRect = parRowPanel.GetChild(0).gameObject.GetComponent<RectTransform>();
            //rowRect.gameObject.SetActive(true);

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

                rowRect.GetChild(col).GetComponent<UnityEngine.UI.Image>().color = manager.nameBackground2;

                TextMeshProUGUI tmp = rowRect.GetChild(col).transform.GetChild(0).GetComponent<TextMeshProUGUI>();
                if (tmp != null)
                {
                    string newText = "-";
                    if (col == 0)
                    {
                        newText = "Par";
                    }
                    else if (col == columnCount - 1)
                    {
                        if (manager.speedGolfMode)
                            newText = TimeSpan.FromMilliseconds(totalPar).ToString(@"m\:ss");
                        else
                            newText = $"{totalPar}";
                    }
                    else
                    {
                        if (course.drivingRangeMode)
                            newText = "-";
                        else if (manager.speedGolfMode)
                            newText = TimeSpan.FromMilliseconds(parScore).ToString(@"m\:ss");
                        else
                            newText = $"{parScore}";
                    }

                    if (tmp.text != newText)
                    {
                        tmp.text = newText;

                        tmp.color = manager.text;
                    }
                }
            }
        }

        private void UpdateTopRow()
        {
            RectTransform rowRect = topRowPanel.GetChild(0).gameObject.GetComponent<RectTransform>();
            //rowRect.gameObject.SetActive(true);

            // Generate data for all columns
            int columnCount = manager.openPutt.courses.Length + 2;
            for (int col = 0; col < columnCount; col++)
            {

                rowRect.GetChild(col).GetComponent<Image>().color = manager.nameBackground2;

                TextMeshProUGUI tmp = rowRect.GetChild(col).transform.GetChild(0).GetComponent<TextMeshProUGUI>();
                if (tmp != null)
                {
                    string newText = "";
                    if (col == 0)
                    {
                        newText = "#";
                    }
                    else if (col == columnCount - 1)
                    {
                        newText = $"";
                    }
                    else
                    {
                        newText = $"{col}";

                        if (col >= 1 && col < columnCount - 1)
                        {
                            CourseManager course = manager.openPutt.courses[col - 1];

                            if (course != null && course.scoreboardShortName != null && course.scoreboardShortName.Length > 0)
                            {
                                newText = course.scoreboardShortName;
                            }
                        }
                    }

                    if (tmp.text != newText)
                    {
                        tmp.text = newText;
                        tmp.color = manager.text;
                    }
                }
            }
        }

        private void InitUI()
        {
            initializedUI = false;

            if (topRowPanel.childCount == 0)
            {
                CreateRow(0, topRowPanel).GetComponent<Canvas>().enabled = true;
            }
            if (parRowPanel.childCount == 0)
            {
                CreateRow(0, parRowPanel).GetComponent<Canvas>().enabled = true;
            }

            UpdateTopRow();
            UpdateParRow();

            int howManyRows = manager.NumberOfPlayersToDisplay;

            // If we have the wrong number of rows displayed
            if (howManyRows != playerListCanvas.transform.childCount)
            {
                // Delete any excess rows
                for (int i = howManyRows; i < playerListCanvas.transform.childCount; i++)
                {
                    Destroy(playerListCanvas.transform.GetChild(i).gameObject);
                }

                // Create any missing rows
                for (int position = playerListCanvas.transform.childCount; position < howManyRows; position++)
                {
                    CreateRow(position, playerListCanvas.transform);
                }

                // Cache all components that we need for refreshing
                scoreboardRows = new Canvas[howManyRows];
                scoreboardFieldBackgrounds = new Image[howManyRows * NumberOfColumns];
                scoreboardFieldLabels = new TextMeshProUGUI[howManyRows * NumberOfColumns];
                int lastRowID = -1;
                Canvas lastRow = null;
                for (int i = 0; i < howManyRows * NumberOfColumns; i++)
                {
                    int rowID = i <= 0 ? 0 : i / NumberOfColumns;
                    if (lastRowID != rowID)
                        lastRow = playerListCanvas.transform.GetChild(rowID).GetComponent<Canvas>();

                    scoreboardRows[rowID] = lastRow;

                    Transform column = lastRow.transform.GetChild(i <= 0 ? 0 : i % NumberOfColumns);
                    scoreboardFieldBackgrounds[i] = column.GetComponent<UnityEngine.UI.Image>();
                    scoreboardFieldLabels[i] = column.GetChild(0).GetComponent<TextMeshProUGUI>();
                }
            }

            initializedUI = true;
        }

        private RectTransform CreateRow(int position, Transform parent)
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

            //newRow.gameObject.SetActive(false);

            return newRow;
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
            }
        }

        public void OnClubPowerChanged()
        {
            if (manager == null || manager.openPutt == null || manager.openPutt.LocalPlayerManager == null)
                return;

            PlayerManager playerManager = manager.openPutt.LocalPlayerManager;
            playerManager.golfClub.forceMultiplier = clubPowerSlider.value;

            clubPowerValueLabel.text = String.Format("{0:F2}x", clubPowerSlider.value);
        }

        public void OnClubPowerReset()
        {
            if (manager == null || manager.openPutt == null || manager.openPutt.LocalPlayerManager == null)
                return;

            PlayerManager playerManager = manager.openPutt.LocalPlayerManager;
            playerManager.golfClub.forceMultiplier = 1f;

            RefreshSettingsMenu();
        }

        public void OnSFXVolumeChanged()
        {
            if (manager == null || manager.openPutt == null || manager.openPutt.LocalPlayerManager == null)
                return;

            manager.openPutt.SFXController.Volume = sfxVolumeSlider.value;

            sfxVolumeValueLabel.text = String.Format("{0:P0}", sfxVolumeSlider.value);
        }

        public void OnSFXVolumeReset()
        {
            if (manager == null || manager.openPutt == null || manager.openPutt.LocalPlayerManager == null)
                return;

            manager.openPutt.SFXController.Volume = 1f;

            RefreshSettingsMenu();
        }

        public void OnBGMVolumeChanged()
        {
            if (manager == null || manager.openPutt == null || manager.openPutt.LocalPlayerManager == null)
                return;

            foreach (AudioSource audioSource in manager.openPutt.BGMAudioSources)
                audioSource.volume = bgmVolumeSlider.value;

            bgmVolumeValueLabel.text = String.Format("{0:P0}", bgmVolumeSlider.value);
        }

        public void OnBGMVolumeReset()
        {
            if (manager == null || manager.openPutt == null || manager.openPutt.LocalPlayerManager == null)
                return;

            foreach (AudioSource audioSource in manager.openPutt.BGMAudioSources)
                audioSource.volume = 1f;

            RefreshSettingsMenu();
        }

        public void OnWorldVolumeChanged()
        {
            if (manager == null || manager.openPutt == null || manager.openPutt.LocalPlayerManager == null)
                return;

            foreach (AudioSource audioSource in manager.openPutt.WorldAudioSources)
                audioSource.volume = worldVolumeSlider.value;

            worldVolumeValueLabel.text = String.Format("{0:P0}", worldVolumeSlider.value);
        }

        public void OnWorldVolumeReset()
        {
            if (manager == null || manager.openPutt == null || manager.openPutt.LocalPlayerManager == null)
                return;

            foreach (AudioSource audioSource in manager.openPutt.WorldAudioSources)
                audioSource.volume = 1f;

            RefreshSettingsMenu();
        }

        public void OnBallColorChanged()
        {
            if (manager == null || manager.openPutt == null || manager.openPutt.LocalPlayerManager == null)
                return;

            PlayerManager playerManager = manager.openPutt.LocalPlayerManager;

            // Set the new ball colour
            playerManager.BallColor = new Color(ballRColorSlider.value, ballGColorSlider.value, ballBColorSlider.value);

            UpdateBallColorPreview();

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

            manager.RequestPlayerListRefresh();
        }

        public void OnToggleUnlimitedShaftSize()
        {
            if (manager == null || manager.openPutt == null || manager.openPutt.LocalPlayerManager == null)
                return;

            PlayerManager playerManager = manager.openPutt.LocalPlayerManager;
            playerManager.golfClub.enableBigShaft = !playerManager.golfClub.enableBigShaft;

            RefreshSettingsMenu();
        }

        public void OnToggleLeftHand()
        {
            if (manager == null || manager.openPutt == null || manager.openPutt.LocalPlayerManager == null)
                return;

            PlayerManager playerManager = manager.openPutt.LocalPlayerManager;
            playerManager.IsInLeftHandedMode = !playerManager.IsInLeftHandedMode;

            RefreshSettingsMenu();
        }

        public void OnToggleExperimental()
        {
            if (manager == null || manager.openPutt == null || manager.openPutt.LocalPlayerManager == null)
                return;

            PlayerManager playerManager = manager.openPutt.LocalPlayerManager;
            playerManager.golfClub.putter.experimentalCollisionDetection = !playerManager.golfClub.putter.experimentalCollisionDetection;

            RefreshSettingsMenu();
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
            }
        }

        public void OnToggleSettings()
        {
            if (CurrentScoreboardView == ScoreboardView.Settings)
            {
                CurrentScoreboardView = ScoreboardView.Scoreboard;
            }
            else
            {
                CurrentScoreboardView = ScoreboardView.Settings;

                // Close settings tab on other scoreboards
                if (manager != null)
                    manager.OnPlayerOpenSettings(this);
            }
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

            // If we were previously in the normal scoreboard - update player list
            if (!manager.speedGolfMode)
                manager.RequestPlayerListRefresh();

            manager.speedGolfMode = true;
            UpdateParRow();

            CurrentScoreboardView = ScoreboardView.Scoreboard;
        }

        public void OnToggleAllPlayers()
        {
            if (manager == null) return;

            manager.showAllPlayers = !manager.showAllPlayers;
        }

        public void OnShowScoreboard()
        {
            if (manager == null) return;

            // If we were previously in timer mode - update player list
            if (manager.speedGolfMode)
                manager.RequestPlayerListRefresh();

            manager.speedGolfMode = false;
            UpdateParRow();

            CurrentScoreboardView = ScoreboardView.Scoreboard;
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

            manager.RequestPlayerListRefresh();

            OnResetCancel();

            OnToggleSettings();
        }

        public void OnResetCancel()
        {
            resetButton.gameObject.SetActive(true);
            resetConfirmButton.gameObject.SetActive(false);
            resetCancelButton.gameObject.SetActive(false);
        }

        public void SnapTo(ScrollRect scrollRect, Transform target)
        {
            Canvas.ForceUpdateCanvases();

            if (target == null)
                scrollRect.content.anchoredPosition = Vector2.zero;
            else
                scrollRect.content.anchoredPosition =
                        (Vector2)scrollRect.transform.InverseTransformPoint(scrollRect.transform.position)
                        - (Vector2)scrollRect.transform.InverseTransformPoint(target.position);
        }

        public void UpdateTabColours()
        {
            // Update Tab Background Colours
            Color defaultBackground = scoreboardBackground.color;

            Color newScoreboardCol = _currentScoreboardView == ScoreboardView.Scoreboard && (manager == null || !manager.speedGolfMode) ? manager.currentCourseBackground : defaultBackground;
            Color newSpeedrunCol = _currentScoreboardView == ScoreboardView.Scoreboard && (manager != null && manager.speedGolfMode) ? manager.currentCourseBackground : defaultBackground;
            Color newInfoCol = _currentScoreboardView == ScoreboardView.Info ? manager.currentCourseBackground : defaultBackground;
            Color newSettingsCol = _currentScoreboardView == ScoreboardView.Settings ? manager.currentCourseBackground : defaultBackground;

            if (scoreboardTabBackground.color != newScoreboardCol)
                scoreboardTabBackground.color = newScoreboardCol;
            if (scoreboardTimerTabBackground.color != newSpeedrunCol)
                scoreboardTimerTabBackground.color = newSpeedrunCol;
            if (infoTabBackground.color != newInfoCol)
                infoTabBackground.color = newInfoCol;
            if (settingsTabBackground.color != newSettingsCol)
                settingsTabBackground.color = newSettingsCol;
        }

        public void UpdateViewportHeight()
        {
            RectTransform scoreboardPanel = GetComponent<RectTransform>();

            // Update size of canvas so scrollviews work
            RectTransform playerListRect = playerListCanvas.GetComponent<RectTransform>();

            // Get the total height of the player canvas view
            if (scoreboardPanel != null && scoreboardHeader != null && topRowPanel != null && parRowPanel != null)
                totalHeightOfScrollViewport = scoreboardPanel.sizeDelta.y - scoreboardHeader.sizeDelta.y - topRowPanel.sizeDelta.y - parRowPanel.sizeDelta.y;

            if (scoreboardPanel != null && scoreboardHeader != null && topRowPanel != null && parRowPanel != null)
            {
                // Work out how many rows we can fit in that height
                MaxVisibleRowCount = (int)Math.Floor(totalHeightOfScrollViewport / (rowHeight + rowPadding));
                if ((rowHeight + rowPadding) * MaxVisibleRowCount > totalHeightOfScrollViewport)
                    MaxVisibleRowCount--;

                float totalHeightOfAllRows = (rowHeight + rowPadding) * (manager.CurrentPlayerList != null ? manager.CurrentPlayerList.Length : MaxVisibleRowCount);
                playerListRect.sizeDelta = new Vector2(playerListRect.sizeDelta.x, totalHeightOfAllRows);

                UpdateScrollableState();
            }
        }

        /// <summary>
        /// Works out if scrolling should be enabled on this scoreboard
        /// </summary>
        private void UpdateScrollableState()
        {
            // Toggle scrollview if the players list is taller than the viewport
            ScrollRect scrollRect = playerListCanvas.transform.parent.GetComponent<ScrollRect>();

            PlayerManager[] activePlayers = manager.CurrentPlayerList;
            if (activePlayers == null || activePlayers.Length == 0)
                return;

            bool scrollableState = (rowHeight + rowPadding) * activePlayers.Length > totalHeightOfScrollViewport;
            if (scrollRect.enabled != scrollableState)
            {
                scrollRect.enabled = scrollableState;

                // If scrollview is now disabled scroll to top
                if (!scrollRect.enabled)
                    SnapTo(scrollRect, null);

                // Toggle raycast target depending on scrollRect state
                Image scrollPanel = playerListCanvas.transform.parent.GetComponent<Image>();
                if (scrollPanel != null)
                    scrollPanel.raycastTarget = scrollRect.enabled;
            }
        }

        /// <summary>
        /// If this scoreboard is scrollable it will try to find the local player and scroll to them.<br/>
        /// If it isn't scrollable it will lock the scroll position to the top.
        /// </summary>
        public void ScrollToLocalPlayer()
        {
            ScrollRect scrollRect = playerListCanvas.transform.parent.GetComponent<ScrollRect>();
            // If this is the local players row, and we need to scroll to their position
            if (scrollRect != null && scrollRect.enabled)
            {
                PlayerManager[] activePlayers = manager.CurrentPlayerList;
                if (activePlayers == null || activePlayers.Length == 0)
                    return;

                for (int position = 0; position < activePlayers.Length; position++)
                {
                    if (activePlayers[position].Owner != null && activePlayers[position].Owner == Networking.LocalPlayer)
                    {
                        // Find the row transform
                        Transform newRow = playerListCanvas.transform.GetChild(position);
                        if (newRow != null)
                            SnapTo(scrollRect, newRow); // Snap the ScrollView to this position so the local player is in view
                    }
                }
            }
            else
            {
                SnapTo(scrollRect, null); // Scrolls to the top
            }
        }
    }
}