/*
 * Copyright 2026 Jason Landreth, Open Swing Golf
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


using OpenSwingGolfSimulator.Hardware.Ble.Events;
using OpenSwingGolfSimulator.UI.EventArgs;

namespace OpenSwingGolfSimulator.UI.Events
{
    public class EventHandlers
    {
        // --- DEFINING CUSTOM STRONG-TYPED DELEGATES ---
        public delegate void ShotReceivedEventHandler(object sender, ShotEventArgs e);
        public delegate void ClubChangedEventHandler(object sender, ClubChangedEventArgs e);
        public delegate void ChartClearedEventHandler(object sender, ChartClearedEventArgs e);
        public delegate void LaunchMonitorStatusChangedEventHandler(object sender, LaunchMonitorStatusEventArgs e);
        public delegate void BallStrikingDetectedEventHandler(object sender, RawLaunchDataEventArgs e);

        /// <summary>
        /// Raised each time a shot is recorded and normalized metrics are produced.
        /// </summary>
        public event ShotDataChangedEventHandler? ShotMetricsSnapshotReceived;

        /// <summary>
        /// Handler signature for <see cref="ShotMetrics"/>.
        /// </summary>
        /// <param name="sender">The device that raised the event.</param>
        /// <param name="e">The event data carrying the raw metrics.</param>
        public delegate void MetricsEventHandler(object sender, MetricsEventArgs e);

        /// <summary>
        /// Handler signature for <see cref="ShotMetricsSnapshotReceived"/>.
        /// </summary>
        /// <param name="sender">The device that raised the event.</param>
        /// <param name="e">The event data carrying normalized metrics.</param>
        public delegate void ShotDataChangedEventHandler(object sender, ShotMetricsSnapshotEventArgs e);
    }
}
