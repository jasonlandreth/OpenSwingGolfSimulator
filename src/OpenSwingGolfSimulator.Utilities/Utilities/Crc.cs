/*
 * SPDX-License-Identifier: Apache-2.0
 *
 * Copyright (c) 2026 Jason Landreth
 *
 * Licensed under the Apache License, Version 2.0 (the "License");
 * you may not use this file except in compliance with the License.
 * You may obtain a copy of the License at
 *
 *     http://apache.org
 *
 * Unless required by applicable law or agreed to in writing, software
 * distributed under the License is distributed on an "AS IS" BASIS,
 * WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 * See the License for the specific language governing permissions and
 * limitations under the License.
 */

namespace OpenSwingGolfSimulator.Utilities
{
  /// <summary>
  /// CRC (ARC/IBM polynomial) checksum computation, used to validate received frames and generate checksums for outgoing ones.
  /// </summary>
  public static class Crc
  {
    private const ushort POLYNOMIAL = 0xA001;
    static readonly ushort[] Table = new ushort[256];

    public static byte[] ComputeChecksum(IEnumerable<byte> Bytes)
    {
      ushort crc = 0;
      foreach (byte b in Bytes)
      {
        crc = (ushort)((crc >> 8) ^ Table[(byte)(crc ^ b)]);
      }
      return BitConverter.GetBytes(crc);
    }

    static Crc()
    {
      ushort value;
      ushort temp;
      for (ushort i = 0; i < Table.Length; ++i)
      {
        value = 0;
        temp = i;
        for (byte j = 0; j < 8; ++j)
        {
          if (((value ^ temp) & 0x0001) != 0)
          {
            value = (ushort)((value >> 1) ^ POLYNOMIAL);
          }
          else
          {
            value >>= 1;
          }
          temp >>= 1;
        }
        Table[i] = value;
      }
    }
  }
}
