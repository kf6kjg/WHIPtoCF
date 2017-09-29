// AssetServerCF.cs
//
// Author:
//       Ricky Curtice <ricky@rwcproductions.com>
//
// Copyright (c) 2017 Richard Curtice
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
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using InWorldz.Data.Assets.Stratus;
using log4net;
using net.openstack.Core.Domain;
using net.openstack.Core.Exceptions.Response;

namespace CloudFiles {
	internal class AssetServer : IDisposable {
		private static readonly ILog LOG = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

		private const int DEFAULT_READ_TIMEOUT = 45 * 1000;
		private const int DEFAULT_WRITE_TIMEOUT = 10 * 1000;
		/// <summary>
		/// How many hex characters to use for the CF container prefix
		/// </summary>
		private const int CONTAINER_UUID_PREFIX_LEN = 4;

		private CloudIdentity _cloudIdentity;
		private string _defaultRegion;
		private bool _useInternalURL;
		private string _containerPrefix;

		private string _serverHandle { get; set; }

		private InWorldz.Data.Assets.Stratus.CoreExt.ExtendedCloudFilesProvider _provider;

		public AssetServer(string serverTitle, string username, string apiKey, string defaultRegion, bool useInternalUrl, string containerPrefix) {
			_serverHandle = serverTitle;

			_defaultRegion = defaultRegion;
			_useInternalURL = useInternalUrl;
			_containerPrefix = containerPrefix;

			_cloudIdentity = new CloudIdentity { Username = username, APIKey = apiKey };
			var restService = new InWorldz.Data.Assets.Stratus.CoreExt.ExtendedJsonRestServices(DEFAULT_READ_TIMEOUT, DEFAULT_WRITE_TIMEOUT);
			_provider = new InWorldz.Data.Assets.Stratus.CoreExt.ExtendedCloudFilesProvider(_cloudIdentity, _defaultRegion, null, restService);

			//warm up
			_provider.GetAccountHeaders(useInternalUrl: _useInternalURL, region: _defaultRegion);

			LOG.Info($"[CF] [{_serverHandle}] CF connection prepared for region '{_defaultRegion}' and prefix '{_containerPrefix}' under user '{_cloudIdentity.Username}'.");
		}

		public void StoreAssetSync(StratusAsset asset) {
			if (asset == null) throw new ArgumentNullException(nameof(asset));
			if (asset.Id == Guid.Empty) throw new ArgumentException("Assets must not have a zero ID");

			using (var memStream = new MemoryStream()) {
				try {
					ProtoBuf.Serializer.Serialize(memStream, asset);
					memStream.Position = 0;

					var assetIdStr = asset.Id.ToString();

					var mheaders = GenerateStorageHeaders(asset, memStream);

					WarnIfLongOperation("CreateObject",
						() => _provider.CreateObject(
							GenerateContainerName(asset.Id),
							memStream,
							GenerateAssetObjectName(asset.Id),
							"application/octet-stream",
							headers: mheaders,
							useInternalUrl: _useInternalURL,
							region: _defaultRegion
						)
					);
				}
				catch (ResponseException e) {
					if (e.Response.StatusCode == System.Net.HttpStatusCode.PreconditionFailed) {
						throw new AssetExistsException(asset.Id, e);
					}

					throw new AssetWriteException(asset.Id, e);
				}
			}
		}

		public bool VerifyAssetIdSync(Guid assetId) {
			using (var memStream = new MemoryStream()) {
				try {
					WarnIfLongOperation($"GetObjectMetaData for {assetId}", () => _provider.GetObjectMetaData(
						GenerateContainerName(assetId),
						GenerateAssetObjectName(assetId),
						_defaultRegion,
						_useInternalURL
					));
				}
				catch {
					return false;
				}

				return true;
			}
		}	

		/// <summary>
		/// CF containers are PREFIX_#### where we use the first N chars of the hex representation
		/// of the asset ID to partition the space. The hex alpha chars in the container name are uppercase.
		/// </summary>
		/// <param name="assetId"></param>
		/// <returns></returns>
		private string GenerateContainerName(Guid assetId) {
			return _containerPrefix + assetId.ToString("N").Substring(0, CONTAINER_UUID_PREFIX_LEN).ToUpper();
		}

		/// <summary>
		/// The object name is defined by the assetId, dashes stripped, with the .asset prefix.
		/// </summary>
		/// <param name="assetId"></param>
		/// <returns></returns>
		private static string GenerateAssetObjectName(Guid assetId) {
			return assetId.ToString("N") + ".asset";
		}

		private Dictionary<string, string> GenerateStorageHeaders(StratusAsset asset, MemoryStream stream) {
			//the HTTP headers only accept letters and digits
			var fixedName = new StringBuilder();
			var appended = false;
			foreach (var letter in asset.Name) {
				var c = (char)(0x000000ff & (uint)letter);
				if (c == 127 || (c < ' ' && c != '\t')) {
					continue;
				}

				fixedName.Append(letter);
				appended = true;
			}

			if (!appended) {
				fixedName.Append("empty");
			}

			var headers = new Dictionary<string, string> {
				{"ETag", Md5Hash(stream)},
				{"X-Object-Meta-Temp", asset.Temporary ? "1" : "0"},
				{"X-Object-Meta-Local", asset.Local ? "1" : "0"},
				{"X-Object-Meta-Type", asset.Type.ToString()},
				{"X-Object-Meta-Name", fixedName.ToString()},
				{"If-None-Match", "*"},
			};

			stream.Position = 0;

			return headers;
		}

		private static string Md5Hash(Stream data) {
			byte[] hash;
			using (var md5 = MD5.Create()) {
				hash = md5.ComputeHash(data);
			}
			return ByteArrayToHexString(hash);
		}

		private static string ByteArrayToHexString(byte[] dataMd5) {
			var sb = new StringBuilder();
			for (var i = 0; i < dataMd5.Length; i++) {
				sb.AppendFormat("{0:x2}", dataMd5[i]);
			}
			return sb.ToString();
		}

		private void WarnIfLongOperation(string opName, Action operation) {
			const long WARNING_TIME = 5000; // ms

			var stopwatch = new System.Diagnostics.Stopwatch();
			stopwatch.Start();
			operation();
			stopwatch.Stop();

			if (stopwatch.ElapsedMilliseconds >= WARNING_TIME) {
				LOG.Warn($"[CF_SERVER] [{_serverHandle}] Slow CF operation {opName} took {stopwatch.ElapsedMilliseconds} ms.");
			}
		}

		#region IDisposable Support
		private bool disposedValue; // To detect redundant calls

		protected virtual void Dispose(bool disposing) {
			if (!disposedValue) {
				if (disposing) {
					// dispose managed state (managed objects).
					_provider = null;
				}

				// TODO: free unmanaged resources (unmanaged objects) and override a finalizer below.
				// TODO: set large fields to null.

				disposedValue = true;
			}
		}

		// TODO: override a finalizer only if Dispose(bool disposing) above has code to free unmanaged resources.
		// ~AssetServerCF() {
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
