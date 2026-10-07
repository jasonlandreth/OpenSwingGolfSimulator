 
using OpenSwingGolfSimulator.Shared.Models;
using OpenSwingGolfSimulator.Shared.Enums;  

namespace OpenSwingGolf.Doman.Modules
{
    public static class SampleShotData
    {
        /// <summary>
        /// 7-Iron Shot: 150-yard total flight resulting in a Pull-Hook.
        /// Populates raw metrics for BallData (BallSpeed in meters/second) 
        /// and ClubData (ClubHeadSpeed in mph, angles in degrees).
        /// </summary>
        public static ShotData SevenIronTestData => new ShotData
        {
            ShotId = 1001,
            ShotType = ShotType.Normal,
            Ball = new BallData
            {
                BallSpeed = 105f,                   // ~105 mph  
                VerticalLaunchAngle = 18.5f,          // Degrees
                HorizontalLaunchAngle = -3.5f,    // Pulled left (-3.5°)
                TotalSpin = 6200.0f,                  // RPM
                SpinAxis = -11.0f,                    // Degrees left tilt
                BackSpin = 6086.0f,                   // RPM (6200 * cos(-11°))
                SideSpin = -1183.0f,                  // RPM (6200 * sin(-11°))
                SpinCalcType = SpinCalcType.Measured
            },
            Club = new ClubData
            {
                ClubHeadSpeed = 76.1f,                // Initial speed in mph
                FaceToTarget = -6.5f,                 // Closed face (-6.5°)
                PathToTarget = -2.5f,                 // Out-To-In path (-2.5°)
                AttackAngle = -4.2f                   // Downward attack angle (-4.2°)
            },
            Swing = new SwingData
            {
                BackswingStart = 10000,
                DownswingStart = 10800,
                Impact = 11100,
                FollowThroughEnd = 12000
            }
        };

        /// <summary>
        /// Driver Shot: 250-yard total flight resulting in a Draw.
        /// Populates raw metrics for BallData (BallSpeed in meters/second) 
        /// and ClubData (ClubHeadSpeed in mph, angles in degrees).
        /// </summary>
        public static ShotData DriverTestData => new ShotData
        {
            ShotId = 1002,
            ShotType = ShotType.Normal,
            Ball = new BallData
            {
                BallSpeed = 152f,                   // ~152 mph converted to m/s (152 * 0.44704)
                VerticalLaunchAngle = 11.5f,          // Degrees
                HorizontalLaunchAngle = 2.0f,     // Pushed right (+2.0°)
                TotalSpin = 2300.0f,                  // RPM
                SpinAxis = -8.5f,                     // Degrees left tilt
                BackSpin = 2275.0f,                   // RPM (2300 * cos(-8.5°))
                SideSpin = -339.0f,                   // RPM (2300 * sin(-8.5°))
                SpinCalcType = SpinCalcType.Measured
            },
            Club = new ClubData
            {
                ClubHeadSpeed = 104.1f,               // Initial speed in mph
                FaceToTarget = 1.0f,                  // Open to target line (+1.0°)
                PathToTarget = 4.0f,                  // In-To-Out path (+4.0°)
                AttackAngle = 2.5f                    // Ascending attack angle (+2.5°)
            },
            Swing = new SwingData
            {
                BackswingStart = 20000,
                DownswingStart = 20850,
                Impact = 21150,
                FollowThroughEnd = 22100
            }
        };
    }

}
