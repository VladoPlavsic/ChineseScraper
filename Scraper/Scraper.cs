using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using HtmlAgilityPack;

namespace Scraper
{
    
    /*
        The way program works. We send get request and fetch all available currencies, write them to dictionary.
        Then we use page api for sending requests (POST with headers that include currencyName, startDate, endDate and page number if 
        page != 0 (if page == 0 we shouldn't pass any page)
        After that we send requests and increment page number until we get "sorry no data" or something like that, meaning there is no
        data after that page on. (Every time we get some result, we append it to a file with currency name)
        Parsing response is done using HtmlAgilityPack.
     */
    class Scraper
    {
        private readonly String _URL = "https://srh.bankofchina.com/search/whpj/searchen.jsp";
        
        //x-paths
        private readonly String _currencyXpath = "//option"; //Available currency options
        private readonly String _tablePath = "/html/body/table[2]//tr"; //Table containing data

        private readonly String _dataPath; //Data containing folder path
        
        private readonly Dictionary<int, String> _currency = new Dictionary<int, String>(); //Dictionary containing available currencies

        private readonly int _startingIndex = 2;

        private readonly DateTime _startDate;
        private readonly DateTime _endDate;
        private readonly String _dtFormat = "yyyy'-'MM'-'dd";
        
        //We pass containing folder path and number of days we want to fetch data for [App.config.xml contains these data]
        public Scraper(String path, int days)
        {
            _dataPath = path;
            _endDate = DateTime.Today;
            _startDate = _endDate.AddDays(-days);
        }


        public void Main()
        {
            //Get availabe currencies
            GetCurrencies();
            //Loop thru every currency available
            
            //Uncomment foreach loop and comment UseThreeThreads if you want to use all threads, or leave as it is to use only 3 threads
            
            /* 
            foreach (var kv in _currency)
            {   
                SpawnThreads(kv.Value);
            }
            */
            
            UseThreeThreads();
        }

        private void SpawnThreads(String currency)
        {
            Thread t = new Thread(() => { GetDataForCurrency(currency); });
            t.Start();
        }

        private void UseThreeThreads()
        {
            int split = _currency.Count() / 3;
            Thread t1 = new Thread(() =>
            {
                for (int i = _startingIndex; i < split + _startingIndex; i++)
                {
                    Console.WriteLine("Getting data for currency " + _currency[i]);                    
                }
            });
            Thread t2 = new Thread(() =>
            {
                for (int i = split + _startingIndex; i < split * 2 + _startingIndex; i++)
                {
                    Console.WriteLine("Getting data for currency " + _currency[i]);              
                    GetDataForCurrency(_currency[i]);

                }
            });
            Thread t3 = new Thread(() =>
            {
                for (int i = split * 2 + _startingIndex; i < _currency.Count() + _startingIndex; i++)
                {
                    Console.WriteLine("Getting data for currency " + _currency[i]);              
                    GetDataForCurrency(_currency[i]);

                }
            });
            
            t1.Start();
            t2.Start();
            t3.Start();
        }

        //Get all available currencies
        private void GetCurrencies()
        {
                Console.WriteLine("Fetching available currencies...");
                int index = _startingIndex;

                StreamReader reader = new StreamReader(MakeRequestCurrency());
                String responseFromServer = reader.ReadToEnd();
                HtmlDocument htmlDoc = new HtmlDocument();
                htmlDoc.LoadHtml(responseFromServer);
                
                try
                {
                    HtmlNodeCollection htmlBody =
                        htmlDoc.DocumentNode.SelectNodes(_currencyXpath);
                    
                    foreach (var node in htmlBody)
                    {
                        if(node.InnerText.Contains("Select"))
                            continue;
                        _currency.Add(index, node.InnerText.Trim());
                        index++;
                    }
                }
                catch (Exception e)
                {
                    Console.WriteLine("Exception raised " + e);
                    return;
                }
       
                Console.WriteLine(String.Format("Found {0} currencies", (index - _startingIndex)));
                foreach (var k in _currency)
                {
                    Console.WriteLine(String.Format("Key: {0} and Value {1}", k.Key, k.Value));
                }

        }

        public void GetDataForCurrency(String currency)
        {
            var page = 0;
            
            Stream dataStream; 
            using (dataStream = MakeRequestData(currency, page))
            {
                Console.WriteLine(String.Format("Fetching data from page {0} for currency {1}", page, currency));
                StreamReader reader = new StreamReader(dataStream);
                String responseFromServer = reader.ReadToEnd();
                bool hasContent = HasContent(responseFromServer, currency, page);
                //We do this for as long as there is content on the page (increment page number, send request, get response, check if
                //it has data, if it does, write to file, increment page and keep looping)
                while (hasContent)
                {
                    page += 1;
                    Console.WriteLine(String.Format("Fetching data from page {0} for currency {1}", page, currency));
                    dataStream = MakeRequestData(currency, page);
                    reader = new StreamReader(dataStream);
                    responseFromServer = reader.ReadToEnd();
                    hasContent = HasContent(responseFromServer, currency, page);

                }
            }
        }

        //Send GET request and return data Stream (Fetching available currencies)
        public Stream MakeRequestCurrency()
        {
            var handler = new HttpClientHandler();
            handler.UseCookies = false;
            WebResponse response;

            handler.AutomaticDecompression = ~DecompressionMethods.None;

            using (var httpClient = new HttpClient(handler))
            {
                var request = WebRequest.Create(_URL);
                request.Method = "GET";
            
                request.ContentType = "application/x-www-form-urlencoded";

                System.Text.EncodingProvider provider = System.Text.CodePagesEncodingProvider.Instance;
                Encoding.RegisterProvider(provider);

                response = request.GetResponse();

                Stream dataStream = response.GetResponseStream();
                return dataStream;
            }
        }
        
        //Send POST request and return data Stream (Fetching data for a currency at page=page)
        public Stream MakeRequestData(String currency, int page)
        {
            var handler = new HttpClientHandler();
            handler.UseCookies = false;
            WebResponse response;
            
            handler.AutomaticDecompression = ~DecompressionMethods.None;

            using (var httpClient = new HttpClient(handler))
            {
                var request = WebRequest.Create(_URL);
                request.Method = "POST";

                String headers;
                //If page == 0 we do not include it to headers
                if (page == 0)
                {
                    headers = String.Format(
                        "erectDate={0}&nothing={1}&pjname={2}", _startDate.ToString(_dtFormat),
                        _endDate.ToString(_dtFormat), currency);
                }
                else
                {
                    headers = String.Format(
                        "erectDate={0}&nothing={1}&pjname={2}&page={3}", _startDate.ToString(_dtFormat),
                        _endDate.ToString(_dtFormat), currency, page.ToString());
                }

                byte[] byteArray = Encoding.UTF8.GetBytes(headers);

                request.ContentType = "application/x-www-form-urlencoded";

                System.Text.EncodingProvider provider = System.Text.CodePagesEncodingProvider.Instance;
                Encoding.RegisterProvider(provider);

                Stream dataStream = request.GetRequestStream();
                dataStream.Write(byteArray, 0, byteArray.Length);
                dataStream.Close();
                
                response = request.GetResponse();

                dataStream = response.GetResponseStream();
                return dataStream;
            }
        }
        //Check if there is content in given response, and write it to file and return True, else return False.
        public bool HasContent(String content, String currency, int page)
        {
            var htmlDoc = new HtmlDocument();
            htmlDoc.LoadHtml(content);

            HtmlNodeCollection htmlBody;
                try
                {
                    htmlBody =
                        htmlDoc.DocumentNode.SelectNodes(_tablePath);
                }
                catch (Exception e)
                {
                    Console.WriteLine("Exception raised " + e);
                    return false;
                }

                String[] list = null;
                using (StreamWriter sw = new StreamWriter(_dataPath + currency + _startDate.ToString(_dtFormat) + _endDate.ToString(_dtFormat) + ".csv", true))
                {
                    bool newLine = true;
                    foreach (var node in htmlBody)
                    {
                        newLine = true;
                        String data = node.InnerText.Trim();
                        if (data.Contains("soryy") || data.Contains("sorry"))
                        {
                            sw.Flush();
                            sw.Close();
                            Console.WriteLine("No data found!");
                            return false;
                        }

                        if (list == null && page == 0)
                        {
                            list = data.Split(currency)[0].Split("\n");

                            foreach (var line in list)
                            {
                                if (line.Trim().Length > 0)
                                {
                                    sw.Write(line.Trim());
                                    if (line != list.Last())
                                        sw.Write(",");
                                }
                            }

                            sw.WriteLine();
                        }

                        String[] headers =
                        {
                            "Currency", "Name", "Buying", "Rate", "Cash","Buying","Rate", "Selling ","Rate", "Cash","Selling","Rate",
                            "Middle","Rate", "Pub","Time"
                        };

                        List<String> dataArr = data.Split().ToList();
                        
                        foreach (var st in dataArr)
                        {
                            if (st.Trim().Length > 0)
                            {
                                if (headers.Any(st.Contains))
                                {
                                    newLine = false;
                                    break;
                                }
                                if (!st.Equals(currency) && !st.Contains("Currency")) 
                                    sw.Write(",");
                                sw.Write(st);
                            }
                        }
                        if(newLine)
                            sw.WriteLine();
                    }
                }

            return true;
        }
    }
}