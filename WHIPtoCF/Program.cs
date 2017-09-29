// Program.cs
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
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using InWorldz.Data.Assets.Stratus;
using libWHIPVFS;
using log4net;
using log4net.Config;
using Nini.Config;

namespace WHIPtoCF {
	class Application {
		private static readonly ILog LOG = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

		private static readonly string EXECUTABLE_DIRECTORY = Path.GetDirectoryName(Assembly.GetEntryAssembly().CodeBase.Replace("file:/", String.Empty));

		private static readonly string DEFAULT_INI_FILE = "whip2cf.ini";

		public static int Main(string[] args) {
			// Add the arguments supplied when running the application to the configuration
			var configSource = new ArgvConfigSource(args);

			// Commandline switches
			configSource.AddSwitch("Startup", "inifile");
			configSource.AddSwitch("Startup", "logconfig");
			configSource.AddSwitch("Startup", "inputWhipDbFolder", "i");
			configSource.AddSwitch("Startup", "list", "l");
			configSource.AddSwitch("Startup", "help", "h");

			var startupConfig = configSource.Configs["Startup"];
			var startupKeys = startupConfig.GetKeys();

			// Configure nIni aliases and localles
			//Thread.CurrentThread.CurrentCulture = new System.Globalization.CultureInfo("en-US", true);

			configSource.Alias.AddAlias("On", true);
			configSource.Alias.AddAlias("Off", false);
			configSource.Alias.AddAlias("True", true);
			configSource.Alias.AddAlias("False", false);
			configSource.Alias.AddAlias("Yes", true);
			configSource.Alias.AddAlias("No", false);

			if (args.Length <= 0 || startupKeys.Contains("help")) {
				PrintHelp();
				return 0;
			}

			// Configure Log4Net
			var logConfigFile = startupConfig.GetString("logconfig", string.Empty);
			if (string.IsNullOrEmpty(logConfigFile)) {
				XmlConfigurator.Configure();
			}
			else {
				XmlConfigurator.Configure(new FileInfo(logConfigFile));
			}

			// Read in the ini file
			try {
				ReadConfigurationFromINI(configSource);
			}
			catch {
				Console.Error.WriteLine("Error reading configuration file. Check log for details.");
				return 1;
			}

			try {
				var task = Execute(configSource);
				Task.WaitAll(task);
			}
			catch (Exception e) {
				LOG.Fatal("Unhandled error during execution.", e);
				return 1;
			}

			return 0;
		}

		private static async Task Execute(IConfigSource configSource) {
			var startupConfig = configSource.Configs["Startup"];
			var vfs = new WHIPVFS(startupConfig.GetString("inputWhipDbFolder", string.Empty));

			// Using the Pipeline concept: https://msdn.microsoft.com/en-us/library/ff963548.aspx

			var taskFactory = new TaskFactory(TaskCreationOptions.LongRunning, TaskContinuationOptions.None);

			var dataBases = vfs.GetDatabases();
			var assetIndexRecordBuffer = new BlockingCollection<AssetIndexRecord>(4096); // Magic numbers that are pure guesswork. See https://msdn.microsoft.com/en-us/library/ff963548.aspx for how to guess them better.
			var assetIndexRecordBufferFiltered = new BlockingCollection<AssetIndexRecord>(2048);
			var assetBuffer = new BlockingCollection<StratusAsset>(1024);

			var cfConfig = configSource.Configs["CFSettings"];
			var username = cfConfig.GetString("Username", string.Empty);
			var apiKey = cfConfig.GetString("APIKey", string.Empty);
			var useInternalURL = cfConfig.GetBoolean("UseInternalURL", true);
			var defaultRegion = cfConfig.GetString("DefaultRegion", string.Empty);
			var containerPrefix = cfConfig.GetString("ContainerPrefix", string.Empty);

			using (var cloudFiles = new CloudFiles.AssetServer("default", username, apiKey, defaultRegion, useInternalURL, containerPrefix))
			using (var cts = new CancellationTokenSource()) {
				var cancellationToken = cts.Token;

				var readAssetIndexRecords = await taskFactory.StartNew(async () => {
					try {
						foreach (var db in dataBases) {
							if (cancellationToken.IsCancellationRequested) {
								break;
							}

							using (var indexReader = db.CreateIndexReader(AssetScope.Global)) {
								var records = await indexReader.GetAllAssetRecordsAsync(cancellationToken);

								foreach (var record in records) {
									if (cancellationToken.IsCancellationRequested) {
										break;
									}

									if (!record.Deleted) { // Deleted records are ignorable.
										assetIndexRecordBuffer.Add(record, cancellationToken);
									}
								}
							}

							using (var indexReader = db.CreateIndexReader(AssetScope.Local)) {
								var records = await indexReader.GetAllAssetRecordsAsync(cancellationToken);

								foreach (var record in records) {
									if (cancellationToken.IsCancellationRequested) {
										break;
									}

									if (!record.Deleted) { // Deleted records are ignorable.
										assetIndexRecordBuffer.Add(record, cancellationToken);
									}
								}
							}
						}
					}
					catch (Exception e) {
						// If an exception occurs, notify all other pipeline stages.
						cts.Cancel();
						if (!(e is OperationCanceledException)) {
							throw;
						}
					}
					finally {
						assetIndexRecordBuffer.CompleteAdding();
					}
				});

				var filterAssetIndexRecords = taskFactory.StartNew(() => {
					try {
						foreach (var assetIndexRecord in assetIndexRecordBuffer.GetConsumingEnumerable()) {
							if (cancellationToken.IsCancellationRequested) {
								break;
							}

							// Check CF for the asset ID.  If it exists, move on.
							if (!cloudFiles.VerifyAssetIdSync(assetIndexRecord.Id)) {
								assetIndexRecordBufferFiltered.Add(assetIndexRecord, cancellationToken);
							}
						}
					}
					catch (Exception e) {
						// If an exception occurs, notify all other pipeline stages.
						cts.Cancel();
						if (!(e is OperationCanceledException)) {
							throw;
						}
					}
					finally {
						assetIndexRecordBuffer.CompleteAdding();
					}
				});

				var readAssets = await taskFactory.StartNew(async () => {
					try {
						foreach (var assetIndexRecord in assetIndexRecordBufferFiltered.GetConsumingEnumerable()) {
							if (cancellationToken.IsCancellationRequested) {
								break;
							}

							var prefix = assetIndexRecord.getPrefix();

							var database = dataBases.First(db => db.Prefix == prefix);

							using (var dataReader = database.CreateDataFileReader(assetIndexRecord.Scope)) {
								var asset = await dataReader.GetAssetAsync(assetIndexRecord, cancellationToken);

								var stratusAsset = new StratusAsset() {
									CreateTime = asset.GetCreateTime().DateTime,
									Data = asset.GetAssetData(),
									Description = asset.GetDescription(),
									Id = assetIndexRecord.Id,
									Local = asset.IsLocal(),
									Name = asset.GetName(),
									StorageFlags = 0, // From Halcyon CloudFilesAssetClient.cs::StoreAsset(AssetBase) near line 535: for now we're not going to use compression etc, so set to zero
									Temporary = asset.IsTemporary(),
									Type = (sbyte)asset.GetAssetType(),
								};

								assetBuffer.Add(stratusAsset, cancellationToken);
							}
						}
					}
					catch (Exception e) {
						// If an exception occurs, notify all other pipeline stages.
						cts.Cancel();
						if (!(e is OperationCanceledException)) {
							throw;
						}
					}
					finally {
						assetBuffer.CompleteAdding();
					}
				});

				var uploadAssets = taskFactory.StartNew(() => {
					try {
						foreach (var asset in assetBuffer.GetConsumingEnumerable()) {
							if (cancellationToken.IsCancellationRequested) {
								break;
							}

							cloudFiles.StoreAssetSync(asset);
						}
					}
					catch (Exception e) {
						// If an exception occurs, notify all other pipeline stages.
						cts.Cancel();
						if (!(e is OperationCanceledException)) {
							throw;
						}
					}
				});

				Task.WaitAll(readAssetIndexRecords, filterAssetIndexRecords, readAssets, uploadAssets);
			}
		}


		private static void PrintHelp() {
			Console.Error.Write($@"Cron's WHIP DB to CloudFiles transfer tool {Assembly.GetExecutingAssembly().GetName().Version}
Usage: whip2cf.exe [OPTION]... -i PATH

Mandatory arguments to long options are mandatory for short options too.

Startup:
  -h,  --help                      print this help

Logging and config file: (optional)
       --inifile=FILE              specify config file to use
       --logconfig=FILE            read logging configuration from FILE

Control:
  -i,  --inputWhipDbFolder=PATH    path to WHIP DB folder
  -l,  --list                      only list the asset IDs in the WHIP DB

Server settings for CF are in the INI file.

If --inifile is not specified the file name serached for is
  asset_transfer_tool.ini

INI search starts with the path as given to --inifile, which if it is relative
  will start at the CWD. If that was not found, was unreadable, or had an error
  the executable's folder is prepended and the tried again.

Log file location and name is specified in the config file.

List mode, --list, writes all the asset IDs to STDOUT.  Sort order is not
  guaranteed.
");
		}

		private static void ReadConfigurationFromINI(IConfigSource configSource) {
			var startupConfig = configSource.Configs["Startup"];
			var iniFileName = startupConfig.GetString("inifile", DEFAULT_INI_FILE);

			var found_at_given_path = false;

			try {
				startupConfig.ConfigSource.Merge(new IniConfigSource(iniFileName));
				found_at_given_path = true;
			}
			catch {
				LOG.Warn($"[MAIN] Failure reading configuration file at {Path.GetFullPath(iniFileName)}");
			}

			if (!found_at_given_path) {
				// Combine with true path to binary and try again.
				iniFileName = Path.Combine(EXECUTABLE_DIRECTORY, iniFileName);

				try {
					startupConfig.ConfigSource.Merge(new IniConfigSource(iniFileName));
				}
				catch {
					LOG.Fatal($"[MAIN] Failure reading configuration file at {Path.GetFullPath(iniFileName)}");
					throw;
				}
			}
		}
	}
}
