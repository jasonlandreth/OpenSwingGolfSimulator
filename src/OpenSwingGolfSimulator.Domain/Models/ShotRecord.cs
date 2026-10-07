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

using OpenSwingGolf.Doman.Enums;
using OpenSwingGolfSimulator.Shared.Enums;
using Vector3D = OpenSwingGolfSimulator.Shared.Models.Vector3D;

namespace OpenSwingGolfSimulator.Domain.Models
{
    public class ShotRecord
    {
        /// <summary>
        /// The shape of the shot, as determined by the launch monitor.
        /// </summary>
        public ShotShape ShotShape { get; set; }

        /// <summary>
        /// Sampled points of the ball's flight, from launch through first landing, in yards.
        /// </summary>
        public List<Vector3D> CarryYards { get; set; } = new List<Vector3D>();

        /// <summary>
        /// Sampled points of the ball's roll after landing, from first landing to final rest, in yards.
        /// </summary>
        public List<Vector3D> RolloutYards { get; set; } = new List<Vector3D>();

        /// <summary>
        /// Carry distance plus roll distance, in yards.
        /// </summary>
        public double TotalYards { get; set; }

        /// <summary
        /// >Carry distance only, in yards.
        /// </summary>
        public double TotalCarryYards { get; set; }

        /// <summary>
        /// Roll distance only, in yards.
        /// </summary>
        public double TotalRolloutYards { get; set; }

        /// <summary>
        /// Peak height of the flight (apex), in feet. Display-only.
        /// </summary>
        public double ApexFeet { get; set; }

        /// <summary>
        /// The angle at which the ball leaves the clubface, in degrees. Display-only.
        /// </summary>
        public double LaunchAngle { get; set; }

        /// <summary>
        /// The lateral deviation of the ball from the target line, in yards. Display-only.
        /// </summary>
        public double LateralDeviation { get; set; }

        /// <summary>
        /// Not used by ShotTraceControl, for the same reason as CurveBias.
        /// </summary>
        public double ApexTimeFraction { get; set; }
    }

}
