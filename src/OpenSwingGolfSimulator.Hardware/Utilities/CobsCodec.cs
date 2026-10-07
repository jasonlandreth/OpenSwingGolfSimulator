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

namespace OpenSwingGolfSimulator.Hardware.Utilities
{
  /// <summary>
  /// Consistent Overhead Byte Stuffing (ConsistentOverheadByte) encoding/decoding, used to frame messages so a
  /// 0x00 byte can be used unambiguously as a packet delimiter over the Bluetooth byte stream.
  /// </summary>
  public static class CobsCodec
  {
    /// <summary>
    /// COBS-encodes a byte sequence, replacing every 0x00 byte with a distance-to-next-zero marker.
    /// </summary>
    /// <param name="Input">The raw bytes to encode.</param>
    /// <returns>The COBS-encoded bytes.</returns>
    public static IEnumerable<byte> Encode(IEnumerable<byte> Input)
    {
      List<byte> result = new List<byte>();
      int distanceIndex = 0;
      byte distance = 1;

      foreach (byte b in Input)
      {
        if (b != 0 && distance < 255)
        {
          result.Add(b);
          distance++;
        }
        else
        {
          result.Insert(distanceIndex, (byte)distance);
          distanceIndex = result.Count;
          distance = 1;
        }
      }

      if (result.Count != 255 && result.Count > 0)
      {
        result.Insert(distanceIndex, distance);
      }

      return result;
    }

    /// <summary>
    /// Decodes a COBS-encoded byte sequence back into its original raw bytes.
    /// </summary>
    /// <param name="Input">The COBS-encoded bytes to decode.</param>
    /// <returns>The decoded raw bytes, or an empty list if the input is malformed.</returns>
    public static IEnumerable<byte> Decode(IEnumerable<byte> Input)
    {
      byte[] inputArray = Input.ToArray();
      List<byte> result = new List<byte>();
      int distanceIndex = 0;
      byte distance = 1;

      while (distanceIndex < inputArray.Length)
      {
        distance = inputArray[distanceIndex];

        if (inputArray.Length < distanceIndex + distance || distance < 1)
        {
          return new List<byte>();
        }

        if (distance > 1)
        {
          for (byte i = 1; i < distance; i++)
          {
            result.Add(inputArray[distanceIndex + i]);
          }
        }

        distanceIndex += distance;

        if (distance < 0xFF && distanceIndex < inputArray.Length)
        {
          result.Add(0);
        }
      }

      return result;
    }
  }
}
