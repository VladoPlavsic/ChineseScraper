using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using OpenQA.Selenium.Firefox;
using OpenQA.Selenium.Support.UI;
using HtmlAgilityPack;

namespace Scraper
{
    
    /*
        The way program works. We use selenium, got to the web page, get all available currencies, write them to dictionary
        Then we use page api for sending requests (POST with headers that include currencyName, startDate, endDate and page number if 
        page != 0 (if page == 0 we shouldn't pass any page)
        After that we send requests and increment page number until we get "sorry no data" or something like that, meaning there is no
        data after that page on. (Every time we get some result, we append it to a file with currency name)
        Parsing response is done using HtmlAgilityPack.
     */
    class Scraper
    {
        private String _URL = "https://srh.bankofchina.com/search/whpj/searchen.jsp";
        
        //x-paths
        private String _currencyXpath = "/html/body/table[1]/tbody/tr/td[4]/select/option[";

        private String _dataPath;
        
        private Dictionary<int, String> _currency = new Dictionary<int, String>();

        private FirefoxDriverService _service;
        private FirefoxDriver _driver;
        private WebDriverWait _wait;

        private int _startingIndex = 2;

        private DateTime _startDate;
        private DateTime _endDate;
        private String _dtFormat = "yyyy'-'MM'-'dd";
        
        public Scraper(string path)
        {
            _dataPath = path;
            _endDate = DateTime.Today;
            _startDate = _endDate.AddDays(-2);
            
            _service = FirefoxDriverService.CreateDefaultService();
            _driver = new FirefoxDriver(_service);
            _wait = new WebDriverWait(_driver, new TimeSpan(0, 1, 0));
            _driver.Url = _URL;
        }

        public void Main()
        {
            GetCurrency();
            //Close SELENIUM DRIVER
            _driver.Close();
            
            //Loop tru every currency available
            foreach (var kv in _currency)
            {
                Console.WriteLine("Getting data for currency " + kv.Value);
                GetDataForCurrency(kv.Value);
            }
        }

        //Get all available currencies using SELENIUM
        private void GetCurrency()
        {
                Console.WriteLine("Fetching available currencies...");
                int index = _startingIndex;

                while (true)
                {
                    try
                    {
                        var element =
                            _driver.FindElementByXPath(
                                String.Format(_currencyXpath + index + "]"));

                        _currency.Add(index, element.Text);
                        index++;
                    }
                    catch
                    {
                        break;
                    }
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
            using (dataStream = MakeRequest(currency, page))
            {
                Console.WriteLine(String.Format("Fetching data from page {0} for currency {1}", page, currency));
                StreamReader reader = new StreamReader(dataStream);
                string responseFromServer = reader.ReadToEnd();
                bool hasContent = HasContent(responseFromServer, currency, page);
                //We do this for as long as there is content on the page (increment page number, send request, get response, check if
                //it has data, if it does, write to file, increment page and keep looping)
                while (hasContent)
                {
                    page += 1;
                    Console.WriteLine(String.Format("Fetching data from page {0} for currency {1}", page, currency));
                    dataStream = MakeRequest(currency, page);
                    reader = new StreamReader(dataStream);
                    responseFromServer = reader.ReadToEnd();
                    hasContent = HasContent(responseFromServer, currency, page);

                }
            }
        }

        //Send POST request and return data Stream
        public Stream MakeRequest(String currency, int page)
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
            String path;
                try
                {
                    path = String.Format("/html/body/table[2]//tr");
                    htmlBody =
                        htmlDoc.DocumentNode.SelectNodes(path);
                }
                catch (Exception e)
                {
                    Console.WriteLine("Exception raised " + e);
                    return false;
                }
                
                using (StreamWriter sw = new StreamWriter(_dataPath + currency + _startDate.ToString(_dtFormat) + _endDate.ToString(_dtFormat) + ".csv", true))
                {
                    foreach (var node in htmlBody)
                    {
                        String data = node.InnerText.Trim();
                        if (data.Contains("soryy") || data.Contains("sorry"))
                        {
                            sw.Flush();
                            sw.Close();
                            Console.WriteLine("No data found!");
                            return false;
                        }

                        String[] headers =
                        {
                            "Currency", "Name", "Buying", "Rate", "Cash","Buying","Rate", "Selling ","Rate", "Cash","Selling","Rate",
                            "Middle","Rate", "Pub","Time"
                        };

                        List<String> dataArr = data.Split().ToList();
                        
                        Console.WriteLine("Printing another node of: " + currency);
                        foreach (var st in dataArr)
                        {
                            if (st.Trim() != "" && st.Trim() != "\n" && st.Trim() != "\t")
                            {
                                if (page > 0 && headers.Any(st.Contains))
                                {
                                    break;
                                }
                                if (!st.Equals(currency) && !st.Contains("Currency")) 
                                    sw.Write(",");
                                sw.Write(st);
                            }
                        }
                        sw.WriteLine();
                    }
                }

            return true;
        }
    }
}