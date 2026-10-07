using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OpenSwingGolfSimulator.Hardware.Ble.Events
{
     

    /// <summary>
    /// Handler signature for <see cref="ReadinessChanged"/>.
    /// </summary>
    /// <param name="sender">The device that raised the event.</param>
    /// <param name="e">The event data carrying the new readiness state.</param>
    public delegate void ReadinessChangedEventHandler(object sender, ReadinessChangedEventArgs e);
    /// <summary>
    /// Event data for <see cref="ReadinessChanged"/>.
    /// </summary>
    public class ReadinessChangedEventArgs : EventArgs
    {
        /// <summary>
        /// Whether the device is now ready to record a shot.
        /// </summary>
        public bool Ready { get; set; }
    }
}
