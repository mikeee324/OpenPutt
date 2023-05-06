using System;
using TMPro;
using UdonSharp;
using UnityEngine;
using UnityEngine.UI;
using VRC.SDK3.Components;
using VRC.SDKBase;

namespace mikeee324.OpenPutt
{
    public enum ScoreboardView
    {
        Scoreboard,
        Info,
        Settings,
        DevMode
    }

    [UdonBehaviourSyncMode(BehaviourSyncMode.None), DefaultExecutionOrder(10)]
    public class Scoreboard : UdonSharpBehaviour
    {
        [Header("This is a scoreboard that you can drag into the scene to display player scores. Make sure the ScoreboardManager has a reference to all scoreboards!")]
        [Header("External References")]
        [Tooltip("This is needed to receive refresh events and give access to the player info")]
        public ScoreboardManager manager;

        [Header("Internal References (All are required to be set)")]
        public ScoreboardPlayerRow[] scoreboardRows = new ScoreboardPlayerRow[0];
        public RectTransform rectTransform;
        public Canvas myCanvas;
        public RectTransform scoreboardHeader;
        public Canvas settingsPanel;
        public Canvas infoPanel;
        public Canvas devModelPanel;
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

        public UnityEngine.UI.Image scoreboardBackground;

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
        public UnityEngine.UI.Image ballColorPreview;

        #region Dev Mode Stuff
        public Slider devModeClubWaitSlider;
        public Slider devModeClubBackstepSlider;
        public Slider devModeClubVelSmoothSlider;
        public Slider devModeBallWeightSlider;
        public Slider devModeBallFrictionSlider;
        public Slider devModeBallDragSlider;
        public Slider devModeBallADragSlider;
        public TextMeshProUGUI devModeClubWaitValueLabel;
        public TextMeshProUGUI devModeClubBackstepValueLabel;
        public TextMeshProUGUI devModeClubVelSmoothValueLabel;
        public TextMeshProUGUI devModeBallWeightValueLabel;
        public TextMeshProUGUI devModeBallFrictionValueLabel;
        public TextMeshProUGUI devModeBallDragValueLabel;
        public TextMeshProUGUI devModeBallADragValueLabel;
        public UnityEngine.UI.Image devModeExperimentalClubCollider;
        public Dropdown devModeColliderVelTypeDropdown;
        #endregion

        public UnityEngine.UI.Image verticalHitsCheckbox;
        public UnityEngine.UI.Image isPlayingCheckbox;
        public UnityEngine.UI.Image leftHandModeCheckbox;
        public UnityEngine.UI.Image enableBigShaftCheckbox;
        public UnityEngine.UI.Image courseReplaysCheckbox;
        public Material checkboxOn;
        public Material checkboxOff;
        public Button resetButton;
        public Button resetConfirmButton;
        public Button resetCancelButton;

        [Space, Header("Sizing")]
        public float nameColumnWidth = 0.35f;
        public float totalColumnWidth = 0.2f;
        public float columnPadding = 0.005f;
        public float rowPadding = 0.01f;
        public float rowHeight = 0.15f;

        private bool initializedUI = false;
        public bool HasInitializedUI => initializedUI;
        private float totalHeightOfScrollViewport = 0f;
        private ScoreboardView _currentScoreboardView = ScoreboardView.Settings;

        public int NumberOfColumns => manager != null && manager.openPutt != null ? manager.openPutt.courses.Length + 2 : 0;
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
                            infoPanel.enabled = false;
                            scoreboardCanvas.enabled = true;
                            break;
                        case ScoreboardView.Info:
                            settingsPanel.gameObject.SetActive(false);
                            devModelPanel.gameObject.SetActive(false);
                            infoPanel.enabled = true;
                            scoreboardCanvas.enabled = false;
                            break;
                        case ScoreboardView.DevMode:
                            settingsPanel.gameObject.SetActive(false);
                            devModelPanel.gameObject.SetActive(true);
                            infoPanel.enabled = false;
                            scoreboardCanvas.enabled = false;

                            RefreshSettingsMenu();
                            break;
                        case ScoreboardView.Settings:
                            settingsPanel.gameObject.SetActive(true);
                            devModelPanel.gameObject.SetActive(false);
                            infoPanel.enabled = false;
                            scoreboardCanvas.enabled = false;

                            RefreshSettingsMenu();

                            OnResetCancel();
                            break;
                    }

                    // Toggle extra canvases (parent canvas.enabled doesn't seem to be passed down properly)
                    if (scoreboardCanvas != null)
                    {
                        if (playerListCanvas != null)
                            playerListCanvas.enabled = scoreboardCanvas.enabled;
                        if (topRowCanvas != null)
                            topRowCanvas.enabled = scoreboardCanvas.enabled;
                        if (parRowCanvas != null)
                            parRowCanvas.enabled = scoreboardCanvas.enabled;

                        for (int i = 0; i < scoreboardRows.Length; i++)
                            if (manager.CurrentPlayerList != null && i < manager.CurrentPlayerList.Length)
                                scoreboardRows[i].UpdateVisibility(manager.CurrentPlayerList[i]);
                    }

                    if (manager != null)
                        manager.requestedScoreboardView = value;
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

            if (topRowCanvas == null)
                topRowCanvas = topRowPanel.transform.GetChild(0).GetComponent<Canvas>();
            if (parRowCanvas == null)
                parRowCanvas = parRowPanel.transform.GetChild(0).GetComponent<Canvas>();

            CurrentScoreboardView = ScoreboardView.Info;

            // This is here because i haven't figured out how to make editor scripts properly yet
            scoreboardRows = new ScoreboardPlayerRow[playerListCanvas.transform.childCount];
            for (int i = 0; i < playerListCanvas.transform.childCount; i++)
                scoreboardRows[i] = playerListCanvas.transform.GetChild(i).GetComponent<ScoreboardPlayerRow>();

            // Make sure that the scoreboard has basically no thickness so the laser pointer works properly
            if (rectTransform == null)
                rectTransform = transform.GetChild(0).GetComponent<RectTransform>();
            if (rectTransform != null)
            {
                Vector3 scoreboardScale = rectTransform.localScale;
                if (scoreboardScale.z != 0.01f)
                    rectTransform.localScale = new Vector3(scoreboardScale.x, scoreboardScale.y, 0.01f);
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
            if (manager == null || manager.openPutt == null || manager.openPutt.LocalPlayerManager == null)
                return;

            PlayerManager playerManager = manager.openPutt.LocalPlayerManager;

            clubPowerSlider.value = playerManager.golfClub.forceMultiplier;
            clubPowerValueLabel.text = String.Format("{0:F2}x", clubPowerSlider.value);

            sfxVolumeSlider.value = playerManager.openPutt.SFXController.Volume;
            sfxVolumeValueLabel.text = String.Format("{0:P0}", sfxVolumeSlider.value);

            devModeBallWeightSlider.value = playerManager.golfBall.BallWeight;
            devModeBallWeightValueLabel.text = String.Format("{0:F2}", devModeBallWeightSlider.value);

            devModeBallFrictionSlider.value = playerManager.golfBall.BallFriction;
            devModeBallFrictionValueLabel.text = String.Format("{0:F2}", devModeBallFrictionSlider.value);

            devModeClubWaitSlider.value = playerManager.golfClub.putter.hitWaitFrames;
            devModeClubWaitValueLabel.text = String.Format("{0:F0}", devModeClubWaitSlider.value);

            devModeClubBackstepSlider.value = playerManager.golfClub.putter.multiFrameAverageMaxBacksteps;
            devModeClubBackstepValueLabel.text = String.Format("{0:F0}", devModeClubBackstepSlider.value);

            devModeClubVelSmoothSlider.value = playerManager.golfClub.putter.singleFrameSmoothFactor;
            devModeClubVelSmoothValueLabel.text = String.Format("{0:F2}", devModeClubVelSmoothSlider.value);

            devModeColliderVelTypeDropdown.value = (int)playerManager.golfClub.putter.velocityCalculationType;
            devModeClubVelSmoothSlider.transform.parent.gameObject.SetActive(devModeColliderVelTypeDropdown.value == 1);
            devModeClubBackstepSlider.transform.parent.gameObject.SetActive(devModeColliderVelTypeDropdown.value == 2);

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
            devModeExperimentalClubCollider.material = playerManager.golfClub.putter.smoothedHitDirection ? checkboxOn : checkboxOff;
        }

        public void UpdateBallColorPreview()
        {
            if (manager == null || manager.openPutt == null || manager.openPutt.LocalPlayerManager == null)
                return;

            PlayerManager playerManager = manager.openPutt.LocalPlayerManager;
            ballColorPreview.color = playerManager.BallColor;
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

        public void OnBallWeightReset()
        {
            PlayerManager player = manager.openPutt.LocalPlayerManager;

            if (player == null) return;

            player.golfBall.BallWeight = player.golfBall.DefaultBallWeight;

            RefreshSettingsMenu();
        }

        public void OnBallWeightChanged()
        {
            PlayerManager player = manager.openPutt.LocalPlayerManager;

            if (player == null) return;

            player.golfBall.BallWeight = devModeBallWeightSlider.value;
            devModeBallWeightValueLabel.text = String.Format("{0:F2}", devModeBallWeightSlider.value);
        }

        public void OnClubWaitFramesReset()
        {
            PlayerManager player = manager.openPutt.LocalPlayerManager;

            if (player == null) return;

            player.golfClub.putter.hitWaitFrames = 2;

            RefreshSettingsMenu();
        }

        public void OnClubWaitFramesChanged()
        {
            PlayerManager player = manager.openPutt.LocalPlayerManager;

            if (player == null) return;

            player.golfClub.putter.hitWaitFrames = Mathf.RoundToInt(devModeClubWaitSlider.value);
            devModeClubWaitValueLabel.text = String.Format("{0:F0}", devModeClubWaitSlider.value);
        }

        public void OnClubHitBackstepReset()
        {
            PlayerManager player = manager.openPutt.LocalPlayerManager;

            if (player == null) return;

            player.golfClub.putter.multiFrameAverageMaxBacksteps = 4;

            RefreshSettingsMenu();
        }

        public void OnClubHitBackstepChanged()
        {
            PlayerManager player = manager.openPutt.LocalPlayerManager;

            if (player == null) return;

            player.golfClub.putter.multiFrameAverageMaxBacksteps = Mathf.RoundToInt(devModeClubBackstepSlider.value);
            devModeClubBackstepValueLabel.text = String.Format("{0:F0}", devModeClubBackstepSlider.value);
        }

        public void OnClubHitVelSmoothReset()
        {
            PlayerManager player = manager.openPutt.LocalPlayerManager;

            if (player == null) return;

            player.golfClub.putter.multiFrameAverageMaxBacksteps = 4;

            RefreshSettingsMenu();
        }

        public void OnClubHitVelSmoothChanged()
        {
            PlayerManager player = manager.openPutt.LocalPlayerManager;

            if (player == null) return;

            player.golfClub.putter.singleFrameSmoothFactor = Mathf.RoundToInt(devModeClubVelSmoothSlider.value);
            devModeClubVelSmoothValueLabel.text = String.Format("{0:F2}", devModeClubVelSmoothSlider.value);
        }

        public void OnBallFrictionReset()
        {
            PlayerManager player = manager.openPutt.LocalPlayerManager;

            if (player == null) return;

            player.golfBall.BallFriction = player.golfBall.DefaultBallFriction;

            RefreshSettingsMenu();
        }

        public void OnBallFrictionChanged()
        {
            PlayerManager player = manager.openPutt.LocalPlayerManager;

            if (player == null) return;

            player.golfBall.BallFriction = devModeBallFrictionSlider.value;
            devModeBallFrictionValueLabel.text = String.Format("{0:F2}", devModeBallFrictionSlider.value);
        }

        public void OnBallADragReset()
        {
            PlayerManager player = manager.openPutt.LocalPlayerManager;

            if (player == null) return;

            player.golfBall.BallAngularDrag = player.golfBall.DefaultBallAngularDrag;

            RefreshSettingsMenu();
        }

        public void OnBallADragChanged()
        {
            PlayerManager player = manager.openPutt.LocalPlayerManager;

            if (player == null) return;

            player.golfBall.BallAngularDrag = devModeBallADragSlider.value;
            devModeBallADragValueLabel.text = String.Format("{0:F2}", devModeBallADragSlider.value);
        }

        public void OnBallDragReset()
        {
            PlayerManager player = manager.openPutt.LocalPlayerManager;

            if (player == null) return;

            player.golfBall.BallDrag = player.golfBall.DefaultBallDrag;

            RefreshSettingsMenu();
        }

        public void OnBallDragChanged()
        {
            PlayerManager player = manager.openPutt.LocalPlayerManager;

            if (player == null) return;

            player.golfBall.BallDrag = devModeBallDragSlider.value;
            devModeBallDragValueLabel.text = String.Format("{0:F2}", devModeBallDragSlider.value);
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

        public void OnToggleExperimentalClub()
        {
            if (manager == null || manager.openPutt == null || manager.openPutt.LocalPlayerManager == null)
                return;

            PlayerManager playerManager = manager.openPutt.LocalPlayerManager;

            playerManager.golfClub.putter.smoothedHitDirection = !playerManager.golfClub.putter.smoothedHitDirection;

            RefreshSettingsMenu();
        }

        public void OnColliderVelocityTypeChanged()
        {
            if (manager == null || manager.openPutt == null || manager.openPutt.LocalPlayerManager == null)
                return;

            PlayerManager playerManager = manager.openPutt.LocalPlayerManager;
            int val = devModeColliderVelTypeDropdown.value;
            playerManager.golfClub.putter.velocityCalculationType = (ClubColliderVelocityType)val;

            devModeClubVelSmoothSlider.transform.parent.gameObject.SetActive(val == 1);
            devModeClubBackstepSlider.transform.parent.gameObject.SetActive(val == 2);

            RefreshSettingsMenu();
        }

        public void OnTogglePlayerManager()
        {
            Utils.Log(this, "Player manager toggle");
            if (manager == null || manager.openPutt == null || manager.openPutt.LocalPlayerManager == null)
                return;
            Utils.Log(this, "Player manager toggle2");

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
                VRCPickup pickup;
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

            playerManager.UpdateTotals();

            playerManager.openPutt.OnPlayerUpdate(playerManager);
            playerManager.RequestSync();
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
            playerManager.golfClub.putter.smoothedHitDirection = !playerManager.golfClub.putter.smoothedHitDirection;

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
            if (manager != null)
            {
                manager.requestedScoreboardView = ScoreboardView.Settings;
                manager.OnPlayerOpenSettings(this);
            }
            CurrentScoreboardView = ScoreboardView.Settings;
        }

        public void OnToggleDevMode()
        {
            if (manager != null)
            {
                manager.requestedScoreboardView = ScoreboardView.DevMode;
                manager.OnPlayerOpenSettings(this);
            }
            CurrentScoreboardView = ScoreboardView.DevMode;
        }

        public void OnToggleInfo()
        {
            if (manager != null)
            {
                manager.requestedScoreboardView = ScoreboardView.Info;
            }
            CurrentScoreboardView = ScoreboardView.Info;
        }

        public void OnToggleTimerMode()
        {
            if (manager == null) return;

            manager.SpeedGolfMode = true;
            if (parRowPanel.transform.childCount > 0)
                parRowPanel.GetChild(0).GetComponent<ScoreboardPlayerRow>().Refresh();

            manager.requestedScoreboardView = ScoreboardView.Scoreboard;
            CurrentScoreboardView = ScoreboardView.Scoreboard;
        }

        public void OnShowScoreboard()
        {
            if (manager == null) return;

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

                pm.UpdateTotals();

                pm.openPutt.OnPlayerUpdate(pm);

                Utils.Log(this, "Player reset their scores");
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
            Color defaultBackground = Color.clear;
            Color selectedBackground = Color.white;

            Color newScoreboardCol = _currentScoreboardView == ScoreboardView.Scoreboard && (manager == null || !manager.SpeedGolfMode) ? selectedBackground : defaultBackground;
            Color newSpeedrunCol = _currentScoreboardView == ScoreboardView.Scoreboard && (manager != null && manager.SpeedGolfMode) ? selectedBackground : defaultBackground;
            Color newInfoCol = _currentScoreboardView == ScoreboardView.Info ? selectedBackground : defaultBackground;
            Color newSettingsCol = _currentScoreboardView == ScoreboardView.Settings ? selectedBackground : defaultBackground;
            Color newDevModeCol = _currentScoreboardView == ScoreboardView.DevMode ? selectedBackground : defaultBackground;

            if (scoreboardTabBackground.colors.normalColor != newScoreboardCol)
            {
                ColorBlock colorBlock = scoreboardTabBackground.colors;
                colorBlock.normalColor = newScoreboardCol;
                scoreboardTabBackground.colors = colorBlock;
            }
            if (scoreboardTimerTabBackground.colors.normalColor != newSpeedrunCol)
            {
                ColorBlock colorBlock = scoreboardTimerTabBackground.colors;
                colorBlock.normalColor = newSpeedrunCol;
                scoreboardTimerTabBackground.colors = colorBlock;
            }
            if (infoTabBackground.colors.normalColor != newInfoCol)
            {
                ColorBlock colorBlock = infoTabBackground.colors;
                colorBlock.normalColor = newInfoCol;
                infoTabBackground.colors = colorBlock;
            }
            if (settingsTabBackground.colors.normalColor != newSettingsCol)
            {
                ColorBlock colorBlock = settingsTabBackground.colors;
                colorBlock.normalColor = newSettingsCol;
                settingsTabBackground.colors = colorBlock;
            }
            if (devModeTabBackground.colors.normalColor != newDevModeCol)
            {
                ColorBlock colorBlock = devModeTabBackground.colors;
                colorBlock.normalColor = newDevModeCol;
                devModeTabBackground.colors = colorBlock;
            }
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
                UnityEngine.UI.Image scrollPanel = playerListCanvas.transform.parent.GetComponent<UnityEngine.UI.Image>();
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

    public static class ScoreboardExtensions
    {
        public static ScoreboardPlayerRow CreateRow(this Scoreboard scoreboard, int rowID, RectTransform parent = null)
        {
            int scoreboardColumnCount = scoreboard.manager.openPutt.courses.Length + 2; // + Name + Total
            RectTransform newRow = GameObject.Instantiate(scoreboard.manager.rowPrefab).GetComponent<RectTransform>();

            ScoreboardPlayerRow row = newRow.GetComponent<ScoreboardPlayerRow>();
            row.name = $"Player {rowID}";
            row.gameObject.SetActive(true);
            row.rowCanvas = row.GetComponent<Canvas>();
            row.scoreboard = scoreboard;
            //if (parent == null)
            //    row.player = scoreboard.manager.openPutt.objectAssigner.transform.GetChild(rowID).GetComponent<PlayerManager>();
            row.rectTransform = newRow;
            row.columns = new ScoreboardPlayerColumn[scoreboardColumnCount];

            row.rectTransform.anchoredPosition = new Vector3(0f, -(scoreboard.rowHeight + scoreboard.rowPadding) * rowID);

            float columnXOffset = 0f;
            for (int col = 0; col < scoreboardColumnCount; col++)
            {
                RectTransform rect = GameObject.Instantiate(scoreboard.manager.colPrefab).GetComponent<RectTransform>();

                if (col == 0)
                    rect.name = "Player Name";
                else if (col == scoreboardColumnCount - 1)
                    rect.name = "Player Total Score";
                else
                    rect.name = $"Course {col}";

                ScoreboardPlayerColumn rowCol = rect.GetComponent<ScoreboardPlayerColumn>();
                rowCol.scoreboardRow = row;
                rowCol.colBackground = rect.GetComponent<UnityEngine.UI.Image>();
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
                    float widthForEachHole = (scoreboard.rectTransform.sizeDelta.x - scoreboard.nameColumnWidth - scoreboard.totalColumnWidth - (scoreboard.columnPadding * (scoreboardColumnCount - 1))) / (scoreboardColumnCount - 2);
                    rect.sizeDelta = new Vector2(widthForEachHole, scoreboard.rowHeight);
                }

                rect.GetChild(0).GetComponent<RectTransform>().sizeDelta = rect.sizeDelta;

                columnXOffset += rect.sizeDelta.x + scoreboard.columnPadding;
            }

            // Position this row in the list
            if (parent == null)
                newRow.SetParent(scoreboard.playerListCanvas.GetComponent<RectTransform>(), false);
            else
                newRow.SetParent(parent, false);


            if (parent == null && rowID < scoreboard.scoreboardRows.Length)
                scoreboard.scoreboardRows[rowID] = row;

            return row;
        }
    }
}