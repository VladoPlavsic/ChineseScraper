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
            doc.Load(Directory.GetCurrentDirectory().Replace("/bin/Debug/netcoreapp3.1", "") + "/App.config.xml");
            String path = doc.GetElementsByTagName("Path")[0].InnerXml;
            Scraper sc = new Scraper(path);
            sc.Main();
        }
    }
}