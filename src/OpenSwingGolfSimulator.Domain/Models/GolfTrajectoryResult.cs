 
using OpenSwingGolfSimulator.Shared.Enums;
using OpenSwingGolfSimulator.Shared.Models;

namespace OpenSwingGolfSimulator.Domain.Models
{
    public class GolfTrajectoryResult
    {
        /// <summary> Carry distance in yards </summary>
        public double CarryDistanceYards { get; set; }

        /// <summary> Total distance including roll in yards </summary>
        public double TotalDistanceYards { get; set; }

        /// <summary> Distance rolled after landing in yards </summary>
        public double RolloutDistanceYards { get; set; }

        /// <summary> Peak trajectory height in feet </summary>
        public double ApexHeightFeet { get; set; }

        /// <summary> Lateral deviation from target center line at carry point (+ Right, - Left) in yards </summary>
        public double CarryOfflineYards { get; set; }

        /// <summary> Final lateral deviation from target center line after roll (+ Right, - Left) in yards </summary>
        public double TotalOfflineYards { get; set; }

        /// <summary> Total time in air in seconds </summary>
        public double FlightTimeSeconds { get; set; }

        /// <summary> Impact velocity prior to bounce (mph) </summary>
        public double LandingAngleDegrees { get; set; }

        /// <summary> Classified shot shape </summary>
        public ShotShape ShotClassification { get; set; }

        /// <summary> 3D coordinates representing the flight path suitable for canvas rendering </summary>
        public List<Vector3D> TrajectoryPoints { get; set; } = new List<Vector3D>();
    }
}
