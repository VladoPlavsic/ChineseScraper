using System;
using System.IO;
using System.Xml;

namespace Scraper
{
    class Program
    {
        static void Main(string[] args)
        {
            XmlDocument doc = new XmlDocument();
            //Load app.config file (CurrentDirectory = etc.../bin/debug/netcoreapp3.1). We have to go up few directories,
            //that's why we replace this (/bin/debug/netcoreapp3.1) part of the string.
            doc.Load(Directory.GetCurrentDirectory().Replace("/bin/Debug/netcoreapp3.1", "") + "/App.config.xml");
            String path = doc.GetElementsByTagName("Path")[0].InnerXml;
            int days = Convert.ToInt32(doc.GetElementsByTagName("Days")[0].InnerXml);
            Scraper sc = new Scraper(path, days);
            sc.Main();
        }
    }
}