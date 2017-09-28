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
using System.Threading.Tasks;
using log4net;
using log4net.Config;
using Nini.Config;

namespace WHIPtoCF {
	class Application {
		private static readonly ILog LOG = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

		private static readonly string EXECUTABLE_DIRECTORY = Path.GetDirectoryName(Assembly.GetEntryAssembly().CodeBase.Replace("file:/", String.Empty));

		private static readonly string DEFAULT_INI_FILE = "whip2cf.ini";

		private static readonly int DEFAULT_BUFFER_SIZE = 32;

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

			// Just write a tool that reads the WHIP DB direct and uses the CF bulk API.

			// Read the folder list - this gives the DB list.


			// Read in the byte arrays from each WHIP DB
			// if list mode, collect the IDs and dump them to STDOUT


			// ...

			// Create the needed folder structure, write the asset files into it, package the .tar.gz or .tar.bz2
			// https://github.com/adamhathcock/sharpcompress/wiki/API-Examples

			Environment.Exit(0);
			return 0;
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
