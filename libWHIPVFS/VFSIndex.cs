// VFSIndex.cs
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
using System.Collections.Concurrent;
using System.Collections.Generic;
using Mono.Data.Sqlite;
using System.Diagnostics.Contracts;
using System.IO;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using log4net;

namespace libWHIPVFS {
	public class VFSIndex : IDisposable {
		private static readonly ILog LOG = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

		private readonly string _indexDbPath;
		private readonly AssetScope _scope;
		private SqliteConnection _indexDbConnection;

		internal VFSIndex(string indexDbPath, AssetScope scope) {
			Contract.Requires(indexDbPath != null);
			Contract.Requires(Directory.Exists(indexDbPath), "Specified indexDbPath does not exist!"); // Still might not later, but an upfront check is not unreasonable.

			_indexDbPath = indexDbPath;

			LOG.Debug($"Initializing connection to Index DB '{_indexDbPath}'");

			_indexDbConnection = new SqliteConnection($"Data Source={_indexDbPath};Version=3;");

			_scope = scope;
		}

		public async Task<IEnumerable<AssetIndexRecord>> GetAllAssetRecordsAsync(CancellationToken cancellationToken = default(CancellationToken)) {
			var result = new ConcurrentBag<AssetIndexRecord>();

			try {
				LOG.Debug($"Opening Index DB '{_indexDbPath}'");
				await _indexDbConnection.OpenAsync(cancellationToken);

				var sql = "SELECT asset_id, position, type, created_on, deleted FROM VFSDataIndex";
				var command = new SqliteCommand(sql, _indexDbConnection);
				using (var reader = await command.ExecuteReaderAsync(cancellationToken)) {
					while (await reader.ReadAsync(cancellationToken)) {
						var idString = (string)reader["asset_id"];
						Guid id;
						if (!Guid.TryParse(idString, out id)) {
							LOG.Warn($"Found invalid asset ID '{idString}' in '{_indexDbPath}'.");
							continue;
						}

						/* BUG: Invalid cast exceptions...
						var createdOnString = (string)reader["created_on"];
						DateTimeOffset createdOn;
						if (!DateTimeOffset.TryParse(createdOnString, out createdOn)) {
							LOG.Warn($"Found invalid asset creation date '{createdOnString}' in '{_indexDbPath}'.");
							continue;
						}
						*/

						var position = (long)reader["position"];
						var type = (byte)(long)reader["type"]; // Don't you love SQLite?
						var deleted = (byte)reader["deleted"] == 1;

						var indexRecord = new AssetIndexRecord {
							//CreatedOn = createdOn,
							DataFilePosition = position,
							Deleted = deleted,
							Id = id,
							Scope = _scope,
							Type = type,
						};

						result.Add(indexRecord);
					}
				}
				_indexDbConnection.Close();
				LOG.Debug($"Closed Index DB '{_indexDbPath}'");
			}
			catch (Exception e) {
				LOG.Error($"Error reading all asset index records from Index DB '{_indexDbPath}'", e);
				throw;
			}

			return result;
		}

		#region IDisposable Support
		private bool disposedValue = false; // To detect redundant calls

		protected virtual void Dispose(bool disposing) {
			if (!disposedValue) {
				if (disposing) {
					// dispose managed state (managed objects).

					_indexDbConnection.Dispose();
				}

				// TODO: free unmanaged resources (unmanaged objects) and override a finalizer below.
				// TODO: set large fields to null.

				disposedValue = true;
			}
		}

		// TODO: override a finalizer only if Dispose(bool disposing) above has code to free unmanaged resources.
		// ~VFSIndex() {
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
