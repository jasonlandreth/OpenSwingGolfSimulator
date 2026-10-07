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
  /// Provides BLE protocol transport behavior for launch monitor devices.
  /// </summary>
  public abstract class BleProtocolDevice2 : IDisposable
  {
    private const int SETUP_TIMEOUT_SECONDS = 15;
    private const int HANDSHAKE_TIMEOUT_SECONDS = 10;
    private const int RESPONSE_TIMEOUT_SECONDS = 15;
    private const int BLE_CHUNK_SIZE = 19;

    /// <summary>
    /// Gets the underlying Bluetooth LE device.
    /// </summary>
    public BluetoothDevice Device { get; }

    /// <summary>
    /// Gets the latest battery percentage.
    /// </summary>
    public int Battery { get; private set; }

    /// <summary>
    /// Gets the model read from the device information service.
    /// </summary>
    public string? Model { get; private set; }

    /// <summary>
    /// Gets the firmware read from the device information service.
    /// </summary>
    public string? Firmware { get; private set; }

    /// <summary>
    /// Gets the serial number read from the device information service.
    /// </summary>
    public string? Serial { get; private set; }

    /// <summary>
    /// Raised when a protobuf message is received.
    /// </summary>
    public event MessageEventHandler? MessageReceived;

    /// <summary>
    /// Raised when a protobuf message is sent.
    /// </summary>
    public event MessageEventHandler? MessageSent;

    /// <summary>
    /// Raised when battery value changes.
    /// </summary>
    public event BatteryEventHandler? BatteryLifeUpdated;

    /// <summary>
    /// Raised when setup fails.
    /// </summary>
    public event SetupErrorEventHandler? SetupError;

    /// <summary>
    /// Initializes a new instance of the <see cref="BleProtocolDevice"/> class.
    /// </summary>
    /// <param name="Device">
    /// Connected BLE device.
    /// </param>
    protected BleProtocolDevice2(BluetoothDevice Device)
    {
      this.Device = Device;

      _cancellation = new CancellationTokenSource();
      _writerTask = Task.Run(WriterThread, _cancellation.Token);
      _readerTask = Task.Run(ReaderThread, _cancellation.Token);
      _processorTask = Task.Run(MessageProcessorThread, _cancellation.Token);
    }

    /// <summary>
    /// Performs common setup logic and protocol handshake.
    /// </summary>
    /// <returns>
    /// <c>true</c> when setup succeeds; otherwise <c>false</c>.
    /// </returns>
    public virtual bool Setup()
    {
      try
      {
        GattService? deviceInfoService = Device.Gatt
            .GetPrimaryServiceAsync(BleGattGuids.DEVICE_INFO_SERVICE_UUID)
            .WaitAsync(TimeSpan.FromSeconds(SETUP_TIMEOUT_SECONDS)).Result;

        if (deviceInfoService == null)
        {
          RaiseSetupError("Device info service was not found.");
          return false;
        }

        Serial = ReadAsciiCharacteristic(deviceInfoService, BleGattGuids.SERIAL_NUMBER_CHARACTERISTIC_UUID);
        Firmware = ReadAsciiCharacteristic(deviceInfoService, BleGattGuids.FIRMWARE_CHARACTERISTIC_UUID);
        Model = ReadAsciiCharacteristic(deviceInfoService, BleGattGuids.MODEL_CHARACTERISTIC_UUID);

        GattService? batteryService = Device.Gatt
            .GetPrimaryServiceAsync(BleGattGuids.BATTERY_SERVICE_UUID)
            .WaitAsync(TimeSpan.FromSeconds(SETUP_TIMEOUT_SECONDS)).Result;

        if (batteryService == null)
        {
          RaiseSetupError("Battery service was not found.");
          return false;
        }

        GattCharacteristic? batteryCharacteristic = batteryService
            .GetCharacteristicAsync(BleGattGuids.BATTERY_CHARACTERISTIC_UUID)
            .WaitAsync(TimeSpan.FromSeconds(SETUP_TIMEOUT_SECONDS)).Result;

        if (batteryCharacteristic == null)
        {
          RaiseSetupError("Battery characteristic was not found.");
          return false;
        }

        batteryCharacteristic.CharacteristicValueChanged += (_, e) =>
        {
          byte[]? value = e.Value;
          if (value != null && value.Length > 0)
          {
            Battery = value[0];
            BatteryLifeUpdated?.Invoke(this, new BatteryEventArgs { Battery = Battery });
          }
        };
        batteryCharacteristic.StartNotificationsAsync().Wait(TimeSpan.FromSeconds(SETUP_TIMEOUT_SECONDS));

        GattService? interfaceService = Device.Gatt
            .GetPrimaryServiceAsync(BleGattGuids.DEVICE_INTERFACE_SERVICE)
            .WaitAsync(TimeSpan.FromSeconds(SETUP_TIMEOUT_SECONDS)).Result;

        if (interfaceService == null)
        {
          RaiseSetupError("Device interface service was not found.");
          return false;
        }

        _writerCharacteristic = interfaceService
            .GetCharacteristicAsync(BleGattGuids.DEVICE_INTERFACE_WRITER)
            .WaitAsync(TimeSpan.FromSeconds(SETUP_TIMEOUT_SECONDS)).Result;

        if (_writerCharacteristic == null)
        {
          RaiseSetupError("Device interface writer characteristic was not found.");
          return false;
        }

        GattCharacteristic? notifierCharacteristic = interfaceService
            .GetCharacteristicAsync(BleGattGuids.DEVICE_INTERFACE_NOTIFIER)
            .WaitAsync(TimeSpan.FromSeconds(SETUP_TIMEOUT_SECONDS)).Result;

        if (notifierCharacteristic == null)
        {
          RaiseSetupError("Device interface notifier characteristic was not found.");
          return false;
        }

        notifierCharacteristic.CharacteristicValueChanged += (_, E) =>
        {
          byte[]? value = E.Value;
          if (value != null)
          {
            ReadBytes(new BleBuffer(value));
          }
        };
        notifierCharacteristic.StartNotificationsAsync().Wait(TimeSpan.FromSeconds(SETUP_TIMEOUT_SECONDS));

        if (!PerformHandshake())
        {
          RaiseSetupError("Handshake did not complete within the timeout.");
          return false;
        }

        return true;
      }
      catch (Exception ex)
      {
        Exception real = UnwrapAggregate(ex);
        RaiseSetupError(real.Message, real);
        return false;
      }
    }

    /// <summary>
    /// Sends a protobuf request and waits for a matching response.
    /// </summary>
    /// <param name="Proto">
    /// Protobuf request message.
    /// </param>
    /// <returns>
    /// Response message when available; otherwise <c>null</c>.
    /// </returns>
    public IMessage? SendProtobufRequest(IMessage Proto)
    {
      if (_writerCharacteristic == null)
      {
        Log.Warn("SendProtobufRequest called before writer characteristic is available.");
        return null;
      }

      _responseSignal.Reset();

      byte[] payload = Proto.ToByteArray();
      int payloadLength = payload.Length;

      byte[] wireMessage = "B313".ToByteArray()
          .Concat(BitConverter.GetBytes(_requestCounter))
          .Append((byte)0x00)
          .Append((byte)0x00)
          .Concat(BitConverter.GetBytes(payloadLength))
          .Concat(BitConverter.GetBytes(payloadLength))
          .Concat(payload)
          .ToArray();

      WriteMessage(wireMessage);
      MessageSent?.Invoke(this, new MessageEventArgs { Message = Proto });

      if (_responseSignal.Wait(TimeSpan.FromSeconds(RESPONSE_TIMEOUT_SECONDS)))
      {
        _requestCounter++;
        return _lastResponse;
      }

      Log.Warn($"No protobuf response for request counter {_requestCounter}.");
      return null;
    }

    /// <summary>
    /// Sends raw message bytes after framing and chunking.
    /// </summary>
    /// <param name="Bytes">
    /// Raw protocol message bytes.
    /// </param>
    public void WriteMessage(byte[] Bytes)
    {
      ushort totalLength = (ushort)(2 + Bytes.Length + 2);
      IEnumerable<byte> withLength = BitConverter.GetBytes(totalLength).Concat(Bytes);
      IEnumerable<byte> framed = withLength.Concat(withLength.Checksum());
      List<byte> encoded = CobsCodec.Encode(framed).Prepend((byte)0x00).Append((byte)0x00).ToList();

      while (encoded.Count > BLE_CHUNK_SIZE)
      {
        SendBytes(encoded.Take(BLE_CHUNK_SIZE));
        encoded = encoded.Skip(BLE_CHUNK_SIZE).ToList();
      }

      if (encoded.Count > 0)
      {
        SendBytes(encoded);
      }
    }

    /// <summary>
    /// Sends a buffer message after framing and chunking.
    /// </summary>
    /// <param name="Buffer">
    /// Raw protocol buffer.
    /// </param>
    public void WriteMessage(BleBuffer Buffer)
    {
      WriteMessage(Buffer.ToArray());
    }

    /// <summary>
    /// Sends bytes expressed as hex text.
    /// </summary>
    /// <param name="HexBytes">
    /// Hex string.
    /// </param>
    public void SendBytes(string HexBytes)
    {
      SendBytes(HexBytes.ToByteArray());
    }

    /// <summary>
    /// Handles device-specific protobuf request/notification messages.
    /// </summary>
    /// <param name="Request">
    /// Parsed protobuf message.
    /// </param>
    public abstract void HandleProtobufRequest(IMessage Request);

    /// <summary>
    /// Releases managed resources and stops worker threads.
    /// </summary>
    public void Dispose()
    {
      if (_disposed)
      {
        return;
      }

      _disposed = true;
      _cancellation.Cancel();

      try
      {
        Task.WaitAll(new[] { _writerTask, _readerTask, _processorTask }, TimeSpan.FromSeconds(5));
      }
      catch
      {
        // Ignored during shutdown.
      }

      _writerSignal.Dispose();
      _readerSignal.Dispose();
      _processorSignal.Dispose();
      _handshakeSignal.Dispose();
      _responseSignal.Dispose();
      _cancellation.Dispose();

      Device.Gatt?.Disconnect();
      GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Event signature for sent/received protobuf messages.
    /// </summary>
    public delegate void MessageEventHandler(object sender, MessageEventArgs E);

    /// <summary>
    /// Event signature for battery updates.
    /// </summary>
    public delegate void BatteryEventHandler(object sender, BatteryEventArgs E);

    /// <summary>
    /// Event signature for setup errors.
    /// </summary>
    public delegate void SetupErrorEventHandler(object sender, SetupErrorEventArgs E);

    /// <summary>
    /// Message event payload.
    /// </summary>
    public sealed class MessageEventArgs : EventArgs
    {
      /// <summary>
      /// Gets or sets the protobuf message.
      /// </summary>
      public IMessage? Message { get; set; }
    }

    /// <summary>
    /// Battery event payload.
    /// </summary>
    public sealed class BatteryEventArgs : EventArgs
    {
      /// <summary>
      /// Gets or sets battery percentage.
      /// </summary>
      public int Battery { get; set; }
    }

    /// <summary>
    /// Setup error event payload.
    /// </summary>
    public sealed class SetupErrorEventArgs : EventArgs
    {
      /// <summary>
      /// Gets or sets human-readable setup failure message.
      /// </summary>
      public string Message { get; set; } = string.Empty;

      /// <summary>
      /// Gets or sets optional exception associated with setup failure.
      /// </summary>
      public Exception? Exception { get; set; }
    }

    private static readonly log4net.ILog Log = log4net.LogManager.GetLogger(typeof(BleProtocolDevice));

    private readonly ConcurrentQueue<BleBuffer> _writerQueue = new();
    private readonly ConcurrentQueue<BleBuffer> _readerQueue = new();
    private readonly ConcurrentQueue<BleBuffer> _processQueue = new();

    private readonly AutoResetEvent _writerSignal = new(false);
    private readonly AutoResetEvent _readerSignal = new(false);
    private readonly AutoResetEvent _processorSignal = new(false);

    private readonly ManualResetEventSlim _handshakeSignal = new(false);
    private readonly ManualResetEventSlim _responseSignal = new(false);

    private readonly CancellationTokenSource _cancellation;
    private readonly Task _writerTask;
    private readonly Task _readerTask;
    private readonly Task _processorTask;

    private readonly List<byte> _encodedAccumulator = new();

    private GattCharacteristic? _writerCharacteristic;
    private bool _handshakeCompleted;
    private byte _sessionHeader;
    private int _requestCounter;
    private IMessage? _lastResponse;
    private bool _disposed;

    /// <summary>
    /// Reads an ASCII characteristic from a given service.
    /// </summary>
    /// <param name="Service">
    /// Parent service.
    /// </param>
    /// <param name="CharacteristicGuid">
    /// Characteristic UUID.
    /// </param>
    /// <returns>
    /// ASCII string value when available; otherwise <c>null</c>.
    /// </returns>
    private static string? ReadAsciiCharacteristic(GattService Service, Guid CharacteristicGuid)
    {
      GattCharacteristic? characteristic = Service
          .GetCharacteristicAsync(CharacteristicGuid)
          .WaitAsync(TimeSpan.FromSeconds(SETUP_TIMEOUT_SECONDS)).Result;

      if (characteristic == null)
      {
        return null;
      }

      byte[]? bytes = characteristic
          .ReadValueAsync()
          .WaitAsync(TimeSpan.FromSeconds(SETUP_TIMEOUT_SECONDS)).Result;

      return bytes == null ? null : Encoding.ASCII.GetString(bytes);
    }

    /// <summary>
    /// Attempts handshake with the device protocol.
    /// </summary>
    /// <returns>
    /// <c>true</c> when handshake completes; otherwise <c>false</c>.
    /// </returns>
    private bool PerformHandshake()
    {
      _handshakeCompleted = false;
      _sessionHeader = 0x00;
      _handshakeSignal.Reset();

      SendBytes("000000000000000000010000");
      return _handshakeSignal.Wait(TimeSpan.FromSeconds(HANDSHAKE_TIMEOUT_SECONDS));
    }

    /// <summary>
    /// Worker loop that writes queued BLE chunks.
    /// </summary>
    private void WriterThread()
    {
      while (!_cancellation.IsCancellationRequested)
      {
        if (!_writerQueue.TryDequeue(out BleBuffer? chunk))
        {
          _writerSignal.WaitOne(250);
          continue;
        }

        try
        {
          _writerCharacteristic?.WriteValueWithResponseAsync(chunk.ToArray());
        }
        catch (Exception ex)
        {
          Log.Warn("BLE write failed.", ex);
        }
      }
    }

    /// <summary>
    /// Worker loop that accumulates encoded chunks and emits decoded frames.
    /// </summary>
    private void ReaderThread()
    {
      while (!_cancellation.IsCancellationRequested)
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

        if (!_handshakeCompleted || header == 0x00)
        {
          ContinueHandshake(payload);
          continue;
        }

        AppendEncodedPayload(payload);
      }
    }

    /// <summary>
    /// Worker loop that processes complete decoded frames.
    /// </summary>
    private void MessageProcessorThread()
    {
      while (!_cancellation.IsCancellationRequested)
      {
        if (!_processQueue.TryDequeue(out BleBuffer? frame))
        {
          _processorSignal.WaitOne(250);
          continue;
        }

        ProcessFrame(frame.ToArray());
      }
    }

    /// <summary>
    /// Continues handshake based on incoming pre-session payload.
    /// </summary>
    /// <param name="Payload">
    /// Incoming payload without session header.
    /// </param>
    private void ContinueHandshake(ReadOnlySpan<byte> Payload)
    {
      byte[] bytes = Payload.ToArray();
      string hex = bytes.ToHexString();

      if (!hex.StartsWith("010000000000000000010000") || bytes.Length <= 12)
      {
        return;
      }

      _sessionHeader = bytes[12];
      SendBytes(new byte[] { 0x00 });
      _handshakeCompleted = true;
      _handshakeSignal.Set();
    }

    /// <summary>
    /// Appends encoded payload data and emits a decoded frame when complete.
    /// </summary>
    /// <param name="Payload">
    /// Encoded payload without session header.
    /// </param>
    private void AppendEncodedPayload(ReadOnlySpan<byte> Payload)
    {
      if (Payload.Length == 0)
      {
        return;
      }

      bool frameEnds = Payload[^1] == 0x00;

      if (Payload[0] == 0x00)
      {
        _encodedAccumulator.Clear();
        Payload = Payload[1..];
      }

      if (frameEnds && Payload.Length > 0)
      {
        Payload = Payload[..^1];
      }

      if (Payload.Length > 0)
      {
        _encodedAccumulator.AddRange(Payload.ToArray());
      }

      if (!frameEnds || _encodedAccumulator.Count == 0)
      {
        return;
      }

      byte[] decoded = CobsCodec.Decode(_encodedAccumulator.ToArray()).ToArray();
      _encodedAccumulator.Clear();

      if (decoded.Length >= 4)
      {
        _processQueue.Enqueue(new BleBuffer(decoded));
        _processorSignal.Set();
      }
    }

    /// <summary>
    /// Processes a decoded protocol frame.
    /// </summary>
    /// <param name="Frame">
    /// Decoded frame bytes.
    /// </param>
    private void ProcessFrame(byte[] Frame)
    {
      if (Frame.Length < 4)
      {
        return;
      }

      bool crcOk = BitConverter.ToUInt16(Frame.SkipLast(2).Checksum()) == BitConverter.ToUInt16(Frame.TakeLast(2).ToArray());
      if (!crcOk)
      {
        Log.Warn("CRC mismatch.");
      }

      byte[] message = Frame.Skip(2).SkipLast(2).ToArray();
      if (message.Length < 4)
      {
        Acknowledge(message, new byte[] { 0x00 });
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
            MessageReceived?.Invoke(this, new MessageEventArgs { Message = _lastResponse });
            _responseSignal.Set();
          }
          catch (Exception ex)
          {
            Log.Warn("Failed parsing protobuf response.", ex);
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
              MessageReceived?.Invoke(this, new MessageEventArgs { Message = request });
              HandleProtobufRequest(request);
            }
            catch (Exception ex)
            {
              Log.Warn("Failed parsing protobuf request.", ex);
            }
          });
        }
      }

      Acknowledge(message, ackBody);
    }

    /// <summary>
    /// Sends protocol acknowledgement for a received message.
    /// </summary>
    /// <param name="Message">
    /// Original message bytes.
    /// </param>
    /// <param name="ResponseBody">
    /// Acknowledgement body.
    /// </param>
    private void Acknowledge(IEnumerable<byte> Message, IEnumerable<byte> ResponseBody)
    {
      WriteMessage("8813".ToByteArray().Concat(Message.Take(2)).Concat(ResponseBody).ToArray());
    }

    /// <summary>
    /// Queues incoming BLE bytes for reader processing.
    /// </summary>
    /// <param name="Bytes">
    /// Incoming chunk.
    /// </param>
    private void ReadBytes(BleBuffer Bytes)
    {
      _readerQueue.Enqueue(Bytes);
      _readerSignal.Set();
    }

    /// <summary>
    /// Sends raw bytes by prefixing the current session header and queueing for write.
    /// </summary>
    /// <param name="Bytes">
    /// Raw bytes to send.
    /// </param>
    private void SendBytes(IEnumerable<byte> Bytes)
    {
      byte[] withHeader = Bytes.Prepend(_sessionHeader).ToArray();
      _writerQueue.Enqueue(new BleBuffer(withHeader));
      _writerSignal.Set();
    }

    /// <summary>
    /// Raises setup error event and logs details.
    /// </summary>
    /// <param name="Message">
    /// Error message.
    /// </param>
    /// <param name="Exception">
    /// Optional exception.
    /// </param>
    private void RaiseSetupError(string Message, Exception? Exception = null)
    {
      if (Exception == null)
      {
        Log.Error(Message);
      }
      else
      {
        Log.Error(Message, Exception);
      }

      SetupError?.Invoke(this, new SetupErrorEventArgs
      {
        Message = Message,
        Exception = Exception
      });
    }

    /// <summary>
    /// Unwraps aggregate exceptions to the innermost exception.
    /// </summary>
    /// <param name="Ex">
    /// Exception to unwrap.
    /// </param>
    /// <returns>
    /// Innermost exception when available; otherwise original exception.
    /// </returns>
    private static Exception UnwrapAggregate(Exception Ex)
    {
      while (Ex is AggregateException agg && agg.InnerException != null)
      {
        Ex = agg.InnerException;
      }

      return Ex;
    }
  }
}
