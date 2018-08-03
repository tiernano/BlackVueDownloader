using BlackVueDownloader.PCL;
using System;
using System.Threading;

namespace BlackVueDownloader.TestConsoleApp
{
	class Program
	{
		static void Main(string[] args)
		{

			var progress = new Progress<Tuple<double, string, string, string>>();

			progress.ProgressChanged += (sender, value) =>
			{
				Console.Write("\r%{0:N0}\t Total Downloaded: {1}\t Total Size: {2}\t Per Second: {3}\t", value.Item1, value.Item3, value.Item2, value.Item4);
			};
			var cancellationToken = new CancellationToken();

			BlackVueDownloader.PCL.BlackVueDownloader downloader = new PCL.BlackVueDownloader();
			downloader.DownloadFileFromWebAsync(args[0], progress, cancellationToken, @"c:\temp\test.file").Wait();
		}		
	}
}
