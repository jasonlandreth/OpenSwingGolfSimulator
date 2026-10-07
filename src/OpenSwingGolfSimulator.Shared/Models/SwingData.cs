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

namespace OpenSwingGolfSimulator.Shared.Models
{

    /// <summary>
    /// Swing timing metrics for a single shot.
    /// </summary>
    public class SwingData
    {
        /// <summary>
        /// The timestamp when the backswing starts, in milliseconds since the device started.
        /// </summary>
        public uint BackswingStart { get; init; }

        /// <summary>
        /// The timestamp when the downswing starts, in milliseconds since the device started.
        /// </summary>
        public uint DownswingStart { get; init; }

        /// <summary>
        /// The timestamp when the impact occurs, in milliseconds since the device started.
        /// </summary>
        public uint Impact { get; init; }

        /// <summary>
        /// The timestamp when the follow-through ends, in milliseconds since the device started.
        /// </summary>
        public uint FollowThroughEnd { get; init; }
    }
}
