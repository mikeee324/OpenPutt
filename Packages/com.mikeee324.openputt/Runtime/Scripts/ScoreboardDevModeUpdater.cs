using UdonSharp;
using VRC.SDKBase;

namespace dev.mikeee324.OpenPutt
{
    public class ScoreboardDevModeUpdater : UdonSharpBehaviour
    {
        public Scoreboard scoreboard;
        private OpenPutt openPutt;
        private PlayerManager localPlayerManager;

        void Start()
        {
        }

        private void Update()
        {
            if (!Utilities.IsValid(openPutt))
                openPutt = scoreboard.manager.openPutt;

            if (Utilities.IsValid(openPutt) && !Utilities.IsValid(localPlayerManager))
                localPlayerManager = openPutt.LocalPlayerManager;

            if (!Utilities.IsValid(localPlayerManager))
                return;

            scoreboard.devModeLastClubHitSpeed.text = $"{localPlayerManager.golfClubHead.LastKnownHitVelocity:F2} {localPlayerManager.golfClubHead.LastKnownHitType}";
            scoreboard.devModeLastClubHitDirBias.text = $"{localPlayerManager.golfClubHead.LastKnownHitDirBias * 100f:F0}%";
            scoreboard.devModeBallSpeed.text = $"{localPlayerManager.golfBall.BallCurrentSpeed:F2}";
            scoreboard.devModeClubSpeed.text = $"{localPlayerManager.golfClub.FrameHeadSpeed.magnitude:F2}";
        }
    }
}