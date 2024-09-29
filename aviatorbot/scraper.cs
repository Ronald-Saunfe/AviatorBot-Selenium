using OpenQA.Selenium;
using OpenQA.Selenium.Chrome;
using OpenQA.Selenium.Support.UI;
using WebDriverManager;
using WebDriverManager.DriverConfigs.Impl;
using WebDriverManager.Helpers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.IO;

class scraper
{
    static Queue<string> multiplierQueue;
    static Queue<string> tempmultiplierQueue;
    static List<string> lastMultipliers;
    static WebDriverWait wait;
    static IWebDriver driver;
    static Boolean newvalue = false;

    static void otherfunc(string[] args)
    {
        var chromeDriverPath = new DriverManager().SetUpDriver(new ChromeConfig(), VersionResolveStrategy.MatchingBrowser);

        ChromeOptions options = new ChromeOptions();
        options.DebuggerAddress = "127.0.0.1:9222";

        ChromeDriverService service = ChromeDriverService.CreateDefaultService(Path.GetDirectoryName(chromeDriverPath));

        driver = new ChromeDriver(service, options);

        string url = "https://1whpc.com/casino/play/aviator";
        if (driver.Url != url)
        {
            driver.Navigate().GoToUrl(url);
            Console.WriteLine($"Navigated to {url}");
        }
        else
        {
            Console.WriteLine($"Already on {url}");
        }

        wait = new WebDriverWait(driver, TimeSpan.FromSeconds(20));
        wait.Until(SeleniumExtras.WaitHelpers.ExpectedConditions.FrameToBeAvailableAndSwitchToIt(By.XPath("//*[@id=\"casino\"]/main/div/div/div[2]/div/iframe")));
        Console.WriteLine("Switched to iframe");


        multiplierQueue = new Queue<string>(100000);
        lastMultipliers = new List<string>();
        tempmultiplierQueue = new Queue<string>(1);

        while (true)
        {
            // Find the element by the given XPath
            IWebElement element = driver.FindElement(By.XPath("/html/body/app-root/app-game/div/div[1]/div[2]/div/div[1]/app-bets-widget/div/app-all-bets-tab/div/app-header/div[1]/div[1]/div[2]"));

            // Extract the text from the located element
            string extractedValue = element.Text;
            if (extractedValue == "0")
            {
                getmultipliers();
                //Console.WriteLine("Extracted Value: " + extractedValue);
                Thread.Sleep(5000);
            }

        }
    }

    private static void getmultipliers()
    {
        try
        {
            List<string> currentMultipliers = GetCurrentMultipliers(driver, wait);

            if (!currentMultipliers.SequenceEqual(lastMultipliers))
            {
                // Reverse the order of multipliers
                currentMultipliers.Reverse();

                //Console.WriteLine("New "+currentMultipliers.Count+" prev "+lastMultipliers.Count);

                
                //Console.WriteLine("New multipliers added to traindata.txt in reversed order");
                if (newvalue == false)
                {
                    foreach (string multiplier in currentMultipliers)
                    {

                        multiplierQueue.Enqueue(multiplier);
                        //Console.WriteLine(multiplier);
                    }
                    newvalue = true;
                    // Append reversed multipliers to file
                    AppendMultipliersToFile(multiplierQueue);
                }
                else
                {
                    multiplierQueue.Enqueue(currentMultipliers[currentMultipliers.Count-1]);
                    // Append reversed multipliers to file
                    tempmultiplierQueue.Clear();
                    tempmultiplierQueue.Enqueue(currentMultipliers[currentMultipliers.Count - 1]);
                    AppendMultipliersToFile(tempmultiplierQueue);
                }


                //Console.WriteLine("===========");

                //Console.WriteLine("New multipliers (reversed order):");
                foreach (string newMultiplier in currentMultipliers)
                {
                    //Console.WriteLine(newMultiplier);
                }

                Console.WriteLine("Multipliers: " + string.Join(", ", multiplierQueue.ToList()));

                lastMultipliers = new List<string>(currentMultipliers);
            }

            Thread.Sleep(5000); // Check every second
        }
        catch (Exception ex)
        {
            Console.WriteLine($"An error occurred: {ex.Message}");
            // If we lose the frame, try to switch back to it
            try
            {
                driver.SwitchTo().DefaultContent();
                wait.Until(SeleniumExtras.WaitHelpers.ExpectedConditions.FrameToBeAvailableAndSwitchToIt(By.XPath("//*[@id=\"casino\"]/main/div/div/div[2]/div/iframe")));
                Console.WriteLine("Switched back to iframe after error");
            }
            catch (Exception frameEx)
            {
                Console.WriteLine($"Error switching back to frame: {frameEx.Message}");
            }
        }

    }

    static List<string> GetCurrentMultipliers(IWebDriver driver, WebDriverWait wait)
    {
        const int maxRetries = 3;
        for (int attempt = 0; attempt < maxRetries; attempt++)
        {
            try
            {
                var multipliers = wait.Until(SeleniumExtras.WaitHelpers.ExpectedConditions.PresenceOfAllElementsLocatedBy(By.CssSelector(".bubble-multiplier.font-weight-bold")));
                return multipliers.Where(m => !string.IsNullOrEmpty(m.Text)).Select(m => m.Text).ToList();
            }
            catch (StaleElementReferenceException)
            {
                if (attempt == maxRetries - 1) throw;
                Console.WriteLine("Stale element encountered. Retrying...");
                Thread.Sleep(2000); // Short delay before retry
            }
        }
        return new List<string>(); // This line should never be reached due to the throw in the loop
    }

    static void AppendMultipliersToFile(Queue<string> multipliers)
    {
        string filePath = "C:\\Users\\ronal\\source\\repos\\aviatorbot\\aviatorbot\\traindata.txt";
        try
        {
            // Check if the file exists and has content
            bool fileExists = File.Exists(filePath);
            bool fileHasContent = fileExists && new FileInfo(filePath).Length > 0;

            using (StreamWriter sw = File.AppendText(filePath))
            {
                // If the file has content and we're not at the start of a line, add a comma
                if (fileHasContent && sw.BaseStream.Position > 0 && !EndsWithNewLine(filePath))
                {
                    sw.Write(",");
                }

                // Write the multipliers
                sw.Write(string.Join(",", multipliers));

                // Don't add a newline character here
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error writing to file: {ex.Message}");
        }
    }

    // Helper method to check if the file ends with a newline
    static bool EndsWithNewLine(string filePath)
    {
        using (var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
        {
            if (stream.Length == 0) return true; // Empty file

            stream.Seek(-1, SeekOrigin.End);
            int lastByte = stream.ReadByte();
            return lastByte == 10 || lastByte == 13; // 10 is LF, 13 is CR
        }
    }

}