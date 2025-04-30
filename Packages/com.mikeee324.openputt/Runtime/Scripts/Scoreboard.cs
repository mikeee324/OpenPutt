using System;
using TMPro;
using UdonSharp;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;
using Varneon.VUdon.ArrayExtensions;
using VRC.SDK3.Components;
using VRC.SDKBase;
using Random = UnityEngine.Random;

namespace dev.mikeee324.OpenPutt
{
    public enum ScoreboardView
    {
        Scoreboard,
        Info,
        Settings,
        DevMode,
        OpenPutt
    }

    [UdonBehaviourSyncMode(BehaviourSyncMode.None), DefaultExecutionOrder(10)]
    public class Scoreboard : UdonSharpBehaviour
    {
        [Header("This is a scoreboard that you can drag into the scene to display player scores. Make sure the ScoreboardManager has a reference to all scoreboards!")] [Header("External References")] [Tooltip("This is needed to receive refresh events and give access to the player info")]
        public ScoreboardManager manager;

        [Header("Internal References (All are required to be set)")]
        public ScoreboardPlayerRow[] scoreboardRows = new ScoreboardPlayerRow[0];

        public RectTransform rectTransform;
        public Canvas myCanvas;
        public RectTransform scoreboardHeader;
        public Canvas settingsPanel;
        public Canvas infoPanel;
        public Canvas devModelPanel;
        public Canvas openPuttPanel;
        public RectTransform parRowPanel;
        public RectTransform topRowPanel;
        public Canvas parRowCanvas;
        public Canvas topRowCanvas;
        public Canvas scoreboardCanvas;
        public Canvas playerListCanvas;
        public GameObject rowPrefab;
        public GameObject columnPrefab;
        public Button scoreboardTabBackground;
        public Button scoreboardTimerTabBackground;
        public Button infoTabBackground;
        public Button settingsTabBackground;
        public Button devModeTabBackground;
        public Button openPuttTabBackground;
        public RectTransform leftTabsPanel;
        public RectTransform rightTabsPanel;
        public TextMeshProUGUI creditsText;
        public TextMeshProUGUI updateAvailableLabel;
        public TextMeshProUGUI changelogText;

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

        #region Dev Mode Stuff

        public TextMeshProUGUI devModeLastClubHitSpeed;
        public TextMeshProUGUI devModeLastClubHitDirBias;
        public TextMeshProUGUI devModeBallSpeed;
        public TextMeshProUGUI devModeClubSpeed;

        [FormerlySerializedAs("devModeClubVelSmoothSlider")]
        public Slider devModeVelOffsetFrameSlider;

        public Slider devModeVelSmoothingFrameSlider;
        public Slider devModeBallWeightSlider;
        public Slider devModeHitWaitFramesSlider;
        public Slider devModeBallFrictionSlider;
        public Slider devModeBallDragSlider;
        public Slider devModeBallADragSlider;
        public Slider devModeBallMaxSpeedSlider;
        public TMP_Dropdown devModeVelocityTypeDropdown;
        public TextMeshProUGUI devModeOffsetFrameValueLabel;
        public TextMeshProUGUI devModeHitWaitFrameValueLabel;
        public TextMeshProUGUI devModeSmoothingFrameValueLabel;
        public TextMeshProUGUI devModeBallWeightValueLabel;
        public TextMeshProUGUI devModeBallFrictionValueLabel;
        public TextMeshProUGUI devModeBallDragValueLabel;
        public TextMeshProUGUI devModeBallADragValueLabel;
        public TextMeshProUGUI devModeBallMaxSpeedValueLabel;
        public Image devModeForAllCheckbox;
        public Image footColliderCheckbox;
        public Image clubRendererCheckbox;
        public Image balLGroundedCheckbox;
        public Image ballSnappingCheckbox;
        public Transform devModeSettingsBox;

        #endregion

        public GameObject desktopModeBox;

        public Image verticalHitsCheckbox;
        public Image isPlayingCheckbox;
        public Image leftHandModeCheckbox;
        public Image clubThrowCheckbox;
        public Image enableBigShaftCheckbox;
        public Image courseReplaysCheckbox;
        public Image invertCameraXCheckbox;
        public Image invertCameraYCheckbox;
        public Sprite checkboxOn;
        public Sprite checkboxOff;
        public Button resetButton;
        public Button resetConfirmButton;
        public Button resetCancelButton;

        [Space, Header("Sizing")]
        public float nameColumnWidth = 0.35f;

        public float totalColumnWidth = 0.2f;
        public float columnPadding = 0.005f;
        public float rowPadding = 0.01f;
        public float rowHeight = 0.15f;

        private bool initializedUI;
        public bool HasInitializedUI => initializedUI;
        private float totalHeightOfScrollViewport;
        private ScoreboardView _currentScoreboardView = ScoreboardView.Settings;

        public int NumberOfColumns => Utilities.IsValid(manager) && Utilities.IsValid(manager.openPutt) ? manager.openPutt.courses.Length + 2 : 0;

        [HideInInspector]
        public int MaxVisibleRowCount = 12;

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
                            devModelPanel.gameObject.SetActive(false);
                            openPuttPanel.gameObject.SetActive(false);
                            infoPanel.enabled = false;
                            scoreboardCanvas.enabled = true;
                            break;
                        case ScoreboardView.Info:
                            settingsPanel.gameObject.SetActive(false);
                            devModelPanel.gameObject.SetActive(false);
                            openPuttPanel.gameObject.SetActive(false);
                            infoPanel.enabled = true;
                            scoreboardCanvas.enabled = false;
                            break;
                        case ScoreboardView.OpenPutt:
                            settingsPanel.gameObject.SetActive(false);
                            devModelPanel.gameObject.SetActive(false);
                            openPuttPanel.gameObject.SetActive(true);
                            infoPanel.enabled = false;
                            scoreboardCanvas.enabled = false;

                            if (Utilities.IsValid(manager))
                            {
                                manager.requestedScoreboardView = ScoreboardView.OpenPutt;

                                var latest = manager.openPutt.latestOpenPuttVer;
                                var current = manager.openPutt.CurrentVersion;

                                if (current.Length > 0 && latest.Length > 0)
                                {
                                    updateAvailableLabel.gameObject.SetActive(latest != current);
                                    updateAvailableLabel.text = $"Update Available!\nLatest version is {latest}\nVersion in this world is {current}";
                                }
                                else
                                {
                                    updateAvailableLabel.gameObject.SetActive(false);
                                }

                                changelogText.text = manager.openPutt.openPuttChangelog;
                            }

                            break;
                        case ScoreboardView.DevMode:
                            settingsPanel.gameObject.SetActive(false);
                            devModelPanel.gameObject.SetActive(true);
                            openPuttPanel.gameObject.SetActive(false);
                            infoPanel.enabled = false;
                            scoreboardCanvas.enabled = false;

                            RefreshDevModeMenu();
                            break;
                        case ScoreboardView.Settings:
                            settingsPanel.gameObject.SetActive(true);
                            devModelPanel.gameObject.SetActive(false);
                            openPuttPanel.gameObject.SetActive(false);
                            infoPanel.enabled = false;
                            scoreboardCanvas.enabled = false;

                            /*if (Utils.LocalPlayerIsValid())
                            {
                                switch (Networking.LocalPlayer.GetPlatform())
                                {
                                    case DevicePlatform.Desktop:
                                    case DevicePlatform.AndroidMobile:
                                        desktopModeBox.gameObject.SetActive(true);
                                        break;
                                    default:
                                        desktopModeBox.gameObject.SetActive(false);
                                        break;
                                }
                            }*/

                            RefreshSettingsMenu();

                            OnResetCancel();
                            break;
                    }

                    // Toggle extra canvases (parent canvas.enabled doesn't seem to be passed down properly)
                    if (Utilities.IsValid(scoreboardCanvas))
                    {
                        if (Utilities.IsValid(playerListCanvas))
                            playerListCanvas.enabled = scoreboardCanvas.enabled;
                        if (Utilities.IsValid(topRowCanvas))
                            topRowCanvas.enabled = scoreboardCanvas.enabled;
                        if (Utilities.IsValid(parRowCanvas))
                            parRowCanvas.enabled = scoreboardCanvas.enabled;

                        for (var i = 0; i < scoreboardRows.Length; i++)
                            if (Utilities.IsValid(manager.CurrentPlayerList) && i < manager.CurrentPlayerList.Length)
                                scoreboardRows[i].UpdateVisibility(manager.CurrentPlayerList[i]);
                    }

                    if (Utilities.IsValid(manager))
                        manager.requestedScoreboardView = value;

                    if (manager.devModeTaps < 30 && value != ScoreboardView.Settings)
                        manager.devModeTaps = 0;

                    if (Utilities.IsValid(manager.openPutt))
                    {
                        switch (_currentScoreboardView)
                        {
                            case ScoreboardView.Settings:
                            case ScoreboardView.DevMode:
                                manager.openPutt.SavePersistantData();
                                break;
                        }
                    }
                }

                _currentScoreboardView = value;
                UpdateTabColours();
            }
        }

        void Start()
        {
            if (!Utilities.IsValid(manager) || !Utilities.IsValid(manager.openPutt))
            {
                OpenPuttUtils.LogError(this, "Missing references to manager or OpenPutt! Disabling this scoreboard.");
                gameObject.SetActive(false);
                return;
            }

            if (!Utilities.IsValid(topRowCanvas))
                topRowCanvas = topRowPanel.transform.GetChild(0).GetComponent<Canvas>();
            if (!Utilities.IsValid(parRowCanvas))
                parRowCanvas = parRowPanel.transform.GetChild(0).GetComponent<Canvas>();

            CurrentScoreboardView = ScoreboardView.Info;

            // This is here because i haven't figured out how to make editor scripts properly yet
            scoreboardRows = new ScoreboardPlayerRow[playerListCanvas.transform.childCount];
            for (var i = 0; i < playerListCanvas.transform.childCount; i++)
                scoreboardRows[i] = playerListCanvas.transform.GetChild(i).GetComponent<ScoreboardPlayerRow>();

            // Make sure that the scoreboard has basically no thickness so the laser pointer works properly
            if (!Utilities.IsValid(rectTransform))
                rectTransform = transform.GetChild(0).GetComponent<RectTransform>();
            if (Utilities.IsValid(rectTransform))
            {
                var scoreboardScale = rectTransform.localScale;
                if (!Mathf.Approximately(scoreboardScale.z, 0.01f))
                    rectTransform.localScale = new Vector3(scoreboardScale.x, scoreboardScale.y, 0.01f);
            }

            if (Utilities.IsValid(creditsText))
            {
                creditsText.text = creditsText.text.Replace("{OpenPuttCurrVer}", manager.openPutt.CurrentVersion);
            }

            SendCustomEventDelayedSeconds(nameof(InitUI), 0.05f);
        }

        public void InitUI()
        {
            initializedUI = false;

            if (topRowPanel.transform.childCount > 0)
                topRowPanel.GetChild(0).GetComponent<ScoreboardPlayerRow>().Refresh();
            if (parRowPanel.transform.childCount > 0)
                parRowPanel.GetChild(0).GetComponent<ScoreboardPlayerRow>().Refresh();

            initializedUI = true;
        }

        /// <summary>
        /// Updates all UI elements on the settings screen so they match the current state of the game
        /// </summary>
        public void RefreshSettingsMenu()
        {
            if (!Utilities.IsValid(manager) || !Utilities.IsValid(manager.openPutt) || !Utilities.IsValid(manager.openPutt.LocalPlayerManager))
                return;

            var playerManager = manager.openPutt.LocalPlayerManager;

            clubPowerSlider.value = playerManager.golfClub.forceMultiplier;
            clubPowerValueLabel.text = $"{clubPowerSlider.value:F2}x";

            sfxVolumeSlider.value = playerManager.openPutt.SFXController.Volume;
            sfxVolumeValueLabel.text = $"{sfxVolumeSlider.value:P0}";

            // Just use the first audio source volume
            foreach (var audioSource in manager.openPutt.BGMAudioSources)
            {
                if (!Utilities.IsValid(audioSource)) continue;
                bgmVolumeSlider.value = audioSource.volume;
                bgmVolumeValueLabel.text = $"{bgmVolumeSlider.value:P0}";
                break;
            }

            bgmVolumeSlider.transform.parent.gameObject.SetActive(manager.openPutt.BGMAudioSources.Length > 0);

            // Just use the first audio source volume
            foreach (var audioSource in manager.openPutt.WorldAudioSources)
            {
                if (!Utilities.IsValid(audioSource)) continue;
                worldVolumeSlider.value = audioSource.volume;
                worldVolumeValueLabel.text = $"{worldVolumeSlider.value:P0}";
                break;
            }

            worldVolumeSlider.transform.parent.gameObject.SetActive(manager.openPutt.WorldAudioSources.Length > 0);

            var color = playerManager.BallColor;

            ballRColorSlider.value = color.r;
            ballGColorSlider.value = color.g;
            ballBColorSlider.value = color.b;

            ballColorPreview.color = color;

            courseReplaysCheckbox.sprite = manager.openPutt.replayableCourses ? checkboxOn : checkboxOff;

            verticalHitsCheckbox.sprite = playerManager.openPutt.enableVerticalHits ? checkboxOn : checkboxOff;
            isPlayingCheckbox.sprite = playerManager.isPlaying ? checkboxOff : checkboxOn;
            leftHandModeCheckbox.sprite = playerManager.IsInLeftHandedMode ? checkboxOn : checkboxOff;
            enableBigShaftCheckbox.sprite = playerManager.golfClub.enableBigShaft ? checkboxOn : checkboxOff;
            clubThrowCheckbox.sprite = playerManager.golfClub.throwEnabled ? checkboxOn : checkboxOff;
        }

        public void UpdateBallColorPreview()
        {
            if (!Utilities.IsValid(manager) || !Utilities.IsValid(manager.openPutt) || !Utilities.IsValid(manager.openPutt.LocalPlayerManager))
                return;

            var playerManager = manager.openPutt.LocalPlayerManager;
            ballColorPreview.color = playerManager.BallColor;
        }

        public void OnClubPowerChanged()
        {
            if (!Utilities.IsValid(manager) || !Utilities.IsValid(manager.openPutt) || !Utilities.IsValid(manager.openPutt.LocalPlayerManager))
                return;

            var playerManager = manager.openPutt.LocalPlayerManager;
            playerManager.golfClub.forceMultiplier = clubPowerSlider.value;

            clubPowerValueLabel.text = $"{clubPowerSlider.value:F2}x";
        }

        public void OnClubPowerReset()
        {
            if (!Utilities.IsValid(manager) || !Utilities.IsValid(manager.openPutt) || !Utilities.IsValid(manager.openPutt.LocalPlayerManager))
                return;

            var playerManager = manager.openPutt.LocalPlayerManager;
            playerManager.golfClub.forceMultiplier = 1f;

            RefreshSettingsMenu();
        }

        public void OnSFXVolumeChanged()
        {
            if (!Utilities.IsValid(manager) || !Utilities.IsValid(manager.openPutt) || !Utilities.IsValid(manager.openPutt.LocalPlayerManager))
                return;

            manager.openPutt.SFXController.Volume = sfxVolumeSlider.value;

            sfxVolumeValueLabel.text = $"{sfxVolumeSlider.value:P0}";
        }

        public void OnSFXVolumeReset()
        {
            if (!Utilities.IsValid(manager) || !Utilities.IsValid(manager.openPutt) || !Utilities.IsValid(manager.openPutt.LocalPlayerManager))
                return;

            manager.openPutt.SFXController.Volume = 1f;

            RefreshSettingsMenu();
        }

        public void OnBGMVolumeChanged()
        {
            if (!Utilities.IsValid(manager) || !Utilities.IsValid(manager.openPutt) || !Utilities.IsValid(manager.openPutt.LocalPlayerManager))
                return;

            foreach (var audioSource in manager.openPutt.BGMAudioSources)
                audioSource.volume = bgmVolumeSlider.value;

            bgmVolumeValueLabel.text = $"{bgmVolumeSlider.value:P0}";
        }

        public void OnBGMVolumeReset()
        {
            if (!Utilities.IsValid(manager) || !Utilities.IsValid(manager.openPutt) || !Utilities.IsValid(manager.openPutt.LocalPlayerManager))
                return;

            foreach (var audioSource in manager.openPutt.BGMAudioSources)
                audioSource.volume = 1f;

            RefreshSettingsMenu();
        }

        public void OnWorldVolumeChanged()
        {
            if (!Utilities.IsValid(manager) || !Utilities.IsValid(manager.openPutt) || !Utilities.IsValid(manager.openPutt.LocalPlayerManager))
                return;

            foreach (var audioSource in manager.openPutt.WorldAudioSources)
                audioSource.volume = worldVolumeSlider.value;

            worldVolumeValueLabel.text = $"{worldVolumeSlider.value:P0}";
        }

        public void OnWorldVolumeReset()
        {
            if (!Utilities.IsValid(manager) || !Utilities.IsValid(manager.openPutt) || !Utilities.IsValid(manager.openPutt.LocalPlayerManager))
                return;

            foreach (var audioSource in manager.openPutt.WorldAudioSources)
                audioSource.volume = 1f;

            RefreshSettingsMenu();
        }

        public void OnBallColorChanged()
        {
            if (!Utilities.IsValid(manager) || !Utilities.IsValid(manager.openPutt) || !Utilities.IsValid(manager.openPutt.LocalPlayerManager))
                return;

            var playerManager = manager.openPutt.LocalPlayerManager;

            // Set the new ball colour
            playerManager.BallColor = new Color(ballRColorSlider.value, ballGColorSlider.value, ballBColorSlider.value);

            UpdateBallColorPreview();

            // Tell other players about the change straight away if possible
            if (manager.openPutt.playerSyncType < PlayerSyncType.FinishOnly)
                playerManager.RequestSerialization();
        }

        public void OnBallColorReset()
        {
            if (!Utilities.IsValid(manager) || !Utilities.IsValid(manager.openPutt) || !Utilities.IsValid(manager.openPutt.LocalPlayerManager))
                return;

            var playerManager = manager.openPutt.LocalPlayerManager;

            playerManager.BallColor = new Color(Random.Range(0, 1f), Random.Range(0, 1f), Random.Range(0, 1f));

            RefreshSettingsMenu();

            // Tell other players about the change straight away if possible
            if (manager.openPutt.playerSyncType < PlayerSyncType.FinishOnly)
                playerManager.RequestSerialization();
        }

        public void OnToggleVerticalHits()
        {
            if (!Utilities.IsValid(manager) || !Utilities.IsValid(manager.openPutt) || !Utilities.IsValid(manager.openPutt.LocalPlayerManager))
                return;

            var playerManager = manager.openPutt.LocalPlayerManager;
            playerManager.openPutt.enableVerticalHits = !playerManager.openPutt.enableVerticalHits;

            RefreshSettingsMenu();
        }

        public void OnToggleClubRenderer()
        {
            if (!Utilities.IsValid(manager) || !Utilities.IsValid(manager.openPutt) || !Utilities.IsValid(manager.openPutt.LocalPlayerManager))
                return;

            var playerManager = manager.openPutt.LocalPlayerManager;

            var isActive = playerManager.golfClubVisualiser.gameObject.activeInHierarchy;

            playerManager.golfClubVisualiser.gameObject.SetActive(!isActive);

            RefreshDevModeMenu();
        }

        public void OnToggleBallGrounded()
        {
            if (!Utilities.IsValid(manager) || !Utilities.IsValid(manager.openPutt) || !Utilities.IsValid(manager.openPutt.LocalPlayerManager))
                return;

            var playerManager = manager.openPutt.LocalPlayerManager;

            playerManager.golfBall.ballGroundedDebug = !playerManager.golfBall.ballGroundedDebug;

            RefreshDevModeMenu();
        }

        public void OnToggleBallSnap()
        {
            if (!Utilities.IsValid(manager) || !Utilities.IsValid(manager.openPutt) || !Utilities.IsValid(manager.openPutt.LocalPlayerManager))
                return;

            var playerManager = manager.openPutt.LocalPlayerManager;

            playerManager.golfBall.enableBallSnap = !playerManager.golfBall.enableBallSnap;

            RefreshDevModeMenu();
        }

        public void OnToggleClubThrow()
        {
            if (!Utilities.IsValid(manager) || !Utilities.IsValid(manager.openPutt) || !Utilities.IsValid(manager.openPutt.LocalPlayerManager))
                return;

            var playerManager = manager.openPutt.LocalPlayerManager;

            playerManager.golfClub.throwEnabled = !playerManager.golfClub.throwEnabled;

            RefreshSettingsMenu();
        }

        public void OnTogglePlayerManager()
        {
            if (!Utilities.IsValid(manager) || !Utilities.IsValid(manager.openPutt) || !Utilities.IsValid(manager.openPutt.LocalPlayerManager))
                return;

            var playerManager = manager.openPutt.LocalPlayerManager;
            playerManager.isPlaying = !playerManager.isPlaying;

            if (playerManager.isPlaying)
            {
                playerManager.openPutt.leftShoulderPickup.gameObject.SetActive(true);
                playerManager.openPutt.rightShoulderPickup.gameObject.SetActive(true);
            }
            else
            {
                // Drop the shoulder objects and set them back to Vector3.zero
                var attachedObject = playerManager.openPutt.leftShoulderPickup.ObjectToAttach;
                VRCPickup pickup;
                if (Utilities.IsValid(attachedObject))
                {
                    pickup = attachedObject.GetComponent<VRCPickup>();
                    if (Utilities.IsValid(pickup))
                        pickup.Drop();

                    attachedObject.transform.localPosition = Vector3.zero;
                }

                attachedObject = playerManager.openPutt.rightShoulderPickup.ObjectToAttach;
                if (Utilities.IsValid(attachedObject))
                {
                    pickup = attachedObject.GetComponent<VRCPickup>();
                    if (Utilities.IsValid(pickup))
                        pickup.Drop();

                    attachedObject.transform.localPosition = Vector3.zero;
                }

                //Drop the BodyMountedObjects
                pickup = playerManager.openPutt.leftShoulderPickup.gameObject.GetComponent<VRCPickup>();
                if (Utilities.IsValid(pickup))
                    pickup.Drop();
                pickup = playerManager.openPutt.rightShoulderPickup.gameObject.GetComponent<VRCPickup>();
                if (Utilities.IsValid(pickup))
                    pickup.Drop();

                // Disable the pickups
                playerManager.openPutt.leftShoulderPickup.gameObject.SetActive(false);
                playerManager.openPutt.rightShoulderPickup.gameObject.SetActive(false);
            }

            RefreshSettingsMenu();

            playerManager.UpdateTotals();

            playerManager.openPutt.OnPlayerUpdate(playerManager);
            playerManager.RequestSync();
        }

        public void OnToggleUnlimitedShaftSize()
        {
            if (!Utilities.IsValid(manager) || !Utilities.IsValid(manager.openPutt) || !Utilities.IsValid(manager.openPutt.LocalPlayerManager))
                return;

            var playerManager = manager.openPutt.LocalPlayerManager;
            playerManager.golfClub.enableBigShaft = !playerManager.golfClub.enableBigShaft;

            RefreshSettingsMenu();
        }

        public void OnToggleLeftHand()
        {
            if (!Utilities.IsValid(manager) || !Utilities.IsValid(manager.openPutt) || !Utilities.IsValid(manager.openPutt.LocalPlayerManager))
                return;

            var playerManager = manager.openPutt.LocalPlayerManager;
            playerManager.IsInLeftHandedMode = !playerManager.IsInLeftHandedMode;

            RefreshSettingsMenu();
        }

        public void OnToggleDevModeForAll()
        {
            if (!Utilities.IsValid(manager) || !Utilities.IsValid(manager.openPutt) || !Utilities.IsValid(manager.openPutt.devModePlayerWhitelist))
                return;

            var localPlayerName = OpenPuttUtils.LocalPlayerIsValid() ? Networking.LocalPlayer.displayName : null;

            if (Utilities.IsValid(localPlayerName) && manager.openPutt.devModePlayerWhitelist.Contains(localPlayerName))
            {
                OpenPuttUtils.SetOwner(Networking.LocalPlayer, manager.openPutt.gameObject);
                manager.openPutt.enableDevModeForAll = !manager.openPutt.enableDevModeForAll;
                manager.openPutt.RequestSerialization();
            }

            RefreshDevModeMenu();
        }

        public void OnToggleFootCollider()
        {
            if (!Utilities.IsValid(manager) || !Utilities.IsValid(manager.openPutt) || !Utilities.IsValid(manager.openPutt.footCollider))
                return;

            manager.openPutt.footCollider.gameObject.SetActive(!manager.openPutt.footCollider.gameObject.activeSelf);

            if (manager.openPutt.footCollider.gameObject.activeSelf)
                manager.openPutt.LocalPlayerManager.golfClubHead.targetOverride = manager.openPutt.footCollider.transform;
            else
                manager.openPutt.LocalPlayerManager.golfClubHead.targetOverride = null;

            RefreshDevModeMenu();
        }

        public void OnToggleCourseReplays()
        {
            if (!Utilities.IsValid(manager) || !Utilities.IsValid(manager.openPutt) || !Utilities.IsValid(manager.openPutt.LocalPlayerManager))
                return;

            manager.openPutt.replayableCourses = !manager.openPutt.replayableCourses;

            RefreshSettingsMenu();
        }

        public void OnToggleSettings()
        {
            if (Utilities.IsValid(manager))
            {
                manager.devModeTaps++;
                manager.requestedScoreboardView = ScoreboardView.Settings;
                manager.OnPlayerOpenSettings(this);
            }

            CurrentScoreboardView = ScoreboardView.Settings;
        }

        public void OnToggleDevMode()
        {
            if (Utilities.IsValid(manager))
            {
                manager.requestedScoreboardView = ScoreboardView.DevMode;
                manager.OnPlayerOpenSettings(this);
            }

            CurrentScoreboardView = ScoreboardView.DevMode;
        }

        public void OnToggleInfo()
        {
            if (Utilities.IsValid(manager))
            {
                manager.requestedScoreboardView = ScoreboardView.Info;
            }

            CurrentScoreboardView = ScoreboardView.Info;
        }


        public void OnShowPrefabInfo()
        {
            if (Utilities.IsValid(manager))
                manager.requestedScoreboardView = ScoreboardView.OpenPutt;

            CurrentScoreboardView = ScoreboardView.OpenPutt;
        }

        public void OnToggleTimerMode()
        {
            if (!Utilities.IsValid(manager)) return;

            manager.SpeedGolfMode = true;
            if (parRowPanel.transform.childCount > 0)
                parRowPanel.GetChild(0).GetComponent<ScoreboardPlayerRow>().Refresh();

            manager.requestedScoreboardView = ScoreboardView.Scoreboard;
            CurrentScoreboardView = ScoreboardView.Scoreboard;
        }

        public void OnShowScoreboard()
        {
            if (!Utilities.IsValid(manager)) return;

            manager.SpeedGolfMode = false;
            if (parRowPanel.transform.childCount > 0)
                parRowPanel.GetChild(0).GetComponent<ScoreboardPlayerRow>().Refresh();

            manager.requestedScoreboardView = ScoreboardView.Scoreboard;
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
            if (!Utilities.IsValid(manager) || !Utilities.IsValid(manager.openPutt)) return;

            var pm = manager.openPutt.LocalPlayerManager;
            if (Utilities.IsValid(pm))
            {
                pm.ResetPlayerScores();

                if (Utilities.IsValid(pm.golfClub))
                {
                    if (Utilities.IsValid(pm.golfClub.pickup))
                        pm.golfClub.pickup.Drop();
                    if (Utilities.IsValid(pm.golfClub.openPuttSync))
                        pm.golfClub.openPuttSync.Respawn();
                }

                if (Utilities.IsValid(pm.golfBall))
                {
                    if (pm.golfBall.GetComponent<VRCPickup>() != null)
                        pm.golfBall.GetComponent<VRCPickup>().Drop();
                    if (Utilities.IsValid(pm.golfClub.openPuttSync))
                        pm.golfClub.openPuttSync.Respawn();
                    pm.golfBall.BallIsMoving = false;
                }

                pm.RequestSync();

                pm.UpdateTotals();

                pm.openPutt.OnPlayerUpdate(pm);

                if (manager.openPutt.debugMode)
                    OpenPuttUtils.Log(this, "Player reset their scores");
            }

            OnResetCancel();

            OnToggleSettings();
        }

        public void OnResetCancel()
        {
            resetButton.gameObject.SetActive(true);
            resetConfirmButton.gameObject.SetActive(false);
            resetCancelButton.gameObject.SetActive(false);
        }

        #region Dev Mode UI Functions

        public void RefreshDevModeMenu()
        {
            if (!Utilities.IsValid(manager) || !Utilities.IsValid(manager.openPutt) || !Utilities.IsValid(manager.openPutt.LocalPlayerManager))
                return;

            var playerManager = manager.openPutt.LocalPlayerManager;

            devModeBallWeightSlider.value = playerManager.golfBall.BallWeight;
            devModeBallWeightValueLabel.text = $"{devModeBallWeightSlider.value:F2}";

            devModeBallFrictionSlider.value = playerManager.golfBall.BallFriction;
            devModeBallFrictionValueLabel.text = $"{devModeBallFrictionSlider.value:F2}";

            devModeBallDragSlider.value = playerManager.golfBall.BallDrag;
            devModeBallDragValueLabel.text = $"{devModeBallDragSlider.value:F3}";

            devModeBallMaxSpeedSlider.value = playerManager.golfBall.BallMaxSpeed;
            devModeBallMaxSpeedValueLabel.text = $"{devModeBallMaxSpeedSlider.value:F0}";

            devModeBallADragSlider.value = playerManager.golfBall.BallAngularDrag;
            devModeBallADragValueLabel.text = $"{devModeBallADragSlider.value:F2}";

            devModeVelOffsetFrameSlider.value = manager.openPutt.controllerTracker.endOffset;
            devModeOffsetFrameValueLabel.text = $"{devModeVelOffsetFrameSlider.value:F0}";

            devModeVelSmoothingFrameSlider.value = manager.openPutt.controllerTracker.lookbackFrames;
            devModeSmoothingFrameValueLabel.text = $"{devModeVelSmoothingFrameSlider.value:F0}";

            devModeHitWaitFramesSlider.value = playerManager.golfClubHead.hitWaitFrames;
            devModeHitWaitFrameValueLabel.text = $"{devModeHitWaitFramesSlider.value:F0}";

            //devModeVelocityTypeDropdown.value = (int)playerManager.golfClub.velocityTrackingType;

            balLGroundedCheckbox.sprite = playerManager.golfBall.ballGroundedDebug ? checkboxOn : checkboxOff;
            ballSnappingCheckbox.sprite = playerManager.golfBall.enableBallSnap ? checkboxOn : checkboxOff;
            devModeForAllCheckbox.sprite = manager.openPutt.enableDevModeForAll ? checkboxOn : checkboxOff;
            footColliderCheckbox.sprite = manager.openPutt.footCollider.gameObject.activeSelf ? checkboxOn : checkboxOff;
            clubRendererCheckbox.sprite = playerManager.golfClubVisualiser.gameObject.activeSelf ? checkboxOn : checkboxOff;
        }

        public void OnBallWeightReset()
        {
            var player = manager.openPutt.LocalPlayerManager;

            if (!Utilities.IsValid(player)) return;

            player.golfBall.BallWeight = player.golfBall.DefaultBallWeight;

            RefreshDevModeMenu();
        }

        public void OnBallWeightChanged()
        {
            var player = manager.openPutt.LocalPlayerManager;

            if (!Utilities.IsValid(player)) return;

            player.golfBall.BallWeight = devModeBallWeightSlider.value;
            devModeBallWeightValueLabel.text = $"{devModeBallWeightSlider.value:F2}";
        }

        public void OnHitWaitFramesChanged()
        {
            var player = manager.openPutt.LocalPlayerManager;

            if (!Utilities.IsValid(player)) return;

            player.golfClub.putter.hitWaitFrames = (int)devModeHitWaitFramesSlider.value;

            RefreshDevModeMenu();
        }

        public void OnClubWaitFramesReset()
        {
            var player = manager.openPutt.LocalPlayerManager;

            if (!Utilities.IsValid(player)) return;

            player.golfClub.putter.hitWaitFrames = 0;

            RefreshDevModeMenu();
        }

        public void OnClubHitBackstepReset()
        {
            var player = manager.openPutt.LocalPlayerManager;

            if (!Utilities.IsValid(player)) return;

            //player.golfClub.putter.multiFrameAverageMaxBacksteps = 4;

            RefreshDevModeMenu();
        }

        public void OnClubHitVelSmoothReset()
        {
            var player = manager.openPutt.LocalPlayerManager;

            if (!Utilities.IsValid(player)) return;

            //player.golfClub.putter.multiFrameAverageMaxBacksteps = 4;

            RefreshDevModeMenu();
        }

        public void OnVelocityTrackingChanged()
        {
            var player = manager.openPutt.LocalPlayerManager;

            if (!Utilities.IsValid(player)) return;

            //player.golfClub.velocityTrackingType = (GolfClubTrackingType)devModeVelocityTypeDropdown.value;

            RefreshDevModeMenu();
        }

        public void OnVelocityOffsetFrameChanged()
        {
            var player = manager.openPutt.LocalPlayerManager;

            if (!Utilities.IsValid(player)) return;

            player.openPutt.controllerTracker.endOffset = (int)devModeVelOffsetFrameSlider.value;

            RefreshDevModeMenu();
        }

        public void OnVelocitySmoothingFrameChanged()
        {
            var player = manager.openPutt.LocalPlayerManager;

            if (!Utilities.IsValid(player)) return;

            player.openPutt.controllerTracker.lookbackFrames = (int)devModeVelSmoothingFrameSlider.value;

            RefreshDevModeMenu();
        }

        public void OnBallFrictionReset()
        {
            var player = manager.openPutt.LocalPlayerManager;

            if (!Utilities.IsValid(player)) return;

            player.golfBall.BallFriction = player.golfBall.DefaultBallFriction;

            RefreshDevModeMenu();
        }

        public void OnBallFrictionChanged()
        {
            var player = manager.openPutt.LocalPlayerManager;

            if (!Utilities.IsValid(player)) return;

            player.golfBall.BallFriction = devModeBallFrictionSlider.value;
            devModeBallFrictionValueLabel.text = $"{devModeBallFrictionSlider.value:F2}";
        }

        public void OnBallADragReset()
        {
            var player = manager.openPutt.LocalPlayerManager;

            if (!Utilities.IsValid(player)) return;

            player.golfBall.BallAngularDrag = player.golfBall.DefaultBallAngularDrag;

            RefreshDevModeMenu();
        }

        public void OnBallADragChanged()
        {
            var player = manager.openPutt.LocalPlayerManager;

            if (!Utilities.IsValid(player)) return;

            player.golfBall.BallAngularDrag = devModeBallADragSlider.value;
            devModeBallADragValueLabel.text = $"{devModeBallADragSlider.value:F2}";
        }

        public void OnBallDragReset()
        {
            var player = manager.openPutt.LocalPlayerManager;

            if (!Utilities.IsValid(player)) return;

            player.golfBall.BallDrag = player.golfBall.DefaultBallDrag;

            RefreshDevModeMenu();
        }

        public void OnBallDragChanged()
        {
            var player = manager.openPutt.LocalPlayerManager;

            if (!Utilities.IsValid(player)) return;

            var roundedVal = $"{devModeBallDragSlider.value:F3}";

            player.golfBall.BallDrag = float.Parse(roundedVal);
            devModeBallDragValueLabel.text = roundedVal;
        }

        public void OnBallMaxSpeedReset()
        {
            var player = manager.openPutt.LocalPlayerManager;

            if (!Utilities.IsValid(player)) return;

            player.golfBall.BallMaxSpeed = player.golfBall.DefaultBallMaxSpeed;

            RefreshDevModeMenu();
        }

        public void OnBallMaxSpeedChanged()
        {
            var player = manager.openPutt.LocalPlayerManager;

            if (!Utilities.IsValid(player)) return;

            player.golfBall.BallMaxSpeed = devModeBallMaxSpeedSlider.value;
            devModeBallMaxSpeedValueLabel.text = $"{devModeBallMaxSpeedSlider.value:F0}";
        }

        #endregion

        public void SnapTo(ScrollRect scrollRect, Transform target)
        {
            Canvas.ForceUpdateCanvases();

            if (!Utilities.IsValid(target))
                scrollRect.content.anchoredPosition = Vector2.zero;
            else
                scrollRect.content.anchoredPosition = (Vector2)scrollRect.transform.InverseTransformPoint(scrollRect.transform.position) - (Vector2)scrollRect.transform.InverseTransformPoint(target.position);
        }

        public void UpdateTabColours()
        {
            // Update Tab Background Colours
            var defaultBackground = Color.clear;
            var selectedBackground = Color.white;

            var newScoreboardCol = _currentScoreboardView == ScoreboardView.Scoreboard && (!Utilities.IsValid(manager) || !manager.SpeedGolfMode) ? selectedBackground : defaultBackground;
            var newSpeedrunCol = _currentScoreboardView == ScoreboardView.Scoreboard && (Utilities.IsValid(manager) && manager.SpeedGolfMode) ? selectedBackground : defaultBackground;
            var newInfoCol = _currentScoreboardView == ScoreboardView.Info ? selectedBackground : defaultBackground;
            var newSettingsCol = _currentScoreboardView == ScoreboardView.Settings ? selectedBackground : defaultBackground;
            var newDevModeCol = _currentScoreboardView == ScoreboardView.DevMode ? selectedBackground : defaultBackground;
            var newOpenPuttCol = _currentScoreboardView == ScoreboardView.OpenPutt ? selectedBackground : defaultBackground;

            if (scoreboardTabBackground.colors.normalColor != newScoreboardCol)
            {
                var colorBlock = scoreboardTabBackground.colors;
                colorBlock.normalColor = newScoreboardCol;
                scoreboardTabBackground.colors = colorBlock;
            }

            if (scoreboardTimerTabBackground.colors.normalColor != newSpeedrunCol)
            {
                var colorBlock = scoreboardTimerTabBackground.colors;
                colorBlock.normalColor = newSpeedrunCol;
                scoreboardTimerTabBackground.colors = colorBlock;
            }

            if (infoTabBackground.colors.normalColor != newInfoCol)
            {
                var colorBlock = infoTabBackground.colors;
                colorBlock.normalColor = newInfoCol;
                infoTabBackground.colors = colorBlock;
            }

            if (settingsTabBackground.colors.normalColor != newSettingsCol)
            {
                var colorBlock = settingsTabBackground.colors;
                colorBlock.normalColor = newSettingsCol;
                settingsTabBackground.colors = colorBlock;
            }

            if (openPuttTabBackground.colors.normalColor != newOpenPuttCol)
            {
                var colorBlock = openPuttTabBackground.colors;
                colorBlock.normalColor = newOpenPuttCol;
                openPuttTabBackground.colors = colorBlock;
            }

            var devModeEnabled = manager.LocalPlayerCanAccessDevMode || manager.LocalPlayerCanAccessToolbox;
            devModeTabBackground.gameObject.SetActive(devModeEnabled);

            rightTabsPanel.sizeDelta = new Vector2(devModeEnabled ? 1.01f : .81f, rightTabsPanel.sizeDelta.y);

            devModeSettingsBox.gameObject.SetActive(manager.LocalPlayerCanAccessDevMode);

            if (devModeTabBackground.colors.normalColor != newDevModeCol)
            {
                var colorBlock = devModeTabBackground.colors;
                colorBlock.normalColor = newDevModeCol;
                devModeTabBackground.colors = colorBlock;
            }
        }

        public void UpdateViewportHeight()
        {
            var scoreboardPanel = GetComponent<RectTransform>();

            // Update size of canvas so scrollviews work
            var playerListRect = playerListCanvas.GetComponent<RectTransform>();

            // Get the total height of the player canvas view
            if (Utilities.IsValid(scoreboardPanel) && Utilities.IsValid(scoreboardHeader) && Utilities.IsValid(topRowPanel) && Utilities.IsValid(parRowPanel))
                totalHeightOfScrollViewport = scoreboardPanel.sizeDelta.y - scoreboardHeader.sizeDelta.y - topRowPanel.sizeDelta.y - parRowPanel.sizeDelta.y;

            if (Utilities.IsValid(scoreboardPanel) && Utilities.IsValid(scoreboardHeader) && Utilities.IsValid(topRowPanel) && Utilities.IsValid(parRowPanel))
            {
                // Work out how many rows we can fit in that height
                MaxVisibleRowCount = (int)Math.Floor(totalHeightOfScrollViewport / (rowHeight + rowPadding));
                if ((rowHeight + rowPadding) * MaxVisibleRowCount > totalHeightOfScrollViewport)
                    MaxVisibleRowCount--;

                var totalHeightOfAllRows = (rowHeight + rowPadding) * (Utilities.IsValid(manager.CurrentPlayerList) ? manager.CurrentPlayerList.Length : MaxVisibleRowCount);
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
            var scrollRect = playerListCanvas.transform.parent.GetComponent<ScrollRect>();

            var activePlayers = manager.CurrentPlayerList;
            if (!Utilities.IsValid(activePlayers) || activePlayers.Length == 0)
                return;

            var scrollableState = (rowHeight + rowPadding) * activePlayers.Length > totalHeightOfScrollViewport;
            if (scrollRect.enabled != scrollableState)
            {
                scrollRect.enabled = scrollableState;

                // If scrollview is now disabled scroll to top
                if (!scrollRect.enabled)
                    SnapTo(scrollRect, null);

                // Toggle raycast target depending on scrollRect state
                var scrollPanel = playerListCanvas.transform.parent.GetComponent<Image>();
                if (Utilities.IsValid(scrollPanel))
                    scrollPanel.raycastTarget = scrollRect.enabled;
            }
        }

        /// <summary>
        /// If this scoreboard is scrollable it will try to find the local player and scroll to them.<br/>
        /// If it isn't scrollable it will lock the scroll position to the top.
        /// </summary>
        public void ScrollToLocalPlayer()
        {
            var scrollRect = playerListCanvas.transform.parent.GetComponent<ScrollRect>();
            // If this is the local players row, and we need to scroll to their position
            if (Utilities.IsValid(scrollRect) && scrollRect.enabled)
            {
                var activePlayers = manager.CurrentPlayerList;
                if (!Utilities.IsValid(activePlayers) || activePlayers.Length == 0)
                    return;

                for (var position = 0; position < activePlayers.Length; position++)
                {
                    if (Utilities.IsValid(activePlayers[position].Owner) && activePlayers[position].Owner == Networking.LocalPlayer)
                    {
                        // Find the row transform
                        var newRow = playerListCanvas.transform.GetChild(position);
                        if (Utilities.IsValid(newRow))
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

    public static class ScoreboardExtensions
    {
        public static ScoreboardPlayerRow CreateRow(this Scoreboard scoreboard, int rowID, RectTransform parent = null)
        {
            var scoreboardColumnCount = scoreboard.manager.openPutt.courses.Length + 2; // + Name + Total
            var newRow = GameObject.Instantiate(scoreboard.manager.rowPrefab).GetComponent<RectTransform>();

            var row = newRow.GetComponent<ScoreboardPlayerRow>();
            row.name = $"Player {rowID}";
            row.gameObject.SetActive(true);
            row.rowCanvas = row.GetComponent<Canvas>();
            row.scoreboard = scoreboard;
            //if (!Utilities.IsValid(parent))
            //    row.player = scoreboard.manager.openPutt.objectAssigner.transform.GetChild(rowID).GetComponent<PlayerManager>();
            row.rectTransform = newRow;
            row.columns = new ScoreboardPlayerColumn[scoreboardColumnCount];

            row.rectTransform.anchoredPosition = new Vector3(0f, -(scoreboard.rowHeight + scoreboard.rowPadding) * rowID);

            var columnXOffset = 0f;
            for (var col = 0; col < scoreboardColumnCount; col++)
            {
                var rect = GameObject.Instantiate(scoreboard.manager.colPrefab).GetComponent<RectTransform>();

                if (col == 0)
                    rect.name = "Player Name";
                else if (col == scoreboardColumnCount - 1)
                    rect.name = "Player Total Score";
                else
                    rect.name = $"Course {col}";

                var rowCol = rect.GetComponent<ScoreboardPlayerColumn>();
                rowCol.scoreboardRow = row;
                rowCol.colBackground = rect.GetComponent<Image>();
                rowCol.colText = rect.GetChild(0).GetComponent<TextMeshProUGUI>();
                row.columns[col] = rowCol;

                rect.SetParent(newRow, false);
                rect.anchoredPosition = new Vector3(columnXOffset, 0f);

                if (col == 0)
                {
                    rect.sizeDelta = new Vector2(scoreboard.nameColumnWidth, scoreboard.rowHeight);
                }
                else if (col == scoreboardColumnCount - 1)
                {
                    rect.sizeDelta = new Vector2(scoreboard.totalColumnWidth, scoreboard.rowHeight);
                }
                else
                {
                    var widthForEachHole = (scoreboard.rectTransform.sizeDelta.x - scoreboard.nameColumnWidth - scoreboard.totalColumnWidth - (scoreboard.columnPadding * (scoreboardColumnCount - 1))) / (scoreboardColumnCount - 2);
                    rect.sizeDelta = new Vector2(widthForEachHole, scoreboard.rowHeight);
                }

                rect.GetChild(0).GetComponent<RectTransform>().sizeDelta = rect.sizeDelta;

                columnXOffset += rect.sizeDelta.x + scoreboard.columnPadding;
            }

            // Position this row in the list
            if (!Utilities.IsValid(parent))
                newRow.SetParent(scoreboard.playerListCanvas.GetComponent<RectTransform>(), false);
            else
                newRow.SetParent(parent, false);


            if (!Utilities.IsValid(parent) && rowID < scoreboard.scoreboardRows.Length)
                scoreboard.scoreboardRows[rowID] = row;

            return row;
        }
    }
}