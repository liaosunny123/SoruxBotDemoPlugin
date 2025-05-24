using System.Text;
using System.Text.RegularExpressions;
using Microsoft.Playwright;

namespace EpicMo.SoruxBot.Demo.Ability;

public class WebPageTextExtractor
{
    private IPlaywright _playwright;
    private IBrowser _browser;
    
    /// <summary>
    /// 初始化Playwright和浏览器
    /// </summary>
    public async Task InitializeAsync()
    {
        // 确保浏览器已安装
        await EnsureBrowsersInstalledAsync();
        
        _playwright = await Playwright.CreateAsync();
        _browser = await _playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions
        {
            Headless = true, // 无头模式
            Args = new[] { "--no-sandbox", "--disable-setuid-sandbox" }
        });
    }
    
    /// <summary>
    /// 检查并自动安装浏览器
    /// </summary>
    private async Task EnsureBrowsersInstalledAsync()
    {
        try
        {
            Console.WriteLine("检查浏览器安装状态...");
            
            // 尝试创建一个临时的Playwright实例来检查浏览器
            using var tempPlaywright = await Playwright.CreateAsync();
            
            try
            {
                // 尝试启动浏览器来检查是否已安装
                var tempBrowser = await tempPlaywright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions
                {
                    Headless = true
                });
                
                await tempBrowser.CloseAsync();
                Console.WriteLine("浏览器已安装，无需下载。");
                return;
            }
            catch (PlaywrightException ex) when (ex.Message.Contains("Executable doesn't exist"))
            {
                Console.WriteLine("检测到浏览器未安装，开始自动下载...");
                await InstallBrowsersAsync();
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"检查浏览器状态时出错: {ex.Message}");
            Console.WriteLine("尝试安装浏览器...");
            await InstallBrowsersAsync();
        }
    }
    
    /// <summary>
    /// 自动安装Playwright浏览器
    /// </summary>
    private async Task InstallBrowsersAsync()
    {
        try
        {
            Console.WriteLine("正在下载和安装浏览器，这可能需要几分钟时间...");
            
            // 使用Microsoft.Playwright的安装程序
            var exitCode = Microsoft.Playwright.Program.Main(new[] { "install", "chromium" });
            
            if (exitCode == 0)
            {
                Console.WriteLine("浏览器安装成功！");
            }
            else
            {
                throw new Exception($"浏览器安装失败，退出代码: {exitCode}");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"自动安装浏览器失败: {ex.Message}");
            Console.WriteLine("请手动运行以下命令安装浏览器:");
            Console.WriteLine("dotnet exec microsoft.playwright.dll install chromium");
            throw new Exception("浏览器安装失败，请手动安装", ex);
        }
    }
    
    /// <summary>
    /// 获取网页的全部文字内容
    /// </summary>
    /// <param name="url">要抓取的网页URL</param>
    /// <param name="timeout">超时时间(毫秒)，默认30秒</param>
    /// <returns>网页的文字内容</returns>
    public async Task<string> ExtractWebPageTextAsync(string url, int timeout = 30000)
    {
        if (string.IsNullOrEmpty(url))
            throw new ArgumentException("URL不能为空", nameof(url));
            
        if (_browser == null)
            await InitializeAsync();
            
        IPage page = null;
        try
        {
            // 创建新页面
            page = await _browser.NewPageAsync();
            
            // 导航到目标URL
            await page.GotoAsync(url, new PageGotoOptions
            {
                WaitUntil = WaitUntilState.DOMContentLoaded,
                Timeout = timeout
            });
            
            // 等待页面完全加载
            await page.WaitForLoadStateAsync(LoadState.NetworkIdle, new PageWaitForLoadStateOptions
            {
                Timeout = timeout
            });
            
            // 移除不需要的元素（广告、导航等）
            await RemoveUnwantedElementsAsync(page);
            
            // 获取页面标题
            string title = await page.TitleAsync();
            
            // 获取主要内容区域的文本
            string mainContent = await ExtractMainContentAsync(page);
            
            // 构建最终文本
            StringBuilder result = new StringBuilder();
            if (!string.IsNullOrEmpty(title))
            {
                result.AppendLine($"标题: {title}");
                result.AppendLine();
            }
            
            result.AppendLine("内容:");
            result.AppendLine(mainContent);
            
            return CleanText(result.ToString());
        }
        catch (Exception ex)
        {
            throw new Exception($"提取网页内容时发生错误: {ex.Message}", ex);
        }
        finally
        {
            if (page != null)
                await page.CloseAsync();
        }
    }
    
    /// <summary>
    /// 移除不需要的页面元素
    /// </summary>
    private async Task RemoveUnwantedElementsAsync(IPage page)
    {
        // 移除常见的干扰元素
        string[] selectors = {
            "script", "style", "nav", "header", "footer", 
            ".advertisement", ".ads", ".sidebar", ".menu",
            "#advertisement", "#ads", "#sidebar", "#menu",
            "[class*='ad']", "[id*='ad']", "[class*='banner']"
        };
        
        foreach (string selector in selectors)
        {
            try
            {
                await page.EvaluateAsync($@"
                    document.querySelectorAll('{selector}').forEach(el => el.remove())
                ");
            }
            catch
            {
                // 忽略移除失败的情况
            }
        }
    }
    
    /// <summary>
    /// 提取主要内容
    /// </summary>
    private async Task<string> ExtractMainContentAsync(IPage page)
    {
        // 尝试多种选择器来获取主要内容
        string[] contentSelectors = {
            "article", "main", "[role='main']", ".content", "#content",
            ".post", ".entry", ".article", "body"
        };
        
        foreach (string selector in contentSelectors)
        {
            try
            {
                var elements = await page.QuerySelectorAllAsync(selector);
                if (elements.Count > 0)
                {
                    var textContent = await elements[0].InnerTextAsync();
                    if (!string.IsNullOrWhiteSpace(textContent) && textContent.Length > 100)
                    {
                        return textContent;
                    }
                }
            }
            catch
            {
                continue;
            }
        }
        
        // 如果没有找到特定的内容区域，获取整个body的文本
        try
        {
            return await page.InnerTextAsync("body");
        }
        catch
        {
            return await page.TextContentAsync("body") ?? "";
        }
    }
    
    /// <summary>
    /// 清理文本内容
    /// </summary>
    private string CleanText(string text)
    {
        if (string.IsNullOrEmpty(text))
            return string.Empty;
            
        // 移除多余的空白字符
        text = Regex.Replace(text, @"\s+", " ");
        
        // 移除多余的换行符
        text = Regex.Replace(text, @"\n\s*\n", "\n\n");
        
        // 移除行首行尾的空白
        var lines = text.Split('\n');
        var cleanedLines = new List<string>();
        
        foreach (var line in lines)
        {
            var trimmedLine = line.Trim();
            if (!string.IsNullOrEmpty(trimmedLine))
            {
                cleanedLines.Add(trimmedLine);
            }
        }
        
        return string.Join("\n", cleanedLines);
    }
    
    /// <summary>
    /// 释放资源
    /// </summary>
    public async Task DisposeAsync()
    {
        if (_browser != null)
        {
            await _browser.CloseAsync();
            _browser = null;
        }
        
        if (_playwright != null)
        {
            _playwright.Dispose();
            _playwright = null;
        }
    }
}
