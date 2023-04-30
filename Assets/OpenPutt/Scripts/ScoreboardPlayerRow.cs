﻿
using mikeee324.OpenPutt;
using UdonSharp;
using UnityEngine;
using UnityEngine.UI;
using VRC.SDKBase;
using VRC.Udon;

namespace mikeee324.OpenPutt
{
    public enum ScoreboardPlayerRowType
    {
        Normal = 0,
        Par = 1,
        Header = 2
    }

    [UdonBehaviourSyncMode(BehaviourSyncMode.None), DefaultExecutionOrder(100)]
    public class ScoreboardPlayerRow : UdonSharpBehaviour
    {
        public Scoreboard scoreboard;
        public ScoreboardPlayerRowType rowType;
        //public PlayerManager player;
        public ScoreboardPlayerColumn[] columns;
        public RectTransform rectTransform;
        public Canvas rowCanvas;

        public int CurrentPosition { get; private set; }
        public bool IsVisible
        {
            get => _isVisible;
            set
            {
                _isVisible = value;
                rowCanvas.enabled = value;
            }
        }
        public float RowHeight => scoreboard.rowHeight;
        public float RowPadding => scoreboard.rowPadding;
        public int NumberOfColumns => transform.childCount;
        private int rowIndex => CurrentPosition;
        private bool isEvenRow => rowIndex % 2 == 0;
        private bool _isVisible = false;

        private void Start()
        {
            columns = new ScoreboardPlayerColumn[NumberOfColumns];
            for (int i = 0; i < NumberOfColumns; i++)
                columns[i] = transform.GetChild(i).GetComponent<ScoreboardPlayerColumn>();
        }

        /// <summary>
        /// Updates text and colours for this row
        /// </summary>
        /// <param name="player">The player that this row is displaying (Also used to update Par/Course headings when null)</param>
        public void Refresh(PlayerManager player = null)
        {
            UpdateVisibility(player);

            for (int i = 0; i < columns.Length; i++)
                columns[i].Refresh(player);

            if (player != null)
                player.scoreboardRowNeedsUpdating = false;
        }

        /// <summary>
        /// Toggles visibility of this row
        /// </summary>
        /// <param name="player">The player that we want to display in this row</param>
        public void UpdateVisibility(PlayerManager player)
        {
            bool visible = scoreboard.scoreboardCanvas.enabled;

            if (visible && (int)rowType == (int)ScoreboardPlayerRowType.Normal)
            {
                if (player == null || !player.gameObject.activeSelf || (scoreboard.manager.hideInactivePlayers && !player.PlayerHasStartedPlaying))
                    visible = false;
            }

            if (visible && !gameObject.activeSelf)
            {
                gameObject.SetActive(true);
            }

            rowCanvas.enabled = visible;
        }

        /// <summary>
        /// Sets the position of this row in the list
        /// </summary>
        /// <param name="position">The position to move this row to</param>
        /// <returns>True if this row needs its colours updating, false if nothing else needs to happen</returns>
        public bool SetPosition(int position)
        {
            bool wasAnEvenRow = isEvenRow;

            CurrentPosition = position;

            rectTransform.anchoredPosition = new Vector3(0f, -(RowHeight + RowPadding) * position);
            // If we swapped type of row, refresh it as the background colour will need to change
            return wasAnEvenRow != isEvenRow;
        }

    }
}