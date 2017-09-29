// VFSDataFile.cs
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
using System.Diagnostics.Contracts;
using System.IO;
using System.IO.MemoryMappedFiles;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using log4net;

namespace libWHIPVFS {
	public class VFSDataFile : IDisposable {
		private static readonly ILog LOG = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

		private readonly string _dataDbPath;
		private MemoryMappedFile _mmf;

		internal VFSDataFile(string dataDbPath) {
			Contract.Requires(Directory.Exists(dataDbPath), "Specified dataDbPath does not exist!"); // Still might not later, but an upfront check is not unreasonable.

			_dataDbPath = dataDbPath;

			LOG.Debug($"Initializing connection to Data DB '{_dataDbPath}'");

			_mmf = MemoryMappedFile.CreateFromFile(_dataDbPath, FileMode.Open);
		}

		public async Task<Asset> GetAssetAsync(AssetIndexRecord indexRecord, CancellationToken cancellationToken = default(CancellationToken)) {
			Contract.Requires(indexRecord != null);
			Contract.Requires(indexRecord.DataFilePosition > 0);

			LOG.Debug($"Reading asset ID '{indexRecord.Id}' from Data DB '{_dataDbPath}'");

			try {
				int assetLength;

				using (var stream = _mmf.CreateViewStream(indexRecord.DataFilePosition, 4)) {
					// Read the asset size (first 4 bytes) stored in network byte order
					var lengthBytes = new byte[4];
					await stream.ReadAsync(lengthBytes, 0, 4, cancellationToken);
					if (BitConverter.IsLittleEndian) {
						Array.Reverse(lengthBytes);
					}
					assetLength = BitConverter.ToInt32(lengthBytes, 0);
				}
				using (var stream = _mmf.CreateViewStream(indexRecord.DataFilePosition + 4, assetLength)) {
					// Read in the asset
					var assetData = new byte[assetLength];
					await stream.ReadAsync(assetData, 0, assetLength, cancellationToken);

					return new Asset {
						RawData = assetData,
					};
				}
			}
			catch (Exception e) {
				LOG.Error($"Error reading asset ID '{indexRecord.Id}' from Data DB '{_dataDbPath}'", e);
				throw;
			}
		}

		#region IDisposable Support
		private bool disposedValue = false; // To detect redundant calls

		protected virtual void Dispose(bool disposing) {
			if (!disposedValue) {
				if (disposing) {
					// dispose managed state (managed objects).

					_mmf.Dispose();
				}

				// TODO: free unmanaged resources (unmanaged objects) and override a finalizer below.
				// TODO: set large fields to null.

				disposedValue = true;
			}
		}

		// TODO: override a finalizer only if Dispose(bool disposing) above has code to free unmanaged resources.
		// ~VFSDatabase() {
		//   // Do not change this code. Put cleanup code in Dispose(bool disposing) above.
		//   Dispose(false);
		// }

		// This code added to correctly implement the disposable pattern.
		public void Dispose() {
			// Do not change this code. Put cleanup code in Dispose(bool disposing) above.
			Dispose(true);
			// TODO: uncomment the following line if the finalizer is overridden above.
			// GC.SuppressFinalize(this);
		}
		#endregion
	}
}