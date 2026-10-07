 
using LaunchMonitor.Proto; 
namespace OpenSwingGolfSimulator.Hardware.Ble.Events
{
    /// <summary>
    /// Handler signature for <see cref="Error"/>.
    /// </summary>
    /// <param name="sender">The device that raised the event.</param>
    /// <param name="e">The event data carrying the error message and severity.</param>
    public delegate void ErrorEventHandler(object sender, ErrorEventArgs e);

    /// <summary>
    /// Event data for <see cref="Error"/>.
    /// </summary>
    public class ErrorEventArgs : EventArgs
    {
        /// <summary>
        /// A human-readable description of the error.
        /// </summary>
        public string? Message { get; set; }

        /// <summary>
        /// The severity of the error, as reported by the device or assigned locally.
        /// </summary>
        public Error.Types.Severity Severity { get; set; }
    }
}
