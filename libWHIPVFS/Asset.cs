﻿// Asset.cs
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
using System.Text;

namespace libWHIPVFS {
	public class Asset {
		public byte[] Data { get; set; }

		public Guid getUUID() {
			if (Data == null || Data.Length < 32) {
				throw new InvalidOperationException("Data has not been set or is incomplete.");
			}

			return Guid.Parse(Encoding.UTF8.GetString(Data, 0, 32));
		}

		public bool isLocal() {
			if (Data == null || Data.Length < 33) {
				throw new InvalidOperationException("Data has not been set or is incomplete.");
			}

			return Data[33] == 1;
		}

		public byte getType() {
			if (Data == null || Data.Length < 32) {
				throw new InvalidOperationException("Data has not been set or is incomplete.");
			}

			return Data[32];
		}
	}
}
