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

using System.Collections;

namespace OpenSwingGolfSimulator.Hardware.Ble
{
  /// <summary>
  /// Transport-level byte buffer for BLE read/write chunks and protocol frame processing.
  /// Use this as a semantic replacement for raw <see cref="byte"/> arrays.
  /// </summary>
  public sealed class BleBuffer : IEnumerable<byte>, IEquatable<BleBuffer>
  {
    private readonly byte[] _buffer;

    /// <summary>
    /// Creates a new buffer from a byte array.
    /// </summary>
    /// <param name="buffer">Source data. Null becomes an empty buffer.</param>
    public BleBuffer(byte[]? buffer = null)
    {
      _buffer = buffer ?? Array.Empty<byte>();
    }

    /// <summary>
    /// Number of bytes in this buffer.
    /// </summary>
    public int Length => _buffer.Length;

    /// <summary>
    /// Byte indexer.
    /// </summary>
    public byte this[int index]
    {
      get => _buffer[index];
      set => _buffer[index] = value;
    }

    /// <summary>
    /// Returns a span view over the current buffer.
    /// </summary>
    public ReadOnlySpan<byte> AsSpan() => _buffer;

    /// <summary>
    /// Returns a copy as a single <see cref="byte"/> array.
    /// </summary>
    public byte[] ToArray() => (byte[])_buffer.Clone();

    /// <summary>
    /// Returns chunked arrays (overload returning <see cref="byte"/>[][]).
    /// </summary>
    /// <param name="chunkSize">Size of each chunk. Must be greater than 0.</param>
    public byte[][] ToArray(int chunkSize)
    {
      if (chunkSize <= 0)
      {
        throw new ArgumentOutOfRangeException(nameof(chunkSize), "chunkSize must be greater than 0.");
      }

      if (_buffer.Length == 0)
      {
        return Array.Empty<byte[]>();
      }

      int chunkCount = (_buffer.Length + chunkSize - 1) / chunkSize;
      byte[][] chunks = new byte[chunkCount][];

      for (int i = 0; i < chunkCount; i++)
      {
        int offset = i * chunkSize;
        int len = Math.Min(chunkSize, _buffer.Length - offset);

        chunks[i] = new byte[len];
        Array.Copy(_buffer, offset, chunks[i], 0, len);
      }

      return chunks;
    }

    public IEnumerator<byte> GetEnumerator() => ((IEnumerable<byte>)_buffer).GetEnumerator();
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    public bool Equals(BleBuffer? other)
      => other is not null && AsSpan().SequenceEqual(other.AsSpan());

    public override bool Equals(object? obj) => obj is BleBuffer other && Equals(other);
    public override int GetHashCode() => HashCode.Combine(_buffer.Length);

    public override string ToString() => BitConverter.ToString(_buffer);

    /// <summary>
    /// Implicit conversion from raw byte array to <see cref="BleBuffer"/>.
    /// </summary>
    public static implicit operator BleBuffer(byte[] value) => new(value);

    /// <summary>
    /// Implicit conversion from <see cref="BleBuffer"/> to raw byte array.
    /// </summary>
    public static implicit operator byte[](BleBuffer value) => value._buffer;
  }
}
