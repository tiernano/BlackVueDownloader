using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using ByteSizeLib;
using Flurl.Http;
using NLog;

namespace BlackVueDownloader.PCL
{
    public static class BlackVueDownloaderExtensions
    {
        public const string FileSeparator = "\r\n";

        // Extension method to parse file list response into string array
        public static string [] ParseBody(this string s)
        {
            return s.Replace($"v:1.00{FileSeparator}", "").Replace($"v:2.00{FileSeparator}", "").Replace(FileSeparator, " ").Split(' ');
        }
    }

    public class BlackVueDownloader
    {
        private readonly IFileSystemHelper _fileSystemHelper;
        public BlackVueDownloaderCopyStats BlackVueDownloaderCopyStats;
	    Logger logger = LogManager.GetCurrentClassLogger();

		/// <summary>
		/// Instance Downloader with Moq friendly constructor
		/// </summary>
		/// <param name="fileSystemHelper"></param>
		public BlackVueDownloader(IFileSystemHelper fileSystemHelper)
        {
            _fileSystemHelper = fileSystemHelper;
            BlackVueDownloaderCopyStats = new BlackVueDownloaderCopyStats();
        }

        /// <summary>
        /// Instance Downloader with base constructor
        /// </summary>
        public BlackVueDownloader() : this (new FileSystemHelper()) {}

        /// <summary>
        /// Main control flow
        /// </summary>
        /// <param name="ip"></param>
        /// <param name="directory"></param>
        public void Run(DownloadOptions options)
        {
            var body = QueryCameraForFileList(options.IPAddr);
            var list = GetListOfFilesFromResponse(body);
			if (options.LastDays.HasValue)
			{
				list = FilterList(list, options.LastDays);
			}

            var tempdir = Path.Combine(options.OutputDirectory, "_tmp");
            var targetdir = Path.Combine(options.OutputDirectory, "Record");

            CreateDirectories(tempdir, targetdir);

            ProcessList(options.IPAddr, list, tempdir, targetdir);
        }

		public List<Tuple<string, DateTime>> FilterList(List<Tuple<string,DateTime>> input, int? lastDays)
		{
			DateTime startDate = DateTime.Now;
			DateTime endDate = startDate.AddDays(-1);
			if (lastDays.HasValue)
			{
				endDate = startDate.AddDays(lastDays.Value * -1);
			}

			return FilterList(input, startDate, endDate);
		}

		public List<Tuple<string, DateTime>> FilterList(List<Tuple<string, DateTime>> input, DateTime startDate, DateTime endDate)
		{
			List<Tuple<string, DateTime>> resultList = (from x in input
														where x.Item2 >= startDate && x.Item2 <= endDate
														select x).ToList();
			return resultList;
		}

		public void CreateDirectories(string tempdir, string targetdir)
        {
            if (!_fileSystemHelper.DirectoryExists(tempdir))
                _fileSystemHelper.CreateDirectory(tempdir);

            if (!_fileSystemHelper.DirectoryExists(targetdir))
                _fileSystemHelper.CreateDirectory(targetdir);
        }

        public static bool IsValidIp(string ip)
        {
	        return IPAddress.TryParse(ip, out _);
        }

        /// <summary>
        /// Connect to the camera and get a list of files
        /// </summary>
        /// <param name="body"></param>
        /// <returns>Normalized list of files</returns>
        public List<Tuple<string, DateTime>> GetListOfFilesFromResponse(string body)
        {
            // Strip the header. Parse each element of the body, strip the non-filename part, and return a list.
            var files =  body.ParseBody().Select(e => e.Replace("n:/Record/", "").Replace(",s:1000000", "")).ToList();

			List<Tuple<string, DateTime>> result = new List<Tuple<string, DateTime>>();

			foreach(var x in files)
			{
				string[] parts = x.Split('_');

				string datePart = parts[0];
				string timePart = parts[1];

				string overall = $"{datePart} {timePart}";

				var date =  DateTime.ParseExact(overall, "yyyyMMdd HHmmss", CultureInfo.InvariantCulture);
				result.Add(new Tuple<string, DateTime>(x, date));
			}

	        return result.OrderBy(x => x.Item2).ToList();
        }

		/// <summary>
		/// For given camera ip, filename, and filetype, download the file and return a status
		/// </summary>
		/// <param name="ip"></param>
		/// <param name="filename"></param>
		/// <param name="filetype"></param>
		/// <param name="tempdir"></param>
		/// <param name="targetdir"></param>
		public void DownloadFile(string ip, string filename, string filetype, string tempdir, string targetdir)
		{
			string filepath;
			string tempFilepath;

			try
			{
				filepath = Path.Combine(targetdir, filename);
			}
			catch (Exception e)
			{
				logger.Error($"Path Combine exception for filepath, filename {filename}, Exception Message: {e.Message}");
				BlackVueDownloaderCopyStats.Errored++;
				return;
			}

			try
			{
				tempFilepath = Path.Combine(tempdir, filename);
			}
			catch (Exception e)
			{
				logger.Error($"Path Combine exception for temp_filepath, filename {filename}, Exception Message: {e.Message}");
				BlackVueDownloaderCopyStats.Errored++;
				return;
			}

			if (_fileSystemHelper.Exists(filepath))
			{
				logger.Info($"File exists {filepath}, ignoring");
				BlackVueDownloaderCopyStats.Ignored++;
			}
			else
			{
				try
				{
					var url = $"http://{ip}/Record/{filename}";

					var tempfile = Path.Combine(tempdir, filename);
					var targetfile = Path.Combine(targetdir, filename);

					// If it already exists in the _tmp directory, delete it.
					if (_fileSystemHelper.Exists(tempFilepath))
					{
						logger.Info($"File exists in tmp {tempFilepath}, deleting");
						BlackVueDownloaderCopyStats.TmpDeleted++;
						_fileSystemHelper.Delete(tempFilepath);
					}

					// Download to the temp directory, that way, if the file is partially downloaded,
					// it won't leave a partial file in the target directory
					logger.Info($"Downloading {filetype} file: {url}");
					Stopwatch st = Stopwatch.StartNew();

					var progress = new Progress<string>();

					progress.ProgressChanged += (sender, value) =>
					{
						Console.Write(value);
					};

					var cancellationToken = new CancellationTokenSource();

					DownloadFileFromWebAsync(url, progress, cancellationToken.Token, tempFilepath).Wait(cancellationToken.Token);

					st.Stop();
					BlackVueDownloaderCopyStats.DownloadingTime = BlackVueDownloaderCopyStats.DownloadingTime.Add(st.Elapsed);

					FileInfo fi = new FileInfo(tempfile);

					BlackVueDownloaderCopyStats.TotalDownloaded += fi.Length;

					// File downloaded. Move from temp to target.
					_fileSystemHelper.Move(tempfile, targetfile);

					logger.Info($"Downloaded {filetype} file: {url}");
					BlackVueDownloaderCopyStats.Copied++;
				}
				catch (FlurlHttpTimeoutException e)
				{
					logger.Error($"FlurlHttpTimeoutException: {e.Message}");
					BlackVueDownloaderCopyStats.Errored++;
				}
				catch (FlurlHttpException e)
				{
					if (e.Call.Response != null)
					{
						logger.Error($"Failed with response code: {e.Call.Response.StatusCode}");
					}
					Console.Write($"Failed before getting a response: {e.Message}");
					BlackVueDownloaderCopyStats.Errored++;
				}
				catch (Exception e)
				{
					logger.Error($"Exception: {e.Message}");
					BlackVueDownloaderCopyStats.Errored++;
				}
			}
		}

		/// <summary>
		/// For the list, loop through and process it
		/// </summary>
		/// <param name="ip"></param>
		/// <param name="list"></param>
		/// <param name="tempdir"></param>
		/// <param name="targetdir"></param>
		public void ProcessList(string ip, List<Tuple<string, DateTime>> list, string tempdir, string targetdir)
        {
            var sw = new Stopwatch();
            sw.Start();

            // The list includes _NF and _NR files.
            // Loop through and download each, but also try and download .gps and .3gf files
            foreach (var s in list)
            {
                logger.Info($"Processing File: {s}");

                DownloadFile(ip, s.Item1, "video", tempdir, targetdir);

                // Line below because the list may include _NF and _NR named files.  Only continue if it's an NF.
                // Otherwise it's trying to download files that are probably already downloaded
                if (!s.Item1.Contains("_NF.mp4")) continue;

				// Make filenames for accompanying gps file
                var gpsfile = s.Item1.Replace("_NF.mp4", "_N.gps");
                DownloadFile(ip, gpsfile, "gps", tempdir, targetdir);

				// Make filenames for accompanying gff file
				var gffile = s.Item1.Replace("_NF.mp4", "_N.3gf");
                DownloadFile(ip, gffile, "3gf", tempdir, targetdir);
            }

            sw.Stop();
            BlackVueDownloaderCopyStats.TotalTime = sw.Elapsed;

            logger.Info(
                $"Copied {BlackVueDownloaderCopyStats.Copied}, Ignored {BlackVueDownloaderCopyStats.Ignored}, Errored {BlackVueDownloaderCopyStats.Errored}, TmpDeleted {BlackVueDownloaderCopyStats.TmpDeleted}, TotalTime {BlackVueDownloaderCopyStats.TotalTime}");

	        logger.Info(
		        $"Downloaded {ByteSize.FromBytes(BlackVueDownloaderCopyStats.TotalDownloaded).ToString()} in {BlackVueDownloaderCopyStats.DownloadingTime}");
        }

        /// <summary>
        /// Get a raw string response from the camera
        /// </summary>
        /// <param name="ip"></param>
        /// <returns>Raw string list of files</returns>
        public string QueryCameraForFileList(string ip)
        {
            try
            {
                var url = $"http://{ip}/blackvue_vod.cgi";

                var fileListBody = url.GetStringAsync();
                fileListBody.Wait();

                var content = fileListBody.Result;

                return content;
            }
            catch (FlurlHttpTimeoutException e)
            {
                throw new Exception(e.Message);
            }
            catch (FlurlHttpException e)
            {
                if (e.Call.Response != null)
                {
                    throw new Exception($"Failed with response code : {e.Call.Response.StatusCode}");
                }
                throw new Exception($"Failed before getting a response: {e.Message}");
            }
            catch (Exception e)
            {
                throw new Exception(e.Message);
            }
        }

		public async Task DownloadFileFromWebAsync(string url, IProgress<string> progress, CancellationToken token, string outputFile)
		{
			WebClient client = new WebClient();

			client.DownloadProgressChanged += (sender, args) =>
			{
				progress.Report(
					$"Total: {args.TotalBytesToReceive} \t Transfered: {args.BytesReceived} \t ({args.ProgressPercentage}%)");
			};

			await client.DownloadFileTaskAsync(new Uri(url), outputFile);

			//var progressMsgHandler = new ProgressMessageHandler(new HttpClientHandler());

			//progressMsgHandler.HttpReceiveProgress += (sender, e) =>
			//{
			//	progress.Report(
			//		$"Total: {e.TotalBytes} \t Transfered: {e.BytesTransferred} \t ({e.ProgressPercentage}%)");
			//};


			//var client = new HttpClient(progressMsgHandler);

			//using (HttpResponseMessage response = await client.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, token))
			//using (Stream streamToReadFrom = await response.Content.ReadAsStreamAsync())
			//{
			//	if (!response.IsSuccessStatusCode)
			//	{
			//		throw new Exception(string.Format("The request returned with HTTP status code {0}", response.StatusCode));
			//	}

			//	string fileToWriteTo = Path.GetTempFileName();
			//	using (Stream streamToWriteTo = File.Open(fileToWriteTo, FileMode.Create))
			//	{
			//		await streamToReadFrom.CopyToAsync(streamToWriteTo, token);
			//	}
			//}
		}
	}
}