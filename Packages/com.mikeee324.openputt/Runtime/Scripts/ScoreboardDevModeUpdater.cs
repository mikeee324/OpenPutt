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

            scoreboard.devModeLastClubHitSpeed.text = string.Format("{0:F2} {1}", localPlayerManager.golfClubHead.LastKnownHitVelocity, localPlayerManager.golfClubHead.LastKnownHitType);
            scoreboard.devModeLastClubHitDirBias.text = string.Format("{0:F0}%", localPlayerManager.golfClubHead.LastKnownHitDirBias * 100f);
            scoreboard.devModeBallSpeed.text = string.Format("{0:F2}", localPlayerManager.golfBall.BallCurrentSpeed);
            scoreboard.devModeClubSpeed.text = string.Format("{0:F2}",  localPlayerManager.golfClubHead.FrameVelocitySmoothed.magnitude);
        }
    }
}