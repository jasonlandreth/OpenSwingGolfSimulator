/*
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
 */

using Google.Protobuf;
using InTheHand.Bluetooth;
using LaunchMonitor.Proto;
using OpenSwingGolfSimulator.Hardware.Utilities;
using OpenSwingGolfSimulator.Utilities.ExtensionMethods;
using System.Collections.Concurrent;
using System.Text; 

namespace OpenSwingGolfSimulator.Hardware.Ble
{

  /// <summary>
  /// Provides BLE transport behavior for launch-monitor style devices:
  /// setup, handshake, framing, CRC validation, and protobuf request/response handling.
  /// </summary>
  public abstract class BleDeviceTransport : IDisposable
  {
    /// <summary>
    /// Creates a new instance of the <see cref="BleDeviceTransport"/> class.
    /// </summary>
    /// <param name="device">
    /// Connected BLE device.
    /// </param>
    protected BleDeviceTransport(BluetoothDevice device)
    {
      Device = device;

      _cts = new CancellationTokenSource();
      _writerTask = Task.Run(WriterLoop, _cts.Token);
      _readerTask = Task.Run(ReaderLoop, _cts.Token);
      _processorTask = Task.Run(ProcessLoop, _cts.Token);
    }

    /// <summary>
    /// Gets the connected BLE device.
    /// </summary>
    public BluetoothDevice Device { get; }

    /// <summary>
    /// Gets the latest battery percentage.
    /// </summary>
    public int Battery { get; private set; }

    /// <summary>
    /// Gets the model value reported by the device.
    /// </summary>
    public string? Model { get; private set; }

    /// <summary>
    /// Gets the firmware value reported by the device.
    /// </summary>
    public string? Firmware { get; private set; }

    /// <summary>
    /// Gets the serial value reported by the device.
    /// </summary>
    public string? Serial { get; private set; }

    /// <summary>
    /// Raised when a protobuf message is received.
    /// </summary>
    public event EventHandler<MessageEventArgs>? MessageReceived;

    /// <summary>
    /// Raised when a protobuf message is sent.
    /// </summary>
    public event EventHandler<MessageEventArgs>? MessageSent;

    /// <summary>
    /// Raised when battery changes.
    /// </summary>
    public event EventHandler<BatteryEventArgs>? BatteryChanged;

    /// <summary>
    /// Raised when setup fails.
    /// </summary>
    public event EventHandler<SetupErrorEventArgs>? SetupError;

    /// <summary>
    /// Performs common setup logic:
    /// read device info, subscribe battery, subscribe transport notifier, and handshake.
    /// </summary>
    /// <returns>
    /// <c>true</c> when setup succeeds; otherwise <c>false</c>.
    /// </returns>
    public virtual bool Setup()
    {
      try
      {
        GattService? deviceInfo = Device.Gatt
            .GetPrimaryServiceAsync(BleGattGuids.DEVICE_INFO_SERVICE_UUID)
            .WaitAsync(TimeSpan.FromSeconds(15)).Result;

        if (deviceInfo == null)
        {
          RaiseSetupError("Device info service not found.");
          return false;
        }

        Serial = ReadAsciiCharacteristic(deviceInfo, BleGattGuids.SERIAL_NUMBER_CHARACTERISTIC_UUID);
        Firmware = ReadAsciiCharacteristic(deviceInfo, BleGattGuids.FIRMWARE_CHARACTERISTIC_UUID);
        Model = ReadAsciiCharacteristic(deviceInfo, BleGattGuids.MODEL_CHARACTERISTIC_UUID);

        GattService? batteryService = Device.Gatt
            .GetPrimaryServiceAsync(BleGattGuids.BATTERY_SERVICE_UUID)
            .WaitAsync(TimeSpan.FromSeconds(15)).Result;

        if (batteryService == null)
        {
          RaiseSetupError("Battery service not found.");
          return false;
        }

        GattCharacteristic? battery = batteryService
            .GetCharacteristicAsync(BleGattGuids.BATTERY_CHARACTERISTIC_UUID)
            .WaitAsync(TimeSpan.FromSeconds(15)).Result;

        if (battery == null)
        {
          RaiseSetupError("Battery characteristic not found.");
          return false;
        }

        battery.CharacteristicValueChanged += (_, e) =>
        {
          byte[]? value = e.Value;
          if (value != null && value.Length > 0)
          {
            Battery = value[0];
            BatteryChanged?.Invoke(this, new BatteryEventArgs(Battery));
          }
        };

        battery.StartNotificationsAsync().Wait(TimeSpan.FromSeconds(15));

        GattService? transportService = Device.Gatt
            .GetPrimaryServiceAsync(BleGattGuids.DEVICE_INTERFACE_SERVICE)
            .WaitAsync(TimeSpan.FromSeconds(15)).Result;

        if (transportService == null)
        {
          RaiseSetupError("Transport service not found.");
          return false;
        }

        _writerCharacteristic = transportService
            .GetCharacteristicAsync(BleGattGuids.DEVICE_INTERFACE_WRITER)
            .WaitAsync(TimeSpan.FromSeconds(15)).Result;

        if (_writerCharacteristic == null)
        {
          RaiseSetupError("Transport writer characteristic not found.");
          return false;
        }

        GattCharacteristic? notifier = transportService
            .GetCharacteristicAsync(BleGattGuids.DEVICE_INTERFACE_NOTIFIER)
            .WaitAsync(TimeSpan.FromSeconds(15)).Result;

        if (notifier == null)
        {
          RaiseSetupError("Transport notifier characteristic not found.");
          return false;
        }

        notifier.CharacteristicValueChanged += (_, e) =>
        {
          byte[]? value = e.Value;
          if (value != null)
          {
            EnqueueIncoming(new BleBuffer(value));
          }
        };

        notifier.StartNotificationsAsync().Wait(TimeSpan.FromSeconds(15));

        if (!PerformHandshake())
        {
          RaiseSetupError("Handshake timeout.");
          return false;
        }

        return true;
      }
      catch (Exception ex)
      {
        RaiseSetupError($"Setup failed: {UnwrapAggregate(ex).Message}", UnwrapAggregate(ex));
        return false;
      }
    }

    /// <summary>
    /// Sends a protobuf request and waits for its response.
    /// </summary>
    /// <param name="message">
    /// Protobuf request message.
    /// </param>
    /// <returns>
    /// Matching response or <c>null</c> on timeout/failure.
    /// </returns>
    public IMessage? SendProtobufRequest(IMessage message)
    {
      if (_writerCharacteristic == null)
      {
        return null;
      }

      _responseGate.Reset();

      byte[] payload = message.ToByteArray();
      int len = payload.Length;

      byte[] wire = "B313".ToByteArray()
          .Concat(BitConverter.GetBytes(_requestCounter))
          .Append((byte)0x00)
          .Append((byte)0x00)
          .Concat(BitConverter.GetBytes(len))
          .Concat(BitConverter.GetBytes(len))
          .Concat(payload)
          .ToArray();

      WriteMessage(wire);
      MessageSent?.Invoke(this, new MessageEventArgs(message));

      if (_responseGate.Wait(TimeSpan.FromSeconds(15)))
      {
        _requestCounter++;
        return _lastResponse;
      }

      return null;
    }

    /// <summary>
    /// Sends a raw message (unframed payload).
    /// </summary>
    /// <param name="bytes">
    /// Message bytes.
    /// </param>
    public void WriteMessage(byte[] bytes)
    {
      ushort frameLength = (ushort)(2 + bytes.Length + 2);
      IEnumerable<byte> withLength = BitConverter.GetBytes(frameLength).Concat(bytes);
      IEnumerable<byte> frame = withLength.Concat(withLength.Checksum());
      List<byte> encoded = CobsCodec.Encode(frame).Prepend((byte)0x00).Append((byte)0x00).ToList();

      while (encoded.Count > 19)
      {
        EnqueueOutgoing(new BleBuffer(encoded.Take(19).ToArray()));
        encoded = encoded.Skip(19).ToList();
      }

      if (encoded.Count > 0)
      {
        EnqueueOutgoing(new BleBuffer(encoded.ToArray()));
      }
    }

    /// <summary>
    /// Sends bytes represented as hex.
    /// </summary>
    /// <param name="hex">
    /// Hex string.
    /// </param>
    public void SendBytes(string hex)
    {
      SendBytes(hex.ToByteArray());
    }

    /// <summary>
    /// Releases transport resources.
    /// </summary>
    public void Dispose()
    {
      if (_disposed)
      {
        return;
      }

      _disposed = true;

      _cts.Cancel();

      try
      {
        Task.WaitAll(new[] { _writerTask, _readerTask, _processorTask }, TimeSpan.FromSeconds(5));
      }
      catch
      {
        // ignore during shutdown
      }

      _cts.Dispose();
      _writerSignal.Dispose();
      _readerSignal.Dispose();
      _processSignal.Dispose();
      _handshakeGate.Dispose();
      _responseGate.Dispose();

      Device.Gatt?.Disconnect();
      GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Handles device-specific protobuf requests/notifications.
    /// </summary>
    /// <param name="request">
    /// Parsed protobuf message.
    /// </param>
    protected abstract void HandleProtobufRequest(IMessage request);

    /// <summary>
    /// Event data containing a protobuf message.
    /// </summary>
    public sealed class MessageEventArgs : EventArgs
    {
      /// <summary>
      /// Creates new <see cref="MessageEventArgs"/>.
      /// </summary>
      /// <param name="message">
      /// Message instance.
      /// </param>
      public MessageEventArgs(IMessage? message)
      {
        Message = message;
      }

      /// <summary>
      /// Gets the protobuf message.
      /// </summary>
      public IMessage? Message { get; }
    }

    /// <summary>
    /// Event data containing battery value.
    /// </summary>
    public sealed class BatteryEventArgs : EventArgs
    {
      /// <summary>
      /// Creates new <see cref="BatteryEventArgs"/>.
      /// </summary>
      /// <param name="battery">
      /// Battery percentage.
      /// </param>
      public BatteryEventArgs(int battery)
      {
        Battery = battery;
      }

      /// <summary>
      /// Gets battery percentage.
      /// </summary>
      public int Battery { get; }
    }

    /// <summary>
    /// Event data for setup errors.
    /// </summary>
    public sealed class SetupErrorEventArgs : EventArgs
    {
      /// <summary>
      /// Creates new <see cref="SetupErrorEventArgs"/>.
      /// </summary>
      /// <param name="message">
      /// Error message.
      /// </param>
      /// <param name="exception">
      /// Optional exception.
      /// </param>
      public SetupErrorEventArgs(string message, Exception? exception)
      {
        Message = message;
        Exception = exception;
      }

      /// <summary>
      /// Gets the error message.
      /// </summary>
      public string Message { get; }

      /// <summary>
      /// Gets the optional exception.
      /// </summary>
      public Exception? Exception { get; }
    }

    private static readonly log4net.ILog Log = log4net.LogManager.GetLogger(typeof(BleDeviceTransport));

    private readonly ConcurrentQueue<BleBuffer> _writerQueue = new();
    private readonly ConcurrentQueue<BleBuffer> _readerQueue = new();
    private readonly ConcurrentQueue<BleBuffer> _processQueue = new();

    private readonly AutoResetEvent _writerSignal = new(false);
    private readonly AutoResetEvent _readerSignal = new(false);
    private readonly AutoResetEvent _processSignal = new(false);

    private readonly ManualResetEventSlim _handshakeGate = new(false);
    private readonly ManualResetEventSlim _responseGate = new(false);

    private readonly CancellationTokenSource _cts;
    private readonly Task _writerTask;
    private readonly Task _readerTask;
    private readonly Task _processorTask;

    private readonly List<byte> _encodedAccumulator = new();

    private GattCharacteristic? _writerCharacteristic;

    private int _requestCounter;
    private IMessage? _lastResponse;
    private bool _handshakeComplete;
    private byte _sessionHeader;
    private bool _disposed;

    /// <summary>
    /// Reads an ASCII string from a characteristic.
    /// </summary>
    private static string? ReadAsciiCharacteristic(GattService service, Guid characteristicId)
    {
      GattCharacteristic? characteristic = service
          .GetCharacteristicAsync(characteristicId)
          .WaitAsync(TimeSpan.FromSeconds(15)).Result;

      if (characteristic == null)
      {
        return null;
      }

      byte[]? data = characteristic.ReadValueAsync().WaitAsync(TimeSpan.FromSeconds(15)).Result;
      return data == null ? null : Encoding.ASCII.GetString(data);
    }

    /// <summary>
    /// Attempts to perform protocol handshake.
    /// </summary>
    private bool PerformHandshake()
    {
      _handshakeComplete = false;
      _sessionHeader = 0x00;
      _handshakeGate.Reset();

      SendBytes("000000000000000000010000");
      return _handshakeGate.Wait(TimeSpan.FromSeconds(10));
    }

    /// <summary>
    /// Writer worker loop.
    /// </summary>
    private void WriterLoop()
    {
      while (!_cts.IsCancellationRequested)
      {
        if (!_writerQueue.TryDequeue(out BleBuffer? item))
        {
          _writerSignal.WaitOne(250);
          continue;
        }

        try
        {
          _writerCharacteristic?.WriteValueWithResponseAsync(item.ToArray());
        }
        catch (Exception ex)
        {
          Log.Warn("BLE write failed.", ex);
        }
      }
    }

    /// <summary>
    /// Reader worker loop.
    /// </summary>
    private void ReaderLoop()
    {
      while (!_cts.IsCancellationRequested)
      {
        if (!_readerQueue.TryDequeue(out BleBuffer? chunk))
        {
          _readerSignal.WaitOne(250);
          continue;
        }

        byte[] bytes = chunk.ToArray();
        if (bytes.Length == 0)
        {
          continue;
        }

        byte header = bytes[0];
        ReadOnlySpan<byte> payload = bytes.AsSpan(1);

        if (!_handshakeComplete || header == 0)
        {
          TryCompleteHandshake(payload);
          continue;
        }

        AppendEncodedPayload(payload);
      }
    }

    /// <summary>
    /// Processor worker loop.
    /// </summary>
    private void ProcessLoop()
    {
      while (!_cts.IsCancellationRequested)
      {
        if (!_processQueue.TryDequeue(out BleBuffer? frame))
        {
          _processSignal.WaitOne(250);
          continue;
        }

        ProcessDecodedFrame(frame.ToArray());
      }
    }

    /// <summary>
    /// Appends encoded bytes and emits decoded frame when end marker appears.
    /// </summary>
    /// <param name="payload">
    /// Incoming encoded payload (without session header).
    /// </param>
    private void AppendEncodedPayload(ReadOnlySpan<byte> payload)
    {
      if (payload.Length == 0)
      {
        return;
      }

      bool frameEnd = payload[^1] == 0x00;

      if (payload[0] == 0x00)
      {
        _encodedAccumulator.Clear();
        payload = payload[1..];
      }

      if (frameEnd && payload.Length > 0)
      {
        payload = payload[..^1];
      }

      if (payload.Length > 0)
      {
        _encodedAccumulator.AddRange(payload.ToArray());
      }

      if (!frameEnd || _encodedAccumulator.Count == 0)
      {
        return;
      }

      byte[] decoded = CobsCodec.Decode(_encodedAccumulator.ToArray()).ToArray();
      _encodedAccumulator.Clear();

      if (decoded.Length >= 4)
      {
        _processQueue.Enqueue(new BleBuffer(decoded));
        _processSignal.Set();
      }
    }

    /// <summary>
    /// Tries to complete handshake from received pre-session payload.
    /// </summary>
    /// <param name="payload">
    /// Pre-session payload bytes.
    /// </param>
    private void TryCompleteHandshake(ReadOnlySpan<byte> payload)
    {
      byte[] bytes = payload.ToArray();
      string hex = bytes.ToHexString();

      if (!hex.StartsWith("010000000000000000010000") || bytes.Length <= 12)
      {
        return;
      }

      _sessionHeader = bytes[12];
      SendBytes(new byte[] { 0x00 });
      _handshakeComplete = true;
      _handshakeGate.Set();
    }

    /// <summary>
    /// Processes a decoded protocol frame.
    /// </summary>
    /// <param name="frame">
    /// Frame bytes.
    /// </param>
    private void ProcessDecodedFrame(byte[] frame)
    {
      if (frame.Length < 4)
      {
        return;
      }

      bool crcValid = BitConverter.ToUInt16(frame.SkipLast(2).Checksum()) == BitConverter.ToUInt16(frame.TakeLast(2).ToArray());
      if (!crcValid)
      {
        Log.Warn("CRC mismatch.");
      }

      byte[] message = frame.Skip(2).SkipLast(2).ToArray();
      if (message.Length < 4)
      {
        AcknowledgeMessage(message, new byte[] { 0x00 });
        return;
      }

      string prefix = message.ToHexString();
      List<byte> ackBody = new() { 0x00 };

      if (prefix.StartsWith("B413"))
      {
        ushort counter = BitConverter.ToUInt16(message[2..4]);
        ackBody.AddRange(message[2..4]);
        ackBody.AddRange("00000000000000".ToByteArray());

        if (counter == _requestCounter && message.Length > 16)
        {
          try
          {
            _lastResponse = WrapperProto.Parser.ParseFrom(message.Skip(16).ToArray());
            MessageReceived?.Invoke(this, new MessageEventArgs(_lastResponse));
            _responseGate.Set();
          }
          catch (Exception ex)
          {
            Log.Warn("Failed to parse protobuf response.", ex);
          }
        }
      }
      else if (prefix.StartsWith("B313"))
      {
        ackBody.AddRange(message[2..4]);
        ackBody.AddRange("00000000000000".ToByteArray());

        if (message.Length > 16)
        {
          byte[] payload = message.Skip(16).ToArray();
          _ = Task.Run(() =>
          {
            try
            {
              IMessage request = WrapperProto.Parser.ParseFrom(payload);
              MessageReceived?.Invoke(this, new MessageEventArgs(request));
              HandleProtobufRequest(request);
            }
            catch (Exception ex)
            {
              Log.Warn("Failed to parse protobuf request.", ex);
            }
          });
        }
      }

      AcknowledgeMessage(message, ackBody);
    }

    /// <summary>
    /// Sends acknowledgement for a received message.
    /// </summary>
    private void AcknowledgeMessage(IEnumerable<byte> message, IEnumerable<byte> responseBody)
    {
      WriteMessage("8813".ToByteArray().Concat(message.Take(2)).Concat(responseBody).ToArray());
    }

    /// <summary>
    /// Enqueues inbound bytes for reader loop.
    /// </summary>
    private void EnqueueIncoming(BleBuffer bytes)
    {
      _readerQueue.Enqueue(bytes);
      _readerSignal.Set();
    }

    /// <summary>
    /// Enqueues outbound bytes for writer loop.
    /// </summary>
    private void EnqueueOutgoing(BleBuffer bytes)
    {
      byte[] framed = bytes.ToArray().Prepend(_sessionHeader).ToArray();
      _writerQueue.Enqueue(new BleBuffer(framed));
      _writerSignal.Set();
    }

    /// <summary>
    /// Sends raw payload bytes.
    /// </summary>
    private void SendBytes(IEnumerable<byte> bytes)
    {
      EnqueueOutgoing(new BleBuffer(bytes.ToArray()));
    }

    /// <summary>
    /// Raises setup error event and logs message.
    /// </summary>
    private void RaiseSetupError(string message, Exception? exception = null)
    {
      if (exception == null)
      {
        Log.Error(message);
      }
      else
      {
        Log.Error(message, exception);
      }

      SetupError?.Invoke(this, new SetupErrorEventArgs(message, exception));
    }

    /// <summary>
    /// Unwraps aggregate exceptions.
    /// </summary>
    private static Exception UnwrapAggregate(Exception ex)
    {
      while (ex is AggregateException agg && agg.InnerException != null)
      {
        ex = agg.InnerException;
      }

      return ex;
    }
  }
}
