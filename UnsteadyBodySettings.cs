namespace UnsteadyBody
{
    public class UnsteadyBodySettings
    {
        /// <summary>0 Wobbly, 1 Comical, 2 Absurd, 3 Ludicrous.</summary>
        public int Strength = 1;

        /// <summary>0 On (safe speed), 1 Off at 2x, 2 Off at 4x. Same distance, less time.</summary>
        public int SpeedLimit = 0;

        /// <summary>Downward strikes launch them skywards.</summary>
        public bool PogoLaunch = true;

        /// <summary>Upward strikes in the air slam them back down.</summary>
        public bool UpwardSlam = true;

        /// <summary>Show a bubble at the screen edge when they're off screen.</summary>
        public bool TrackerBubble = true;

        /// <summary>Whoosh when a launch starts, thud when it hits something solid.</summary>
        public bool Sounds = true;

        /// <summary>Experimental: an unfinished launch continues into the next room.</summary>
        public bool CarryMomentum = false;

        /// <summary>Write the mod's step-by-step details to ModLog.txt (for bug reports).</summary>
        public bool DetailedLogging = false;
    }

    /// <summary>Saved with each save file.</summary>
    public class UnsteadyBodySaveData
    {
        /// <summary>Whether the charm has been turned over to Unsteady Body.</summary>
        public bool Unsteady = false;
    }
}
