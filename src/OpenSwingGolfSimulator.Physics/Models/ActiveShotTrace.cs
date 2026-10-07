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
    /// Tracks the animation state of a single shot's ground-track line on the dispersion chart,
    /// from the moment it is added through its draw-in, hold, and fade-out phases. One instance
    /// exists per shot that is still visibly animating; once a trace has fully faded it is removed
    /// from the control's active list (its landing dot, however, persists permanently — see
    /// <see cref="GolfShotDispersionControl"/>'s landing-dot list).
    /// </summary>
    public sealed class ActiveShotTrace
    {
        /// <summary>
        /// The full top-down path of the shot, in yards, combining carry and rollout points in
        /// order from the tee to the final resting position. Each point's X is lateral offset in
        /// yards and Y is downrange distance in yards; height is not used by this control.
        /// </summary>
        public List<PointF> PathPointsYards { get; set; } = new List<PointF>();

        /// <summary>
        /// The lateral offset of the shot's final resting position, in yards. Positive values are
        /// right of center, negative values are left. Displayed in the floating callout once the
        /// line finishes drawing.
        /// </summary>
        public double FinalLateralOffsetYards { get; set; }

        /// <summary>
        /// The total time, in seconds, that has elapsed since this trace was created. Advanced
        /// once per animation timer tick and used to derive both the line's draw progress and,
        /// once drawing completes, its hold-then-fade opacity.
        /// </summary>
        public double ElapsedSeconds { get; set; }

        /// <summary>
        /// True once this trace's landing dot has been added to the control's permanent dot list
        /// and the dispersion ellipse has been recomputed. Set the instant the line finishes
        /// drawing, so each trace contributes its dot exactly once.
        /// </summary>
        public bool HasAddedLandingDot { get; set; }
    }
}
