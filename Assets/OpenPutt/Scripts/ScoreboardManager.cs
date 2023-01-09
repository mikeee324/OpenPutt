
using System;
using Cyan.PlayerObjectPool;
using UdonSharp;
using UnityEngine;
using UnityEngine.SocialPlatforms.Impl;
using VRC.SDKBase;

namespace mikeee324.OpenPutt
{
    public enum PlayerSyncType
    {
        /// <summary>
        /// All score updates will be displayed as soon as the refresh interval allows
        /// </summary>
        All,
        /// <summary>
        /// Only player course start/end updates will be synced
        /// </summary>
        StartAndFinish,
        /// <summary>
        /// Only player course finish events will be synced
        /// </summary>
        FinishOnly
    }

    [UdonBehaviourSyncMode(BehaviourSyncMode.None)]
    public class ScoreboardManager : UdonSharpBehaviour
    {
        [Header("The scoreboard manager handles refreshing all scoreboards in the scene and makes sure it doesn't happen too often.")]
        public OpenPutt openPutt;

        public Scoreboard[] scoreboards;

        [Space, Header("Settings"), Range(1f, 5f)]
        public float maxRefreshInterval = 1f;
        private float refreshTimer = 0f;

        /// <summary>
        /// Used to prevent updates running every frame as it's not needed here
        /// </summary>
        private float updateTimer = 0f;

        [Tooltip("Should the scoreboards show all players in a scrollable list? (This has a noticeable performance impact)")]
        public bool showAllPlayers = false;

        void Start()
        {
            if (openPutt == null)
            {
                Utils.LogError(this, "Missing OpenPutt Reference! Disabling Scoreboards");
                gameObject.SetActive(false);
                foreach (Scoreboard scoreboard in scoreboards)
                    scoreboard.gameObject.SetActive(false);
                return;
            }

            foreach (Scoreboard scoreboard in scoreboards)
                scoreboard.manager = this;

            RequestRefresh();
        }

        private void Update()
        {
            updateTimer += Time.deltaTime;

            if (updateTimer > maxRefreshInterval)
            {
                foreach (Scoreboard scoreboard in scoreboards)
                {
                    bool isNowActive = false;
                    switch (scoreboard.scoreboardVisiblility)
                    {
                        case ScoreboardVisibility.AlwaysVisible:
                            isNowActive = true;
                            break;
                        case ScoreboardVisibility.NearbyOnly:
                            if (Networking.LocalPlayer != null && Networking.LocalPlayer.IsValid())
                                if (Vector3.Distance(Networking.LocalPlayer.GetPosition(), scoreboard.transform.position) < scoreboard.nearbyMaxRadius)
                                    isNowActive = true;
                            break;
                        case ScoreboardVisibility.NearbyAndCourseFinished:
                            if (Networking.LocalPlayer != null && Networking.LocalPlayer.IsValid())
                                if (Vector3.Distance(Networking.LocalPlayer.GetPosition(), scoreboard.transform.position) < scoreboard.nearbyMaxRadius)
                                    isNowActive = true;

                            if (isNowActive && scoreboard.attachedToCourse >= 0)
                            {
                                PlayerManager playerManager = openPutt != null ? openPutt.LocalPlayerManager : null;

                                if (playerManager == null || !playerManager.IsReady || playerManager.courseStates[scoreboard.attachedToCourse] != CourseState.Completed)
                                    isNowActive = false;
                            }
                            break;
                        case ScoreboardVisibility.Hidden:
                            isNowActive = false;
                            break;
                    }

                    // If the state of this scoreboard has changed
                    if (scoreboard.gameObject.activeInHierarchy != isNowActive)
                    {
                        scoreboard.gameObject.SetActive(isNowActive);
                        if (isNowActive)
                        {
                            // Swap back to the scoreboard when disabling this scoreboard
                            scoreboard.CurrentScoreboardView = ScoreboardView.Scoreboard;
                            scoreboard.RefreshScoreboard();
                        }
                    }
                }
                updateTimer = 0f;
            }

            if (refreshTimer != -1f)
            {
                refreshTimer += Time.deltaTime;

                if (refreshTimer >= maxRefreshInterval)
                {
                    refreshTimer = -1f;
                    foreach (Scoreboard scoreboard in scoreboards)
                        if (scoreboard.gameObject.activeInHierarchy)
                            scoreboard.RefreshScoreboard();
                    if (Utils.LocalPlayerIsValid() && Networking.LocalPlayer.displayName == "mikeee324")
                    {
                        string playerStatesLog = "Scoreboards updating! Player States\r\n";
                        if (openPutt != null && openPutt.objectPool != null && openPutt.objectAssigner != null)
                        {
                            Component[] activeObjects = openPutt.objectAssigner._GetActivePoolObjects();

                            for (int i = 0; i < activeObjects.Length; i++)
                            {
                                PlayerManager pm = activeObjects[i].GetComponent<PlayerManager>();
                                if (pm != null)
                                    playerStatesLog += $"{pm.ToString()}\r\n";
                            }
                        }
                        Utils.Log(this, playerStatesLog);
                    }
                }
            }
        }

        public void RequestRefresh()
        {
            if (refreshTimer <= 0f)
                refreshTimer = 0f;
        }
    }
}