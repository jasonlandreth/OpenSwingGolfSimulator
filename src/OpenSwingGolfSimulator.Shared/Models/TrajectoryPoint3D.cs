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


namespace OpenSwingGolfSimulator.Shared.Models
{
    /// <summary>
    /// A single sampled point along a golf shot's trajectory, as reported by a launch monitor or
    /// physics simulation. Axis convention: <see cref="X"/> is the lateral offset from the
    /// target centerline, in yards, where positive values are to the right of center and
    /// negative values are to the left; <see cref="Y"/> is the downrange distance from the tee,
    /// in yards; and <see cref="Z"/> is the height above the ground, in feet. Note that Z uses a
    /// different unit (feet) than X and Y (yards), matching how the underlying shot data is
    /// reported. If your data source uses a different axis or unit convention, remap it when you
    /// construct these points rather than changing the rendering math.
    /// </summary>
    public struct TrajectoryPoint3D
    {
        /// <summary>
        /// The lateral offset from the target centerline, in yards. Positive values are to the
        /// right of center; negative values are to the left.
        /// </summary>
        public double X { get; set; }

        /// <summary>
        /// The downrange distance from the tee, in yards, measured along the ground.
        /// </summary>
        public double Y { get; set; }

        /// <summary>
        /// The height above the ground, in feet. Zero represents a point on the ground.
        /// </summary>
        public double Z { get; set; }

        /// <summary>
        /// Constructs a new trajectory sample point.
        /// </summary>
        /// <param name="LateralOffsetYards">
        /// The lateral offset from centerline, in yards (positive = right, negative = left).
        /// </param>
        /// <param name="DownrangeDistanceYards">The downrange distance from the tee, in yards.</param>
        /// <param name="HeightFeet">The height above the ground, in feet.</param>
        public TrajectoryPoint3D(double LateralOffsetYards, double DownrangeDistanceYards, double HeightFeet)
        {
            X = LateralOffsetYards;
            Y = DownrangeDistanceYards;
            Z = HeightFeet;
        }
    }

}
