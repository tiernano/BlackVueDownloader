using CommandLine;
using System;
using System.Collections.Generic;
using System.Text;

namespace BlackVueDownloader.PCL
{
	public class DownloadOptions
	{
		[Option('i', "ip", Required = true, HelpText = "IP Address of BlackVue camera")]
		public string IPAddr { get; set; }

		[Option('d', "directory", Required = true, HelpText = "The Local directory you want files written to")]
		public string OutputDirectory { get; set; }

		[Option('l', "last", Required = false, HelpText = "Download Last X days of files")]
		public int? LastDays { get; set; }
	}
}
