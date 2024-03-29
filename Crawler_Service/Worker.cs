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
            Console.WriteLine("」」」」」start programme」」」」」");

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

            //瑚�踏┰T�{�P�悒�            
            if (!Directory.Exists(directoryName))
            {
                Console.WriteLine("not exists[" + directoryName + "]");
                Directory.CreateDirectory(directoryName);
            }
            //瑚�透R�W�P�T�{


            //�p�G瑚�豆s�b�A��魁的�X��
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
                parkInfos.Add(new ParkInfo() { ParkingName = "葦┏兜�W細", ParkingRestNum = "角�l┏��", CreateTime = "��雀�俵�" });
            }


            Console.WriteLine("---End read csv---");

            Console.WriteLine("---START request data---");
            //AngleSharp
            var responseMessage = await httpClient.GetAsync(url); //�o�e出�D
            if (responseMessage.StatusCode == System.Net.HttpStatusCode.OK)
            {
                //���oHTML
                Console.WriteLine("step: Get HTML");
                string response = responseMessage.Content.ReadAsStringAsync().Result;

                // �魯�AngleSharp�肘昆e�m�]�w
                var config = Configuration.Default;
                var context = BrowsingContext.New(config);

                //�Nи�魅�httpclient�絵讓左螫透颪Jres.Contentい())
                Console.WriteLine("step: INPUT context");
                var document = await context.OpenAsync(res => res.Content(response));

                //�}�l狐�R                
                Console.WriteLine("step:[" + DTNow + "]Start Analysis Data");
                Console.WriteLine("----- Start Console data -----");
                var DataRow = document.QuerySelectorAll("div.custom-rwd-list > div.custom-rwd-row ");
                for (var i = 0; i < DataRow.Length; i++)
                {
                    //���o�C�咾遣勢�TextContent

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
                        Console.WriteLine("葦┏兜:[" + data.ParkingName + "], 角�l:[" + data.ParkingRestNum + "]");
                        parkInfos.Add(new ParkInfo() { ParkingName = data.ParkingName, ParkingRestNum = data.ParkingRestNum, CreateTime = data.CreateTime });

                    }
                }
                Console.WriteLine("----- End Console data -----");

                //CSV
                Console.WriteLine("-----Start Write CSV -----");

                if (!File.Exists(filepath))
                {
                    //�p�Gぃ�s�b�A営ノCreate
                    Console.WriteLine("not exists[" + filepath + "]");
                    fs = new FileStream(filepath, FileMode.CreateNew);
                }
                else
                {
                    //�p�G�s�b�A�}衛�端g�J
                    Console.WriteLine("exists[" + filepath + "]");
                    //�廡鐇筆廳茜D胃的紬��(Normal)
                    System.IO.FileInfo FileAttribute = new FileInfo(filepath);
                    FileAttribute.Attributes = FileAttributes.Normal;

                    //�}衛CSV隻��
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
                Console.WriteLine("柵�}�G" + url + "���A翁�`�G" + responseMessage.StatusCode);
            }
        }
    }
}
