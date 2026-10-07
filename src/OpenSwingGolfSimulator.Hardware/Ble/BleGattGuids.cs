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

namespace OpenSwingGolfSimulator.Hardware.Ble
{
  /// <summary>
  /// Centralized BLE GATT UUID constants used by BLE device transport classes.
  /// </summary>
  public static class BleGattGuids
  {
    /// <summary>
    /// The standard Bluetooth SIG battery service UUID.
    /// </summary>
    public static Guid BATTERY_SERVICE_UUID = Guid.Parse("0000180f-0000-1000-8000-00805f9b34fb");

    /// <summary>
    /// The standard Bluetooth SIG battery level characteristic UUID.
    /// </summary>
    public static Guid BATTERY_CHARACTERISTIC_UUID = Guid.Parse("00002a19-0000-1000-8000-00805f9b34fb");

    /// <summary>
    /// The standard Bluetooth SIG device information service UUID.
    /// </summary>
    public static Guid DEVICE_INFO_SERVICE_UUID = Guid.Parse("0000180a-0000-1000-8000-00805f9b34fb");

    /// <summary>
    /// The standard Bluetooth SIG firmware revision characteristic UUID.
    /// </summary>
    public static Guid FIRMWARE_CHARACTERISTIC_UUID = Guid.Parse("00002a28-0000-1000-8000-00805f9b34fb");

    /// <summary>
    /// The standard Bluetooth SIG model number characteristic UUID.
    /// </summary>
    public static Guid MODEL_CHARACTERISTIC_UUID = Guid.Parse("00002a24-0000-1000-8000-00805f9b34fb");

    /// <summary>
    /// The standard Bluetooth SIG serial number characteristic UUID.
    /// </summary>
    public static Guid SERIAL_NUMBER_CHARACTERISTIC_UUID = Guid.Parse("00002a25-0000-1000-8000-00805f9b34fb");

    /// <summary>
    /// The device-specific GATT service that carries the raw request/response byte stream.
    /// </summary>
    public static Guid DEVICE_INTERFACE_SERVICE = Guid.Parse("6A4E2800-667B-11E3-949A-0800200C9A66");

    /// <summary>
    /// The characteristic the device notifies incoming bytes on.
    /// </summary>
    public static Guid DEVICE_INTERFACE_NOTIFIER = Guid.Parse("6A4E2812-667B-11E3-949A-0800200C9A66");

    /// <summary>
    /// The characteristic outgoing bytes are written to.
    /// </summary>
    public static Guid DEVICE_INTERFACE_WRITER = Guid.Parse("6A4E2822-667B-11E3-949A-0800200C9A66");

    /// <summary>
    /// The characteristic that notifies bytes following each recorded shot (currently unparsed).
    /// </summary>
    public static Guid MEASUREMENT_CHARACTERISTIC_UUID = Guid.Parse("6A4E3401-667B-11E3-949A-0800200C9A66");
    /// <summary>
    /// The device-specific GATT service carrying the measurement/control/status characteristics.
    /// </summary>
    public static Guid MEASUREMENT_SERVICE_UUID = Guid.Parse("6A4E3400-667B-11E3-949A-0800200C9A66");

    /// <summary>
    /// The control point characteristic used for waiting-device responses (currently unused).
    /// </summary>
    public static Guid CONTROL_POINT_CHARACTERISTIC_UUID = Guid.Parse("6A4E3402-667B-11E3-949A-0800200C9A66");

    /// <summary>
    /// The characteristic that notifies the device's awake/ready status.
    /// </summary>
    public static Guid STATUS_CHARACTERISTIC_UUID = Guid.Parse("6A4E3403-667B-11E3-949A-0800200C9A66");
  }
}
