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
using OpenSwingGolfSimulator.Utilities;

namespace OpenSwingGolfSimulator.Utilities.ExtensionMethods
{
  /// <summary>
  /// Extension methods for converting between hex strings and byte sequences.
  /// </summary>
  public static class ByteExtensionMethods
  {
    /// <summary>
    /// Converts a hex string (no separators, e.g. "0A1B2C") into a byte array.
    /// </summary>
    /// <param name="HexString">The hex string to convert.</param>
    /// <returns>The decoded bytes.</returns>
    public static byte[] ToByteArray(this string HexString)
    {
      byte[] retval = new byte[HexString.Length / 2];
      for (int i = 0; i < HexString.Length; i += 2)
      {
        retval[i / 2] = Convert.ToByte(HexString.Substring(i, 2), 16);
      }

      return retval;
    }

    /// <summary>
    /// Converts a byte array into an uppercase hex string with no separators.
    /// </summary>
    /// <param name="bytes">The bytes to convert.</param>
    /// <returns>The hex string.</returns>
    public static string ToHexString(this byte[] Bytes) => BitConverter.ToString(Bytes).Replace("-", string.Empty);

    /// <summary>
    /// Converts a byte sequence into an uppercase hex string with no separators.
    /// </summary>
    /// <param name="Bytes">The bytes to convert.</param>
    /// <returns>The hex string.</returns>
    public static string ToHexString(this IEnumerable<byte> Bytes) => ToHexString(Bytes.ToArray());

    /// <summary>
    /// Computes the CRC16 checksum of a byte sequence. See <see cref="Crc.ComputeChecksum"/>.
    /// </summary>
    /// <param name="Bytes">The bytes to checksum.</param>
    /// <returns>The two-byte checksum.</returns>
    public static byte[] Checksum(this IEnumerable<byte> Bytes) => Crc.ComputeChecksum(Bytes);
  }
}
