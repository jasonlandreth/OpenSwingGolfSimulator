/*
 * Copyright 2026 Jason Landreth, Open Swing Golf Simulator
 *
 * Licensed under the Apache License, Version 2.0 (the "License");
 * you may not use this file except in compliance with the License.
 * You may obtain a copy of the License at
 *
 *     http://apache.org
 *
 * Unless required by applicable law or agreed to in writing, software
 * distributed under the License is distributed on an "AS IS" BASIS,
 * WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 * See the License for the specific language governing permissions and
 * limitations under the License.
 */


using OpenSwingGolfSimulator.Shared.Models; 

namespace OpenSwingGolfSimulator.Physics.Models
{
    /// <summary>
    /// The full record for a single golf shot, as sent from the simulator's physics engine or
    /// launch monitor integration, to be animated by <see cref="GolfShotTracerControl"/>. The
    /// control does not compute or estimate any trajectory geometry itself: every point the ball
    /// passes through during flight and roll must be supplied here.
    /// </summary>
    public sealed class GolfShotTrajectoryData
    {
        /// <summary>
        /// The sampled trajectory points making up the airborne portion of the shot, ordered from
        /// the tee (first point) to the moment the ball first touches the ground (last point).
        /// Must contain at least two points.
        /// </summary>
        public List<Vector3D> CarryTrajectoryPoints { get; set; } = new List<Vector3D>();

        /// <summary>
        /// The sampled trajectory points making up the ground-roll portion of the shot after
        /// landing, ordered from the landing spot (first point) to where the ball comes to rest
        /// (last point). Pass an empty list (not null) for a shot with no rollout.
        /// </summary>
        public List<Vector3D> RolloutTrajectoryPoints { get; set; } = new List<Vector3D>();

        /// <summary>
        /// The full list of trajectory points for the shot, including both carry and rollout, 
        /// in order from tee to final resting position. This is a convenience property that 
        /// concatenates <see cref="CarryTrajectoryPoints"/> and <see cref="RolloutTrajectoryPoints"/> 
        /// into a single list.
        /// </summary>
        public List<Vector3D> TotalTrajectoryPoints => CarryTrajectoryPoints.Concat(RolloutTrajectoryPoints).ToList();

        /// <summary>
        /// The peak height reached during the shot's flight, in feet, as reported in the shot
        /// record. Used for the apex callout text; the apex marker's on-screen position is
        /// instead located by scanning <see cref="CarryTrajectoryPoints"/> for its highest point,
        /// so the marker lines up exactly with the drawn arc even if this value is rounded.
        /// </summary>
        public double ApexHeightFeet { get; set; }

        /// <summary>
        /// The total distance of the shot, in yards, from tee to final resting position (carry
        /// plus rollout).
        /// </summary>
        public double TotalDistanceYards { get; set; }

        /// <summary>
        /// The carry distance of the shot, in yards (distance from tee to the point the ball
        /// first touches the ground).
        /// </summary>
        public double TotalCarryYards { get; set; }

        /// <summary>
        /// The rollout distance of the shot, in yards (distance the ball travels along the ground
        /// after landing, before coming to rest).
        /// </summary>
        public double TotalRolloutYards { get; set; }

        /// <summary>
        /// The launch angle of the shot, in degrees, measured from the ground plane. Retained for
        /// display purposes alongside the tracer; not used to compute the drawn geometry, since
        /// the actual trajectory points already encode the real flight path.
        /// </summary>
        public double LaunchAngleDegrees { get; set; }

        /// <summary>
        /// The side angle of the shot, in degrees (positive = finished right, negative = finished
        /// left). Retained for display purposes alongside the tracer; not used to compute the
        /// drawn geometry.
        /// </summary>
        public double SideAngleDegrees { get; set; }

        /// <summary>
        /// The club path of the shot, in degrees (positive = swung right/in-to-out, negative =
        /// swung left/out-to-in, matching the same lateral sign convention as
        /// <see cref="TrajectoryPoint3D.X"/>). Retained for display purposes; not directly
        /// consumed by <see cref="ShotShapeClassifier"/>, which uses
        /// <see cref="FaceToPathDegrees"/> (face relative to this path) instead.
        /// </summary>
        public double ClubPathDegrees { get; set; }

        /// <summary>
        /// The clubface angle at impact relative to the target line, in degrees (positive = open/
        /// aimed right, negative = closed/aimed left). This is the primary driver of where the
        /// ball starts and is what <see cref="ShotShapeClassifier"/> uses as the start-direction
        /// input, since face angle accounts for roughly 80-90% of initial ball direction.
        /// </summary>
        public double FaceToTargetDegrees { get; set; }

        /// <summary>
        /// The clubface angle at impact relative to the club path, in degrees (Face minus Path;
        /// positive = face open relative to path, causing right-curving sidespin; negative = face
        /// closed relative to path, causing left-curving sidespin). This is the primary driver of
        /// how much and which way the ball curves after it starts, and is what
        /// <see cref="ShotShapeClassifier"/> uses as its curve-amount input.
        /// </summary>
        public double FaceToPathDegrees { get; set; }

        /// <summary>
        /// The total curve imparted on the shot, in yards (positive = curved right, negative =
        /// curved left), as computed by the launch monitor or physics engine from spin rate and
        /// spin axis. Retained for display purposes (for example, "curved 11 yards right")
        /// alongside <see cref="FaceToPathDegrees"/>; not used by <see cref="ShotShapeClassifier"/>
        /// for the classification itself, since yards of curve scales with club and shot distance
        /// while Face-to-Path degrees does not.
        /// </summary>
        public double CurveYards { get; set; }

        /// <summary>
        /// The vertical angle of the clubhead's path at impact relative to the ground, in degrees
        /// (positive = ascending/hitting up on the ball, negative = descending/hitting down).
        /// Affects launch conditions and spin loft in the vertical plane; retained for display
        /// purposes only, since shot shape (Pull/Push/Hook/Draw/Fade/Slice) is a horizontal-plane
        /// classification that this value does not factor into.
        /// </summary>
        public double AngleOfAttackDegrees { get; set; }

        /// <summary>
        /// The measured ball speed of the shot, in miles per hour. Retained for display purposes
        /// alongside the tracer; not used to compute the drawn geometry.
        /// </summary>
        public double BallSpeedMph { get; set; }

        /// <summary>
        /// The smash factor of the shot (ball speed divided by club head speed). Retained for
        /// display purposes alongside the tracer; not used to compute the drawn geometry.
        /// </summary>
        public double SmashFactor { get; set; }
    }
}

