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

namespace OpenSwingGolfSimulator.UI.EventArgs
{
    /// <summary>
    /// Carries all of the data describing a single completed golf shot from
    /// wherever that shot was simulated (typically a physics engine class) to
    /// any listener that wants to visualize, log, or otherwise react to it. This
    /// class exists specifically so it can be passed as the event-args parameter
    /// of an <c>EventHandler&lt;ShotRecordArgs&gt;</c> style event - for example
    /// a <c>ShotExecuted</c> event raised by a physics engine once it finishes
    /// simulating a swing - which <see cref="DrivingRangeControl.OnShotExecuted"/>
    /// is designed to be attached to directly.
    /// </summary>
    public class ShotRecordArgs : System.EventArgs
    {

        /// <summary>
        /// Gets the full simulated ball flight for this shot, as a time-ordered
        /// list of world-space samples. <see cref="DrivingRangeControl"/> reads
        /// this list to build and animate the tracer; it is the only property
        /// on this class that actually affects what gets drawn, everything else
        /// here is purely informational display text.
        /// </summary>
        public IReadOnlyList<Vector3D> SimulatedFlightPath { get; }

        /// <summary>
        /// Gets or sets the speed of the club head at the moment it struck the
        /// ball, measured in miles per hour. This is a display-only statistic
        /// shown in the driving range view's data bar; it does not influence
        /// how the tracer itself is drawn, since the actual flight shape comes
        /// entirely from <see cref="SimulatedFlightPath"/>.
        /// </summary>
        public double ClubSpeedMph { get; set; }

        /// <summary>
        /// Gets or sets the speed of the ball immediately after impact,
        /// measured in miles per hour. Along with <see cref="ClubSpeedMph"/>,
        /// this is one of the two inputs typically used to calculate
        /// <see cref="SmashFactor"/>, though this class does not compute that
        /// value itself - it is expected to be supplied already calculated.
        /// </summary>
        public double BallSpeedMph { get; set; }

        /// <summary>
        /// Gets or sets the smash factor for the shot, defined as ball speed
        /// divided by club speed. Higher values indicate more efficient energy
        /// transfer from the club to the ball at impact. This is a display-only
        /// statistic and is not used in any trajectory calculation here.
        /// </summary>
        public double SmashFactor { get; set; }

        /// <summary>
        /// Gets or sets the carry distance of the shot, measured in yards - that
        /// is, the distance from the tee to the point where the ball first
        /// touches the ground, not including any roll afterward. This is shown
        /// in the data bar; the actual landing point drawn on screen comes from
        /// <see cref="SimulatedFlightPath"/>; the two should agree but this
        /// property is not derived from that list automatically.
        /// </summary>
        public double CarryYards { get; set; }

        /// <summary>
        /// Gets or sets the total distance of the shot, measured in yards,
        /// including roll after the ball lands (that is, carry distance plus
        /// roll distance). This is a display-only statistic shown in the data
        /// bar.
        /// </summary>
        public double TotalYards { get; set; }

        /// <summary>
        /// Gets or sets the direction the club head was moving through impact
        /// relative to the target line, measured in degrees. A positive value
        /// means the club was moving from in-to-out (toward the right of the
        /// target line, for a right-handed golfer); a negative value means
        /// out-to-in. This is a display-only statistic.
        /// </summary>
        public double ClubPathDeg { get; set; }

        /// <summary>
        /// Gets or sets the direction the club face was pointing at the moment
        /// of impact relative to the target line, measured in degrees. A
        /// positive value means the face was open (pointing right of target for
        /// a right-handed golfer); a negative value means closed. This is a
        /// display-only statistic.
        /// </summary>
        public double FaceAngleDeg { get; set; }

        /// <summary>
        /// Gets or sets the difference between <see cref="FaceAngleDeg"/> and
        /// <see cref="ClubPathDeg"/>, measured in degrees. This value is
        /// generally considered the strongest single predictor of the ball's
        /// initial curve direction. This is a display-only statistic.
        /// </summary>
        public double FaceToPathDeg { get; set; }

        /// <summary>
        /// Gets or sets the vertical angle of the club head's motion through
        /// impact, measured in degrees. A positive value means the club was
        /// moving upward (an ascending strike, typical of a driver hit off a
        /// tee); a negative value means it was still moving downward
        /// (a descending strike, typical of an iron). This is a display-only
        /// statistic.
        /// </summary>
        public double AttackAngleDeg { get; set; }

        /// <summary>
        /// Gets or sets the combined spin rate imparted on the ball at impact,
        /// measured in revolutions per minute. This is a display-only statistic
        /// shown in the data bar and does not, by itself, change the trajectory
        /// drawn from <see cref="SimulatedFlightPath"/>.
        /// </summary>
        public double SpinRateRpm { get; set; }

        /// <summary>
        /// Gets or sets the name of the player who hit this shot, shown in the
        /// caption line beneath the tracer view alongside the club and ball
        /// name and the timestamp.
        /// </summary>
        public string PlayerName { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the name of the club used for this shot (for example,
        /// "Driver" or "7 Iron"), shown in the caption line beneath the tracer
        /// view.
        /// </summary>
        public string ClubName { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the name or model of the ball used for this shot (for
        /// example, "Premium"), shown in the caption line beneath the tracer
        /// view.
        /// </summary>
        public string BallName { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the date and time the shot was struck, shown in the
        /// caption line beneath the tracer view. Defaults to the moment this
        /// <see cref="ShotRecordArgs"/> instance was constructed, so callers
        /// only need to set this explicitly if they are recording a shot that
        /// happened at a different time (for example, when replaying shots from
        /// a saved session).
        /// </summary>
        public DateTime Timestamp { get; set; } = DateTime.Now;

        /// <summary>
        /// Creates a new <see cref="ShotRecordArgs"/> wrapping the simulated
        /// flight path of one shot. The display statistics (club speed, ball
        /// speed, carry, and so on) are intentionally left to be set afterward
        /// through this class's settable properties rather than being required
        /// here, since not every caller will have all of them available at
        /// construction time.
        /// </summary>
        /// <param name="SimulatedFlightPath">
        /// The full simulated ball flight, expressed as a list of
        /// <see cref="Vector3D"/> samples in the order they occurred
        /// (that is, each sample's elapsed flight time should be greater than
        /// or equal to the one before it). Must contain at least two samples,
        /// since a single point cannot describe a path with any direction or
        /// length. This list is stored by reference rather than copied, so the
        /// caller should treat it as immutable once this constructor has run.
        /// </param>
        /// <exception cref="ArgumentNullException">
        /// Thrown when <paramref name="SimulatedFlightPath"/> is null.
        /// </exception>
        /// <exception cref="ArgumentException">
        /// Thrown when <paramref name="SimulatedFlightPath"/> contains fewer
        /// than two samples.
        /// </exception>
        public ShotRecordArgs(IReadOnlyList<Vector3D> SimulatedFlightPath)
        {
            if (SimulatedFlightPath == null)
            {
                throw new ArgumentNullException(nameof(SimulatedFlightPath));
            }

            if (SimulatedFlightPath.Count < 2)
            {
                throw new ArgumentException("The simulated flight path must contain at least two samples.", nameof(SimulatedFlightPath));
            }

            this.SimulatedFlightPath = SimulatedFlightPath;
        }


    }
}
