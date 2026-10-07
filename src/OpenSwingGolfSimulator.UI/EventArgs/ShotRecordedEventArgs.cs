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

using OpenSwingGolfSimulator.Physics.Models; 

namespace OpenSwingGolfSimulator.UI.EventArgs
{
    /// <summary>
    /// Event arguments carrying a single completed shot record, intended for a parent-level
    /// "a new shot came in" event (for example, an <c>OnShotRecorded</c> event raised by whatever
    /// component owns the connection to your launch monitor or physics engine). Both
    /// <see cref="GolfShotTracerControl"/> and <see cref="GolfShotDispersionControl"/> expose an
    /// <c>OnShotRecorded</c> method whose signature matches <see cref="EventHandler{TEventArgs}"/>
    /// of this type, so either control's method can be subscribed directly to that parent event
    /// without any wrapping lambda:
    /// <code>
    /// parentShotSource.OnShotRecorded += tracerControl.OnShotRecorded;
    /// parentShotSource.OnShotRecorded += dispersionControl.OnShotRecorded;
    /// ...
    /// parentShotSource.OnShotRecorded?.Invoke(this, new ShotRecordedEventArgs(newShotRecord));
    /// </code>
    /// One shot record then drives both controls from a single raised event, and neither control
    /// needs to know the other exists.
    /// </summary>
    public sealed class ShotRecordedEventArgs : System.EventArgs
    {
        /// <summary>
        /// The trajectory record for the shot that was just recorded — the same type consumed
        /// directly by <see cref="GolfShotTracerControl.StartShot(GolfShotTrajectoryData)"/> and
        /// <see cref="GolfShotDispersionControl.AddShot(GolfShotTrajectoryData)"/>.
        /// </summary>
        public GolfShotTrajectoryData ShotRecord { get; }

        /// <summary>
        /// Constructs a new event args instance wrapping the given shot record.
        /// </summary>
        /// <param name="ShotRecord">
        /// The trajectory record for the shot that was just recorded. Must not be null.
        /// </param>
        public ShotRecordedEventArgs(GolfShotTrajectoryData ShotRecord)
        {
            this.ShotRecord = ShotRecord ?? throw new ArgumentNullException(nameof(ShotRecord));
        }
    }
}
