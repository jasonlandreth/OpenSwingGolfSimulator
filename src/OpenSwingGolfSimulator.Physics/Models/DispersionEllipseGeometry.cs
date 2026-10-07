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

using System.Drawing; 

namespace OpenSwingGolfSimulator.Physics.Models
{
    /// <summary>
    /// Bundles the computed center and radii of the dispersion ellipse that encompasses every
    /// recorded landing dot, expressed in yards (not screen pixels). Produced by
    /// <see cref="GolfShotDispersionControl"/>'s ellipse-geometry calculation and consumed when
    /// painting the ellipse.
    /// </summary>
    public struct DispersionEllipseGeometry
    {
        /// <summary>
        /// The center of the ellipse, in yards, using the same lateral/downrange axis convention
        /// as <see cref="TrajectoryPoint3D"/> (X = lateral offset, Y = downrange distance).
        /// </summary>
        public
            PointF CenterYards
        { get; set; }

        /// <summary>
        /// The horizontal (lateral) semi-axis of the ellipse, in yards.
        /// </summary>
        public double LateralRadiusYards { get; set; }

        /// <summary>
        /// The vertical (downrange) semi-axis of the ellipse, in yards.
        /// </summary>
        public double DownrangeRadiusYards { get; set; }
    }
}
