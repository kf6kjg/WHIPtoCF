// TestWHIPVFS.cs
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
using System.IO;
using System.Linq;
using libWHIPVFS;
using NUnit.Framework;

namespace testWHIPVFS {
	[TestFixture]
	public class TestWHIPVFS {
		[OneTimeSetUp]
		public void Setup() {
			// Verify that the test data files are in place.
			IEnumerable<string> vfsFolders = null;
			Assert.DoesNotThrow(() => {
				vfsFolders = Directory.EnumerateDirectories(Constants.VFS_PATH, "???");
			}, $"Could not find the test data folder!");

			Assert.IsNotNull(vfsFolders, $"Could not find the test data folder!");

			Assert.That(vfsFolders.Where(folderPath => folderPath.EndsWith("000", StringComparison.InvariantCultureIgnoreCase)).Any, $"Could not find expected folder '000' in test data!");
			Assert.That(vfsFolders.Where(folderPath => folderPath.EndsWith("b3e", StringComparison.InvariantCultureIgnoreCase)).Any, $"Could not find expected folder 'b3e' in test data!");

			var folder0a3 = Directory.EnumerateFiles(Path.Combine(Constants.VFS_PATH, "000"))
			                         .Select(filePath => Path.GetFileName(filePath));
			var folderb3e = Directory.EnumerateFiles(Path.Combine(Constants.VFS_PATH, "b3e"))
															 .Select(filePath => Path.GetFileName(filePath));

			Assert.That(folder0a3.Contains("globals.idx"), $"Could not find expected file 'globals.idx' in folder '000' in test data!");
			Assert.That(folder0a3.Contains("globals.data"), $"Could not find expected file 'globals.data' in folder '000' in test data!");
			Assert.That(folder0a3.Contains("locals.idx"), $"Could not find expected file 'locals.idx' in folder '000' in test data!");
			//Assert.That(folder0a3.Contains("locals.data"), $"Could not find expected file 'locals.data' in folder '000' in test data!");

			Assert.That(folderb3e.Contains("globals.idx"), $"Could not find expected file 'globals.idx' in folder 'b3e' in test data!");
			Assert.That(folderb3e.Contains("globals.data"), $"Could not find expected file 'globals.data' in folder 'b3e' in test data!");
			Assert.That(folderb3e.Contains("locals.idx"), $"Could not find expected file 'locals.idx' in folder 'b3e' in test data!");
			//Assert.That(folderb3e.Contains("locals.data"), $"Could not find expected file 'locals.data' in folder 'b3e' in test data!");
		}

		[Test]
		public void Test0CreateClassNoExceptions() {
			Assert.DoesNotThrow(() => new WHIPVFS(Constants.VFS_PATH));
		}

		[Test]
		public void TestCallGetDatabasesNoExceptions() {
			var vfs = new WHIPVFS(Constants.VFS_PATH);

			Assert.DoesNotThrow(() => vfs.GetDatabases());
		}

		[Test]
		public void TestCallGetDatabasesHasContent() {
			var vfs = new WHIPVFS(Constants.VFS_PATH);

			var dbs = vfs.GetDatabases();

			Assert.That(dbs.Any, $"No DBs found?!");
		}
	}
}
