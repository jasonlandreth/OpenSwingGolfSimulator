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

using Google.Protobuf;
using InTheHand.Bluetooth;
using LaunchMonitor.Proto;
using OpenSwingGolfSimulator.Hardware.Utilities;
using OpenSwingGolfSimulator.Utilities.ExtensionMethods;
using System.Text; 

namespace OpenSwingGolfSimulator.Hardware.Ble
{
    /// <summary>
    /// Base Bluetooth LE device transport shared by every device type: COBS framing, CRC16
    /// checking, the handshake sequence, device-info reads, and the protobuf request/response
    /// cycle. Subclasses (such as <see cref="LaunchMonitorDevice"/>) add device-specific GATT
    /// services on top of this.
    /// </summary>
    public abstract class BleProtocolDevice : IDisposable
    {
        /// <summary>
        /// The log4net logger for this class.
        /// </summary>
        private static readonly log4net.ILog Log = log4net.LogManager.GetLogger(typeof(BleProtocolDevice));



        /// <summary>
        /// The underlying Bluetooth LE device this transport is connected to.
        /// </summary>
        public BluetoothDevice Device { get; }

        /// <summary>
        /// The device's most recently reported battery level, as a percentage from 0 to 100.
        /// </summary>
        public int Battery
        {
            get => _mBattery;
            set
            {
                _mBattery = value;
                BatteryLifeUpdated?.Invoke(this, new BatteryEventArgs() { Battery = value });
            }
        }

        /// <summary>
        /// The device's reported model name, populated during <see cref="Setup"/>.
        /// </summary>
        public string? Model { get; private set; }

        /// <summary>
        /// The device's reported firmware version, populated during <see cref="Setup"/>.
        /// </summary>
        public string? Firmware { get; private set; }

        /// <summary>
        /// The device's reported serial number, populated during <see cref="Setup"/>.
        /// </summary>
        public string? Serial { get; private set; }

        /// <summary>
        /// Raised whenever a protobuf message is received from the device (both responses and device-initiated requests).
        /// </summary>
        public event MessageEventHandler? MessageReceived;

        /// <summary>
        /// Raised whenever a protobuf message is sent to the device.
        /// </summary>
        public event MessageEventHandler? MessageSent;

        /// <summary>
        /// Handler signature for <see cref="BleProtocolDevice.MessageReceived"/> and <see cref="MessageSent"/>.
        /// </summary>
        /// <param name="Sender">The device that raised the event.</param>
        /// <param name="E">The event data, carrying the protobuf message.</param>
        public delegate void MessageEventHandler(object Sender, MessageEventArgs E);

        /// <summary>
        /// Event data for <see cref="BleProtocolDevice.MessageReceived"/> and <see cref="MessageSent"/>.
        /// </summary>
        public class MessageEventArgs : EventArgs
        {
            /// <summary>
            /// The protobuf message that was sent or received.
            /// </summary>
            public IMessage? Message { get; set; }
        }

        /// <summary>
        /// Raised whenever the device's battery level is reported or changes.
        /// </summary>
        public event BatteryEventHandler? BatteryLifeUpdated;

        /// <summary>
        /// Handler signature for <see cref="BatteryLifeUpdated"/>.
        /// </summary>
        /// <param name="sender">The device that raised the event.</param>
        /// <param name="E">The event data, carrying the battery percentage.</param>
        public delegate void BatteryEventHandler(object sender, BatteryEventArgs E);

        /// <summary>
        /// Event data for <see cref="BatteryLifeUpdated"/>.
        /// </summary>
        public class BatteryEventArgs : EventArgs
        {
            /// <summary>
            /// The device's battery level, as a percentage from 0 to 100.
            /// </summary>
            public int Battery { get; set; }
        }

        /// <summary>
        /// Raised when <see cref="Setup"/> encounters a problem it can't recover from - a GATT
        /// service/characteristic that couldn't be found, a timeout, or any other exception. Setup()
        /// itself never throws; every failure is logged via log4net and surfaced here, and Setup()
        /// returns false instead.
        /// </summary>
        public event SetupErrorEventHandler? SetupError;

        /// <summary>
        /// Handler signature for <see cref="SetupError"/>.
        /// </summary>
        /// <param name="sender">The device that raised the event.</param>
        /// <param name="E">The event data, carrying the failure message and, if available, the exception.</param>
        public delegate void SetupErrorEventHandler(object sender, SetupErrorEventArgs E);

        /// <summary>
        /// Event data for <see cref="SetupError"/>.
        /// </summary>
        public class SetupErrorEventArgs : EventArgs
        {
            /// <summary>
            /// A human-readable description of what failed.
            /// </summary>
            public string Message { get; set; } = string.Empty;

            /// <summary>
            /// The exception that caused the failure, if the failure was due to a caught exception.
            /// </summary>
            public Exception? Exception { get; set; }
        }

        /// <summary>
        /// Signaled whenever an item is queued for the writer thread.
        /// </summary>
        private readonly EventWaitHandle _mWriterSignal = new AutoResetEvent(false);

        /// <summary>
        /// Outgoing raw byte chunks waiting to be written to the GATT writer characteristic.
        /// </summary>
        private readonly Queue<BleBuffer> _mWriterQueue = new Queue<BleBuffer>();

        /// <summary>
        /// Signaled whenever an item is queued for the reader thread.
        /// </summary>
        private readonly EventWaitHandle _mReaderSignal = new AutoResetEvent(false);

        /// <summary>
        /// Raw byte chunks received from the device, awaiting reassembly into a complete frame.
        /// </summary>
        private readonly Queue<BleBuffer> _mReaderQueue = new Queue<BleBuffer>();

        /// <summary>
        /// Signaled whenever a complete decoded frame is queued for processing.
        /// </summary>
        private readonly EventWaitHandle _mMsgProcessSignal = new AutoResetEvent(false);

        /// <summary>
        /// Complete decoded frames awaiting processing on the message-processing thread.
        /// </summary>
        private readonly Queue<BleBuffer> _mMsgProcessQueue = new Queue<BleBuffer>();

        /// <summary>
        /// Signaled once the handshake response has been received and acknowledged.
        /// </summary>
        private readonly ManualResetEventSlim _mHandshakeCompleteResetEvent = new ManualResetEventSlim(false);

        /// <summary>
        /// Signaled once a protobuf response matching the current request counter has been received.
        /// </summary>
        private readonly ManualResetEventSlim _mProtoResponseResetEvent = new ManualResetEventSlim(false);

        /// <summary>
        /// The most recently received protobuf message, set just before <see cref="_mProtoResponseResetEvent"/> is signaled.
        /// </summary>
        private IMessage? _mLastProtoReceived;

        /// <summary>
        /// Backing field for <see cref="Battery"/>.
        /// </summary>
        private int _mBattery;

        /// <summary>
        /// Whether the initial handshake has completed successfully.
        /// </summary>
        private bool _mHandshakeComplete = false;

        /// <summary>
        /// The per-connection header byte assigned by the device during the handshake.
        /// </summary>
        private byte _mHeader = 0x00;

        /// <summary>
        /// The counter used to correlate outgoing protobuf requests with their responses.
        /// </summary>
        private int _mProtoRequestCounter = 0;

        /// <summary>
        /// Cancels the three background threads on <see cref="Dispose"/>.
        /// </summary>
        private readonly CancellationTokenSource _mCancellationToken;

        /// <summary>
        /// The background task running <see cref="WriterThread"/>.
        /// </summary>
        private readonly Task _mWriterTask;

        /// <summary>
        /// The background task running <see cref="ReaderThread"/>.
        /// </summary>
        private readonly Task _mReaderTask;

        /// <summary>
        /// The background task running <see cref="MsgProcessingThread"/>.
        /// </summary>
        private readonly Task _mMsgProcessingTask;

        /// <summary>
        /// The GATT characteristic outgoing bytes are written to, set once <see cref="Setup"/> has located it.
        /// </summary>
        private GattCharacteristic? _mGattWriter;

        /// <summary>
        /// Whether <see cref="Dispose(bool)"/> has already run, to guard against running it twice.
        /// </summary>
        private bool _mDisposedValue;

        /// <summary>
        /// Initializes the transport for a given Bluetooth device and starts the three background
        /// threads (writer, reader, and message processing) that run for the lifetime of this instance.
        /// </summary>
        /// <param name="Device">The Bluetooth LE device to communicate with.</param>
        public BleProtocolDevice(BluetoothDevice Device)
        {
            this.Device = Device;

            _mCancellationToken = new CancellationTokenSource();
            _mWriterTask = Task.Run(WriterThread, _mCancellationToken.Token);
            _mReaderTask = Task.Run(ReaderThread, _mCancellationToken.Token);
            _mMsgProcessingTask = Task.Run(MsgProcessingThread, _mCancellationToken.Token);
        }

        /// <summary>
        /// Performs device-info reads and GATT setup shared by every device type. Never throws -
        /// any failure (a missing service/characteristic, a timeout, or any other exception) is
        /// logged and reported via <see cref="SetupError"/>, and this returns false instead.
        /// </summary>
        /// <returns>True if setup and the handshake both completed successfully; false otherwise.</returns>
        public virtual bool Setup()
        {
            try
            {
                Log.Debug("Getting device info service");
                GattService? deviceInfoService = Device.Gatt.GetPrimaryServiceAsync(BleGattGuids.DEVICE_INFO_SERVICE_UUID).WaitAsync(TimeSpan.FromSeconds(15)).Result;
                if (deviceInfoService == null)
                {
                    RaiseSetupError("Device info service (0000180a) was not found on the device.");
                    return false;
                }

                Log.Debug("Reading serial number");
                GattCharacteristic? serialCharacteristic = deviceInfoService.GetCharacteristicAsync(BleGattGuids.SERIAL_NUMBER_CHARACTERISTIC_UUID).WaitAsync(TimeSpan.FromSeconds(15)).Result;
                if (serialCharacteristic == null)
                {
                    RaiseSetupError("Serial number characteristic was not found.");
                    return false;
                }
                byte[]? serialBytes = serialCharacteristic.ReadValueAsync().WaitAsync(TimeSpan.FromSeconds(15)).Result;
                Serial = serialBytes != null ? Encoding.ASCII.GetString(serialBytes) : null;

                Log.Debug("Reading firmware version");
                GattCharacteristic? firmwareCharacteristic = deviceInfoService.GetCharacteristicAsync(BleGattGuids.FIRMWARE_CHARACTERISTIC_UUID).WaitAsync(TimeSpan.FromSeconds(15)).Result;
                if (firmwareCharacteristic == null)
                {
                    RaiseSetupError("Firmware characteristic was not found.");
                    return false;
                }
                byte[]? firmwareBytes = firmwareCharacteristic.ReadValueAsync().WaitAsync(TimeSpan.FromSeconds(15)).Result;
                Firmware = firmwareBytes != null ? Encoding.ASCII.GetString(firmwareBytes) : null;

                Log.Debug("Reading model name");
                GattCharacteristic? modelCharacteristic = deviceInfoService.GetCharacteristicAsync(BleGattGuids.MODEL_CHARACTERISTIC_UUID).WaitAsync(TimeSpan.FromSeconds(15)).Result;
                if (modelCharacteristic == null)
                {
                    RaiseSetupError("Model characteristic was not found.");
                    return false;
                }
                byte[]? modelBytes = modelCharacteristic.ReadValueAsync().WaitAsync(TimeSpan.FromSeconds(15)).Result;
                Model = modelBytes != null ? Encoding.ASCII.GetString(modelBytes) : null;

                Log.Debug("Reading battery life");
                GattService? batteryService = Device.Gatt.GetPrimaryServiceAsync(BleGattGuids.BATTERY_SERVICE_UUID).WaitAsync(TimeSpan.FromSeconds(15)).Result;
                if (batteryService == null)
                {
                    RaiseSetupError("Battery service was not found.");
                    return false;
                }
                GattCharacteristic? batteryCharacteristic = batteryService.GetCharacteristicAsync(BleGattGuids.BATTERY_CHARACTERISTIC_UUID).WaitAsync(TimeSpan.FromSeconds(15)).Result;
                if (batteryCharacteristic == null)
                {
                    RaiseSetupError("Battery characteristic was not found.");
                    return false;
                }
                batteryCharacteristic.CharacteristicValueChanged += (o, E) =>
                {
                    // Read e.Value into a local exactly once - see the note on the same pattern below,
                    // in the device interface notifier's CharacteristicValueChanged handler.
                    byte[]? value = E.Value;
                    if (value != null && value.Length > 0)
                    {
                        Battery = value[0];
                    }
                };
                batteryCharacteristic.StartNotificationsAsync().Wait(TimeSpan.FromSeconds(15));

                Log.Debug("Setting up device interface service");
                GattService? deviceInterfaceService = Device.Gatt.GetPrimaryServiceAsync(BleGattGuids.DEVICE_INTERFACE_SERVICE).WaitAsync(TimeSpan.FromSeconds(15)).Result;
                if (deviceInterfaceService == null)
                {
                    RaiseSetupError("Device interface service was not found.");
                    return false;
                }

                Log.Debug("Getting writer");
                _mGattWriter = deviceInterfaceService.GetCharacteristicAsync(BleGattGuids.DEVICE_INTERFACE_WRITER).WaitAsync(TimeSpan.FromSeconds(15)).Result;
                if (_mGattWriter == null)
                {
                    RaiseSetupError("Device interface writer characteristic was not found.");
                    return false;
                }

                Log.Debug("Getting reader");
                GattCharacteristic? deviceInterfaceNotifier = deviceInterfaceService.GetCharacteristicAsync(BleGattGuids.DEVICE_INTERFACE_NOTIFIER).WaitAsync(TimeSpan.FromSeconds(15)).Result;
                if (deviceInterfaceNotifier == null)
                {
                    RaiseSetupError("Device interface notifier characteristic was not found.");
                    return false;
                }
                deviceInterfaceNotifier.StartNotificationsAsync().Wait(TimeSpan.FromSeconds(15));
                deviceInterfaceNotifier.CharacteristicValueChanged += (o, E) =>
                {
                    // Read e.Value into a local exactly once. If this were checked and then re-read
                    // (e.g. "if (e.Value != null) ReadBytes(e.Value);"), a property that re-reads from an
                    // underlying native buffer on each access could return null on the second read even
                    // though the first check passed - a real race, not just a defensive precaution.
                    byte[]? value = E.Value;
                    if (value != null)
                    {
                        ReadBytes(value);
                    }
                };

                bool handshakeSuccess = PerformHandShake();
                if (!handshakeSuccess)
                {
                    RaiseSetupError("Handshake did not complete within the timeout.");
                }

                return handshakeSuccess;
            }
            catch (Exception ex)
            {
                Exception effective = UnwrapAggregate(ex);
                RaiseSetupError($"Setup() threw {effective.GetType().Name}: {effective.Message}", effective);
                return false;
            }
        }

        /// <summary>
        /// Unwraps a single-inner-exception AggregateException (the usual shape from a faulted .Result) down to the real cause.
        /// </summary>
        /// <param name="Ex">The exception to unwrap.</param>
        /// <returns>The innermost exception, or <paramref name="Ex"/> itself if it isn't an AggregateException.</returns>
        private static Exception UnwrapAggregate(Exception Ex)
        {
            while (Ex is AggregateException agg && agg.InnerException != null)
            {
                Ex = agg.InnerException;
            }
            return Ex;
        }

        /// <summary>
        /// Logs a setup failure and raises <see cref="SetupError"/> with it.
        /// </summary>
        /// <param name="Message">A human-readable description of what failed.</param>
        /// <param name="Exception">The exception that caused the failure, if any.</param>
        private void RaiseSetupError(string Message, Exception? Exception = null)
        {
            if (Exception != null)
            {
                Log.Error($"Setup error: {Message}", Exception);
            }
            else
            {
                Log.Error($"Setup error: {Message}");
            }

            SetupError?.Invoke(this, new SetupErrorEventArgs { Message = Message, Exception = Exception });
        }

        /// <summary>
        /// Background loop that dequeues raw byte chunks received from the device, reassembles them
        /// into complete COBS-encoded frames, decodes them, and queues the result for processing.
        /// Runs until <see cref="_mCancellationToken"/> is cancelled.
        /// </summary>
        private void ReaderThread()
        {
            List<byte> currentMessage = new List<byte>();

            while (!_mCancellationToken.IsCancellationRequested)
            {
                if (_mReaderQueue.Count > 0)
                {
                    // Queue<BleBuffer> can legally hold a null reference (class type), so keep this
                    // defensive check to avoid null dereference if a null is ever enqueued.
                    BleBuffer? dequeued = _mReaderQueue.Dequeue();
                    if (dequeued == null)
                    {
                        continue;
                    }

                    IEnumerable<byte> msg = dequeued.ToArray();

                    if (!msg.Any())
                    {
                        continue;
                    }

                    byte header = msg.First();
                    msg = msg.Skip(1);

                    if (header == 0 || !_mHandshakeComplete)
                    {
                        ContinueHandShake(msg);
                        continue;
                    }

                    bool readComplete = false;

                    if (msg.Any() && msg.Last() == 0x00)
                    {
                        readComplete = true;
                        msg = msg.SkipLast(1);
                    }
                    if (msg.Any() && msg.First() == 0x00)
                    {
                        currentMessage.Clear();
                        msg = msg.Skip(1);
                    }
                    currentMessage.AddRange(msg);

                    if (readComplete && currentMessage.Count > 0)
                    {
                        //Log.Debug($"  -> {currentMessage.ToHexString().PadRight(44)} (encoded)");
                        byte[] decoded = CobsCodec.Decode(currentMessage.ToArray()).ToArray();
                        //Log.Debug($"-> {decoded.ToHexString().PadRight(46)} (decoded)");

                        if (decoded.Length >= 4)
                        {
                            _mMsgProcessQueue.Enqueue(new BleBuffer(decoded));
                            _mMsgProcessSignal.Set();
                        }
                        else
                        {
                            //Log.Debug($"Discarding decoded frame - too short ({decoded.Length} bytes, need at least 4).");
                        }

                        currentMessage.Clear();
                    }
                }
                else
                {
                    _mReaderSignal.WaitOne(5000);
                }
            }
        }

        /// <summary>
        /// Background loop that dequeues outgoing byte chunks and writes them to the GATT writer
        /// characteristic. Runs until <see cref="_mCancellationToken"/> is cancelled.
        /// </summary>
        private void WriterThread()
        {
            while (!_mCancellationToken.IsCancellationRequested)
            {
                if (_mWriterQueue.Count > 0)
                {
                    BleBuffer bytes = _mWriterQueue.Dequeue();
                    _mGattWriter?.WriteValueWithResponseAsync(bytes.ToArray());
                }
                else
                {
                    _mWriterSignal.WaitOne(5000);
                }
            }
        }

        /// <summary>
        /// Background loop that dequeues complete decoded frames and dispatches them to
        /// <see cref="ProcessMessage"/>. Runs until <see cref="_mCancellationToken"/> is cancelled.
        /// </summary>
        private void MsgProcessingThread()
        {
            while (!_mCancellationToken.IsCancellationRequested)
            {
                if (_mMsgProcessQueue.Count > 0)
                {
                    BleBuffer frame = _mMsgProcessQueue.Dequeue();
                    ProcessMessage(frame.ToArray());
                }
                else
                {
                    _mMsgProcessSignal.WaitOne(5000);
                }
            }
        }

        /// <summary>
        /// Sends the initial handshake bytes and waits for the device to respond and acknowledge.
        /// </summary>
        /// <returns>True if the handshake completed within the timeout; false otherwise.</returns>
        public bool PerformHandShake()
        {
            Log.Debug("Starting handshake");
            _mHandshakeComplete = false;
            _mHandshakeCompleteResetEvent.Reset();
            _mHeader = 0x00;
            SendBytes("000000000000000000010000");
            return _mHandshakeCompleteResetEvent.Wait(TimeSpan.FromSeconds(10));
        }

        /// <summary>
        /// Processes a raw byte chunk received before the handshake has completed, checking whether
        /// it matches the expected handshake response and, if so, completing the handshake.
        /// </summary>
        /// <param name="Msg">The raw bytes received, with the leading header byte already stripped.</param>
        private void ContinueHandShake(IEnumerable<byte> Msg)
        {
            byte[] msgArray = Msg.ToArray();
            string msgHex = msgArray.ToHexString();

            if (msgHex.StartsWith("010000000000000000010000") && msgArray.Length > 12)
            {
                _mHeader = msgArray[12];
                SendBytes("00");
                _mHandshakeComplete = true;
                _mHandshakeCompleteResetEvent.Set();
                return;
            }
        }

        /// <summary>
        /// Processes a complete decoded frame: validates its CRC, dispatches it based on its type
        /// prefix (device info, config, protobuf response, or protobuf request), and acknowledges it.
        /// </summary>
        /// <param name="Frame">The complete decoded frame, including its length and CRC fields.</param>
        private void ProcessMessage(byte[] Frame)
        {
            // A valid frame needs at least: 2 length bytes + 2 CRC bytes = 4 bytes minimum, and
            // ReaderThread already filters out anything shorter than this before enqueueing - this
            // check stays as a defensive second layer in case that ever changes.
            if (Frame.Length < 4)
            {
                Log.Debug($"ProcessMessage: discarding frame too short to contain a valid CRC ({Frame.Length} bytes).");
                return;
            }

            if (BitConverter.ToUInt16(Frame.SkipLast(2).Checksum()) != BitConverter.ToUInt16(Frame.TakeLast(2).ToArray()))
            {
                Log.Warn("CRC ERROR");
            }

            byte[] msg = Frame.Skip(2).SkipLast(2).ToArray();

            // The B413/B313 branches below index into msg[2..4] and msg.Skip(16) - guard against a
            // frame too short to contain those fields rather than letting Skip/Range throw or silently
            // read garbage.
            if (msg.Length < 4)
            {
                Log.Debug($"ProcessMessage: message body too short to contain a type header ({msg.Length} bytes). Acknowledging and discarding.");
                AcknowledgeMessage(msg, new List<byte>() { 0x00 });
                return;
            }

            string hex = msg.ToHexString();

            List<byte> ackBody = new List<byte>() { 0x00 };

            if (hex.StartsWith("A013"))
            {
                // device info
            }
            else if (hex.StartsWith("BA13"))
            {
                // config
            }
            else if (hex.StartsWith("B413")) // all protobuf responses
            {
                ushort counter = BitConverter.ToUInt16(msg[2..4]);
                ackBody.AddRange(msg[2..4]);
                ackBody.AddRange("00000000000000".ToByteArray());

                if (counter == _mProtoRequestCounter)
                {
                    if (msg.Length > 16)
                    {
                        try
                        {
                            _mLastProtoReceived = WrapperProto.Parser.ParseFrom(msg.Skip(16).ToArray());
                            MessageReceived?.Invoke(this, new MessageEventArgs() { Message = _mLastProtoReceived });
                            _mProtoResponseResetEvent.Set();
                        }
                        catch (Exception ex)
                        {
                            Log.Warn("Failed to parse protobuf response", ex);
                        }
                    }
                    else
                    {
                        Log.Debug($"B413 response frame too short to contain a protobuf payload ({msg.Length} bytes).");
                    }
                }
            }
            else if (hex.StartsWith("B313")) // all protobuf requests
            {
                ackBody.AddRange(msg[2..4]);
                ackBody.AddRange("00000000000000".ToByteArray());

                if (msg.Length > 16)
                {
                    byte[] payload = msg.Skip(16).ToArray();
                    Task.Run(() =>
                    {
                        try
                        {
                            var request = WrapperProto.Parser.ParseFrom(payload);
                            MessageReceived?.Invoke(this, new MessageEventArgs() { Message = request });
                            HandleProtobufRequest(request);
                        }
                        catch (Exception ex)
                        {
                            Log.Warn("Failed to parse or handle protobuf request", ex);
                        }
                    });
                }
                else
                {
                    Log.Debug($"B313 request frame too short to contain a protobuf payload ({msg.Length} bytes).");
                }
            }

            AcknowledgeMessage(msg, ackBody);
        }

        /// <summary>
        /// Handles a protobuf request/notification received from the device. Implemented by
        /// subclasses, since the shape of these messages is device-type-specific.
        /// </summary>
        /// <param name="Request">The parsed protobuf message.</param>
        public abstract void HandleProtobufRequest(IMessage Request);

        /// <summary>
        /// Sends an acknowledgement frame for a received message.
        /// </summary>
        /// <param name="Msg">The message being acknowledged, used only for its first two bytes (the counter).</param>
        /// <param name="RespBody">The acknowledgement payload to send.</param>
        private void AcknowledgeMessage(IEnumerable<byte> Msg, IEnumerable<byte> RespBody)
        {
            WriteMessage("8813".ToByteArray().Concat(Msg.Take(2)).Concat(RespBody).ToArray());
        }

        /// <summary>
        /// Sends a protobuf request and waits for a matching response. Returns null (rather than
        /// throwing) if the write fails, the device doesn't respond within the timeout, or the
        /// connection isn't currently established - callers should treat a null return as "this
        /// value isn't available right now" rather than a hard failure.
        /// </summary>
        /// <param name="Proto">The protobuf request message to send.</param>
        /// <returns>The matching response message, or null if none was received within the timeout.</returns>
        public IMessage? SendProtobufRequest(IMessage Proto)
        {
            if (_mGattWriter == null)
            {
                Log.Warn("SendProtobufRequest: no GATT writer available (not connected/set up yet).");
                return null;
            }

            _mProtoResponseResetEvent.Reset();

            byte[] bytes = Proto.ToByteArray();
            int l = bytes.Length;
            byte[] fullMsg = "B313".ToByteArray()
              .Concat(BitConverter.GetBytes(_mProtoRequestCounter))
              .Append<byte>(0x00)
              .Append<byte>(0x00)
              .Concat(BitConverter.GetBytes(l))
              .Concat(BitConverter.GetBytes(l))
              .Concat(bytes)
              .ToArray();

            WriteMessage(fullMsg);
            MessageSent?.Invoke(this, new MessageEventArgs() { Message = Proto });
            if (_mProtoResponseResetEvent.Wait(TimeSpan.FromSeconds(15)))
            {
                _mProtoRequestCounter++;
                return _mLastProtoReceived;
            }
            else
            {
                Log.Warn($"Failed to get response for proto {_mProtoRequestCounter} ({Proto.GetType().Name}).");
                return null;
            }
        }

        /// <summary>
        /// Queues a chunk of raw bytes received over BLE for processing by <see cref="ReaderThread"/>.
        /// </summary>
        /// <param name="Bytes">The raw bytes received.</param>
        private void ReadBytes(byte[] Bytes)
        {
            //Log.Debug($"      -> {bytes.ToHexString().PadRight(40)} (ble read)");
            ReadBytes(new BleBuffer(Bytes));
        }

        /// <summary>
        /// Queues a chunk of raw bytes received over BLE for processing by <see cref="ReaderThread"/>.
        /// </summary>
        /// <param name="Bytes">The raw transport buffer received.</param>
        private void ReadBytes(BleBuffer Bytes)
        {
            _mReaderQueue.Enqueue(Bytes);
            _mReaderSignal.Set();
        }

        /// <summary>
        /// Prepends the current session header byte and queues a chunk of raw bytes to be written over BLE.
        /// </summary>
        /// <param name="Bytes">The raw bytes to send.</param>
        private void SendBytes(IEnumerable<byte> Bytes)
        {
            //Log.Debug($"      <- {bytes.ToHexString().PadRight(40)} (ble write)");
            _mWriterQueue.Enqueue(new BleBuffer(Bytes.Prepend(_mHeader).ToArray()));
            _mWriterSignal.Set();
        }

        /// <summary>
        /// Converts a hex string to bytes and queues it to be written over BLE. See <see cref="SendBytes(IEnumerable{byte})"/>.
        /// </summary>
        /// <param name="HexBytes">The bytes to send, as a hex string.</param>
        public void SendBytes(string HexBytes) => SendBytes(HexBytes.ToByteArray());

        /// <summary>
        /// Frames a message from a transport buffer (length + CRC), COBS-encodes it, splits it into
        /// BLE-write-sized chunks, and queues each chunk to be sent.
        /// </summary>
        /// <param name="Buffer">The raw message buffer to send, before framing.</param>
        public void WriteMessage(BleBuffer Buffer) => WriteMessage(Buffer.ToArray());

        /// <summary>
        /// Frames a message (length + CRC), COBS-encodes it, splits it into BLE-write-sized chunks,
        /// and queues each chunk to be sent.
        /// </summary>
        /// <param name="Bytes">The raw message bytes to send, before framing.</param>
        public void WriteMessage(byte[] Bytes)
        {
            //Log.Debug($"<- {bytes.ToHexString().PadRight(46)} (raw)");

            // Length of message + 2 bytes for length field + 2 bytes for crc field
            ushort length = (ushort)(2 + Bytes.Length + 2);
            IEnumerable<byte> bytesWithLength = BitConverter.GetBytes(length).Concat(Bytes);
            IEnumerable<byte> fullFrame = bytesWithLength.Concat(bytesWithLength.Checksum());

            //Log.Debug($"  <- {fullFrame.ToHexString().PadRight(44)} (framed)");

            List<byte> encoded = CobsCodec.Encode(fullFrame).Prepend<byte>(0x00).Append<byte>(0x00).ToList();
            //Log.Debug($"    <- {encoded.ToArray().ToHexString().PadRight(42)} (encoded)");

            while (encoded.Count > 19)
            {
                SendBytes(encoded.Take(19));
                encoded = encoded.Skip(19).ToList();
            }
            if (encoded.Count > 0)
            {
                SendBytes(encoded);
            }
        }

        /// <summary>
        /// Cancels the background threads, unsubscribes all event handlers, and disconnects the GATT
        /// session.
        /// </summary>
        /// <param name="Disposing">True if called from <see cref="Dispose()"/>; false if called from a finalizer.</param>
        protected virtual void Dispose(bool Disposing)
        {
            if (!_mDisposedValue)
            {
                if (Disposing)
                {
                    _mCancellationToken.Cancel();
                    _mWriterTask.Wait();
                    _mReaderTask.Wait();
                    _mMsgProcessingTask.Wait();

                    foreach (var d in MessageSent?.GetInvocationList() ?? Array.Empty<Delegate>())
                    {
                        MessageSent -= (d as MessageEventHandler);
                    }

                    foreach (var d in MessageReceived?.GetInvocationList() ?? Array.Empty<Delegate>())
                    {
                        MessageReceived -= (d as MessageEventHandler);
                    }

                    foreach (var d in BatteryLifeUpdated?.GetInvocationList() ?? Array.Empty<Delegate>())
                    {
                        BatteryLifeUpdated -= (d as BatteryEventHandler);
                    }

                    foreach (var d in SetupError?.GetInvocationList() ?? Array.Empty<Delegate>())
                    {
                        SetupError -= (d as SetupErrorEventHandler);
                    }

                    Device?.Gatt?.Disconnect();
                }

                _mDisposedValue = true;
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
