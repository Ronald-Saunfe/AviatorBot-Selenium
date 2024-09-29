using OpenQA.Selenium;
using OpenQA.Selenium.Chrome;
using OpenQA.Selenium.Support.UI;
using SeleniumExtras.WaitHelpers;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using WebDriverManager.DriverConfigs.Impl;
using WebDriverManager.Helpers;
using WebDriverManager;
using System.Net.NetworkInformation;
using System.Net;
using Microsoft.ML;
using Tensorflow;
using Microsoft.ML.Data;


/*
 init chrome commands
 cd C:\Program Files\Google\Chrome\Application

 chrome.exe --remote-debugging-port=9222 --user-data-dir="C:\Users\ronal\OneDrive\Desktop\Aviator Bot\profile"
*/

namespace AviatorBot
{
    class Program
    {
        private static List<string> multiplierQueue = new List<string>();
        private static List<string> tempMultiplierQueue = new List<string>();
        private static List<string> lastMultipliers = new List<string>();
        private static bool newValue = true;
        private static int timeSteps = 10;
        private static decimal initialBalance = 1000m;
        private static decimal currentBalance = initialBalance;
        private static decimal betAmount = 100m;
        private static float tempPrediction = 0;
        private static Timer cashoutTimer;

        private static Model model;

        static CrashTimePredictor wmodel = new CrashTimePredictor();
        public class MultiplierData
        {
            [LoadColumn(0)]
            public float Multiplier;

            [LoadColumn(1)]
            public float WaitTime;
        }

        public class Prediction
        {
            [ColumnName("Score")]
            public float PredictedWaitTime;
        }

        private static PredictionEngine<MultiplierData, Prediction> predictionEngine;


        static async Task Main(string[] args)
        {
            OpenChromeBrowser();

            var chromeDriverPath = new DriverManager().SetUpDriver(new ChromeConfig(), VersionResolveStrategy.MatchingBrowser);
            ChromeOptions options = new ChromeOptions();
            options.DebuggerAddress = "127.0.0.1:9222";

            ChromeDriverService service = ChromeDriverService.CreateDefaultService(Path.GetDirectoryName(chromeDriverPath));
            
            model = new Model(timeSteps: timeSteps, testSize: 0.2f, nEstimators: 30, randomState: 100);

            using (var driver = new ChromeDriver(service, options))
            {
                var wait = new WebDriverWait(driver, TimeSpan.FromSeconds(20));

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
                // InitializeModel();
                Console.WriteLine("Initializing prediciton");


                wait.Until(ExpectedConditions.FrameToBeAvailableAndSwitchToIt(By.XPath("//*[@id=\"casino\"]/main/div/div/div[2]/div/iframe")));
                Console.WriteLine("Switched to iframe");

                var openmultiplier = wait.Until(ExpectedConditions.ElementToBeClickable(By.XPath("/html/body/app-root/app-game/div/div[1]/div[2]/div/div[2]/div[1]/app-stats-widget/div/div[3]/div")));
                openmultiplier.Click();

                while (true)
                {
                    try
                    {
                        var element = wait.Until(ExpectedConditions.ElementIsVisible(By.XPath("/html/body/app-root/app-game/div/div[1]/div[2]/div/div[1]/app-bets-widget/div/app-all-bets-tab/div/app-header/div[1]/div[1]/div[2]")));
                        string extractedValue = element.Text;
                        if (extractedValue == "0")
                        {
                            await GetMultipliers(driver, wait);
                            await Task.Delay(5000);
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Error in main loop: {ex.Message}");
                        driver.SwitchTo().DefaultContent();
                        wait.Until(ExpectedConditions.FrameToBeAvailableAndSwitchToIt(By.XPath("//*[@id=\"casino\"]/main/div/div/div[2]/div/iframe")));
                    }
                }
            }
        }

        private static void OpenChromeBrowser()
        {
            string chromePath = @"C:\Program Files\Google\Chrome\Application\chrome.exe"; //@"C:\Program Files (x86)\Google\Chrome\Application\chrome.exe";
            string userDataDir = @"C:\Users\ronal\OneDrive\Desktop\Aviator Bot\profile";
            int debuggingPort = 9222;

            if (!Directory.Exists(userDataDir))
            {
                Directory.CreateDirectory(userDataDir);
            }

            // Check if Chrome is already running with the debugging port
            if (IsPortInUse(debuggingPort))
            {
                Console.WriteLine("Chrome is already running with remote debugging port 9222.");
                return;
            }

            string arguments = $"--remote-debugging-port={debuggingPort} --user-data-dir=\"{userDataDir}\"";

            ProcessStartInfo psi = new ProcessStartInfo
            {
                FileName = chromePath,
                Arguments = arguments,
                UseShellExecute = true
            };

            try
            {
                Process.Start(psi);
                Console.WriteLine("Chrome browser opened with remote debugging.");
                Thread.Sleep(5000); // Wait for the browser to initialize
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error opening Chrome browser: {ex.Message}");
            }
        }

        private static bool IsPortInUse(int port)
        {
            IPGlobalProperties ipProperties = IPGlobalProperties.GetIPGlobalProperties();
            IPEndPoint[] tcpEndPoints = ipProperties.GetActiveTcpListeners();

            return tcpEndPoints.Any(endPoint => endPoint.Port == port);
        }

        private static double CalculateWaitTime(float prediction)
        {
            double sec = wmodel.Predict(prediction); 
            return sec;
        }

        private static async Task Cashout(IWebDriver driver, WebDriverWait wait)
        {
            try
            {
                Console.WriteLine("Cashed out");
                var cashoutButton = wait.Until(ExpectedConditions.ElementToBeClickable(By.XPath("/html/body/app-root/app-game/div/div[1]/div[2]/div/div[2]/div[3]/app-bet-controls/div/app-bet-control[1]/div/div[1]/div[2]/button")));
                //cashoutButton.Click();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error during cashout: {ex.Message}");
            }
            finally
            {
                cashoutTimer?.Dispose();
                cashoutTimer = null;
            }
        }

        private static async Task GetMultipliers(IWebDriver driver, WebDriverWait wait)
        {
            cashoutTimer?.Dispose();
            cashoutTimer = null;

            try
            {
                var currentMultipliers = GetCurrentMultipliers(driver, wait);
                if (!currentMultipliers.SequenceEqual(lastMultipliers))
                {
                    currentMultipliers.Reverse();

                    if (tempPrediction != 0)
                    {
                        float actualMultiplier = float.Parse(currentMultipliers.Last().TrimEnd('x'));
                        Console.WriteLine($"Prev prediction: {tempPrediction:F2}, outcome {actualMultiplier:F2}");

                        if (actualMultiplier >= tempPrediction)
                        {
                            decimal profit = betAmount * ((decimal)tempPrediction - 1);
                            currentBalance += profit;
                            //Console.WriteLine($"Win! Profit: {profit:F2}");
                        }
                        else
                        {
                            currentBalance -= betAmount;
                            //Console.WriteLine($"Loss. Lost bet amount: {betAmount}");
                        }

                        Console.WriteLine($"Current Balance: {currentBalance:F2}");

                        if (currentBalance <= 0)
                        {
                            currentBalance = initialBalance;
                        }
                    }

                    if (newValue)
                    {
                        multiplierQueue.AddRange(currentMultipliers);
                        newValue = false;
                        AppendMultipliersToFile(multiplierQueue);
                    }
                    else
                    {
                        multiplierQueue.Add(currentMultipliers.Last());

                        if (tempMultiplierQueue.Count >= timeSteps)
                        {
                            tempMultiplierQueue.Clear();
                        }

                        tempMultiplierQueue.Add(currentMultipliers.Last());
                        AppendMultipliersToFile(tempMultiplierQueue);
                    }

                    var allMultipliers = multiplierQueue.Select(m => float.Parse(m.TrimEnd('x'))).ToArray();
                    var recentMultipliers = allMultipliers.TakeLast(timeSteps).ToArray();
                    var (prediction, confidence) = model.FitAndPredict(allMultipliers, recentMultipliers);
                    tempPrediction = prediction;

                    if (prediction >= 2.0)
                    {
                        Console.WriteLine($"New Prediction: {prediction:F2}, Confidence: {confidence:F2}");

                        var betButton = wait.Until(ExpectedConditions.ElementToBeClickable(By.XPath("/html/body/app-root/app-game/div/div[1]/div[2]/div/div[2]/div[3]/app-bet-controls/div/app-bet-control[1]/div/div[1]/div[2]/button")));
                        
                        if (betButton.Text == "CANCEL")
                        {
                            cashoutTimer?.Dispose();
                            cashoutTimer = null;
                            //betButton.Click();
                            //betButton.Click();
                        }
                        else
                        {
                            //betButton.Click();
                            //Console.WriteLine("Bet placed");
                        }

                        double waitTime = CalculateWaitTime(prediction);
                        cashoutTimer = new Timer(async _ => await Cashout(driver, wait), null, TimeSpan.FromSeconds(waitTime), Timeout.InfiniteTimeSpan);
                        Console.WriteLine($"Duration: {waitTime:F2} seconds\n\n");
                    }

                    lastMultipliers = new List<string>(currentMultipliers);
                }

            }
            catch (Exception ex)
            {
                Console.WriteLine($"An error occurred: {ex.Message}");
                try
                {
                    driver.SwitchTo().DefaultContent();
                    wait.Until(ExpectedConditions.FrameToBeAvailableAndSwitchToIt(By.XPath("//*[@id=\"casino\"]/main/div/div/div[2]/div/iframe")));
                    Console.WriteLine("Switched back to iframe after error");
                }
                catch (Exception frameEx)
                {
                    Console.WriteLine($"Error switching back to frame: {frameEx.Message}");
                }
            }
        }

        private static List<string> GetCurrentMultipliers(IWebDriver driver, WebDriverWait wait)
        {
            const int maxRetries = 3;
            for (int attempt = 0; attempt < maxRetries; attempt++)
            {
                try
                {
                    var multipliers = wait.Until(ExpectedConditions.PresenceOfAllElementsLocatedBy(By.CssSelector(".bubble-multiplier.font-weight-bold")));
                    return multipliers.Select(m => m.Text).Where(t => !string.IsNullOrEmpty(t)).ToList();
                }
                catch (Exception ex)
                {
                    if (attempt == maxRetries - 1)
                        throw;
                    Console.WriteLine("Stale element encountered. Retrying...");
                    Thread.Sleep(2000);
                }
            }
            return new List<string>();
        }

        private static void AppendMultipliersToFile(List<string> multipliers)
        {
            string filePath = "traindata.txt";
            try
            {
                File.AppendAllText(filePath, string.Join(",", multipliers) + ",");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error writing to file: {ex.Message}");
            }
        }
    }
}