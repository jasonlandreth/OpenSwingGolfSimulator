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
    public class EnvironmentalConditions
    {
        /// <summary> Temperature in Fahrenheit </summary>
        public double TemperatureF { get; set; } = 70.0;

        /// <summary> Elevation above sea level in feet </summary>
        public double AltitudeFeet { get; set; } = 0.0;

        /// <summary> Barometric pressure in inches of Mercury (inHg) </summary>
        public double BarometricPressureInHg { get; set; } = 29.92;

        /// <summary> Relative humidity percentage (0 - 100) </summary>
        public double HumidityPercent { get; set; } = 50.0;

        /// <summary> Wind speed in miles per hour (mph) </summary>
        public double WindSpeedMph { get; set; } = 0.0;

        /// <summary> Wind direction relative to target line in degrees (0 = Headwind, 90 = Crosswind Right, 180 = Tailwind) </summary>
        public double WindDirectionDegrees { get; set; } = 0.0;

        /// <summary>
        /// Calculates air density (kg/m³) based on ambient conditions.
        /// Standard Sea Level air density is ~1.225 kg/m³.
        /// </summary>
        public double GetAirDensity()
        {
            double tempC = (TemperatureF - 32.0) * 5.0 / 9.0;
            double tempK = tempC + 273.15;
            double pressurePa = BarometricPressureInHg * 3386.39;

            // Saturation vapor pressure (hPa)
            double eso = 6.1078;
            double c2 = 17.27;
            double c3 = 237.3;
            double pv = (HumidityPercent / 100.0) * (eso * Math.Exp((c2 * tempC) / (c3 + tempC))) * 100.0;

            double pd = pressurePa - pv;
            double Rd = 287.058; // Specific gas constant for dry air
            double Rv = 461.495; // Specific gas constant for water vapor

            return (pd / (Rd * tempK)) + (pv / (Rv * tempK));
        }
    }
}
