using TMPro;
using UdonSharp;
using UnityEngine;
using UnityEngine.UI;
using Varneon.VUdon.ArrayExtensions;
using VRC.SDKBase;

namespace dev.mikeee324.OpenPutt
{
    /// <summary>
    /// The widget backing this control. Pure display plumbing - reused by every setting of the same kind.
    /// </summary>
    public enum ControlKind
    {
        Slider,
        Toggle,
        Dropdown,
    }

    /// <summary>
    /// Which game value this control is bound to. Grouped by value type (float / bool / int) so each
    /// type has one small switch. Adding a setting = one enum entry + one case in the matching switch.
    /// </summary>
    public enum SettingId
    {
        None = 0,

        // float (Slider) - explicit values: id is serialized as an int, so never renumber an existing entry
        ClubPower = 100,
        SfxVolume = 101,
        BgmVolume = 102,
        WorldVolume = 103,
        BallColorH = 104,
        BallColorS = 105,
        BallColorV = 106,
        DevBallWeight = 107,
        DevBallFriction = 108,
        DevBallDrag = 109,
        DevBallADrag = 110,
        // 111-113 were the frame-based velocity/hit-wait settings, replaced by the ms-based ones below.
        // Safe to retire rather than migrate - none of the dev sliders set saveOnChange, so no
        // saved player data ever held a frame count.
        DevTrackingRewindMs = 114,
        DevTrackingSmoothingMs = 115,
        DevHitAimDelayMs = 116,
        DevMinTimeBetweenHitsMs = 117,
        DevBallSnapSteps = 118,

        // bool (Toggle) - explicit values: id is serialized as an int, so never renumber an existing entry
        LeftHandMode = 200,
        ClubThrow = 201,
        ClubAutoHold = 202,
        EnableBigShaft = 203,
        PracticeMode = 204,
        DevForAll = 205,
        FootCollider = 206,
        ClubRenderer = 207,
        BallGrounded = 208,
        // 209 was the ball snapping checkbox, replaced by the DevBallSnapSteps slider (-1 = off)
        SpectatorMode = 210,
        ShoulderPickupRenderer = 211,
        BallSnapDebug = 212,
        DebugLogging = 213,

        // int (Dropdown)
        // VelocityTracking = 300,
    }

    /// <summary>
    /// One reusable settings control on the scoreboard. Configure it entirely in the inspector:
    /// <list type="bullet">
    /// <item><b>Display:</b> <see cref="kind"/>, the widget reference, <see cref="valueLabel"/> + <see cref="labelFormat"/>.</item>
    /// <item><b>Binding / where it's saved:</b> <see cref="id"/> selects the game value; <see cref="saveOnChange"/> persists it.</item>
    /// </list>
    /// <see cref="Scoreboard"/> keeps a <c>ScoreboardControl[]</c> and only ever calls <see cref="Refresh"/>.
    /// The internal switches exist because Udon can't reflect UdonSharp properties or Unity components,
    /// so the bind for those settings has to be real code.
    /// </summary>
    [UdonBehaviourSyncMode(BehaviourSyncMode.None)]
    public class ScoreboardControl : UdonSharpBehaviour
    {
        [OpenPuttFoldoutGroup("Display")]
        public Scoreboard scoreboard;

        [Tooltip("Which widget backs this control - decides which reference below is used")]
        [OpenPuttFoldoutGroup("Display")]
        public ControlKind kind = ControlKind.Slider;

        [OpenPuttFoldoutGroup("Display")]
        public Slider slider;
        [OpenPuttFoldoutGroup("Display")]
        public Image toggleImage;
        [Tooltip("Optional colour swatch for the ball-colour sliders - shows the combined RGB")]
        [OpenPuttFoldoutGroup("Display")]
        public Image colorPreview;
        [Tooltip("HSV ball-colour slider track: an Image stretched over the slider's Background, sharing its rounded-rect material, with the matching ramp as its sprite. Hue = rainbow (drawn untinted), Value = black->white (tinted to the colour), Saturation = no sprite (tinted to the pure colour, with gradientOverlay on top).")]
        [OpenPuttFoldoutGroup("Display")]
        public Image gradientImage;
        [Tooltip("Saturation slider only: a white->transparent gradient sprite layered on top of gradientImage, tinted to grey to complete the grey->colour ramp. Leave empty for Hue/Value.")]
        [OpenPuttFoldoutGroup("Display")]
        public Image gradientOverlay;
        [OpenPuttFoldoutGroup("Display")]
        public TMP_Dropdown dropdown;

        [Tooltip("Optional value readout next to a slider/dropdown")]
        [OpenPuttFoldoutGroup("Display")]
        public TextMeshProUGUI valueLabel;

        [Tooltip("string.Format pattern for the value label, e.g. \"{0:F2}x\" or \"{0:P0}\"")]
        [OpenPuttFoldoutGroup("Display")]
        public string labelFormat = "{0:F2}";

        [Tooltip("Shown instead of the formatted value when a slider sits at its minimum, e.g. \"Off\" for a -1 = disabled slider. Leave empty to always show the value")]
        [OpenPuttFoldoutGroup("Display")]
        public string minValueLabel = "";

        [Tooltip("Which game value this control reads/writes")]
        [OpenPuttFoldoutGroup("Binding")]
        public SettingId id = SettingId.None;

        [Tooltip("This setting is stored as an int - slider values are rounded before saving")]
        [OpenPuttFoldoutGroup("Binding")]
        public bool integerValue = false;

        [Tooltip("Refresh the whole settings/dev menu after a change (for settings that affect others)")]
        [OpenPuttFoldoutGroup("Binding")]
        public bool refreshMenuOnChange = false;

        [Tooltip("Refresh just these other controls after a change, instead of the whole menu - e.g. the other HSV colour sliders sharing this control's colour state")]
        public ScoreboardControl[] controlsToRefreshOnChange = new ScoreboardControl[0];

        [Tooltip("Persist OpenPutt data after a change")]
        [OpenPuttFoldoutGroup("Binding")]
        public bool saveOnChange = false;

        private OpenPutt OpenPutt => Utilities.IsValid(scoreboard) && Utilities.IsValid(scoreboard.manager) ? scoreboard.manager.openPutt : null;
        private PlayerManager Player => Utilities.IsValid(OpenPutt) ? OpenPutt.LocalPlayerManager : null;
        private bool IsReady => Utilities.IsValid(OpenPutt) && Utilities.IsValid(Player);

        // Set while Refresh() pushes a value into a widget, so the widget's onValueChanged callback
        // (OnChanged) ignores it - Udon doesn't expose Slider/Dropdown.SetValueWithoutNotify.
        private bool suppressCallback;

        #region Display plumbing (reused by every setting of this kind)

        /// <summary>Pull the current game value into the widget. Called by the scoreboard's refresh loop.</summary>
        public void Refresh()
        {
            if (!IsReady)
                return;

            switch (kind)
            {
                case ControlKind.Slider:
                    if (!Utilities.IsValid(slider)) return;
                    suppressCallback = true;
                    slider.value = ReadFloat();
                    suppressCallback = false;
                    UpdateLabel(slider.value);
                    if (Utilities.IsValid(colorPreview))
                        colorPreview.color = Player.BallColor;
                    UpdateGradient();
                    break;
                case ControlKind.Toggle:
                    if (!Utilities.IsValid(toggleImage)) return;
                    toggleImage.sprite = ReadBool() ? scoreboard.checkboxOn : scoreboard.checkboxOff;
                    break;
                case ControlKind.Dropdown:
                    if (!Utilities.IsValid(dropdown)) return;
                    suppressCallback = true;
                    dropdown.value = ReadInt();
                    suppressCallback = false;
                    break;
            }
        }

        /// <summary>Wired to the widget's OnValueChanged (slider/dropdown) or button OnClick (toggle).</summary>
        public void OnChanged()
        {
            if (suppressCallback || !IsReady)
                return;

            // Master-only setting - bail before touching anything
            if (id == SettingId.PracticeMode && !OpenPuttUtils.LocalPlayerIsInstanceMaster())
                return;

            switch (kind)
            {
                case ControlKind.Slider:
                    WriteFloat(integerValue ? Mathf.Round(slider.value) : slider.value);
                    UpdateLabel(slider.value);
                    UpdateGradient();
                    break;
                case ControlKind.Toggle:
                    WriteBool(!ReadBool()); // toggles flip current state
                    Refresh();
                    break;
                case ControlKind.Dropdown:
                    WriteInt(dropdown.value);
                    break;
            }

            AfterChange();
        }

        /// <summary>Wired to the control's optional reset button.</summary>
        public void OnReset()
        {
            if (!IsReady)
                return;

            ResetToDefault();
            Refresh();
            AfterChange();
        }

        private void AfterChange()
        {
            if (refreshMenuOnChange)
            {
                scoreboard.RefreshSettingsMenu();
                scoreboard.RefreshDevModeMenu();
            }
            else if (controlsToRefreshOnChange != null)
            {
                foreach (var control in controlsToRefreshOnChange)
                    if (Utilities.IsValid(control))
                        control.Refresh();
            }

            if (saveOnChange && Utilities.IsValid(OpenPutt))
                OpenPutt._SavePersistantData();
        }

        private void UpdateLabel(float value)
        {
            if (!Utilities.IsValid(valueLabel))
                return;

            // Sliders that use their lowest notch as an "off" switch show a word there instead of the number
            if (!string.IsNullOrEmpty(minValueLabel) && Utilities.IsValid(slider) && value <= slider.minValue)
            {
                valueLabel.text = minValueLabel;
                return;
            }

            valueLabel.text = string.Format(labelFormat, value);
        }

        #endregion

        #region Binding (one case per setting, grouped by value type)

        private float ReadFloat()
        {
            switch (id)
            {
                case SettingId.ClubPower: return Player.golfClub.forceMultiplier;
                case SettingId.SfxVolume: return OpenPutt.sfxController.Volume;
                case SettingId.BgmVolume: return FirstVolume(OpenPutt.bgmAudioSources);
                case SettingId.WorldVolume: return FirstVolume(OpenPutt.worldAudioSources);
                case SettingId.BallColorH: return ReadBallHsv(0);
                case SettingId.BallColorS: return ReadBallHsv(1);
                case SettingId.BallColorV: return ReadBallHsv(2);
                case SettingId.DevBallWeight: return Player.golfBall.BallWeight;
                case SettingId.DevBallFriction: return Player.golfBall.BallFriction;
                case SettingId.DevBallDrag: return Player.golfBall.BallDrag;
                case SettingId.DevBallADrag: return Player.golfBall.BallAngularDrag;
                case SettingId.DevTrackingRewindMs: return OpenPutt.controllerTracker.trackingRewindSeconds * 1000f;
                case SettingId.DevTrackingSmoothingMs: return OpenPutt.controllerTracker.trackingSmoothingSeconds * 1000f;
                case SettingId.DevHitAimDelayMs: return Player.golfClubHead.hitAimDelaySeconds * 1000f;
                case SettingId.DevMinTimeBetweenHitsMs: return Player.golfClubHead.minSecondsBetweenHits * 1000f;
                case SettingId.DevBallSnapSteps: return Player.golfBall.snapMinGroundedSteps;
            }
            return 0f;
        }

        private void WriteFloat(float v)
        {
            switch (id)
            {
                case SettingId.ClubPower: Player.golfClub.forceMultiplier = v; break;
                case SettingId.SfxVolume: OpenPutt.sfxController.Volume = v; break;
                case SettingId.BgmVolume: SetVolume(OpenPutt.bgmAudioSources, v); break;
                case SettingId.WorldVolume: SetVolume(OpenPutt.worldAudioSources, v); break;
                case SettingId.BallColorH: WriteBallChannel(0, v); break;
                case SettingId.BallColorS: WriteBallChannel(1, v); break;
                case SettingId.BallColorV: WriteBallChannel(2, v); break;
                case SettingId.DevBallWeight: Player.golfBall.BallWeight = v; break;
                case SettingId.DevBallFriction: Player.golfBall.BallFriction = v; break;
                case SettingId.DevBallDrag: Player.golfBall.BallDrag = float.Parse($"{v:F3}"); break;
                case SettingId.DevBallADrag: Player.golfBall.BallAngularDrag = v; break;
                case SettingId.DevTrackingRewindMs: OpenPutt.controllerTracker.trackingRewindSeconds = v / 1000f; break;
                case SettingId.DevTrackingSmoothingMs: OpenPutt.controllerTracker.trackingSmoothingSeconds = v / 1000f; break;
                case SettingId.DevHitAimDelayMs: Player.golfClubHead.hitAimDelaySeconds = v / 1000f; break;
                case SettingId.DevMinTimeBetweenHitsMs: Player.golfClubHead.minSecondsBetweenHits = v / 1000f; break;
                case SettingId.DevBallSnapSteps: Player.golfBall.snapMinGroundedSteps = Mathf.RoundToInt(v); break;
            }
        }

        private void ResetToDefault()
        {
            switch (id)
            {
                case SettingId.ClubPower: Player.golfClub.forceMultiplier = 1f; break;
                case SettingId.SfxVolume: OpenPutt.sfxController.Volume = 1f; break;
                case SettingId.BgmVolume: SetVolume(OpenPutt.bgmAudioSources, 1f); break;
                case SettingId.WorldVolume: SetVolume(OpenPutt.worldAudioSources, 1f); break;
                case SettingId.BallColorH:
                case SettingId.BallColorS:
                case SettingId.BallColorV:
                    Player.BallColor = Player.Owner.ToColor();
#if !OPENPUTT_DEMO_MODE
                    if (OpenPutt.playerSyncType < PlayerSyncType.FinishOnly)
                        Player.RequestSerialization();
#endif
                    break;
                case SettingId.DevBallWeight: Player.golfBall.BallWeight = Player.golfBall.DefaultBallWeight; break;
                case SettingId.DevBallFriction: Player.golfBall.BallFriction = Player.golfBall.DefaultBallFriction; break;
                case SettingId.DevBallDrag: Player.golfBall.BallDrag = Player.golfBall.DefaultBallDrag; break;
                case SettingId.DevBallADrag: Player.golfBall.BallAngularDrag = Player.golfBall.DefaultBallAngularDrag; break;
                case SettingId.DevTrackingRewindMs: OpenPutt.controllerTracker.trackingRewindSeconds = 0f; break;
                case SettingId.DevTrackingSmoothingMs: OpenPutt.controllerTracker.trackingSmoothingSeconds = 0.022f; break;
                case SettingId.DevHitAimDelayMs: Player.golfClubHead.hitAimDelaySeconds = 0f; break;
                case SettingId.DevMinTimeBetweenHitsMs: Player.golfClubHead.minSecondsBetweenHits = 0.1f; break;
                case SettingId.DevBallSnapSteps: Player.golfBall.snapMinGroundedSteps = Player.golfBall.DefaultSnapMinGroundedSteps; break;
            }
        }

        private bool ReadBool()
        {
            switch (id)
            {
                case SettingId.LeftHandMode: return Player.IsInLeftHandedMode;
                case SettingId.ClubThrow: return Player.golfClub.throwEnabled;
                case SettingId.ClubAutoHold: return Player.golfClub.AutoHoldEnabled;
                case SettingId.EnableBigShaft: return Player.golfClub.enableBigShaft;
                case SettingId.PracticeMode: return OpenPutt.practiceMode;
                case SettingId.DevForAll: return OpenPutt.enableDevModeForAll;
                case SettingId.FootCollider: return OpenPutt.footCollider.gameObject.activeSelf;
                case SettingId.ClubRenderer: return Player.golfClubVisualiser.gameObject.activeSelf;
                case SettingId.BallGrounded: return Player.golfBall.ballGroundedDebug;
                case SettingId.BallSnapDebug: return Player.golfBall.ballSnapDebug;
                case SettingId.DebugLogging: return OpenPutt.debugMode;
                case SettingId.SpectatorMode: return !Player.IsPlaying;
                case SettingId.ShoulderPickupRenderer: return Utilities.IsValid(OpenPutt.leftShoulderPickup) && OpenPutt.leftShoulderPickup.MeshRendererEnabled;
            }
            return false;
        }

        private void WriteBool(bool v)
        {
            switch (id)
            {
                case SettingId.LeftHandMode: Player.IsInLeftHandedMode = v; break;
                case SettingId.ClubThrow: Player.golfClub.throwEnabled = v; break;
                case SettingId.ClubAutoHold: Player.golfClub.AutoHoldEnabled = v; break;
                case SettingId.EnableBigShaft: Player.golfClub.enableBigShaft = v; break;
                case SettingId.PracticeMode:
                {
                    OpenPuttUtils.SetOwner(Networking.LocalPlayer, OpenPutt.gameObject);
                    OpenPutt.practiceMode = v;
#if !OPENPUTT_DEMO_MODE
                    OpenPutt.RequestSerialization();
#endif
                    break;
                }
                // Both ball debugs drive the ball colour, so switching one on switches the other off
                case SettingId.BallGrounded:
                    Player.golfBall.ballGroundedDebug = v;
                    if (v) Player.golfBall.ballSnapDebug = false;
                    break;
                case SettingId.BallSnapDebug:
                    Player.golfBall.ballSnapDebug = v;
                    if (v) Player.golfBall.ballGroundedDebug = false;
                    break;
                // Local only - each player decides how much log spam they want
                case SettingId.DebugLogging: OpenPutt.debugMode = v; break;
                case SettingId.SpectatorMode: Player.IsPlaying = !v; break;
                case SettingId.ShoulderPickupRenderer:
                    if (Utilities.IsValid(OpenPutt.leftShoulderPickup)) OpenPutt.leftShoulderPickup.MeshRendererEnabled = v;
                    if (Utilities.IsValid(OpenPutt.rightShoulderPickup)) OpenPutt.rightShoulderPickup.MeshRendererEnabled = v;
                    break;
                case SettingId.ClubRenderer: Player.golfClubVisualiser.gameObject.SetActive(v); break;
                case SettingId.FootCollider:
                    OpenPutt.footCollider.gameObject.SetActive(v);
                    Player.golfClubHead.targetOverride = v ? OpenPutt.footCollider.transform : null;
                    break;
                case SettingId.DevForAll:
                {
                    // Only whitelisted players may flip this, and it's owner-synced on the OpenPutt object
                    var localPlayerName = OpenPuttUtils.LocalPlayerIsValid() ? Networking.LocalPlayer.displayName : null;
                    if (Utilities.IsValid(localPlayerName) && OpenPutt.devModePlayerWhitelist.Contains(localPlayerName))
                    {
                        OpenPuttUtils.SetOwner(Networking.LocalPlayer, OpenPutt.gameObject);
                        OpenPutt.enableDevModeForAll = v;
#if !OPENPUTT_DEMO_MODE
                        OpenPutt.RequestSerialization();
#endif
                    }
                    break;
                }
            }
        }

        private int ReadInt()
        {
            /*
            switch (id)
            {
                // case SettingId.VelocityTracking: return (int)Player.golfClub.velocityTrackingType;
            }
            */
            return 0;
        }

        private void WriteInt(int v)
        {
            /*
            switch (id)
            {
                // case SettingId.VelocityTracking: Player.golfClub.velocityTrackingType = (GolfClubTrackingType)v; break;
            }
            */
        }

        #endregion

        #region Helpers

        // Gradient track for HSV ball-colour sliders. Each ramp is an Image stretched over the slider's
        // Background, drawn by the rounded-rect shader with the channel's ramp as its sprite, so all that is
        // needed here is a retint whenever the HSV changes. Sharing the Background's rect and material is what
        // keeps the corners smooth - the shader rounds each ramp itself, where clipping one to shape with a
        // Mask would stairstep it, a stencil test being 1 bit wide with no partial coverage.
        private void UpdateGradient()
        {
            var h = scoreboard.ballColorHsvH;
            var s = scoreboard.ballColorHsvS;
            var val = scoreboard.ballColorHsvV;

            switch (id)
            {
                case SettingId.BallColorH:
                    // Rainbow sprite - shown as-is
                    if (Utilities.IsValid(gradientImage)) gradientImage.color = Color.white;
                    break;
                case SettingId.BallColorS:
                    // Solid pure-hue background + white->transparent overlay tinted grey => grey -> colour
                    if (Utilities.IsValid(gradientImage)) gradientImage.color = Color.HSVToRGB(h, 1f, val);
                    if (Utilities.IsValid(gradientOverlay)) gradientOverlay.color = new Color(val, val, val, 1f);
                    break;
                case SettingId.BallColorV:
                    // Black->white sprite tinted by the full-brightness colour => black -> colour
                    if (Utilities.IsValid(gradientImage)) gradientImage.color = Color.HSVToRGB(h, s, 1f);
                    break;
            }
        }

        // Ball colour is stored as RGB on the player, but the sliders drive it in HSV (channel 0=H, 1=S, 2=V).
        // The authoritative HSV lives on the scoreboard (shared by the 3 sliders) so hue/saturation aren't lost
        // when the colour hits greyscale (S=0) or black (V=0), where RGB->HSV can't recover them.
        private float ReadBallHsv(int channel)
        {
            ReseedHsvIfColorChanged();
            if (channel == 0) return scoreboard.ballColorHsvH;
            return channel == 1 ? scoreboard.ballColorHsvS : scoreboard.ballColorHsvV;
        }

        private void WriteBallChannel(int channel, float v)
        {
            ReseedHsvIfColorChanged();
            if (channel == 0) scoreboard.ballColorHsvH = v;
            else if (channel == 1) scoreboard.ballColorHsvS = v;
            else scoreboard.ballColorHsvV = v;

            var c = Color.HSVToRGB(scoreboard.ballColorHsvH, scoreboard.ballColorHsvS, scoreboard.ballColorHsvV);
            Player.BallColor = c;

            if (Utilities.IsValid(colorPreview))
                colorPreview.color = c;

            // Push the change to other players straight away when the sync mode allows it
#if !OPENPUTT_DEMO_MODE
            if (OpenPutt.playerSyncType < PlayerSyncType.FinishOnly)
                Player.RequestSerialization();
#endif
        }

        // Reseed the cached HSV from the stored colour only when they've diverged - i.e. the colour was changed
        // by something other than these sliders (reset, first open, a network update). While dragging, the cache
        // already matches the colour so it's kept, which is what keeps hue/sat alive through greyscale and black.
        private void ReseedHsvIfColorChanged()
        {
            var current = Player.BallColor;
            var fromCache = Color.HSVToRGB(scoreboard.ballColorHsvH, scoreboard.ballColorHsvS, scoreboard.ballColorHsvV);
            if (Mathf.Abs(current.r - fromCache.r) < 0.002f &&
                Mathf.Abs(current.g - fromCache.g) < 0.002f &&
                Mathf.Abs(current.b - fromCache.b) < 0.002f)
                return;

            float h, s, val;
            Color.RGBToHSV(current, out h, out s, out val);
            scoreboard.ballColorHsvH = h;
            scoreboard.ballColorHsvS = s;
            scoreboard.ballColorHsvV = val;
        }

        private float FirstVolume(AudioSource[] sources)
        {
            if (sources == null) return 1f;
            foreach (var src in sources)
                if (Utilities.IsValid(src))
                    return src.volume;
            return 1f;
        }

        private void SetVolume(AudioSource[] sources, float v)
        {
            if (sources == null) return;
            foreach (var src in sources)
                if (Utilities.IsValid(src))
                    src.volume = v;
        }

        #endregion
    }
}
