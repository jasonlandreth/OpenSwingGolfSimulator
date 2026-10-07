using Google.Protobuf;
using InTheHand.Bluetooth;
using LaunchMonitor.Proto;
using Linux.Bluetooth;
using OpenSwingGolfSimulator.Hardware.Models;
using OpenSwingGolfSimulator.Shared.Enums;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static LaunchMonitor.Proto.State.Types;
using static LaunchMonitor.Proto.SubscribeResponse.Types;
using static LaunchMonitor.Proto.WakeUpResponse.Types;
// removed stray using that conflicted with proto-generated Error type
using GattCharacteristic = InTheHand.Bluetooth.GattCharacteristic;

namespace OpenSwingGolfSimulator.Hardware.Ble
{
    /// <summary>
    /// Bluetooth LE device transport for the Garmin Approach R10 (and compatible launch monitors),
    /// adding the measurement/control/status GATT characteristics and the launch-monitor-specific
    /// protobuf request/response methods (wake, status, tilt, alert subscription, shot config, tilt
    /// calibration) on top of the shared <see cref="BleProtocolDevice"/> transport.
    /// </summary>
    public class LaunchMonitorDevice : BleProtocolDevice
    {
        /// <summary>
        /// The log4net logger for this class.
        /// </summary>
        private static readonly log4net.ILog Log = log4net.LogManager.GetLogger(typeof(LaunchMonitorDevice));

        /// <summary>
        /// Conversion factor from meters per second to miles per hour.
        /// </summary>
        private const double MpsToMph = 2.23694d;

        /// <summary>
        /// Shot IDs already raised via <see cref="ShotMetrics"/>, used to filter out duplicate notifications.
        /// </summary>
        private readonly HashSet<uint> _processedShotIDs = new();

        /// <summary>
        /// Backing field for <see cref="CurrentState"/>.
        /// </summary>
        private StateType _currentState;

        /// <summary>
        /// The device's most recently reported operating state. Setting this also updates <see cref="Ready"/>.
        /// </summary>
        public StateType CurrentState
        {
            get => _currentState;
            private set
            {
                _currentState = value;
                Ready = value == StateType.Waiting;
            }
        }

        /// <summary>
        /// The device's most recently reported tilt reading, populated during <see cref="Setup"/> and after each tilt calibration.
        /// </summary>
        public Tilt? DeviceTilt { get; private set; }

        /// <summary>
        /// Backing field for <see cref="Ready"/>.
        /// </summary>
        private bool _ready;

        /// <summary>
        /// Whether the device is currently ready to record a shot. Raises <see cref="ReadinessChanged"/> when it changes.
        /// </summary>
        public bool Ready
        {
            get => _ready;
            private set
            {
                bool changed = _ready != value;
                _ready = value;
                if (changed)
                {
                    ReadinessChanged?.Invoke(this, new ReadinessChangedEventArgs() { Ready = value });
                }
            }
        }

        /// <summary>
        /// Whether to automatically send a wake-up request when the device reports itself asleep. Defaults to true.
        /// </summary>
        public bool AutoWake { get; set; } = true;

        /// <summary>
        /// Whether to run tilt calibration automatically as part of <see cref="Setup"/>. Defaults to true.
        /// </summary>
        public bool CalibrateTiltOnConnect { get; set; } = true;

        /// <summary>
        /// Raised whenever <see cref="Ready"/> changes.
        /// </summary>
        public event ReadinessChangedEventHandler? ReadinessChanged;

        /// <summary>
        /// Handler signature for <see cref="ReadinessChanged"/>.
        /// </summary>
        /// <param name="sender">The device that raised the event.</param>
        /// <param name="e">The event data carrying the new readiness state.</param>
        public delegate void ReadinessChangedEventHandler(object sender, ReadinessChangedEventArgs e);

        /// <summary>
        /// Event data for <see cref="ReadinessChanged"/>.
        /// </summary>
        public class ReadinessChangedEventArgs : EventArgs
        {
            /// <summary>
            /// Whether the device is now ready to record a shot.
            /// </summary>
            public bool Ready { get; set; }
        }

        /// <summary>
        /// Raised when the device reports an error condition.
        /// </summary>
        public event ErrorEventHandler? Error;

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

        /// <summary>
        /// Raised each time a shot is recorded and its raw metrics are received.
        /// </summary>
        public event MetricsEventHandler? ShotMetrics;

        /// <summary>
        /// Raised each time a shot is recorded and normalized metrics are produced.
        /// </summary>
        public event ShotMetricsSnapshotEventHandler? ShotMetricsSnapshotReceived;

        /// <summary>
        /// Handler signature for <see cref="ShotMetrics"/>.
        /// </summary>
        /// <param name="sender">The device that raised the event.</param>
        /// <param name="e">The event data carrying the raw metrics.</param>
        public delegate void MetricsEventHandler(object sender, MetricsEventArgs e);

        /// <summary>
        /// Handler signature for <see cref="ShotMetricsSnapshotReceived"/>.
        /// </summary>
        /// <param name="sender">The device that raised the event.</param>
        /// <param name="e">The event data carrying normalized metrics.</param>
        public delegate void ShotMetricsSnapshotEventHandler(object sender, ShotMetricsSnapshotEventArgs e);

        /// <summary>
        /// Event data for <see cref="ShotMetrics"/>.
        /// </summary>
        public class MetricsEventArgs : EventArgs
        {
            /// <summary>
            /// The raw metrics reported for the shot.
            /// </summary>
            public Metrics? Metrics { get; set; }
        }

        /// <summary>
        /// Event data for <see cref="ShotMetricsSnapshotReceived"/>.
        /// </summary>
        public class ShotMetricsSnapshotEventArgs : EventArgs
        {
            /// <summary>
            /// The normalized shot metrics snapshot.
            /// </summary>
            public required ShotMetricsSnapshot Snapshot { get; init; }
        }

        /// <summary>
        /// Initializes the launch monitor transport for a given Bluetooth device.
        /// </summary>
        /// <param name="Device">The Bluetooth LE device to communicate with.</param>
        public LaunchMonitorDevice(BluetoothDevice Device) : base(Device)
        {
        }

        /// <summary>
        /// Subscribes to the measurement/control/status GATT characteristics, then performs the base
        /// device setup (device info reads and handshake), then wakes the device, requests its
        /// current status and tilt, subscribes to alerts, and optionally runs tilt calibration.
        /// </summary>
        /// <returns>True if every step completed successfully; false otherwise.</returns>
        public override bool Setup()
        {
            try
            {
                Log.Debug("Subscribing to measurement service");
                GattService? measService = Device.Gatt.GetPrimaryServiceAsync(BleGattGuids.MEASUREMENT_SERVICE_UUID).WaitAsync(TimeSpan.FromSeconds(15)).Result;
                if (measService == null)
                {
                    RaiseError("Measurement service was not found on the device.", global::LaunchMonitor.Proto.Error.Types.Severity.Fatal);
                    return false;
                }

                GattCharacteristic? measCharacteristic = measService.GetCharacteristicAsync(BleGattGuids.MEASUREMENT_CHARACTERISTIC_UUID).WaitAsync(TimeSpan.FromSeconds(15)).Result;
                if (measCharacteristic == null)
                {
                    RaiseError("Measurement characteristic was not found.", global::LaunchMonitor.Proto.Error.Types.Severity.Fatal);
                    return false;
                }

                if (!measCharacteristic.StartNotificationsAsync().Wait(TimeSpan.FromSeconds(15)))
                {
                    Log.Error("Error subscribing to measurement characteristic");
                }

                measCharacteristic.CharacteristicValueChanged += (_, _) => { };

                Log.Debug("Subscribing to control service");
                GattCharacteristic? controlPoint = measService.GetCharacteristicAsync(BleGattGuids.CONTROL_POINT_CHARACTERISTIC_UUID).WaitAsync(TimeSpan.FromSeconds(15)).Result;
                if (controlPoint == null)
                {
                    RaiseError("Control point characteristic was not found.", global::LaunchMonitor.Proto.Error.Types.Severity.Fatal);
                    return false;
                }

                if (!controlPoint.StartNotificationsAsync().Wait(TimeSpan.FromSeconds(15)))
                {
                    Log.Error("Error subscribing to the control characteristic");
                }

                controlPoint.CharacteristicValueChanged += (_, _) => { };

                Log.Debug("Subscribing to status service");
                GattCharacteristic? statusCharacteristic = measService.GetCharacteristicAsync(BleGattGuids.STATUS_CHARACTERISTIC_UUID).WaitAsync(TimeSpan.FromSeconds(15)).Result;
                if (statusCharacteristic == null)
                {
                    RaiseError("Status characteristic was not found.", global::LaunchMonitor.Proto.Error.Types.Severity.Fatal);
                    return false;
                }

                if (!statusCharacteristic.StartNotificationsAsync().Wait(TimeSpan.FromSeconds(15)))
                {
                    Log.Error("Error subscribing to the status characteristic");
                }

                statusCharacteristic.CharacteristicValueChanged += (_, e) =>
                {
                    byte[]? value = e.Value;
                    if (value == null || value.Length < 3)
                    {
                        return;
                    }

                    bool isAwake = value[1] == (byte)0;
                    bool isReady = value[2] == (byte)0;

                    _ = isAwake;
                    _ = isReady;
                };

                bool baseSetupSuccess = base.Setup();
                if (!baseSetupSuccess)
                {
                    Log.Error("Error during base device setup");
                    return false;
                }

                WakeDevice();
                CurrentState = StatusRequest() ?? StateType.Error;
                DeviceTilt = GetDeviceTilt();

                List<AlertStatusMessage> alertStatus = SubscribeToAlerts();
                if (alertStatus.Count == 0)
                {
                    Log.Warn("Alert subscription did not return a response (device may not have acknowledged the subscribe request). Continuing anyway.");
                }

                if (CalibrateTiltOnConnect)
                {
                    StartTiltCalibration();
                }

                return true;
            }
            catch (Exception ex)
            {
                Exception effective = ex;
                while (effective is AggregateException agg && agg.InnerException != null)
                {
                    effective = agg.InnerException;
                }

                RaiseError($"Setup() threw {effective.GetType().Name}: {effective.Message}", global::LaunchMonitor.Proto.Error.Types.Severity.Fatal, effective);
                return false;
            }
        }

        /// <summary>
        /// Handles device-initiated protobuf notifications.
        /// </summary>
        /// <param name="Request">The parsed protobuf message received from the device.</param>
        public override void HandleProtobufRequest(IMessage Request)
        {
            if (Request is not WrapperProto wrapperProtoRequest)
            {
                return;
            }

            AlertDetails? notification = wrapperProtoRequest.Event?.Notification?.AlertNotification_;
            if (notification == null)
            {
                return;
            }

            if (notification.State != null)
            {
                CurrentState = notification.State.State_;
                if (notification.State.State_ == StateType.Standby)
                {
                    if (AutoWake)
                    {
                        Log.Info("Device asleep. Sending wakeup call");
                        WakeDevice();
                    }
                    else
                    {
                        Log.Warn("Device asleep. Wake device using button (or enable autowake in settings)");
                    }
                }
            }

            if (notification.Error != null && notification.Error.HasCode)
            {
                Error?.Invoke(this, new ErrorEventArgs()
                {
                    Message = $"{notification.Error.Code} {notification.Error.DeviceTilt}",
                    Severity = notification.Error.Severity
                });
            }

            if (notification.Metrics != null)
            {
                if (!_processedShotIDs.Add(notification.Metrics.ShotId))
                {
                    Log.Warn($"Received duplicate shot data {notification.Metrics.ShotId}. Ignoring");
                }
                else
                {
                    ShotMetrics?.Invoke(this, new MetricsEventArgs() { Metrics = notification.Metrics });

                    ShotMetricsSnapshot snapshot = CreateShotMetricsSnapshot(notification.Metrics);
                    ShotMetricsSnapshotReceived?.Invoke(this, new ShotMetricsSnapshotEventArgs() { Snapshot = snapshot });
                }
            }

            if (notification.TiltCalibration != null)
            {
                DeviceTilt = GetDeviceTilt();
            }
        }

        /// <summary>
        /// Creates a normalized metrics snapshot from raw device metrics.
        /// </summary>
        /// <param name="Metrics">The raw protobuf metrics payload.</param>
        /// <returns>The normalized snapshot for consumer code.</returns>
        private ShotMetricsSnapshot CreateShotMetricsSnapshot(Metrics Metrics)
        {
            Dictionary<GolfShotMetric, double> values = new();

            if (Metrics.BallMetrics is { } ball)
            {
                AddMetricIfPresent(values, GolfShotMetric.BallSpeed, ball.HasBallSpeed, ball.BallSpeed * MpsToMph);
                AddMetricIfPresent(values, GolfShotMetric.VerticalLaunchAngle, ball.HasLaunchAngle, ball.LaunchAngle);
                AddMetricIfPresent(values, GolfShotMetric.HorizontalLaunchAngle, ball.HasLaunchDirection, ball.LaunchDirection);
                AddMetricIfPresent(values, GolfShotMetric.SpinRate, ball.HasTotalSpin, ball.TotalSpin);
                AddMetricIfPresent(values, GolfShotMetric.SpinAxis, ball.HasSpinAxis, ball.SpinAxis);

                if (ball.HasTotalSpin && ball.HasSpinAxis)
                {
                    double spinAxisRadians = ball.SpinAxis * Math.PI / 180d;
                    values[GolfShotMetric.BackSpin] = ball.TotalSpin * Math.Cos(spinAxisRadians);
                    values[GolfShotMetric.SideSpin] = ball.TotalSpin * Math.Sin(spinAxisRadians);
                }
            }

            if (Metrics.ClubMetrics is { } club)
            {
                AddMetricIfPresent(values, GolfShotMetric.ClubHeadSpeed, club.HasClubHeadSpeed, club.ClubHeadSpeed * MpsToMph);
                AddMetricIfPresent(values, GolfShotMetric.FaceToTarget, club.HasClubAngleFace, club.ClubAngleFace);
                AddMetricIfPresent(values, GolfShotMetric.ClubPath, club.HasClubAnglePath, club.ClubAnglePath);
                AddMetricIfPresent(values, GolfShotMetric.AttackAngle, club.HasAttackAngle, club.AttackAngle);

                if (club.HasClubAngleFace && club.HasClubAnglePath)
                {
                    values[GolfShotMetric.FaceToPath] = club.ClubAngleFace - club.ClubAnglePath;
                }
            }

            if (Metrics.SwingMetrics is { } swing)
            {
                AddMetricIfPresent(values, GolfShotMetric.BackswingStart, swing.HasBackSwingStartTime, swing.BackSwingStartTime);
                AddMetricIfPresent(values, GolfShotMetric.DownswingStart, swing.HasDownSwingStartTime, swing.DownSwingStartTime);
                AddMetricIfPresent(values, GolfShotMetric.Impact, swing.HasImpactTime, swing.ImpactTime);
                AddMetricIfPresent(values, GolfShotMetric.FollowThroughEnd, swing.HasFollowThroughEndTime, swing.FollowThroughEndTime);
            }

            return new ShotMetricsSnapshot
            {
                ShotId = Metrics.HasShotId ? Metrics.ShotId : 0u,
                Values = values
            };
        }

        /// <summary>
        /// Adds a metric value to the dictionary only when the source has a value.
        /// </summary>
        /// <param name="Values">The destination metric dictionary.</param>
        /// <param name="Metric">The metric key to write.</param>
        /// <param name="HasValue">Whether the source metric exists.</param>
        /// <param name="Value">The metric value to write.</param>
        private static void AddMetricIfPresent(
          IDictionary<GolfShotMetric, double> Values,
          GolfShotMetric Metric,
          bool HasValue,
          double Value)
        {
            if (HasValue)
            {
                Values[Metric] = Value;
            }
        }

        /// <summary>
        /// Logs an error and raises <see cref="Error"/>.
        /// </summary>
        /// <param name="Message">A human-readable description of the error.</param>
        /// <param name="Severity">The severity to report.</param>
        /// <param name="Exception">The exception that caused the error, if any.</param>
        private void RaiseError(string Message, global::LaunchMonitor.Proto.Error.Types.Severity Severity, Exception? Exception = null)
        {
            if (Exception != null)
            {
                Log.Error(Message, Exception);
            }
            else if (Severity == global::LaunchMonitor.Proto.Error.Types.Severity.Fatal)
            {
                Log.Error(Message);
            }
            else
            {
                Log.Warn(Message);
            }

            Error?.Invoke(this, new ErrorEventArgs() { Message = Message, Severity = Severity });
        }

        /// <summary>
        /// Requests the device's current tilt reading.
        /// </summary>
        /// <returns>The tilt reading, or null if the request failed or timed out.</returns>
        public Tilt? GetDeviceTilt()
        {
            IMessage? resp = SendProtobufRequest(
              new WrapperProto() { Service = new LaunchMonitorService() { TiltRequest = new TiltRequest() } }
            );

            if (resp is WrapperProto wrapperProtoResponse)
            {
                Tilt? tilt = wrapperProtoResponse.Service?.TiltResponse?.Tilt;
                if (tilt == null)
                {
                    RaiseError("GetDeviceTilt: response did not contain a tilt reading.", global::LaunchMonitor.Proto.Error.Types.Severity.Warning);
                }

                return tilt;
            }

            RaiseError("GetDeviceTilt: no response received (request may have timed out).", global::LaunchMonitor.Proto.Error.Types.Severity.Warning);
            return null;
        }

        /// <summary>
        /// Sends a wake-up request to the device.
        /// </summary>
        /// <returns>The response status, or null if the request failed or timed out.</returns>
        public ResponseStatus? WakeDevice()
        {
            IMessage? resp = SendProtobufRequest(
              new WrapperProto() { Service = new LaunchMonitorService() { WakeUpRequest = new WakeUpRequest() } }
            );

            if (resp is WrapperProto wrapperProtoResponse)
            {
                ResponseStatus? status = wrapperProtoResponse.Service?.WakeUpResponse?.Status;
                if (status == null)
                {
                    RaiseError("WakeDevice: response did not contain a status.", global::LaunchMonitor.Proto.Error.Types.Severity.Warning);
                }

                return status;
            }

            RaiseError("WakeDevice: no response received (request may have timed out).", global::LaunchMonitor.Proto.Error.Types.Severity.Warning);
            return null;
        }

        /// <summary>
        /// Requests the device's current operating state.
        /// </summary>
        /// <returns>The device's current state, or null if the request failed or timed out.</returns>
        public StateType? StatusRequest()
        {
            IMessage? resp = SendProtobufRequest(
              new WrapperProto() { Service = new LaunchMonitorService() { StatusRequest = new StatusRequest() } }
            );

            if (resp is WrapperProto wrapperProtoResponse)
            {
                if (wrapperProtoResponse.Service?.StatusResponse?.State != null)
                {
                    return wrapperProtoResponse.Service.StatusResponse.State.State_;
                }

                RaiseError("StatusRequest: response did not contain a state.", global::LaunchMonitor.Proto.Error.Types.Severity.Warning);
                return null;
            }

            RaiseError("StatusRequest: no response received (request may have timed out).", global::LaunchMonitor.Proto.Error.Types.Severity.Warning);
            return null;
        }

        /// <summary>
        /// Subscribes to launch monitor alert notifications from the device.
        /// </summary>
        /// <returns>The device's current alert status list, or an empty list if the request failed, timed out, or the response was empty.</returns>
        public List<AlertStatusMessage> SubscribeToAlerts()
        {
            IMessage? resp = SendProtobufRequest(
              new WrapperProto()
              {
                  Event = new EventSharing()
                  {
                      SubscribeRequest = new SubscribeRequest()
                      {
                          Alerts = { new List<AlertMessage>() { new AlertMessage() { Type = AlertNotification.Types.AlertType.LaunchMonitor } } }
                      }
                  }
              }
            );

            if (resp is WrapperProto wrapperProtoResponse)
            {
                IEnumerable<AlertStatusMessage>? alertStatus = wrapperProtoResponse.Event?.SubscribeRespose?.AlertStatus;
                if (alertStatus != null)
                {
                    return alertStatus.ToList();
                }

                RaiseError("SubscribeToAlerts: response did not contain alert status.", global::LaunchMonitor.Proto.Error.Types.Severity.Warning);
                return new List<AlertStatusMessage>();
            }

            RaiseError("SubscribeToAlerts: no response received (request may have timed out).", global::LaunchMonitor.Proto.Error.Types.Severity.Warning);
            return new List<AlertStatusMessage>();
        }

        /// <summary>
        /// Sends the device's environmental shot configuration, used by its own internal calculations.
        /// </summary>
        /// <param name="Temperature">Ambient temperature, in Fahrenheit.</param>
        /// <param name="Humidity">Relative humidity, 0-1.</param>
        /// <param name="Altitude">Altitude, in meters.</param>
        /// <param name="AirDensity">Relative air density, 1.0 = sea level standard.</param>
        /// <param name="TeeRange">Distance from the device to the tee/ball, in meters.</param>
        /// <returns>True if the device acknowledged the configuration with a success flag; false otherwise.</returns>
        public bool ShotConfig(float Temperature, float Humidity, float Altitude, float AirDensity, float TeeRange)
        {
            IMessage? resp = SendProtobufRequest(new WrapperProto()
            {
                Service = new LaunchMonitorService()
                {
                    ShotConfigRequest = new ShotConfigRequest()
                    {
                        Temperature = Temperature,
                        Humidity = Humidity,
                        Altitude = Altitude,
                        AirDensity = AirDensity,
                        TeeRange = TeeRange
                    }
                }
            });

            if (resp is WrapperProto wrapperProtoResponse)
            {
                bool? success = wrapperProtoResponse.Service?.ShotConfigResponse?.Success;
                if (success == null)
                {
                    RaiseError("ShotConfig: response did not contain a success flag.", global::LaunchMonitor.Proto.Error.Types.Severity.Warning);
                    return false;
                }

                return success.Value;
            }

            RaiseError("ShotConfig: no response received (request may have timed out).", global::LaunchMonitor.Proto.Error.Types.Severity.Warning);
            return false;
        }

        /// <summary>
        /// Requests that the device reset its tilt calibration.
        /// </summary>
        /// <param name="ShouldReset">Whether to actually perform the reset. Defaults to true.</param>
        /// <returns>The reset status, or null if the request failed or timed out.</returns>
        public ResetTiltCalibrationResponse.Types.Status? ResetTiltCalibration(bool ShouldReset = true)
        {
            IMessage? resp = SendProtobufRequest(
              new WrapperProto() { Service = new LaunchMonitorService() { ResetTiltCalRequest = new ResetTiltCalibrationRequest() { ShouldReset = ShouldReset } } }
            );

            if (resp is WrapperProto wrapperProtoResponse)
            {
                ResetTiltCalibrationResponse.Types.Status? status = wrapperProtoResponse.Service?.ResetTiltCalResponse?.Status;
                if (status == null)
                {
                    RaiseError("ResetTiltCalibration: response did not contain a status.", global::LaunchMonitor.Proto.Error.Types.Severity.Warning);
                }

                return status;
            }

            RaiseError("ResetTiltCalibration: no response received (request may have timed out).", global::LaunchMonitor.Proto.Error.Types.Severity.Warning);
            return null;
        }

        /// <summary>
        /// Requests that the device start tilt calibration.
        /// </summary>
        /// <param name="ShouldReset">Unused by the current request shape; retained for API compatibility.</param>
        /// <returns>The calibration status, or null if the request failed or timed out.</returns>
        public StartTiltCalibrationResponse.Types.CalibrationStatus? StartTiltCalibration(bool ShouldReset = true)
        {
            IMessage? resp = SendProtobufRequest(
              new WrapperProto() { Service = new LaunchMonitorService() { StartTiltCalRequest = new StartTiltCalibrationRequest() } }
            );

            if (resp is WrapperProto wrapperProtoResponse)
            {
                StartTiltCalibrationResponse.Types.CalibrationStatus? status = wrapperProtoResponse.Service?.StartTiltCalResponse?.Status;
                if (status == null)
                {
                    RaiseError("StartTiltCalibration: response did not contain a status.", global::LaunchMonitor.Proto.Error.Types.Severity.Warning);
                }

                return status;
            }

            RaiseError("StartTiltCalibration: no response received (request may have timed out).", global::LaunchMonitor.Proto.Error.Types.Severity.Warning);
            return null;
        }

        /// <summary>
        /// Unsubscribes all event handlers before disposing the base transport.
        /// </summary>
        /// <param name="Disposing">True if called from <see cref="IDisposable.Dispose"/>; false if called from a finalizer.</param>
        protected override void Dispose(bool Disposing)
        {
            foreach (Delegate d in ReadinessChanged?.GetInvocationList() ?? Array.Empty<Delegate>())
            {
                ReadinessChanged -= (d as ReadinessChangedEventHandler);
            }

            foreach (Delegate d in Error?.GetInvocationList() ?? Array.Empty<Delegate>())
            {
                Error -= (d as ErrorEventHandler);
            }

            foreach (Delegate d in ShotMetrics?.GetInvocationList() ?? Array.Empty<Delegate>())
            {
                ShotMetrics -= (d as MetricsEventHandler);
            }

            foreach (Delegate d in ShotMetricsSnapshotReceived?.GetInvocationList() ?? Array.Empty<Delegate>())
            {
                ShotMetricsSnapshotReceived -= (d as ShotMetricsSnapshotEventHandler);
            }

            base.Dispose(Disposing);
        }
    }
}

