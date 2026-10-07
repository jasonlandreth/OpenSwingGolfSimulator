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
    internal class DriverSimulation
    {

        public static void RunIronSimulation()
        {
            // 150 Yard Iron Draw Setup
            LaunchPayload ironDrawData = new LaunchPayload
            {
                BallSpeedMPH = 110.0f,       // Typical ball speed for a 150-yard iron shot
                LaunchAngleDeg = 19.5f,      // Standard mid-iron vertical launch window
                AzimuthDeg = 3.5f,     // Starts 3.5° to the RIGHT of center
                TotalSpinRPM = 6200f,        // Clean iron backspin structure
                SpinAxisDeg = -6.5f          // Tilted 6.5° LEFT (Hooks/Draws back over the target line)
            };

            // Running the simulation
            var ironSim = new MilitaryBallisticsEngine();
            ironSim.Initialize(ironDrawData);
            ironSim.TemperatureC = 20.0f;
            ironSim.HumidityPercent = 50.0f;
            ironSim.ElevationMeters = 0.0f;
            ironSim.GrassHardness = SurfaceHardness.Medium;

            ironSim.RunSimulation();
        }
        public static void RunDriverSimulation()
        {
            // 250 Yard Driver Draw Setup
            LaunchPayload driverDrawData = new LaunchPayload
            {
                BallSpeedMPH = 143.5f,       // Yields optimal kinetic energy for a 250yd carry
                LaunchAngleDeg = 12.8f,      // Classic optimized driver launch window
                AzimuthDeg = 4.8f,     // Starts 4.8° to the RIGHT of center to clear the path
                TotalSpinRPM = 2350f,        // Low-spin driver profile for maximum rollout distance
                SpinAxisDeg = -9.0f          // Tilted 9.0° LEFT (Creates the deeper 10-yard curling draw)
            };

            // Running the simulation
            var driverSim = new MilitaryBallisticsEngine();
            driverSim.Initialize(driverDrawData);
            driverSim.TemperatureC = 20.0f;
            driverSim.HumidityPercent = 50.0f;
            driverSim.ElevationMeters = 0.0f;
            driverSim.GrassHardness = SurfaceHardness.Medium;

            driverSim.RunSimulation();

            // Example usage of the UltimateLaunchMonitorEngine with Garmin radar metrics
            float ballSpeedMPH = 150.0f; // Example ball speed in MPH
            float launchAngleDeg = 12.0f; // Example launch angle in degrees
            float launchDirectionDeg = 0.0f; // Example launch direction in degrees
            float totalSpinRPM = 2500.0f; // Example total spin in RPM
            float spinAxisDeg = 0.0f; // Example spin axis in degrees
            UltimateLaunchMonitorEngine engine = GarminRadarBridge.InitializeFromGarmin(
                ballSpeedMPH,
                launchAngleDeg,
                launchDirectionDeg,
                totalSpinRPM,
                spinAxisDeg
            );
            
        }
    }
}
