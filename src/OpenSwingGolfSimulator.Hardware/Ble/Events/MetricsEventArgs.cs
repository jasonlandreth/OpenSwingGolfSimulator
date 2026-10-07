using LaunchMonitor.Proto;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OpenSwingGolfSimulator.Hardware.Ble.Events
{
    // <summary>
    /// Event data for <see cref="ShotMetrics"/>.
    /// </summary>
    public class MetricsEventArgs : EventArgs
    {
        /// <summary>
        /// The raw metrics reported for the shot.
        /// </summary>
        public Metrics? Metrics { get; set; }
    }

    /// <summary>
    /// Handler signature for <see cref="ShotMetrics"/>.
    /// </summary>
    /// <param name="sender">The device that raised the event.</param>
    /// <param name="e">The event data carrying the raw metrics.</param>
    public delegate void MetricsEventHandler(object sender, MetricsEventArgs e);

}
