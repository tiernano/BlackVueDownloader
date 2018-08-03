using BlackVueDownloader.PCL;
using System;
using System.Threading;

namespace BlackVueDownloader.TestConsoleApp
{
	class Program
	{
		static void Main(string[] args)
		{

			var progress = new Progress<string>();

			progress.ProgressChanged += (sender, value) =>
			{
				Console.Write($"\r{value}");
			};
			var cancellationToken = new CancellationToken();

			BlackVueDownloader.PCL.BlackVueDownloader downloader = new PCL.BlackVueDownloader();
			downloader.DownloadFileFromWebAsync(args[0], progress, cancellationToken, @"c:\temp\test.file").Wait(cancellationToken);
		}		
	}
}
