
using System;
using System.Collections.Generic;
using System.Text;

namespace BlackVueDownloader.PCL
{
	public class DownloadOptions
	{		
		public string IPAddr { get; set; }
		public string OutputDirectory { get; set; }
		public int? LastDays { get; set; }
		public bool UseDateFolders { get; set; }
	}
}
