using OpenSwingGolfSimulator.Shared.Enums;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OpenSwingGolfSimulator.Hardware.Models
{
    /// <summary>
    /// Represents normalized shot metrics for consumer code.
    /// </summary>
    public sealed class ShotMetricsSnapshot
    {
        /// <summary>
        /// Gets or sets the shot identifier.
        /// </summary>
        public uint ShotId { get; init; }

        /// <summary>
        /// Gets or sets metric values keyed by <see cref="GolfShotMetric"/>.
        /// </summary>
        public required IReadOnlyDictionary<GolfShotMetric, double> Values { get; init; }

        /// <summary>
        /// Gets a metric value if present.
        /// </summary>
        /// <param name="Metric">Metric key.</param>
        /// <param name="Value">Output value.</param>
        /// <returns>True when present; otherwise false.</returns>
        public bool TryGet(GolfShotMetric Metric, out double Value)
        {
            return Values.TryGetValue(Metric, out Value);
        }
    }
}
