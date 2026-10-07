/*
 * SPDX-License-Identifier: Apache-2.0
 *
 * Copyright (c) 2026 Jason Landreth
 *
 * Licensed under the Apache License, Version 2.0 (the "License");
 * you may not use this file except in compliance with the License.
 * You may obtain a copy of the License at
 *
 *     http://www.apache.org/licenses/LICENSE-2.0
 *
 * Unless required by applicable law or agreed to in writing, software
 * distributed under the License is distributed on an "AS IS" BASIS,
 * WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 * See the License for the specific language governing permissions and
 * limitations under the License.
 *
 * -----------------------------------------------------------------------------
 * Portions of this file are derived from gsp-r10-adapter and remain under MIT:
 *
 * MIT License
 *
 * Copyright (c) 2022 mholow
 *
 * Permission is hereby granted, free of charge, to any person obtaining a copy
 * of this software and associated documentation files (the "Software"), to deal
 * in the Software without restriction, including without limitation the rights
 * to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
 * copies of the Software, and to permit persons to whom the Software is
 * furnished to do so, subject to the following conditions:
 *
 * The above copyright notice and this permission notice shall be included in all
 * copies or substantial portions of the Software.
 *
 * THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
 * IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
 * FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
 * AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
 * LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
 * OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
 * SOFTWARE.
 *
 * Derived from:
 * https://github.com/mholow/gsp-r10-adapter
 */
 
using InTheHand.Bluetooth;
using OpenSwingGolfSimulator.Hardware.Ble;
using OpenSwingGolfSimulator.Hardware.LaunchMonitors.Garmin.GarminEventArgs;
using OpenSwingGolfSimulator.Hardware.Decoders.Decoders;
using OpenSwingGolfSimulator.Shared.Models;

namespace OpenSwingGolfSimulator.Hardware.LaunchMonitors.Garmin
{
    /// <summary>
    /// Connects to a paired Garmin Approach R10 (or compatible) launch monitor over Bluetooth LE
    /// and raises an event with decoded <see cref="ShotData"/> each time a shot is recorded.
    /// The device must already be paired through the operating system's Bluetooth settings
    /// before calling <see cref="ConnectAsync"/> - this class does not perform pairing itself,
    /// only connecting to an already-paired device.
    /// </summary>
    public sealed class GarminR10Client : IDisposable
    {
        /// <summary>
        /// The log4net logger for this class.
        /// </summary>
        private static readonly log4net.ILog Log = log4net.LogManager.GetLogger(typeof(GarminR10Client));

        /// <summary>
        /// Conversion factor from feet to meters, used by <see cref="SendShotConfig"/>.
        /// </summary>
        private static readonly float FeetToMeters = 1f / 3.281f;

        /// <summary>
        /// Decodes raw device metrics into <see cref="ShotData"/>. Injectable so consuming
        /// applications can supply their own implementation (or a mock for testing).
        /// </summary>
        private readonly IMetricsDecoder _metricsDecoder;

        /// <summary>
        /// Whether <see cref="Dispose()"/> has already run, to guard against running it twice.
        /// </summary>
        private bool _disposedValue;

        /// <summary>
        /// The device name most recently passed to <see cref="ConnectAsync"/>, remembered for automatic reconnect attempts.
        /// </summary>
        private string _lastDeviceName = "Approach R10";

        /// <summary>
        /// The max-attempts value most recently passed to <see cref="ConnectAsync"/>, remembered for automatic reconnect attempts.
        /// </summary>
        private int _lastMaxAttempts = 5;

        /// <summary>
        /// The retry-delay value most recently passed to <see cref="ConnectAsync"/>, remembered for automatic reconnect attempts.
        /// </summary>
        private int _lastRetryDelaySeconds = 3;

        /// <summary>
        /// Cancels any in-flight <see cref="ReconnectLoopAsync"/> when a newer disconnect occurs or this instance is disposed.
        /// </summary>
        private CancellationTokenSource? _reconnectCts;

        /// <summary>
        /// Guards against overlapping <see cref="ConnectAsync"/> calls.
        /// </summary>
        private bool _connecting;

        /// <summary>
        /// The underlying Bluetooth device, once found. Null until <see cref="ConnectAsync"/>
        /// has located a paired device with a matching name.
        /// </summary>
        public BluetoothDevice? Device { get; private set; }

        /// <summary>
        /// The underlying launch monitor GATT session, once connected. Null until connection
        /// and device setup have completed successfully.
        /// </summary>
        public LaunchMonitorDevice? LaunchMonitor { get; private set; }

        /// <summary>
        /// The actual current connection state, reflecting the live GATT link rather than just
        /// whether <see cref="ConnectAsync"/> was ever called successfully. False both before
        /// the first successful connection and after a disconnect (including while an automatic
        /// reconnect attempt is in progress).
        /// </summary>
        public bool IsConnected => Device?.Gatt?.IsConnected ?? false;

        /// <summary>
        /// Whether to automatically attempt to reconnect (repeating the same device name and
        /// retry settings originally passed to <see cref="ConnectAsync"/>) if the BLE connection
        /// is lost after a successful connection. Defaults to true.
        /// </summary>
        public bool AutoReconnect { get; set; } = true;

        /// <summary>
        /// Whether to automatically wake the device if it reports itself asleep. Defaults to true.
        /// </summary>
        public bool AutoWake { get; set; } = true;

        /// <summary>
        /// Whether to run tilt calibration automatically once connected. Defaults to true.
        /// </summary>
        public bool CalibrateTiltOnConnect { get; set; } = true;

        /// <summary>
        /// Raised once the device is connected and ready to record shots.
        /// </summary>
        public event GarminR10ConnectedEventHandler? Connected;

        /// <summary>
        /// Handler signature for <see cref="Connected"/>.
        /// </summary>
        /// <param name="sender">The client that raised the event.</param>
        /// <param name="E">The event data, carrying the connected device's info.</param>
        public delegate void GarminR10ConnectedEventHandler(object? sender, GarminR10ConnectedEventArgs E);

        /// <summary>
        /// Raised with a human-readable description of each step as connection and setup progress.
        /// </summary>
        public event GarminR10InfoEventHandler? Info;

        /// <summary>
        /// Handler signature for <see cref="Info"/>.
        /// </summary>
        /// <param name="sender">The client that raised the event.</param>
        /// <param name="E">The event data, carrying the step description.</param>
        public delegate void GarminR10InfoEventHandler(object? sender, GarminR10InfoEventArgs E);

        /// <summary>
        /// Raised if the Bluetooth connection to the device is lost.
        /// </summary>
        public event GarminR10DisconnectedEventHandler? Disconnected;

        /// <summary>
        /// Handler signature for <see cref="Disconnected"/>.
        /// </summary>
        /// <param name="sender">The client that raised the event.</param>
        /// <param name="E">The event data, carrying the disconnected device's name and reconnect status.</param>
        public delegate void GarminR10DisconnectedEventHandler(object? sender, GarminR10DisconnectedEventArgs E);

        /// <summary>
        /// Raised when the device reports an error condition.
        /// </summary>
        public event GarminR10ErrorEventHandler? Error;

        /// <summary>
        /// Handler signature for <see cref="Error"/>.
        /// </summary>
        /// <param name="sender">The client that raised the event.</param>
        /// <param name="E">The event data, carrying the error message.</param>
        public delegate void GarminR10ErrorEventHandler(object? sender, GarminR10ErrorEventArgs E);

        /// <summary>
        /// Raised each time a shot is recorded and decoded.
        /// </summary>
        public event GarminR10ShotReceivedEventHandler? ShotReceived;

        /// <summary>
        /// Handler signature for <see cref="ShotReceived"/>.
        /// </summary>
        /// <param name="sender">The client that raised the event.</param>
        /// <param name="E">The event data, carrying the decoded shot.</param>
        public delegate void GarminR10ShotReceivedEventHandler(object? sender, GarminR10ShotEventArgs E);

        /// <summary>
        /// Raised when the device's battery level is reported or changes.
        /// </summary>
        public event GarminR10BatteryUpdatedEventHandler? BatteryUpdated;

        /// <summary>
        /// Handler signature for <see cref="BatteryUpdated"/>.
        /// </summary>
        /// <param name="sender">The client that raised the event.</param>
        /// <param name="E">The event data, carrying the battery percentage.</param>
        public delegate void GarminR10BatteryUpdatedEventHandler(object? sender, GarminR10BatteryEventArgs E);

        /// <summary>
        /// Raised when the device's ready-to-record state changes.
        /// </summary>
        public event GarminR10ReadyChangedEventHandler? ReadyChanged;

        /// <summary>
        /// Handler signature for <see cref="ReadyChanged"/>.
        /// </summary>
        /// <param name="sender">The client that raised the event.</param>
        /// <param name="E">The event data, carrying the new readiness state.</param>
        public delegate void GarminR10ReadyChangedEventHandler(object? sender, GarminR10ReadyEventArgs E);

        /// <summary>
        /// Constructs a new client.
        /// </summary>
        /// <param name="MetricsDecoder">
        /// The metrics decoder to use when translating raw device metrics into <see cref="ShotData"/>.
        /// Defaults to the built-in <see cref="ShotDataResults.MetricsDecoder"/> if not supplied -
        /// pass a custom implementation to reuse this client from another application with its
        /// own decoding logic, or to substitute a mock for unit testing.
        /// </param>
        public GarminR10Client()
        {
            _metricsDecoder = new MetricsDecoder();
        }

        /// <summary>
        /// Searches the operating system's already-paired Bluetooth devices for one matching
        /// <paramref name="DeviceName"/>, connects to it over BLE, and performs the device
        /// handshake and GATT subscription setup required to start receiving shot data.
        /// First-attempt BLE connections commonly fail on Windows even when the device is
        /// present and paired correctly, so this retries the GATT connection step up to
        /// <paramref name="MaxAttempts"/> times before giving up.
        /// </summary>
        /// <param name="DeviceName">
        /// The exact paired device name to connect to. Defaults to "Approach R10", the R10's
        /// factory Bluetooth name.
        /// </param>
        /// <param name="MaxAttempts">
        /// How many times to retry the GATT connection step before giving up. Defaults to 5.
        /// </param>
        /// <param name="RetryDelaySeconds">
        /// How many seconds to wait between connection attempts. Defaults to 3.
        /// </param>
        /// <returns>True if connection and setup succeeded; false otherwise.</returns>
        public async Task<bool> ConnectAsync(string DeviceName = "Approach R10", int MaxAttempts = 5, int RetryDelaySeconds = 3)
        {
            if (_connecting)
            {
                RaiseError("ConnectAsync called while a connection attempt is already in progress. Ignoring this call.");
                return false;
            }

            _connecting = true;
            _lastDeviceName = DeviceName;
            _lastMaxAttempts = MaxAttempts;
            _lastRetryDelaySeconds = RetryDelaySeconds;

            try
            {
                return await ConnectCoreAsync(DeviceName, MaxAttempts, RetryDelaySeconds);
            }
            finally
            {
                _connecting = false;
            }
        }

        /// <summary>
        /// The actual connection logic invoked by <see cref="ConnectAsync"/>, separated so the public entry point can wrap it with the <see cref="_connecting"/> re-entrancy guard.
        /// </summary>
        /// <param name="DeviceName">The paired device name to connect to.</param>
        /// <param name="MaxAttempts">How many times to retry the GATT connection step before giving up.</param>
        /// <param name="RetryDelaySeconds">How many seconds to wait between connection attempts.</param>
        /// <returns>True if connection and setup succeeded; false otherwise.</returns>
        private async Task<bool> ConnectCoreAsync(string DeviceName, int MaxAttempts, int RetryDelaySeconds)
        {
            RaiseInfo($"Searching for paired device named '{DeviceName}'...");

            try
            {
                Device = await FindPairedDeviceAsync(DeviceName);
            }
            catch (Exception ex)
            {
                RaiseError($"Failed to query paired Bluetooth devices: {ex.GetType().Name}: {ex.Message}. " +
                    "Make sure Bluetooth is turned on.");
                return false;
            }

            if (Device == null)
            {
                RaiseError($"Could not find a paired device named '{DeviceName}'. " +
                    "Pair the R10 through Windows Bluetooth settings before connecting.");
                return false;
            }

            RaiseInfo($"Found paired device: {Device.Name} (ID: {Device.Id})");

            for (int attempt = 1; attempt <= MaxAttempts; attempt++)
            {
                RaiseInfo($"Attempting GATT connection ({attempt}/{MaxAttempts})...");

                try
                {
                    await Device.Gatt.ConnectAsync();
                }
                catch (Exception ex)
                {
                    RaiseError($"Connection attempt {attempt}/{MaxAttempts} threw {ex.GetType().Name}: {ex.Message}");
                }

                if (Device.Gatt.IsConnected)
                {
                    RaiseInfo("GATT connection established.");
                    break;
                }

                if (attempt < MaxAttempts)
                {
                    RaiseError($"Connection attempt {attempt}/{MaxAttempts} failed. Retrying in {RetryDelaySeconds}s...");
                    await Task.Delay(TimeSpan.FromSeconds(RetryDelaySeconds));
                }
            }

            if (!Device.Gatt.IsConnected)
            {
                RaiseError($"Could not connect to '{Device.Name}' after {MaxAttempts} attempts. " +
                    "Make sure the device is awake (press the power button), in range, and not " +
                    "already connected to another app (Garmin Golf, GSPro, etc).");
                return false;
            }

            Device.Gatt.AutoConnect = true;
            Device.GattServerDisconnected += OnDeviceDisconnected;

            // Give Windows a moment to fully populate the GATT service table after a fresh
            // connection - querying services immediately can otherwise time out even though
            // the connection itself succeeded.
            RaiseInfo("Waiting for GATT services to settle...");
            await Task.Delay(TimeSpan.FromSeconds(1));

            RaiseInfo("Starting device setup (service discovery, characteristic subscriptions, handshake)...");
            LaunchMonitor = SetupLaunchMonitor(Device);
            if (LaunchMonitor == null)
            {
                RaiseError("Connected over Bluetooth, but device setup failed.");
                return false;
            }

            RaiseInfo("Device setup complete.");
            Connected?.Invoke(this, new GarminR10ConnectedEventArgs(
                DeviceName: Device.Name ?? DeviceName,
                Model: LaunchMonitor.Model,
                Firmware: LaunchMonitor.Firmware,
                Serial: LaunchMonitor.Serial,
                Battery: LaunchMonitor.Battery));
            return true;
        }

        /// <summary>
        /// Sends the device's environmental shot configuration (temperature, humidity, altitude,
        /// air density, and tee distance), used by the device's own internal calculations.
        /// Call this after <see cref="ConnectAsync"/> succeeds, before recording shots.
        /// </summary>
        /// <param name="TemperatureFahrenheit">Ambient temperature, in Fahrenheit.</param>
        /// <param name="Humidity">Relative humidity, 0-1.</param>
        /// <param name="Altitude">Altitude, in meters.</param>
        /// <param name="AirDensity">Relative air density, 1.0 = sea level standard.</param>
        /// <param name="TeeDistanceFeet">Distance from the device to the tee/ball, in feet.</param>
        /// <returns>True if the device acknowledged the configuration.</returns>
        public bool SendShotConfig(float TemperatureFahrenheit = 60f, float Humidity = 1f,
            float Altitude = 0f, float AirDensity = 1f, float TeeDistanceFeet = 7f)
        {
            if (LaunchMonitor == null)
            {
                return false;
            }

            float teeRangeMeters = TeeDistanceFeet * FeetToMeters;
            return LaunchMonitor.ShotConfig(TemperatureFahrenheit, Humidity, Altitude, AirDensity, teeRangeMeters);
        }

        /// <summary>
        /// Searches the OS's paired Bluetooth devices for one matching the given name.
        /// </summary>
        /// <param name="DeviceName">The exact paired device name to search for.</param>
        /// <returns>The matching device, or null if none was found.</returns>
        private static async Task<BluetoothDevice?> FindPairedDeviceAsync(string DeviceName)
        {
            foreach (BluetoothDevice pairedDevice in await InTheHand.Bluetooth.Bluetooth.GetPairedDevicesAsync())
            {
                if (pairedDevice.Name == DeviceName)
                {
                    return pairedDevice;
                }
            }

            return null;
        }

        /// <summary>
        /// Handles a GATT disconnect: tears down the current session, raises
        /// <see cref="Disconnected"/>, and starts an automatic reconnect loop if
        /// <see cref="AutoReconnect"/> is enabled.
        /// </summary>
        /// <param name="sender">The device that raised the disconnect.</param>
        /// <param name="E">Unused event arguments.</param>
        private void OnDeviceDisconnected(object? sender, EventArgs E)
        {
            string disconnectedDeviceName = Device?.Name ?? _lastDeviceName;

            if (Device != null)
            {
                Device.GattServerDisconnected -= OnDeviceDisconnected;
            }

            LaunchMonitor?.Dispose();
            LaunchMonitor = null;

            Disconnected?.Invoke(this, new GarminR10DisconnectedEventArgs(
                DeviceName: disconnectedDeviceName,
                WillAutoReconnect: AutoReconnect && !_disposedValue));

            if (AutoReconnect && !_disposedValue)
            {
                _reconnectCts?.Cancel();
                _reconnectCts = new CancellationTokenSource();
                _ = ReconnectLoopAsync(_reconnectCts.Token);
            }
        }

        /// <summary>
        /// Repeatedly attempts to reconnect, using the device name and retry settings from the
        /// original <see cref="ConnectAsync"/> call, until it succeeds, the token is cancelled
        /// (a new disconnect superseded this attempt, or <see cref="Dispose"/> was called), or
        /// this instance is disposed. Runs in the background; failures are reported via
        /// <see cref="Error"/> rather than thrown, since nothing is awaiting this task directly.
        /// </summary>
        /// <param name="Token">Cancelled if a newer disconnect/reconnect supersedes this attempt, or on Dispose.</param>
        private async Task ReconnectLoopAsync(CancellationToken Token)
        {
            RaiseInfo("Connection lost. Attempting to reconnect...");

            while (!Token.IsCancellationRequested && !_disposedValue)
            {
                bool connected;
                try
                {
                    connected = await ConnectAsync(_lastDeviceName, _lastMaxAttempts, _lastRetryDelaySeconds);
                }
                catch (Exception ex)
                {
                    RaiseError($"Reconnect attempt threw {ex.GetType().Name}: {ex.Message}");
                    connected = false;
                }

                if (connected)
                {
                    RaiseInfo("Reconnected successfully.");
                    return;
                }

                if (Token.IsCancellationRequested || _disposedValue)
                {
                    return;
                }

                RaiseError("Reconnect attempt failed. Will retry shortly.");

                try
                {
                    await Task.Delay(TimeSpan.FromSeconds(10), Token);
                }
                catch (OperationCanceledException)
                {
                    return;
                }
            }
        }

        /// <summary>
        /// Constructs and configures a <see cref="LaunchMonitorDevice"/> for the given device,
        /// wiring its events through to this client's own events, and runs its <see cref="LaunchMonitorDevice.Setup"/>.
        /// </summary>
        /// <param name="BluetoothDevice">The connected Bluetooth device to set up as a launch monitor.</param>
        /// <returns>The set-up launch monitor device, or null if setup failed.</returns>
        private LaunchMonitorDevice? SetupLaunchMonitor(BluetoothDevice BluetoothDevice)
        {
            LaunchMonitorDevice lm = new LaunchMonitorDevice(BluetoothDevice)
            {
                AutoWake = AutoWake,
                CalibrateTiltOnConnect = CalibrateTiltOnConnect,
            };

            lm.BatteryLifeUpdated += (_, e) => BatteryUpdated?.Invoke(this, new GarminR10BatteryEventArgs(e.Battery));
            lm.Error += (_, e) => RaiseError($"{e.Severity}: {e.Message}");
            lm.SetupError += (_, e) => RaiseError($"Setup error: {e.Message}");
            lm.ReadinessChanged += (_, e) =>
            {
                RaiseInfo($"Device readiness changed: {(e.Ready ? "READY" : "not ready")}");
                ReadyChanged?.Invoke(this, new GarminR10ReadyEventArgs(e.Ready));
            };

            lm.ShotMetrics += (_, e) =>
            {
                if (e.Metrics == null)
                {
                    return;
                }

                RaiseInfo($"Shot metrics received (device shot id {e.Metrics.ShotId}).");
                ShotData shot = _metricsDecoder.Decode(e.Metrics);
                ShotReceived?.Invoke(this, new GarminR10ShotEventArgs(shot));
            };

            RaiseInfo("Reading device info (serial, firmware, model) and subscribing to battery updates...");
            RaiseInfo("Subscribing to measurement, control, and status GATT characteristics...");
            RaiseInfo("Performing device handshake...");

            if (!lm.Setup())
            {
                RaiseError("Device.Setup() returned false - handshake or GATT subscription failed.");
                lm.Dispose();
                return null;
            }

            RaiseInfo($"Handshake complete. Model={lm.Model}, Firmware={lm.Firmware}, Serial={lm.Serial}, Battery={lm.Battery}%");
            RaiseInfo("Waking device (if asleep) and requesting current status...");
            RaiseInfo($"Device state: {lm.CurrentState}, Tilt: {lm.DeviceTilt}");

            if (CalibrateTiltOnConnect)
            {
                RaiseInfo("Running tilt calibration...");
            }

            return lm;
        }

        /// <summary>
        /// Logs an informational message via log4net and raises <see cref="Info"/> with it.
        /// </summary>
        /// <param name="Message">A human-readable description of the step in progress.</param>
        private void RaiseInfo(string Message)
        {
            Log.Info(Message);
            Info?.Invoke(this, new GarminR10InfoEventArgs(Message));
        }

        /// <summary>
        /// Logs an error via log4net and raises <see cref="Error"/> with it.
        /// </summary>
        /// <param name="Message">A human-readable description of the error.</param>
        private void RaiseError(string Message)
        {
            Log.Error(Message);
            Error?.Invoke(this, new GarminR10ErrorEventArgs(Message));
        }

        /// <summary>
        /// Cancels any in-flight reconnect attempt and disconnects the current GATT session.
        /// </summary>
        /// <param name="Disposing">True if called from <see cref="Dispose()"/>; false if called from a finalizer.</param>
        private void Dispose(bool Disposing)
        {
            if (!_disposedValue)
            {
                if (Disposing)
                {
                    _reconnectCts?.Cancel();

                    if (Device != null)
                    {
                        Device.GattServerDisconnected -= OnDeviceDisconnected;
                    }

                    LaunchMonitor?.Dispose();
                }

                _disposedValue = true;
            }
        }

        /// <summary>
        /// Disconnects from the device and releases all resources.
        /// </summary>
        public void Dispose()
        {
            Dispose(Disposing: true);
            GC.SuppressFinalize(this);
        }
    }
}
