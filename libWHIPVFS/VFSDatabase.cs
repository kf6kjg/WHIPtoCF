// VFSDatabase.cs
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

using System.Diagnostics.Contracts;
using System.IO;

namespace libWHIPVFS {
	public class VFSDatabase {
		//private static readonly ILog LOG = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

		private string _prefixFolderPath;

		public string Prefix => _prefixFolderPath.Substring(_prefixFolderPath.Length - 3, 3);

		internal VFSDatabase(string prefixFolderPath) {
			Contract.Requires(prefixFolderPath != null);
			Contract.Requires(Directory.Exists(prefixFolderPath), "Specified prefixFolderPath does not exist!"); // Still might not later, but an upfront check is not unreasonable.

			_prefixFolderPath = prefixFolderPath;
		}

		public VFSIndex CreateIndexReader(AssetScope scope) {
			var indexDatabaseName = scope == AssetScope.Global ? "globals.idx" : "locals.idx";
			var indexDbPath = Path.Combine(_prefixFolderPath, indexDatabaseName);

			return new VFSIndex(indexDbPath, scope);
		}

		public VFSDataFile CreateDataFileReader(AssetScope scope) {
			var dataDatabaseName = scope == AssetScope.Global ? "globals.data" : "locals.data";
			var dataDbPath = Path.Combine(_prefixFolderPath, dataDatabaseName);

			return new VFSDataFile(dataDbPath);
		}
	}
}
