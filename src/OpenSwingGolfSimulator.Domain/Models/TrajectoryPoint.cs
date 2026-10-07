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


using System;

namespace OpenSwingGolf.Doman.Modules
{
    /// <summary>GarminR10Connector.WinFormExample.Data
    /// Represents a single sampled point along a golf ball's simulated flight
    /// path, expressed entirely in world-space yards so the values line up
    /// naturally with the carry and total distance statistics that are already
    /// reported in yards. A full shot is represented as an ordered list of these
    /// samples, taken at increasing values of <see cref="ElapsedFlightTimeInSeconds"/>,
    /// starting at the tee (time zero, all distances zero) and ending when the
    /// ball comes to rest. The <see cref="DrivingRangeControl"/> control consumes
    /// a list of these samples and projects each one onto the screen to draw the
    /// tracer; it does not perform any physics simulation itself.
    /// </summary>
    public readonly struct TrajectoryPoint
    {
        /// <summary>
        /// Creates a new immutable trajectory sample describing where the ball
        /// is, in world space, at one specific moment during its flight.
        /// </summary>
        /// <param name="ElapsedFlightTimeInSeconds">
        /// The number of seconds that have elapsed since the club struck the
        /// ball. This is used to space samples out correctly in time so that
        /// playback animation speed matches how the ball actually accelerates
        /// and decelerates, rather than simply stepping through the list at a
        /// constant rate.
        /// </param>
        /// <param name="DistanceDownrangeInYards">
        /// How far the ball has traveled downrange from the tee, measured in
        /// yards along the target line. This is the "depth" axis used by the
        /// camera projection math - larger values push the point further from
        /// the camera and therefore make it appear smaller and closer to the
        /// horizon on screen.
        /// </param>
        /// <param name="LateralOffsetFromTargetLineInYards">
        /// How far to the left or right of the straight target line the ball
        /// currently is, measured in yards. A positive value means the ball is
        /// to the right of the target line (for example, the result of a fade
        /// or a slice for a right-handed golfer); a negative value means it is
        /// to the left (a draw or a hook).
        /// </param>
        /// <param name="HeightAboveGroundInYards">
        /// How far above the ground the ball currently is, measured in yards.
        /// This should be zero at the moment the ball is struck and again at
        /// the moment it lands, and should rise to its largest value at the
        /// apex of the shot.
        /// </param>
        public TrajectoryPoint(
            float ElapsedFlightTimeInSeconds,
            float DistanceDownrangeInYards,
            float LateralOffsetFromTargetLineInYards,
            float HeightAboveGroundInYards)
        {
            this.ElapsedFlightTimeInSeconds = ElapsedFlightTimeInSeconds;
            this.DistanceDownrangeInYards = DistanceDownrangeInYards;
            this.LateralOffsetFromTargetLineInYards = LateralOffsetFromTargetLineInYards;
            this.HeightAboveGroundInYards = HeightAboveGroundInYards;
        }

        /// <summary>
        /// Gets the number of seconds that have elapsed since the club struck
        /// the ball, at the moment this sample was taken. Used to correctly
        /// pace the flight animation in <see cref="DrivingRangeControl"/> so
        /// that it matches the real timing of the simulated flight rather than
        /// playing back samples at a fixed rate regardless of spacing.
        /// </summary>
        public float ElapsedFlightTimeInSeconds { get; }

        /// <summary>
        /// Gets how far the ball has traveled downrange from the tee at this
        /// sample, measured in yards along the target line. This is the "depth"
        /// coordinate that the camera projection uses to determine how small
        /// and how close to the horizon the point should appear on screen.
        /// </summary>
        public float DistanceDownrangeInYards { get; }

        /// <summary>
        /// Gets how far to the left or right of the straight target line the
        /// ball is at this sample, measured in yards. Positive values are to
        /// the right of the target line and negative values are to the left.
        /// </summary>
        public float LateralOffsetFromTargetLineInYards { get; }

        /// <summary>
        /// Gets how far above the ground the ball is at this sample, measured
        /// in yards. This is expected to be zero at launch and at landing, and
        /// to reach its maximum value at the apex of the shot.
        /// </summary>
        public float HeightAboveGroundInYards { get; }

        /// <summary>
        /// Computes a point that lies at a proportional position between two
        /// trajectory samples. "Lerp" is short for "linear interpolation": it
        /// blends every field of <paramref name="RangeStartSample"/> and
        /// <paramref name="RangeEndSample"/> in direct proportion to
        /// <paramref name="InterpolationFraction"/>, so that a fraction of 0
        /// returns exactly <paramref name="RangeStartSample"/>, a fraction of 1
        /// returns exactly <paramref name="RangeEndSample"/>, and a fraction of
        /// 0.5 returns the point exactly halfway between them in time, distance,
        /// lateral offset, and height. <see cref="DrivingRangeControl"/> uses
        /// this to draw the ball at a smooth in-between position during
        /// animation playback, rather than jumping discretely from one recorded
        /// sample to the next.
        /// </summary>
        /// <param name="RangeStartSample">The trajectory sample that corresponds to an interpolation fraction of 0.</param>
        /// <param name="RangeEndSample">The trajectory sample that corresponds to an interpolation fraction of 1.</param>
        /// <param name="InterpolationFraction">
        /// A value that is expected to be between 0 and 1, describing how far
        /// between <paramref name="RangeStartSample"/> and
        /// <paramref name="RangeEndSample"/> the returned point should lie. A
        /// value outside that range will extrapolate beyond the two samples
        /// rather than throwing an exception.
        /// </param>
        /// <returns>
        /// A new <see cref="TrajectoryPoint"/> whose fields are each the
        /// linear blend of the corresponding fields on
        /// <paramref name="RangeStartSample"/> and <paramref name="RangeEndSample"/>.
        /// </returns>
        public static TrajectoryPoint Lerp(
            TrajectoryPoint RangeStartSample,
            TrajectoryPoint RangeEndSample,
            float InterpolationFraction)
        {
            return new TrajectoryPoint(
                RangeStartSample.ElapsedFlightTimeInSeconds + (RangeEndSample.ElapsedFlightTimeInSeconds - RangeStartSample.ElapsedFlightTimeInSeconds) * InterpolationFraction,
                RangeStartSample.DistanceDownrangeInYards + (RangeEndSample.DistanceDownrangeInYards - RangeStartSample.DistanceDownrangeInYards) * InterpolationFraction,
                RangeStartSample.LateralOffsetFromTargetLineInYards + (RangeEndSample.LateralOffsetFromTargetLineInYards - RangeStartSample.LateralOffsetFromTargetLineInYards) * InterpolationFraction,
                RangeStartSample.HeightAboveGroundInYards + (RangeEndSample.HeightAboveGroundInYards - RangeStartSample.HeightAboveGroundInYards) * InterpolationFraction);
        }
    }
}
