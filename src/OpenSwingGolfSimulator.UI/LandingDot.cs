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

namespace OpenSwingGolfSimulator.UI
{
    /// <summary>
    /// A single permanent marker recorded at a shot's final resting position on the dispersion
    /// chart. Unlike the ground-track line, landing dots never fade — they accumulate across every
    /// shot added to the control, which is what lets the dispersion ellipse describe a shot
    /// pattern rather than a single shot.
    /// </summary>
    public struct LandingDot
    {
        /// <summary>
        /// The dot's position, in yards, using the same lateral/downrange axis convention as
        /// <see cref="TrajectoryPoint3D"/> (X = lateral offset, Y = downrange distance).
        /// </summary>
        public PointF PositionYards { get; set; }
    }
}
