// Copyright 2026 Jason Landreth
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//     http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.

 
using LaunchMonitor.Proto;
using OpenSwingGolfSimulator.Shared.Models;

namespace OpenSwingGolfSimulator.Hardware.Decoders.Decoders;

/// <summary>
/// Decodes raw <see cref="Metrics"/> protobuf messages, as received from the Garmin R10,
/// into the simpler <see cref="ShotDataResults.ShotData"/> shape used by consuming applications.
/// Implementations can be swapped/mocked by consumers that reference this library.
/// </summary>
public interface IMetricsDecoder
{
    /// <summary>
    /// Decodes a raw <see cref="Metrics"/> message into a <see cref="ShotDataResults.ShotData"/> instance.
    /// </summary>
    /// <param name="Metrics">The raw metrics message received from the device.</param>
    /// <returns>The decoded shot data.</returns>
     ShotData Decode(Metrics Metrics);
}
