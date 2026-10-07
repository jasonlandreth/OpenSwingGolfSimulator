using OpenSwingGolfSimulator.Hardware.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OpenSwingGolfSimulator.Hardware.Ble.Events
{
    /// <summary>
    /// Event data for <see cref="ShotMetricsSnapshotReceived"/>.
    /// </summary>
    public class ShotMetricsSnapshotEventArgs : EventArgs
    {
        /// <summary>
        /// The normalized shot metrics snapshot.
        /// </summary>
        public required ShotMetricsSnapshot Snapshot { get; init; }
    }
}
