using System;
using System.ComponentModel.DataAnnotations;
using System.IO;
using System.Reflection;
using BlackVueDownloader.PCL;
using McMaster.Extensions.CommandLineUtils;
using NLog;

namespace BlackVueDownloader
{
    internal class Program
    {
		private static int Main(string[] args)
			=> CommandLineApplication.Execute<Program>(args);

		[Required]
		[Option(Description = "Required: IP Address", LongName = "ipaddress", ShortName = "ip",  ShowInHelpText = true)]
		public string IPAddress { get; set; }

		[Required]
		[Option(Description = "Required: Destination Folder", LongName = "destfolder", ShortName = "dest", ShowInHelpText = true)]
		public string DestinationFolder { get; set; }

		[Option(Description ="Download Files for the last X Days", LongName = "lastdays", ShortName ="days", ShowInHelpText =true)]
		public int LastDays { get; set; }

		private void OnExecute()
		{
			Logger logger = LogManager.GetCurrentClassLogger();

			var version = Assembly.GetEntryAssembly().GetName().Version.ToString();

			logger.Info($"BlackVue Downloader Version {version}");
			
			try
			{
				var blackVueDownloader = new PCL.BlackVueDownloader();

				DownloadOptions downloadOptions = new DownloadOptions()
				{
					LastDays = LastDays,
					OutputDirectory = DestinationFolder,
					IPAddr = IPAddress
				};
				blackVueDownloader.Run(downloadOptions);
				
			}
			catch (Exception e)
			{
				logger.Error($"General exception {e.Message}");
				
			}
		}

		
    }
}
