// TestVFSIndex.cs
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
using System.Collections.Generic;
using System.Linq;
using libWHIPVFS;
using NUnit.Framework;

namespace testWHIPVFS {
	[TestFixture]
	public class TestVFSIndex {
		private IEnumerable<VFSDatabase> _dbs;

		[OneTimeSetUp]
		public void Setup() {
			var vfs = new WHIPVFS(Constants.VFS_PATH);
			_dbs = vfs.GetDatabases();
		}

		[Test]
		public void TestFirstGlobalGetAllAssetRecordsAsyncNoExceptions() {
			using (var index = _dbs.First().CreateIndexReader(AssetScope.Global)) {
				var task = index.GetAllAssetRecordsAsync();
				task.Wait();
			}
		}

		[Test]
		public void TestFirstGlobalGetAllAssetRecordsAsyncHasExpectedRecords() {
			IEnumerable<AssetIndexRecord> records;
			using (var index = _dbs.First().CreateIndexReader(AssetScope.Global)) {
				var task = index.GetAllAssetRecordsAsync();
				task.Wait();
				records = task.Result;
			}

			Assert.That(records.Where(record => record.Id == Guid.Parse("0000000038f91111024e222222111110")).Any, "Missing expected record #1");
			Assert.That(records.Where(record => record.Id == Guid.Parse("00000000000011119999000000000007")).Any, "Missing expected record #2");
			Assert.That(records.Where(record => record.Id == Guid.Parse("00000000000022223333100000001004")).Any, "Missing expected record #3");
		}

		[Test]
		public void TestSecondGlobalGetAllAssetRecordsAsyncNoExceptions() {
			using (var index = _dbs.Skip(1).First().CreateIndexReader(AssetScope.Global)) {
				var task = index.GetAllAssetRecordsAsync();
				task.Wait();
			}
		}

		[Test]
		public void TestSecondGlobalGetAllAssetRecordsAsyncHasExpectedRecords() {
			IEnumerable<AssetIndexRecord> records;
			using (var index = _dbs.Skip(1).First().CreateIndexReader(AssetScope.Global)) {
				var task = index.GetAllAssetRecordsAsync();
				task.Wait();
				records = task.Result;
			}

			Assert.That(records.Where(record => record.Id == Guid.Parse("b3e6b786f81d458297d1d0afdae06d6d")).Any, "Missing expected record #1");
			Assert.That(records.Where(record => record.Id == Guid.Parse("b3ec7d36d75148b89a9984e233d81f1a")).Any, "Missing expected record #2");
			Assert.That(records.Where(record => record.Id == Guid.Parse("b3e49aef1eb845c88d3e44a8ef5dc2be")).Any, "Missing expected record #3");
		}

	}
}
