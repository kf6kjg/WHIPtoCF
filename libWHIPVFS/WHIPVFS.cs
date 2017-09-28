// WHIPVFS.cs
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
using System.Diagnostics.Contracts;
using System.IO;
using System.Linq;
using System.Reflection;
using log4net;

namespace libWHIPVFS {
	public class WHIPVFS {
		private static readonly ILog LOG = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

		private string _folderPath;
		
		public WHIPVFS(string folderPath) {
			Contract.Requires(folderPath != null);
			Contract.Requires(Directory.Exists(folderPath), "Specified folder does not exist!"); // Still might not later, but an upfront check is not unreasonable.

			_folderPath = folderPath;
		}

		public IEnumerable<VFSDatabase> GetDatabases() {
			try {
				return Directory.EnumerateDirectories(_folderPath, "???").Select(prefixFolder => new VFSDatabase(prefixFolder));
			}
			catch (Exception e) {
				LOG.Error("Exception enumerating VFS directories.", e);
				throw;
			}
		}
	}
}
