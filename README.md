# ChineseScraper

To change path for your output directory go to Srcaper/App.config.xml file and change value of "Path"..."Path".
To change start date go to Scraper/App.config.xml file and change value of "Days"..."Days"

It's fast, but it can be faster. Currently program is using only 3 threads for fetching data, if you want to make it super fast, check Main() function in Scraper.cs class, and uncomment "foreach" loop, don't forget to comment the "UseThreeThreads()" metod.

Note: There might be an error (probably not) in Program.cs trying to load .config file due to the project was done in Rider on Linux, and it doesn't allow us using Config file as Visual Studio does!
