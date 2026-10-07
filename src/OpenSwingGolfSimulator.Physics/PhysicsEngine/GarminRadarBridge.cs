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

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OpenSwingGolfSimulator.Physics.PhysicsEngine
{
    public static class GarminRadarBridge
    {
        /// <summary>
        /// Connects your live Garmin Approach R10 radar metrics directly to the 2000Hz Physics Engine.
        /// </summary>
        /// <param name="ballSpeedMPH">Garmin: Ball Speed (MPH)</param>
        /// <param name="launchAngleDeg">Garmin: Launch Angle (Degrees)</param>
        /// <param name="launchDirectionDeg">Garmin: Launch Direction / Azimuth (+ Right, - Left)</param>
        /// <param name="totalSpinRPM">Garmin: Total Spin (RPM)</param>
        /// <param name="spinAxisDeg">Garmin: Spin Axis (+ Right/Slice, - Left/Hook)</param>
        /// <returns>A precisely configured, ready-to-run instance of the Engine</returns>
        public static UltimateLaunchMonitorEngine InitializeFromGarmin(
            float ballSpeedMPH,
            float launchAngleDeg,
            float launchDirectionDeg,
            float totalSpinRPM,
            float spinAxisDeg)
        {
            // Pass your direct Garmin radar metrics directly to our mathematical simulation
            UltimateLaunchMonitorEngine radarCalibratedEngine = new UltimateLaunchMonitorEngine(
                ballSpeedMPH,
                launchAngleDeg,
                launchDirectionDeg,
                totalSpinRPM,
                spinAxisDeg
            );

            return radarCalibratedEngine;
        }
    }
}
