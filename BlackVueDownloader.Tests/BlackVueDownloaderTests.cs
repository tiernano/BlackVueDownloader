using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using BlackVueDownloader.PCL;
using Flurl.Http.Testing;
using Moq;
using Xunit;
using Xunit.Abstractions;

namespace BlackVueDownloader.Tests
{
    public class BlackVueDownloaderTests
    {
        // ReSharper disable once NotAccessedField.Local
        private readonly ITestOutputHelper _output;
		
        public BlackVueDownloaderTests(ITestOutputHelper output)
        {
            _output = output;		
        }

		public static string GenerateRecords(string inputDate, int numRecords, int days)
		{
			string ret = null;

			var date = DateTime.Now;
			
			if (!string.IsNullOrEmpty(inputDate))
			{
				date = DateTime.Parse(inputDate);
			}

			string dateString = date.ToString("yyyyMMdd");

			int time = 120101;
			int daysLeft = days;

			for (var i = 0; i < numRecords; i++)
			{
				ret += $"n:/Record/{dateString}_{time + i}_NF.mp4,s:1000000{BlackVueDownloaderExtensions.FileSeparator}";
				ret += $"n:/Record/{dateString}_{time + i}_NR.mp4,s:1000000";
				if (i + 1 < numRecords)
					ret += BlackVueDownloaderExtensions.FileSeparator;

				if (daysLeft > 1)
				{
					date = date.AddDays(1);
					dateString = date.ToString("yyyyMMdd");
					daysLeft--;
				}
			}

			return ret;
		}

		[Theory]
        [InlineData("192.168.1.1")]
        [InlineData("192.168.1")]
        public void IsValidIpTheory(string ip)
        {
            Assert.True(PCL.BlackVueDownloader.IsValidIp(ip));
        }

        [Theory]
        [InlineData("19-2")]
        [InlineData("not-a good ip address!")]
        public void IsInValidIpTheory(string ip)
        {
            Assert.False(PCL.BlackVueDownloader.IsValidIp(ip));
        }

        [Theory]
		[ClassData(typeof(BlackVueDownloadTestDataStrings))]
        public void GetListOfFilesFromResponseTest(string body, string firstval, int numelements)
        {
            var blackVueDownloader = new PCL.BlackVueDownloader();

            var list = blackVueDownloader.GetListOfFilesFromResponse(body);
            Assert.Equal(numelements, list.Count);
            Assert.Equal(firstval, list[0].Item1);
        }


		[Theory]
		[ClassData(typeof(BlackVueDownloadTestDataDates))]
		public void GetDatesFromFiles(string body, DateTime firstVal, int numElements)
		{
			var blackVueDownloader = new PCL.BlackVueDownloader();

			var list = blackVueDownloader.GetListOfFilesFromResponse(body);

			Assert.Equal(firstVal, list[0].Item2);

			Assert.Equal(numElements, list.Count);
		}

		[Theory]
		[ClassData(typeof(BlackVueDownloadTestDataFilterDates))]
		public void TestDateFilder(string body, DateTime firstVal, int numElements)
		{
			var blackVueDownloader = new PCL.BlackVueDownloader();

			var list = blackVueDownloader.GetListOfFilesFromResponse(body);

			var filteredList = blackVueDownloader.FilterList(list, firstVal, firstVal.AddDays(0));

			Assert.Equal(firstVal, list[0].Item2);

			Assert.Equal(numElements, filteredList.Count);
			
		}

		[Theory]
        [InlineData("192.168.1.1")]
        public void QueryCameraForFileListTest(string ip)
        {
            var blackVueDownloader = new PCL.BlackVueDownloader();

            using (var httpTest = new HttpTest())
            {
                httpTest.RespondWith("this is the body");

                var body = blackVueDownloader.QueryCameraForFileList(ip);

                httpTest.ShouldHaveCalled($"http://{ip}/blackvue_vod.cgi");

                Assert.True(!string.IsNullOrEmpty(body));
            }
        }

        [Theory]
        [InlineData("192.168.1.1")]
        public void EmptyResponseTest(string ip)
        {
            var blackVueDownloader = new PCL.BlackVueDownloader();

            using (var httpTest = new HttpTest())
            {
                httpTest.RespondWith("");

                var body = blackVueDownloader.QueryCameraForFileList(ip);

                httpTest.ShouldHaveCalled($"http://{ip}/blackvue_vod.cgi");

                Assert.True(string.IsNullOrEmpty(body));
            }
        }

        [Theory]
        [InlineData("192.168.1.1")]
        public void InvalidResponseTest(string ip)
        {
            var blackVueDownloader = new PCL.BlackVueDownloader();

            using (var httpTest = new HttpTest())
            {
                httpTest.RespondWith("Simulated Error", 500);

                try
                {
                    blackVueDownloader.QueryCameraForFileList(ip);
                }
                catch (Exception e)
                {
                    Assert.StartsWith("One or more errors occurred.", e.Message);
                }

                httpTest.ShouldHaveCalled($"http://{ip}/blackvue_vod.cgi");
            }
        }

        [Theory]
        [InlineData("192.168.1.99")]
        public void CantFindCameraTest(string ip)
        {
            var blackVueDownloader = new PCL.BlackVueDownloader();

            using (var httpTest = new HttpTest())
            {
                httpTest.SimulateTimeout();

                try
                {
                    blackVueDownloader.QueryCameraForFileList(ip);
                }
                catch (Exception e)
                {
                    Assert.StartsWith("One or more errors occurred.", e.Message);
                }

                httpTest.ShouldHaveCalled($"http://{ip}/blackvue_vod.cgi");
            }
        }

        [Theory]
        [InlineData("192.168.1.99", 10)]
        public void CameraRespondValidTest(string ip, int numRecords)
        {
            var blackVueDownloader = new PCL.BlackVueDownloader();

            using (var httpTest = new HttpTest())
            {
                httpTest.RespondWith(GenerateRecords("04/04/2016", numRecords, 1));

                var body = blackVueDownloader.QueryCameraForFileList(ip);

                httpTest.ShouldHaveCalled($"http://{ip}/blackvue_vod.cgi");

                Assert.True(!string.IsNullOrEmpty(body));

                if (body != null) Assert.Equal(numRecords*2, body.ParseBody().Length);
            }
        }

        [Theory]
        [InlineData("192.168.1.99", 10)]
        [InlineData("192.168.1.99", 1)]
        public void GetListOfFilesAndProcessTest(string ip, int numRecords)
        {
            var targetdir = Path.Combine(Directory.GetCurrentDirectory(), "Record");

            var filesystem = new Mock<IFileSystemHelper>();

            var blackVueDownloader = new PCL.BlackVueDownloader(filesystem.Object);
            var blackVueDownloaderNoMock = new PCL.BlackVueDownloader();

            blackVueDownloaderNoMock.CreateDirectories(targetdir, targetdir);

            var httpTest = new HttpTest();

            var list = blackVueDownloader.GetListOfFilesFromResponse(GenerateRecords("04/04/2016", numRecords,1));

            Assert.Equal(numRecords*2, list.Count);

            // Success test
            for (var i = 0; i < numRecords*4; i++)
            {
                httpTest.RespondWith("OK");
            }
            blackVueDownloader.BlackVueDownloaderCopyStats.Clear();

	        DownloadOptions opt = new DownloadOptions
	        {
		        IPAddr = ip,
		        OutputDirectory = targetdir,
		        TempDir = targetdir,
		        UseDateFolders = true
	        };
            blackVueDownloader.ProcessList(opt, list);
            Assert.Equal(numRecords*4, blackVueDownloader.BlackVueDownloaderCopyStats.Copied);

            // Ignored from above test
            // What happens with the above tests, is that it writes actual files to
            // BlackVueDownloader.Tests\bin\Debug\Record directory,
            // so there should be numrecords * 4 files there
            // And if we loop through again, they should all exist, and therefore be "ignored"
            // We need to do this with an unmocked version of the file system helper
            blackVueDownloaderNoMock.BlackVueDownloaderCopyStats.Clear();

			blackVueDownloaderNoMock.ProcessList(opt,list);
            Assert.Equal(numRecords*4, blackVueDownloaderNoMock.BlackVueDownloaderCopyStats.Ignored);

            // Fail test
            for (var i = 0; i < numRecords*4; i++)
            {
                httpTest.RespondWith("FAILURE", 500);
            }
            blackVueDownloader.BlackVueDownloaderCopyStats.Clear();

	      

	        blackVueDownloader.ProcessList(opt, list);
            Assert.Equal(numRecords*4, blackVueDownloader.BlackVueDownloaderCopyStats.Errored);

            // Timeout Fail test
            for (var i = 0; i < numRecords*4; i++)
            {
                httpTest.SimulateTimeout();
            }
            blackVueDownloader.BlackVueDownloaderCopyStats.Clear();
            blackVueDownloader.ProcessList(opt, list);
            Assert.Equal(numRecords*4, blackVueDownloader.BlackVueDownloaderCopyStats.Errored);
        }

        [Theory]
        [InlineData("192.168.1.99")]
        public void DownloadFileIgnoreTest(string ip)
        {
            var targetdir = Path.Combine(Directory.GetCurrentDirectory(), "Record");

            var filesystem = new Mock<IFileSystemHelper>();

            var blackVueDownloader = new PCL.BlackVueDownloader(filesystem.Object);
            var blackVueDownloaderNoMock = new PCL.BlackVueDownloader();

            blackVueDownloaderNoMock.CreateDirectories(targetdir, targetdir);

            filesystem.Setup(x => x.Exists(Path.Combine(targetdir, "ignorefile.mp4"))).Returns(true);
            blackVueDownloader.DownloadFile(ip, "ignorefile.mp4", "video", targetdir, targetdir);

            Assert.Equal(1, blackVueDownloader.BlackVueDownloaderCopyStats.Ignored);
        }

        [Theory]
        [InlineData("192.168.1.99")]
        public void DownloadFileTmpExistsTest(string ip)
        {
            var tempdir = Path.Combine(Directory.GetCurrentDirectory(), "_tmp");

            var filesystem = new Mock<IFileSystemHelper>();

            var blackVueDownloader = new PCL.BlackVueDownloader(filesystem.Object);
            var blackVueDownloaderNoMock = new PCL.BlackVueDownloader();

            blackVueDownloaderNoMock.CreateDirectories(tempdir, tempdir);

            filesystem.Setup(x => x.Exists(Path.Combine(tempdir, "ignorefile.mp4"))).Returns(true);
            filesystem.Setup(x => x.Delete(Path.Combine(tempdir, "ignorefile.mp4")));
            blackVueDownloader.DownloadFile(ip, Path.Combine(tempdir, "ignorefile.mp4"), "video", tempdir, tempdir);

            Assert.Equal(1, blackVueDownloader.BlackVueDownloaderCopyStats.Ignored);
        }
    }

	public class BlackVueDownloadTestDataStrings : IEnumerable<object[]>
	{
		public IEnumerator<object[]> GetEnumerator()
		{
			yield return new object[] { BlackVueDownloaderTests.GenerateRecords("04/04/2016", 1,1), "20160404_120101_NF.mp4", 2 };
			yield return new object[] { BlackVueDownloaderTests.GenerateRecords("08/08/2018", 2,2), "20180808_120101_NF.mp4", 4 };
			yield return new object[] { BlackVueDownloaderTests.GenerateRecords("13/12/2017", 8,4), "20171213_120101_NF.mp4", 16 };
		}

		IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
	}

	public class BlackVueDownloadTestDataDates : IEnumerable<object[]>
	{
		public IEnumerator<object[]> GetEnumerator()
		{
			yield return new object[] { BlackVueDownloaderTests.GenerateRecords("04/04/2016", 1 ,1), new DateTime(2016,04,04,12,01,01), 2 };
			yield return new object[] { BlackVueDownloaderTests.GenerateRecords("08/08/2018", 2, 2), new DateTime(2018, 08, 08, 12, 01, 01), 4 };
			yield return new object[] { BlackVueDownloaderTests.GenerateRecords("13/12/2017", 8, 4), new DateTime(2017, 12, 13, 12, 01, 01), 16 };
		}

		IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
	}

	public class BlackVueDownloadTestDataFilterDates : IEnumerable<object[]>
	{
		public IEnumerator<object[]> GetEnumerator()
		{
			yield return new object[] { BlackVueDownloaderTests.GenerateRecords("04/04/2016", 1, 1), new DateTime(2016, 04, 04, 12, 01, 01), 2 };
			yield return new object[] { BlackVueDownloaderTests.GenerateRecords("08/08/2018", 2, 2), new DateTime(2018, 08, 08, 12, 01, 01), 2 };
			yield return new object[] { BlackVueDownloaderTests.GenerateRecords("13/12/2017", 8, 3), new DateTime(2017, 12, 13, 12, 01, 01), 2 };
		}

		IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
	}
}