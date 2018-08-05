using BlackVueDownloader.PCL;
using System.ComponentModel.DataAnnotations;
using McMaster.Extensions.CommandLineUtils;
using McMaster.Extensions.CommandLineUtils.Validation;
using NLog;
using System;
using System.Reflection;

namespace BlackVueDownloader
{
	internal class Program
    {
	    private static void Main(string[] args)
	    {
		    var app = new CommandLineApplication();
		    app.HelpOption();

		    var ipAddress = app.Option("-i|--ipaddress <IPAddress>", "The IP of the dashcam",
			    CommandOptionType.SingleValue).IsRequired();

		    var destFolder = app.Option("-d|--destfolder <folder>", "Folder to put files into",
			    CommandOptionType.SingleValue).Accepts(x=>x.ExistingDirectory()).IsRequired();

		    var lastDays = app.Option<int>("-l|--lastdays <NumberOfDays>", "Number of days to download",
			    CommandOptionType.SingleValue).Accepts(o => o.Range(1, 50));

		    var useDateFolder = app.Option("-f|--datefolders",
			    "Use date format for folders, for example 2018-08-03 in the dest folder",
			    CommandOptionType.SingleOrNoValue);

		    var dontDownloadVideo = app.Option("-dv|--novideo", "only download GPS and 3GS files",
			    CommandOptionType.SingleOrNoValue);
			
			app.OnExecute(() =>
			{
				Logger logger = LogManager.GetCurrentClassLogger();

				var version = Assembly.GetEntryAssembly().GetName().Version.ToString();

				logger.Info($"BlackVue Downloader Version {version}");

				try
				{
					var blackVueDownloader = new PCL.BlackVueDownloader();
					
					DownloadOptions downloadOptions = new DownloadOptions()
					{
						LastDays = lastDays.ParsedValue,
						UseDateFolders = useDateFolder.HasValue(),
						OutputDirectory = destFolder.Value(),
						IPAddr = ipAddress.Value(),
						DontDownloadVideo = useDateFolder.HasValue()
					};
					blackVueDownloader.Run(downloadOptions);

				}
				catch (Exception e)
				{
					logger.Error($"General exception {e.Message}");
				}
			});

		    app.Execute(args);
	    }

		

		
    }

}
