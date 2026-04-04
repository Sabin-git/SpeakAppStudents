// Shared data types — no logic. Passed between scripts.

public struct SpeechMetrics
{
    public float wpm;               // rolling 10-second window
    public float rollingAvgWpm;     // session average
    public int   fillerCount;       // filler words counted in last 30s
    public float lastPauseDuration; // seconds since last speech detected
    public float sessionTime;       // total elapsed seconds
}

public enum AudienceState { Engaged = 0, Neutral = 1, Distracted = 2, Restless = 3 }

// Gaze zones classified by HeadTracker.cs based on head orientation
public enum GazeZone { Audience, Lectern, Other, Deadzone }

// Head tracking data emitted by HeadTracker.cs every frame
public struct HeadMetrics
{
    public GazeZone currentZone;      // which zone the presenter is currently looking at
    public bool isFacingCrowd;        // true if currentZone == Audience
    public int gazedAvatarIndex;      // index of avatar in centre of view, -1 if none
    public float timeOnAudience;      // cumulative seconds looking at audience this session
    public float timeOnLectern;       // cumulative seconds looking at lectern this session
    public float timeOnOther;         // cumulative seconds looking elsewhere this session
}
