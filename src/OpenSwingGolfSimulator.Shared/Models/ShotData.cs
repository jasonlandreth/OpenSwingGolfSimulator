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

using LiteDB;
using OpenSwingGolfSimulator.Shared.Enums;


namespace OpenSwingGolfSimulator.Shared.Models
{

    /// <summary>
    /// The complete decoded data for a single shot: identity, ball flight, club delivery, and
    /// swing timing metrics.
    /// </summary>
    public sealed class ShotData
    {
        /// <summary>
        /// The device-assigned identifier for this shot.
        /// </summary>
        [BsonId]
        public uint ShotId { get; init; }

        /// <summary>
        /// Whether this was a normal recorded shot or a practice swing.
        /// </summary>
        public ShotType ShotType { get; init; }

        /// <summary>
        /// Ball flight metrics, if the device reported them for this shot.
        /// </summary>
        public BallData Ball { get; init; }

        /// <summary>
        /// Club delivery metrics, if the device reported them for this shot.
        /// </summary>
        public ClubData Club { get; init; }

        /// <summary>
        /// Swing timing metrics, if the device reported them for this shot.
        /// </summary>
        public SwingData Swing { get; init; }
    }
}

