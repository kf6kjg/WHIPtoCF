// Asset.cs
//
// Author:
//       Ricky Curtice <ricky@rwcproductions.com>
//
// Copyright (c) 2017 Ricky Curtice
//
// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:
//
// The above copyright notice and this permission notice shall be included in
// all copies or substantial portions of the Software.
//
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
// THE SOFTWARE.
using System;
using System.Linq;
using System.Text;

namespace libWHIPVFS {
	public class Asset {
		/// <summary>
		/// Size of the packet header
		/// </summary>
		private const short HEADER_SIZE = 39;
		/// <summary>
		/// location of the type tag
		/// </summary>
		private const short TYPE_TAG_LOC = 32;
		/// <summary>
		/// location of the local tag
		/// </summary>
		private const short LOCAL_TAG_LOC = 33;
		/// <summary>
		/// Location of the temporary tag
		/// </summary>
		private const short TEMPORARY_TAG_LOC = 34;
		/// <summary>
		/// Location of the create time tag
		/// </summary>
		private const short CREATE_TIME_TAG_LOC = 35;
		/// <summary>
		/// Location of the size of the name field
		/// </summary>
		private const short NAME_SIZE_TAG_LOC = 39;

		public byte[] RawData { get; set; }

		public Guid GetUUID() {
			if (RawData == null || RawData.Length < HEADER_SIZE) {
				throw new InvalidOperationException("Data has not been set or is incomplete.");
			}

			return Guid.Parse(Encoding.UTF8.GetString(RawData, 0, 32));
		}

		public bool IsLocal() {
			if (RawData == null || RawData.Length < HEADER_SIZE) {
				throw new InvalidOperationException("Data has not been set or is incomplete.");
			}

			return RawData[LOCAL_TAG_LOC] == 1;
		}

		public byte GetAssetType() {
			if (RawData == null || RawData.Length < HEADER_SIZE) {
				throw new InvalidOperationException("Data has not been set or is incomplete.");
			}

			return RawData[TYPE_TAG_LOC];
		}

		public bool IsTemporary() {
			if (RawData == null || RawData.Length < HEADER_SIZE) {
				throw new InvalidOperationException("Data has not been set or is incomplete.");
			}

			return RawData[TEMPORARY_TAG_LOC] == 1;
		}

		public DateTimeOffset GetCreateTime() {
			if (RawData == null || RawData.Length < HEADER_SIZE) {
				throw new InvalidOperationException("Data has not been set or is incomplete.");
			}
			var timestampData = RawData.Skip(CREATE_TIME_TAG_LOC).Take(4).ToArray();

			if (BitConverter.IsLittleEndian) {
				Array.Reverse(timestampData);
			}

			var timestamp = BitConverter.ToInt32(timestampData, 0);

			return DateTimeOffset_FromUnixTimeSeconds(timestamp);
		}

		public string GetName() {
			if (RawData == null || RawData.Length < HEADER_SIZE) {
				throw new InvalidOperationException("Data has not been set or is incomplete.");
			}

			var nameFieldSize = RawData[NAME_SIZE_TAG_LOC];

			if (nameFieldSize > 0) {
				return Encoding.UTF8.GetString(RawData, NAME_SIZE_TAG_LOC + 1, nameFieldSize);
			}

			return string.Empty;
		}

		public string GetDescription() {
			if (RawData == null || RawData.Length < HEADER_SIZE) {
				throw new InvalidOperationException("Data has not been set or is incomplete.");
			}

			var nameFieldSize = RawData[NAME_SIZE_TAG_LOC];
			var descSizeFieldLoc = NAME_SIZE_TAG_LOC + nameFieldSize + 1;
			var descFieldSize = RawData[descSizeFieldLoc];

			if (descFieldSize > 0) {
				return Encoding.UTF8.GetString(RawData, descSizeFieldLoc + 1, descFieldSize);
			}

			return string.Empty;
		}

		public byte[] GetAssetData() {
			if (RawData == null || RawData.Length < HEADER_SIZE) {
				throw new InvalidOperationException("Data has not been set or is incomplete.");
			}

			var nameFieldSize = RawData[NAME_SIZE_TAG_LOC];
			var descSizeFieldLoc = NAME_SIZE_TAG_LOC + nameFieldSize + 1;
			var descFieldSize = RawData[descSizeFieldLoc];
			var dataSizeFieldLoc = descSizeFieldLoc + descFieldSize + 1;

			var dataSizeRaw = RawData.Skip(dataSizeFieldLoc).Take(4).ToArray();
			if (BitConverter.IsLittleEndian) {
				Array.Reverse(dataSizeRaw);
			}
			var dataSize = BitConverter.ToInt32(dataSizeRaw, 0);
			int dataLoc = dataSizeFieldLoc + 4;

			if (dataSize > 0) {
				return RawData.Skip(dataLoc).Take(dataSize).ToArray();
			}

			return new byte[0];
		}

		// Hackyness because I can't have .NET 4.6 due to Mono ;(
		private static readonly DateTimeOffset epoch = new DateTimeOffset(new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc));
		private static DateTimeOffset DateTimeOffset_FromUnixTimeSeconds(double unixTimeStamp) {
			// Unix timestamp is seconds past epoch
			return epoch.AddSeconds(unixTimeStamp);
		}
	}
}
