using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Net.Http;
using System.IO;
using AngleSharp;
using Microsoft.Extensions.Configuration;

namespace Crawler_Service
{
    public class Worker : BackgroundService
    {
        private readonly ILogger<Worker> _logger;

        public Worker(ILogger<Worker> logger)
        {
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                _logger.LogInformation("Worker running at: {time}", DateTimeOffset.Now);
                await Task.Delay(60000, stoppingToken);
                await ToDo();
            }
        }

        public class ParkInfo
        {
            public string ParkingName { get; set; }
            public string ParkingRestNum { get; set; }
            public string CreateTime { get; set; }
        }

        static async Task ToDo()
        {
            Console.WriteLine("＝＝＝＝＝start programme＝＝＝＝＝");

            Console.WriteLine("-- Basic setting --");
            Microsoft.Extensions.Configuration.IConfiguration AppsettingData = new ConfigurationBuilder().AddJsonFile("appsettings.json", optional: true, reloadOnChange: true).Build();
            HttpClient httpClient = new HttpClient();
            List<ParkInfo> parkInfos = new List<ParkInfo>();
            FileStream fs = null;
            string DTNow = DateTime.Now.ToString("yyyy/MM/dd HH:mm");
            string DTNowForName = DateTime.Now.ToString("yyyyMMdd");
            string url = AppsettingData["URL"];
            string directoryName = AppsettingData["directoryName"];
            if (string.IsNullOrEmpty(directoryName))
            {
                directoryName = "./";
            }
            string fileName = AppsettingData["fileName"] + "_" + DTNowForName;
            string filepath = directoryName + fileName + ".csv";
            Console.WriteLine("url :" + url);
            Console.WriteLine("filepath :" + filepath);

            Console.WriteLine("---START read csv---");

            //資料夾確認與建立            
            if (!Directory.Exists(directoryName))
            {
                Console.WriteLine("not exists[" + directoryName + "]");
                Directory.CreateDirectory(directoryName);
            }
            //資料命名與確認


            //如果資料存在，全部讀出來
            if (File.Exists(filepath))
            {
                Console.WriteLine("exists[" + filepath + "], read data");
                var reader = new StreamReader(File.OpenRead(filepath));
                while (!reader.EndOfStream)
                {
                    var line = reader.ReadLine();
                    var values = line.Split(',');
                    parkInfos.Add(new ParkInfo() { ParkingName = values[0], ParkingRestNum = values[1], CreateTime = values[2] });
                }
            }
            else
            {
                parkInfos.Add(new ParkInfo() { ParkingName = "停車場名稱", ParkingRestNum = "剩餘車位", CreateTime = "紀錄時間" });
            }


            Console.WriteLine("---End read csv---");

            Console.WriteLine("---START request data---");
            //AngleSharp
            var responseMessage = await httpClient.GetAsync(url); //發送請求
            if (responseMessage.StatusCode == System.Net.HttpStatusCode.OK)
            {
                //取得HTML
                Console.WriteLine("step: Get HTML");
                string response = responseMessage.Content.ReadAsStringAsync().Result;

                // 使用AngleSharp時的前置設定
                var config = Configuration.Default;
                var context = BrowsingContext.New(config);

                //將我們用httpclient拿到的資料放入res.Content中())
                Console.WriteLine("step: INPUT context");
                var document = await context.OpenAsync(res => res.Content(response));

                //開始解析                
                Console.WriteLine("step:[" + DTNow + "]Start Analysis Data");
                Console.WriteLine("----- Start Console data -----");
                var DataRow = document.QuerySelectorAll("div.custom-rwd-list > div.custom-rwd-row ");
                for (var i = 0; i < DataRow.Length; i++)
                {
                    //取得每個元素的TextContent

                    if (DataRow[i].QuerySelector("div.custom-rwd-item.column-1 > div > span.parking-name") is null || DataRow[i].QuerySelector("div.custom-rwd-item.column-3 > div.Text_G") is null)
                    {
                        Console.WriteLine("null data");
                    }
                    else
                    {
                        var data = new ParkInfo();
                        data.ParkingName = DataRow[i].QuerySelector("div.custom-rwd-item.column-1 > div > span.parking-name").TextContent;
                        data.ParkingRestNum = DataRow[i].QuerySelector("div.custom-rwd-item.column-3 > div.Text_G").TextContent;
                        data.CreateTime = DTNow;
                        Console.WriteLine("停車場:[" + data.ParkingName + "], 剩餘:[" + data.ParkingRestNum + "]");
                        parkInfos.Add(new ParkInfo() { ParkingName = data.ParkingName, ParkingRestNum = data.ParkingRestNum, CreateTime = data.CreateTime });

                    }
                }
                Console.WriteLine("----- End Console data -----");

                //CSV
                Console.WriteLine("-----Start Write CSV -----");

                if (!File.Exists(filepath))
                {
                    //如果不存在，採用Create
                    Console.WriteLine("not exists[" + filepath + "]");
                    fs = new FileStream(filepath, FileMode.CreateNew);
                }
                else
                {
                    //如果存在，開啟並寫入
                    Console.WriteLine("exists[" + filepath + "]");
                    //修改檔案為非唯讀屬性(Normal)
                    System.IO.FileInfo FileAttribute = new FileInfo(filepath);
                    FileAttribute.Attributes = FileAttributes.Normal;

                    //開啟CSV檔案
                    fs = new FileStream(filepath, FileMode.Open, FileAccess.Write);
                }
                using (var file = new StreamWriter(fs, System.Text.Encoding.UTF8))
                {
                    foreach (var item in parkInfos)
                    {
                        Console.WriteLine("ADD " + item.ParkingName + ", " + item.ParkingRestNum + ", " + item.CreateTime);
                        await file.WriteLineAsync($"{item.ParkingName},{item.ParkingRestNum},{item.CreateTime}");
                    }
                }

                Console.WriteLine("-----End Write CSV -----");
            }
            else
            {
                Console.WriteLine("網址：" + url + "狀態異常：" + responseMessage.StatusCode);
            }
        }
    }
}
