using System;
using TMPro;
using UdonSharp;
using UnityEngine;
using UnityEngine.UI;
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

    public enum ScoreboardInfoTab
    {
        VR,
        Desktop,
        Mobile
    }

    [UdonBehaviourSyncMode(BehaviourSyncMode.None), DefaultExecutionOrder(10)]
    public class Scoreboard : UdonSharpBehaviour
    {
        [OpenPuttDescription("A scoreboard you can place in the scene to show player scores. Make sure the ScoreboardManager has a reference to every scoreboard in the world.")]
        [OpenPuttFoldoutGroup("External References")]
        [Tooltip("This is needed to receive refresh events and give access to the player info")]
        public ScoreboardManager manager;

        public ScoreboardPlayerRow[] scoreboardRows = new ScoreboardPlayerRow[0];

        [OpenPuttFoldoutGroup("Internal References (All are required to be set)")]
        public RectTransform rectTransform;
        [OpenPuttFoldoutGroup("Internal References (All are required to be set)")]
        public Canvas myCanvas;

        [Tooltip("Optional - the colliders the VRChat UI laser hits. Disabled when the player is too far away to keep the board visible but unclickable (see ScoreboardPositioner.interactionMaxRadius). Auto-found in children if left empty.")]
        public Collider[] interactionColliders = new Collider[0];

        private bool isInteractable = true;
        [OpenPuttFoldoutGroup("Internal References (All are required to be set)")]
        public RectTransform scoreboardHeader;
        [Tooltip("Title label in the header - retitled to match whichever page is being shown (see the Header Titles group)")]
        [OpenPuttFoldoutGroup("Internal References (All are required to be set)")]
        public TextMeshProUGUI headerText;
        [OpenPuttFoldoutGroup("Internal References (All are required to be set)")]
        public Canvas settingsPanel;
        [OpenPuttFoldoutGroup("Internal References (All are required to be set)")]
        public Canvas infoPanel;
        [Tooltip("Instructions content shown on the Info tab for VR players")]
        [OpenPuttFoldoutGroup("Internal References (All are required to be set)")]
        public GameObject infoVRCanvas;
        [Tooltip("Instructions content shown on the Info tab for Desktop players")]
        [OpenPuttFoldoutGroup("Internal References (All are required to be set)")]
        public GameObject infoDesktopCanvas;
        [Tooltip("Instructions content shown on the Info tab for Mobile players")]
        [OpenPuttFoldoutGroup("Internal References (All are required to be set)")]
        public GameObject infoMobileCanvas;
        [OpenPuttFoldoutGroup("Internal References (All are required to be set)")]
        public Canvas devModelPanel;
        [OpenPuttFoldoutGroup("Internal References (All are required to be set)")]
        public Canvas openPuttPanel;
        [OpenPuttFoldoutGroup("Internal References (All are required to be set)")]
        public RectTransform parRowPanel;
        [OpenPuttFoldoutGroup("Internal References (All are required to be set)")]
        public RectTransform topRowPanel;
        [OpenPuttFoldoutGroup("Internal References (All are required to be set)")]
        public Canvas parRowCanvas;
        [OpenPuttFoldoutGroup("Internal References (All are required to be set)")]
        public Canvas topRowCanvas;
        [OpenPuttFoldoutGroup("Internal References (All are required to be set)")]
        public Canvas scoreboardCanvas;
        [OpenPuttFoldoutGroup("Internal References (All are required to be set)")]
        public Canvas playerListCanvas;
        [OpenPuttFoldoutGroup("Internal References (All are required to be set)")]
        public GameObject rowPrefab;
        [OpenPuttFoldoutGroup("Internal References (All are required to be set)")]
        public GameObject columnPrefab;
        [OpenPuttFoldoutGroup("Internal References (All are required to be set)")]
        public Button scoreboardTabBackground;
        [OpenPuttFoldoutGroup("Internal References (All are required to be set)")]
        public Button scoreboardTimerTabBackground;
        [OpenPuttFoldoutGroup("Internal References (All are required to be set)")]
        public Button infoTabBackground;
        [Tooltip("Header button that selects the VR instructions on the Info tab")]
        [OpenPuttFoldoutGroup("Internal References (All are required to be set)")]
        public Button infoVRTabBackground;
        [Tooltip("Header button that selects the Desktop instructions on the Info tab")]
        [OpenPuttFoldoutGroup("Internal References (All are required to be set)")]
        public Button infoDesktopTabBackground;
        [Tooltip("Header button that selects the Mobile instructions on the Info tab")]
        [OpenPuttFoldoutGroup("Internal References (All are required to be set)")]
        public Button infoMobileTabBackground;
        [OpenPuttFoldoutGroup("Internal References (All are required to be set)")]
        public Button settingsTabBackground;
        [OpenPuttFoldoutGroup("Internal References (All are required to be set)")]
        public Button devModeTabBackground;
        [OpenPuttFoldoutGroup("Internal References (All are required to be set)")]
        public Button openPuttTabBackground;
        [OpenPuttFoldoutGroup("Internal References (All are required to be set)")]
        public RectTransform leftTabsPanel;
        [OpenPuttFoldoutGroup("Internal References (All are required to be set)")]
        public RectTransform rightTabsPanel;
        [OpenPuttFoldoutGroup("Internal References (All are required to be set)")]
        public TextMeshProUGUI creditsText;
        [OpenPuttFoldoutGroup("Internal References (All are required to be set)")]
        public TextMeshProUGUI updateAvailableLabel;
        [OpenPuttFoldoutGroup("Internal References (All are required to be set)")]
        public TextMeshProUGUI changelogText;

        [OpenPuttFoldoutGroup("Internal References (All are required to be set)")]
        public Image scoreboardBackground;

        [Tooltip("Every ScoreboardControl on the Settings tab - refreshed together by RefreshSettingsMenu")]
        public ScoreboardControl[] settingsControls = new ScoreboardControl[0];
        [Tooltip("Every ScoreboardControl on the Dev Mode tab - refreshed together by RefreshDevModeMenu")]
        public ScoreboardControl[] devModeControls = new ScoreboardControl[0];

        // Authoritative HSV for the ball-colour sliders - shared by the 3 controls so hue/saturation survive
        // greyscale (S=0) and black (V=0), where RGB can't recover them. Reseeded from BallColor when they diverge.
        [HideInInspector] public float ballColorHsvH;
        [HideInInspector] public float ballColorHsvS;
        [HideInInspector] public float ballColorHsvV;

        #region Dev Mode Stuff

        [OpenPuttFoldoutGroup("Internal References (All are required to be set)")]
        public TextMeshProUGUI devModeLastClubHitSpeed;
        [OpenPuttFoldoutGroup("Internal References (All are required to be set)")]
        public TextMeshProUGUI devModeLastClubHitDirBias;
        [OpenPuttFoldoutGroup("Internal References (All are required to be set)")]
        public TextMeshProUGUI devModeBallSpeed;
        [OpenPuttFoldoutGroup("Internal References (All are required to be set)")]
        public TextMeshProUGUI devModeClubSpeed;

        [OpenPuttFoldoutGroup("Internal References (All are required to be set)")]
        public Transform devModeSettingsBox;

        #endregion

        [OpenPuttFoldoutGroup("Internal References (All are required to be set)")]
        public Sprite checkboxOn;
        [OpenPuttFoldoutGroup("Internal References (All are required to be set)")]
        public Sprite checkboxOff;
        [OpenPuttFoldoutGroup("Internal References (All are required to be set)")]
        public Button resetButton;
        [OpenPuttFoldoutGroup("Internal References (All are required to be set)")]
        public Button resetConfirmButton;
        [OpenPuttFoldoutGroup("Internal References (All are required to be set)")]
        public Button resetCancelButton;

        [Space]
        [OpenPuttFoldoutGroup("Sizing")]
        public float nameColumnWidth = 0.35f;

        [OpenPuttFoldoutGroup("Sizing")]
        public float totalColumnWidth = 0.2f;
        [OpenPuttFoldoutGroup("Sizing")]
        public float columnPadding = 0.005f;
        [Tooltip("Empty space left at the left and right edges of every row. Taken off the width the hole columns share between them.")]
        [OpenPuttFoldoutGroup("Sizing")]
        public float sidePadding = 0f;
        [OpenPuttFoldoutGroup("Sizing")]
        public float rowPadding = 0.01f;
        [OpenPuttFoldoutGroup("Sizing")]
        public float rowHeight = 0.15f;

        [Space]
        [Tooltip("Header title for the normal scores page")]
        [OpenPuttFoldoutGroup("Header Titles")]
        public string titleScoreboard = "Scoreboard";
        [Tooltip("Header title for the scores page while it is showing lap times instead of shots")]
        [OpenPuttFoldoutGroup("Header Titles")]
        public string titleSpeedGolf = "Speed Golf";
        [Tooltip("Header title for the Info page. {tab} is replaced with the name of the selected platform tab below.")]
        [OpenPuttFoldoutGroup("Header Titles")]
        public string titleInfo = "How To Play - {tab}";
        [Tooltip("Header title for the Settings page")]
        [OpenPuttFoldoutGroup("Header Titles")]
        public string titleSettings = "Settings";
        [OpenPuttFoldoutGroup("Header Titles")]
        public string titleDevMode = "Dev Mode";
        [OpenPuttFoldoutGroup("Header Titles")]
        public string titleOpenPutt = "OpenPutt";

        [Tooltip("Name used for {tab} while the VR instructions are showing")]
        [OpenPuttFoldoutGroup("Header Titles")]
        public string titleInfoVRTab = "VR";
        [Tooltip("Name used for {tab} while the Desktop instructions are showing")]
        [OpenPuttFoldoutGroup("Header Titles")]
        public string titleInfoDesktopTab = "Desktop";
        [Tooltip("Name used for {tab} while the Mobile instructions are showing")]
        [OpenPuttFoldoutGroup("Header Titles")]
        public string titleInfoMobileTab = "Mobile";

        private bool initializedUI;
        public bool HasInitializedUI => initializedUI;
        private float totalHeightOfScrollViewport;
        private ScoreboardView _currentScoreboardView = ScoreboardView.Settings;
        private ScoreboardInfoTab _currentInfoTab = ScoreboardInfoTab.Desktop;

        // The background colours the info tabs are given in the editor, captured before we ever recolour them
        private bool infoTabDefaultsCaptured;
        private Color infoVRTabDefaultColour;
        private Color infoDesktopTabDefaultColour;
        private Color infoMobileTabDefaultColour;

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

                            // Coming back to the Info tab always resets to the local player's platform instructions
                            CurrentInfoTab = GetLocalPlayerInfoTab();
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

                            RefreshSettingsMenu();

                            OnResetCancel();
                            break;
                    }

                    // Info sub-canvases don't inherit the parent canvas.enabled state, so hide them when leaving the Info tab
                    if (!infoPanel.enabled)
                    {
                        if (Utilities.IsValid(infoVRCanvas))
                            infoVRCanvas.SetActive(false);
                        if (Utilities.IsValid(infoDesktopCanvas))
                            infoDesktopCanvas.SetActive(false);
                        if (Utilities.IsValid(infoMobileCanvas))
                            infoMobileCanvas.SetActive(false);
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
                                manager.openPutt._SavePersistantData();
                                break;
                        }
                    }
                }

                _currentScoreboardView = value;
                UpdateTabColours();
                RefreshHeaderText();
            }
        }

        /// <summary>
        /// Which platform's instructions are shown on the Info tab. Setting it swaps the visible instructions canvas and updates the tab highlight.
        /// </summary>
        public ScoreboardInfoTab CurrentInfoTab
        {
            get => _currentInfoTab;
            set
            {
                _currentInfoTab = value;

                if (Utilities.IsValid(infoVRCanvas))
                    infoVRCanvas.SetActive(value == ScoreboardInfoTab.VR);
                if (Utilities.IsValid(infoDesktopCanvas))
                    infoDesktopCanvas.SetActive(value == ScoreboardInfoTab.Desktop);
                if (Utilities.IsValid(infoMobileCanvas))
                    infoMobileCanvas.SetActive(value == ScoreboardInfoTab.Mobile);

                UpdateInfoTabColours();
                RefreshHeaderText();
            }
        }

        /// <summary>
        /// Works out which instructions tab matches the local player's current platform (VR / Desktop / Mobile)
        /// </summary>
        private ScoreboardInfoTab GetLocalPlayerInfoTab()
        {
            // GetPlatform() already picks the right platform via #if UNITY_ANDROID/UNITY_IOS compiler directives
            switch (Networking.LocalPlayer.GetPlatform())
            {
                case DevicePlatform.PCVR:
                case DevicePlatform.AndroidVR:
                    return ScoreboardInfoTab.VR;
                case DevicePlatform.AndroidMobile:
                    return ScoreboardInfoTab.Mobile;
                default:
                    return ScoreboardInfoTab.Desktop;
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
                if (!string.IsNullOrEmpty(manager.extraCreditsText))
                    creditsText.text += "\n" + manager.extraCreditsText;
            }

            // The UI laser hits these colliders - disabling them blocks clicks without hiding the board
            if (interactionColliders == null || interactionColliders.Length == 0)
                interactionColliders = GetComponentsInChildren<Collider>(true);

            SendCustomEventDelayedSeconds(nameof(InitUI), 0.05f);

            // IsUserInVR() is unreliable in the first frames after load, so re-pick the platform tab once it settles
            SendCustomEventDelayedSeconds(nameof(SelectInfoTabForPlatform), 1f);
        }

        /// <summary>
        /// Re-selects the info tab for the local player's platform. Retries until the local player is valid, then only applies while the Info view is showing so it doesn't yank a player who already switched tabs.
        /// </summary>
        public void SelectInfoTabForPlatform()
        {
            if (!OpenPuttUtils.LocalPlayerIsValid())
            {
                SendCustomEventDelayedSeconds(nameof(SelectInfoTabForPlatform), 1f);
                return;
            }

            if (_currentScoreboardView == ScoreboardView.Info)
                CurrentInfoTab = GetLocalPlayerInfoTab();
        }

        /// <summary>
        /// Toggles click acceptance by enabling/disabling the laser colliders, keeping the board visible (see <see cref="ScoreboardPositioner.interactionMaxRadius"/>)
        /// </summary>
        public void SetInteractable(bool state)
        {
            if (isInteractable == state || interactionColliders == null)
                return;

            isInteractable = state;

            foreach (var interactionCollider in interactionColliders)
                if (Utilities.IsValid(interactionCollider))
                    interactionCollider.enabled = state;
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

            foreach (var control in settingsControls)
                if (Utilities.IsValid(control))
                    control.Refresh();
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

        public void OnToggleInfoVR()
        {
            CurrentInfoTab = ScoreboardInfoTab.VR;
        }

        public void OnToggleInfoDesktop()
        {
            CurrentInfoTab = ScoreboardInfoTab.Desktop;
        }

        public void OnToggleInfoMobile()
        {
            CurrentInfoTab = ScoreboardInfoTab.Mobile;
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
                pm._ResetPlayerScores();

                manager.openPutt.eventHandler.OnPlayerScoreReset(pm.Owner);

                if (Utilities.IsValid(pm.golfClub))
                {
                    if (Utilities.IsValid(pm.golfClub.pickup))
                        pm.golfClub.pickup.Drop();
                    if (Utilities.IsValid(pm.golfClub.openPuttSync))
                        pm.golfClub.openPuttSync._Respawn();
                }

                if (Utilities.IsValid(pm.golfBall))
                {
                    if (pm.golfBall.GetComponent<VRCPickup>() != null)
                        pm.golfBall.GetComponent<VRCPickup>().Drop();
                    if (Utilities.IsValid(pm.golfClub.openPuttSync))
                        pm.golfClub.openPuttSync._Respawn();
                    pm.golfBall.BallIsMoving = false;
                }

                pm._RequestSync();

                pm._UpdateTotals();

                pm.openPutt._OnPlayerUpdate(pm);

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

            foreach (var control in devModeControls)
                if (Utilities.IsValid(control))
                    control.Refresh();
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

        /// <summary>
        /// Retitles the header label to match the page that is currently showing. The Info title can include a {tab} placeholder which is swapped for the selected sub tab's name.
        /// </summary>
        public void RefreshHeaderText()
        {
            if (!Utilities.IsValid(headerText))
                return;

            var newTitle = "";
            switch (_currentScoreboardView)
            {
                case ScoreboardView.Scoreboard:
                    newTitle = Utilities.IsValid(manager) && manager.SpeedGolfMode ? titleSpeedGolf : titleScoreboard;
                    break;
                case ScoreboardView.Info:
                    newTitle = titleInfo;
                    break;
                case ScoreboardView.Settings:
                    newTitle = titleSettings;
                    break;
                case ScoreboardView.DevMode:
                    newTitle = titleDevMode;
                    break;
                case ScoreboardView.OpenPutt:
                    newTitle = titleOpenPutt;
                    break;
            }

            if (newTitle == null)
                newTitle = "";

            var subTabName = CurrentSubTabName();
            newTitle = newTitle.Replace("{tab}", subTabName == null ? "" : subTabName);

            if (headerText.text != newTitle)
                headerText.text = newTitle;
        }

        /// <summary>
        /// The name of the sub tab that is selected on the page currently showing, or an empty string for pages that don't have any
        /// </summary>
        private string CurrentSubTabName()
        {
            switch (_currentScoreboardView)
            {
                case ScoreboardView.Info:
                    switch (_currentInfoTab)
                    {
                        case ScoreboardInfoTab.VR:
                            return titleInfoVRTab;
                        case ScoreboardInfoTab.Mobile:
                            return titleInfoMobileTab;
                        default:
                            return titleInfoDesktopTab;
                    }
                default:
                    return "";
            }
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

            // Header colours are now current, so repaint the sub tabs to match
            UpdateInfoTabColours();
        }

        /// <summary>
        /// Recolours the VR/Desktop/Mobile instructions tabs: the selected one takes the Scoreboard header button's colour, the rest keep their editor background.
        /// </summary>
        public void UpdateInfoTabColours()
        {
            CaptureInfoTabDefaultColours();

            // Selected tab matches the "Scoreboard" button in the header
            var selectedColour = Utilities.IsValid(scoreboardTabBackground) && Utilities.IsValid(scoreboardTabBackground.targetGraphic)
                ? scoreboardTabBackground.targetGraphic.color
                : Color.white;

            SetSubTabColour(infoVRTabBackground, _currentInfoTab == ScoreboardInfoTab.VR ? selectedColour : infoVRTabDefaultColour);
            SetSubTabColour(infoDesktopTabBackground, _currentInfoTab == ScoreboardInfoTab.Desktop ? selectedColour : infoDesktopTabDefaultColour);
            SetSubTabColour(infoMobileTabBackground, _currentInfoTab == ScoreboardInfoTab.Mobile ? selectedColour : infoMobileTabDefaultColour);
        }

        /// <summary>
        /// Remembers the background colour each info tab was given in the editor so we can restore it when the tab is deselected
        /// </summary>
        private void CaptureInfoTabDefaultColours()
        {
            if (infoTabDefaultsCaptured)
                return;

            if (Utilities.IsValid(infoVRTabBackground) && Utilities.IsValid(infoVRTabBackground.targetGraphic))
                infoVRTabDefaultColour = infoVRTabBackground.targetGraphic.color;
            if (Utilities.IsValid(infoDesktopTabBackground) && Utilities.IsValid(infoDesktopTabBackground.targetGraphic))
                infoDesktopTabDefaultColour = infoDesktopTabBackground.targetGraphic.color;
            if (Utilities.IsValid(infoMobileTabBackground) && Utilities.IsValid(infoMobileTabBackground.targetGraphic))
                infoMobileTabDefaultColour = infoMobileTabBackground.targetGraphic.color;

            infoTabDefaultsCaptured = true;
        }

        private void SetSubTabColour(Button tab, Color colour)
        {
            if (!Utilities.IsValid(tab) || !Utilities.IsValid(tab.targetGraphic) || tab.targetGraphic.color == colour)
                return;

            tab.targetGraphic.color = colour;
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

            var isPlayerListRow = !Utilities.IsValid(parent);
            if (isPlayerListRow)
                parent = scoreboard.playerListCanvas.GetComponent<RectTransform>();

            var row = newRow.GetComponent<ScoreboardPlayerRow>();
            row.name = $"Player {rowID}";
            row.gameObject.SetActive(true);
            row.rowCanvas = row.GetComponent<Canvas>();
            row.scoreboard = scoreboard;
            //if (!Utilities.IsValid(parent))
            //    row.player = scoreboard.manager.openPutt.objectAssigner.transform.GetChild(rowID).GetComponent<PlayerManager>();
            row.rectTransform = newRow;
            row.columns = new ScoreboardPlayerColumn[scoreboardColumnCount];

            // Parent the row first so it picks up the panel width before we lay any columns out
            newRow.SetParent(parent, false);

            row.rectTransform.anchoredPosition = new Vector3(0f, -(scoreboard.rowHeight + scoreboard.rowPadding) * rowID);

            // Rows stretch to fill the panel they sit in, which is inset from the scoreboard canvas - measuring the
            // row itself keeps the side padding even on both edges instead of dumping the difference on the right
            var rowWidth = newRow.rect.width;
            if (rowWidth <= 0f)
                rowWidth = scoreboard.rectTransform.sizeDelta.x;

            // Width the columns actually get to use once the side padding is taken off both edges
            var usableWidth = rowWidth - (scoreboard.sidePadding * 2f);

            var columnXOffset = scoreboard.sidePadding;
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
                    var widthForEachHole = (usableWidth - scoreboard.nameColumnWidth - scoreboard.totalColumnWidth - (scoreboard.columnPadding * (scoreboardColumnCount - 1))) / (scoreboardColumnCount - 2);
                    rect.sizeDelta = new Vector2(Mathf.Max(widthForEachHole, 0f), scoreboard.rowHeight);
                }

                rect.GetChild(0).GetComponent<RectTransform>().sizeDelta = rect.sizeDelta;

                columnXOffset += rect.sizeDelta.x + scoreboard.columnPadding;
            }

            if (isPlayerListRow && rowID < scoreboard.scoreboardRows.Length)
                scoreboard.scoreboardRows[rowID] = row;

            return row;
        }
    }
}