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
  /// cycle. Subclasses add device-specific GATT services on top of this.
  /// </summary>
  public abstract class BleBaseDevice : IDisposable
  {
    /// <summary>
    /// The log4net logger for this class.
    /// </summary>
    private static readonly log4net.ILog logger = log4net.LogManager.GetLogger(typeof(BleBaseDevice));

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
    /// Raised whenever a protobuf message is received from the device (responses and device-initiated requests).
    /// </summary>
    public event MessageEventHandler? MessageReceived;

    /// <summary>
    /// Raised whenever a protobuf message is sent to the device.
    /// </summary>
    public event MessageEventHandler? MessageSent;

    public delegate void MessageEventHandler(object sender, MessageEventArgs E);

    public class MessageEventArgs : EventArgs
    {
      public IMessage? Message { get; set; }
    }

    public event BatteryEventHandler? BatteryLifeUpdated;

    public delegate void BatteryEventHandler(object sender, BatteryEventArgs E);

    public class BatteryEventArgs : EventArgs
    {
      public int Battery { get; set; }
    }

    /// <summary>
    /// Raised when <see cref="Setup"/> encounters a problem it can't recover from.
    /// </summary>
    public event SetupErrorEventHandler? SetupError;

    public delegate void SetupErrorEventHandler(object sender, SetupErrorEventArgs E);

    public class SetupErrorEventArgs : EventArgs
    {
      public string Message { get; set; } = string.Empty;
      public Exception? Exception { get; set; }
    }

    private readonly EventWaitHandle _mWriterSignal = new AutoResetEvent(false);
    private readonly Queue<BleBuffer> _mWriterQueue = new();

    private readonly EventWaitHandle _mReaderSignal = new AutoResetEvent(false);
    private readonly Queue<BleBuffer> _mReaderQueue = new();

    private readonly EventWaitHandle _mMsgProcessSignal = new AutoResetEvent(false);
    private readonly Queue<BleBuffer> _mMsgProcessQueue = new();

    private readonly ManualResetEventSlim _mHandshakeCompleteResetEvent = new(false);
    private readonly ManualResetEventSlim _mProtoResponseResetEvent = new(false);

    private IMessage? _mLastProtoReceived;
    private int _mBattery;
    private bool _mHandshakeComplete;
    private byte _mHeader = 0x00;
    private int _mProtoRequestCounter;

    private readonly CancellationTokenSource _mCancellationToken;
    private readonly Task _mWriterTask;
    private readonly Task _mReaderTask;
    private readonly Task _mMsgProcessingTask;

    private GattCharacteristic? _mGattWriter;
    private bool _mDisposedValue;

    /// <summary>
    /// Initializes the transport for a given Bluetooth device and starts writer/reader/processor tasks.
    /// </summary>
    public BleBaseDevice(BluetoothDevice Device)
    {
      this.Device = Device;
      _mCancellationToken = new CancellationTokenSource();
      _mWriterTask = Task.Run(WriterThread, _mCancellationToken.Token);
      _mReaderTask = Task.Run(ReaderThread, _mCancellationToken.Token);
      _mMsgProcessingTask = Task.Run(MsgProcessingThread, _mCancellationToken.Token);
    }

    /// <summary>
    /// Performs device-info reads and GATT setup shared by every device type.
    /// </summary>
    public virtual bool Setup()
    {
      try
      {
        logger.Debug("Getting device info service");
        GattService? deviceInfoService = Device.Gatt
            .GetPrimaryServiceAsync(BleGattGuids.DEVICE_INFO_SERVICE_UUID)
            .WaitAsync(TimeSpan.FromSeconds(15)).Result;

        if (deviceInfoService == null)
        {
          RaiseSetupError("Device info service (0000180a) was not found on the device.");
          return false;
        }

        logger.Debug("Reading serial number");
        GattCharacteristic? serialCharacteristic = deviceInfoService
            .GetCharacteristicAsync(BleGattGuids.SERIAL_NUMBER_CHARACTERISTIC_UUID)
            .WaitAsync(TimeSpan.FromSeconds(15)).Result;

        if (serialCharacteristic == null)
        {
          RaiseSetupError("Serial number characteristic was not found.");
          return false;
        }

        byte[]? serialBytes = serialCharacteristic.ReadValueAsync().WaitAsync(TimeSpan.FromSeconds(15)).Result;
        Serial = serialBytes != null ? Encoding.ASCII.GetString(serialBytes) : null;

        logger.Debug("Reading firmware version");
        GattCharacteristic? firmwareCharacteristic = deviceInfoService
            .GetCharacteristicAsync(BleGattGuids.FIRMWARE_CHARACTERISTIC_UUID)
            .WaitAsync(TimeSpan.FromSeconds(15)).Result;

        if (firmwareCharacteristic == null)
        {
          RaiseSetupError("Firmware characteristic was not found.");
          return false;
        }

        byte[]? firmwareBytes = firmwareCharacteristic.ReadValueAsync().WaitAsync(TimeSpan.FromSeconds(15)).Result;
        Firmware = firmwareBytes != null ? Encoding.ASCII.GetString(firmwareBytes) : null;

        logger.Debug("Reading model name");
        GattCharacteristic? modelCharacteristic = deviceInfoService
            .GetCharacteristicAsync(BleGattGuids.MODEL_CHARACTERISTIC_UUID)
            .WaitAsync(TimeSpan.FromSeconds(15)).Result;

        if (modelCharacteristic == null)
        {
          RaiseSetupError("Model characteristic was not found.");
          return false;
        }

        byte[]? modelBytes = modelCharacteristic.ReadValueAsync().WaitAsync(TimeSpan.FromSeconds(15)).Result;
        Model = modelBytes != null ? Encoding.ASCII.GetString(modelBytes) : null;

        logger.Debug("Reading battery life");
        GattService? batteryService = Device.Gatt
            .GetPrimaryServiceAsync(BleGattGuids.BATTERY_SERVICE_UUID)
            .WaitAsync(TimeSpan.FromSeconds(15)).Result;

        if (batteryService == null)
        {
          RaiseSetupError("Battery service was not found.");
          return false;
        }

        GattCharacteristic? batteryCharacteristic = batteryService
            .GetCharacteristicAsync(BleGattGuids.BATTERY_CHARACTERISTIC_UUID)
            .WaitAsync(TimeSpan.FromSeconds(15)).Result;

        if (batteryCharacteristic == null)
        {
          RaiseSetupError("Battery characteristic was not found.");
          return false;
        }

        batteryCharacteristic.CharacteristicValueChanged += (o, E) =>
        {
          byte[]? value = E.Value;
          if (value != null && value.Length > 0)
          {
            Battery = value[0];
          }
        };
        batteryCharacteristic.StartNotificationsAsync().Wait(TimeSpan.FromSeconds(15));

        logger.Debug("Setting up device interface service");
        GattService? deviceInterfaceService = Device.Gatt
            .GetPrimaryServiceAsync(BleGattGuids.DEVICE_INTERFACE_SERVICE)
            .WaitAsync(TimeSpan.FromSeconds(15)).Result;

        if (deviceInterfaceService == null)
        {
          RaiseSetupError("Device interface service was not found.");
          return false;
        }

        logger.Debug("Getting writer");
        _mGattWriter = deviceInterfaceService
            .GetCharacteristicAsync(BleGattGuids.DEVICE_INTERFACE_WRITER)
            .WaitAsync(TimeSpan.FromSeconds(15)).Result;

        if (_mGattWriter == null)
        {
          RaiseSetupError("Device interface writer characteristic was not found.");
          return false;
        }

        logger.Debug("Getting reader");
        GattCharacteristic? deviceInterfaceNotifier = deviceInterfaceService
            .GetCharacteristicAsync(BleGattGuids.DEVICE_INTERFACE_NOTIFIER)
            .WaitAsync(TimeSpan.FromSeconds(15)).Result;

        if (deviceInterfaceNotifier == null)
        {
          RaiseSetupError("Device interface notifier characteristic was not found.");
          return false;
        }

        deviceInterfaceNotifier.StartNotificationsAsync().Wait(TimeSpan.FromSeconds(15));
        deviceInterfaceNotifier.CharacteristicValueChanged += (o, E) =>
        {
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

    private static Exception UnwrapAggregate(Exception Ex)
    {
      while (Ex is AggregateException agg && agg.InnerException != null)
      {
        Ex = agg.InnerException;
      }
      return Ex;
    }

    private void RaiseSetupError(string Message, Exception? Exception = null)
    {
      if (Exception != null)
      {
        logger.Error($"Setup error: {Message}", Exception);
      }
      else
      {
        logger.Error($"Setup error: {Message}");
      }

      SetupError?.Invoke(this, new SetupErrorEventArgs { Message = Message, Exception = Exception });
    }

    private void ReaderThread()
    {
      List<byte> currentMessage = new();

      while (!_mCancellationToken.IsCancellationRequested)
      {
        if (_mReaderQueue.Count > 0)
        {
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
            byte[] decoded = CobsCodec.Decode(currentMessage.ToArray()).ToArray();

            if (decoded.Length >= 4)
            {
              _mMsgProcessQueue.Enqueue(new BleBuffer(decoded));
              _mMsgProcessSignal.Set();
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

    public bool PerformHandShake()
    {
      logger.Debug("Starting handshake");
      _mHandshakeComplete = false;
      _mHandshakeCompleteResetEvent.Reset();
      _mHeader = 0x00;
      SendBytes("000000000000000000010000");
      return _mHandshakeCompleteResetEvent.Wait(TimeSpan.FromSeconds(10));
    }

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
      }
    }

    private void ProcessMessage(byte[] Frame)
    {
      if (Frame.Length < 4)
      {
        logger.Debug($"ProcessMessage: discarding frame too short ({Frame.Length} bytes).");
        return;
      }

      if (BitConverter.ToUInt16(Frame.SkipLast(2).Checksum()) != BitConverter.ToUInt16(Frame.TakeLast(2).ToArray()))
      {
        logger.Warn("CRC ERROR");
      }

      byte[] msg = Frame.Skip(2).SkipLast(2).ToArray();

      if (msg.Length < 4)
      {
        logger.Debug($"ProcessMessage: message body too short ({msg.Length} bytes).");
        AcknowledgeMessage(msg, new List<byte> { 0x00 });
        return;
      }

      string hex = msg.ToHexString();
      List<byte> ackBody = new() { 0x00 };

      if (hex.StartsWith("A013"))
      {
        // device info
      }
      else if (hex.StartsWith("BA13"))
      {
        // config
      }
      else if (hex.StartsWith("B413")) // protobuf responses
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
              MessageReceived?.Invoke(this, new MessageEventArgs { Message = _mLastProtoReceived });
              _mProtoResponseResetEvent.Set();
            }
            catch (Exception ex)
            {
              logger.Warn("Failed to parse protobuf response", ex);
            }
          }
          else
          {
            logger.Debug($"B413 response frame too short to contain a protobuf payload ({msg.Length} bytes).");
          }
        }
      }
      else if (hex.StartsWith("B313")) // protobuf requests
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
              MessageReceived?.Invoke(this, new MessageEventArgs { Message = request });
              HandleProtobufRequest(request);
            }
            catch (Exception ex)
            {
              logger.Warn("Failed to parse or handle protobuf request", ex);
            }
          });
        }
        else
        {
          logger.Debug($"B313 request frame too short to contain a protobuf payload ({msg.Length} bytes).");
        }
      }

      AcknowledgeMessage(msg, ackBody);
    }

    public abstract void HandleProtobufRequest(IMessage Request);

    private void AcknowledgeMessage(IEnumerable<byte> Msg, IEnumerable<byte> RespBody)
    {
      WriteMessage("8813".ToByteArray().Concat(Msg.Take(2)).Concat(RespBody).ToArray());
    }

    public IMessage? SendProtobufRequest(IMessage Proto)
    {
      if (_mGattWriter == null)
      {
        logger.Warn("SendProtobufRequest: no GATT writer available (not connected/set up yet).");
        return null;
      }

      _mProtoResponseResetEvent.Reset();

      byte[] bytes = Proto.ToByteArray();
      int l = bytes.Length;
      byte[] fullMsg = "B313".ToByteArray()
          .Concat(BitConverter.GetBytes(_mProtoRequestCounter))
          .Append((byte)0x00)
          .Append((byte)0x00)
          .Concat(BitConverter.GetBytes(l))
          .Concat(BitConverter.GetBytes(l))
          .Concat(bytes)
          .ToArray();

      WriteMessage(fullMsg);
      MessageSent?.Invoke(this, new MessageEventArgs { Message = Proto });

      if (_mProtoResponseResetEvent.Wait(TimeSpan.FromSeconds(15)))
      {
        _mProtoRequestCounter++;
        return _mLastProtoReceived;
      }

      logger.Warn($"Failed to get response for proto {_mProtoRequestCounter} ({Proto.GetType().Name}).");
      return null;
    }

    private void ReadBytes(byte[] Bytes) => ReadBytes(new BleBuffer(Bytes));

    private void ReadBytes(BleBuffer Bytes)
    {
      _mReaderQueue.Enqueue(Bytes);
      _mReaderSignal.Set();
    }

    private void SendBytes(IEnumerable<byte> Bytes)
    {
      _mWriterQueue.Enqueue(new BleBuffer(Bytes.Prepend(_mHeader).ToArray()));
      _mWriterSignal.Set();
    }

    public void SendBytes(string HexBytes) => SendBytes(HexBytes.ToByteArray());

    public void WriteMessage(BleBuffer Buffer) => WriteMessage(Buffer.ToArray());

    public void WriteMessage(byte[] Bytes)
    {
      ushort length = (ushort)(2 + Bytes.Length + 2);
      IEnumerable<byte> bytesWithLength = BitConverter.GetBytes(length).Concat(Bytes);
      IEnumerable<byte> fullFrame = bytesWithLength.Concat(bytesWithLength.Checksum());

      List<byte> encoded = CobsCodec.Encode(fullFrame).Prepend((byte)0x00).Append((byte)0x00).ToList();

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

    protected virtual void Dispose(bool Disposing)
    {
      if (_mDisposedValue)
      {
        return;
      }

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

    public void Dispose()
    {
      Dispose(Disposing: true);
      GC.SuppressFinalize(this);
    }
  }
}
