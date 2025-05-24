using System.Text;
using System.Text.RegularExpressions;
using Microsoft.Playwright;

namespace EpicMo.SoruxBot.Demo.Ability;

public class WebPageTextExtractor
{
    private IPlaywright _playwright;
    private IBrowser _browser;
    private readonly Random _random = new Random();
    
    // 常见的User-Agent列表
    private readonly string[] _userAgents = {
        "Mozilla/5.0 (Macintosh; Intel Mac OS X 10_15_7) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/136.0.0.0 Safari/537.36 Edg/136.0.0.0"
    };
    
    /// <summary>
    /// 初始化Playwright和浏览器
    /// </summary>
    public async Task InitializeAsync()
    {
        _playwright = await Playwright.CreateAsync();
        
        // 增强的浏览器启动配置，模拟真实用户
        _browser = await _playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions
        {
            Headless = true,
            Args = new[] { 
                "--no-sandbox", 
                "--disable-setuid-sandbox",
                "--disable-dev-shm-usage",
                "--disable-gpu",
                "--disable-extensions",
                "--no-first-run",
                "--disable-default-apps",
                "--disable-features=VizDisplayCompositor",
                "--disable-background-networking",
                "--disable-background-timer-throttling",
                "--disable-renderer-backgrounding",
                "--disable-backgrounding-occluded-windows",
                "--disable-ipc-flooding-protection",
                "--window-size=1920,1080",
                "--disable-blink-features=AutomationControlled"
            }
        });
    }
    
    /// <summary>
    /// 获取网页的全部文字内容
    /// </summary>
    /// <param name="url">要抓取的网页URL</param>
    /// <param name="timeout">超时时间(毫秒)，默认60秒</param>
    /// <param name="retryCount">重试次数，默认3次</param>
    /// <returns>网页的文字内容</returns>
    public async Task<string> ExtractWebPageTextAsync(string url, int timeout = 60000, int retryCount = 3)
    {
        if (string.IsNullOrEmpty(url))
            throw new ArgumentException("URL不能为空", nameof(url));
            
        if (_browser == null)
            await InitializeAsync();

        Exception lastException = null;
        
        for (int attempt = 1; attempt <= retryCount; attempt++)
        {
            IPage page = null;
            try
            {
                Console.WriteLine($"第 {attempt} 次尝试访问: {url}");
                
                // 创建上下文和页面，设置反检测
                var context = await _browser.NewContextAsync(new BrowserNewContextOptions
                {
                    UserAgent = _userAgents[_random.Next(_userAgents.Length)],
                    ViewportSize = new ViewportSize { Width = 1920, Height = 1080 },
                    JavaScriptEnabled = true,
                    ExtraHTTPHeaders = new Dictionary<string, string>
                    {
                        { "Accept", "text/html,application/xhtml+xml,application/xml;q=0.9,image/webp,*/*;q=0.8" },
                        { "Accept-Language", "zh-CN,zh;q=0.9,en;q=0.8" },
                        { "Accept-Encoding", "gzip, deflate, br" },
                        { "Connection", "keep-alive" },
                        { "Upgrade-Insecure-Requests", "1" },
                        { "Sec-Fetch-Dest", "document" },
                        { "Sec-Fetch-Mode", "navigate" },
                        { "Sec-Fetch-Site", "none" },
                        { "Cache-Control", "max-age=0" }
                    }
                });
                
                page = await context.NewPageAsync();
                
                await SetupAntiDetection(page);
                
                await Task.Delay(_random.Next(1000, 3000));
                
                var response = await page.GotoAsync(url, new PageGotoOptions
                {
                    WaitUntil = WaitUntilState.DOMContentLoaded,
                    Timeout = timeout
                });
                
                // 检查响应状态
                if (response != null && !response.Ok)
                {
                    Console.WriteLine($"HTTP响应状态: {response.Status}");
                }
                
                // 智能等待页面加载完成
                await WaitForPageReady(page, timeout);
                
                // 检查是否遇到防护页面
                if (await IsProtectionPage(page))
                {
                    Console.WriteLine("检测到防护页面，等待跳转...");
                    await WaitForProtectionBypass(page, timeout);
                }
                
                // 移除不需要的元素
                await RemoveUnwantedElementsAsync(page);
                
                // 获取页面标题
                string title = await page.TitleAsync();
                
                // 获取主要内容
                string mainContent = await ExtractMainContentAsync(page);
                
                // 验证内容质量
                if (IsContentValid(title, mainContent))
                {
                    // 构建最终文本
                    StringBuilder result = new StringBuilder();
                    if (!string.IsNullOrEmpty(title) && !title.Contains("Just a moment"))
                    {
                        result.AppendLine($"标题: {title}");
                        result.AppendLine();
                    }
                    
                    result.AppendLine("内容:");
                    result.AppendLine(mainContent);
                    
                    Console.WriteLine($"成功提取内容，第 {attempt} 次尝试");
                    return CleanText(result.ToString());
                }
                else
                {
                    throw new Exception($"提取的内容质量不佳，标题: {title}，内容长度: {mainContent?.Length ?? 0}");
                }
            }
            catch (Exception ex)
            {
                lastException = ex;
                Console.WriteLine($"第 {attempt} 次尝试失败: {ex.Message}");
                
                if (attempt < retryCount)
                {
                    // 重试前等待更长时间
                    int delay = _random.Next(3000, 8000) * attempt;
                    Console.WriteLine($"等待 {delay/1000} 秒后重试...");
                    await Task.Delay(delay);
                }
            }
            finally
            {
                if (page != null)
                {
                    try
                    {
                        await page.Context.CloseAsync();
                    }
                    catch { }
                }
            }
        }
        
        throw new Exception($"经过 {retryCount} 次尝试仍无法成功提取内容。最后错误: {lastException?.Message}", lastException);
    }
    
    /// <summary>
    /// 设置反检测机制
    /// </summary>
    private async Task SetupAntiDetection(IPage page)
    {
        try
        {
            await page.AddInitScriptAsync(@"
                // 移除webdriver标识
                Object.defineProperty(navigator, 'webdriver', {
                    get: () => undefined,
                });
                
                // 模拟真实的chrome对象
                window.chrome = {
                    runtime: {},
                    loadTimes: function() {},
                    csi: function() {},
                    app: {}
                };
                
                // 重写navigator.permissions
                const originalQuery = window.navigator.permissions.query;
                window.navigator.permissions.query = (parameters) => (
                    parameters.name === 'notifications' ?
                        Promise.resolve({ state: Notification.permission }) :
                        originalQuery(parameters)
                );
                
                // 模拟真实的插件
                Object.defineProperty(navigator, 'plugins', {
                    get: () => [1, 2, 3, 4, 5],
                });
                
                // 模拟真实的语言设置
                Object.defineProperty(navigator, 'languages', {
                    get: () => ['zh-CN', 'zh', 'en'],
                });
            ");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"设置反检测脚本时出错: {ex.Message}");
        }
    }
    
    private async Task WaitForPageReady(IPage page, int timeout)
    {
        try
        {
            await page.WaitForSelectorAsync("body", new PageWaitForSelectorOptions
            {
                Timeout = 10000
            });
            
            await Task.Delay(_random.Next(2000, 5000));
        }
        catch (TimeoutException)
        {
            Console.WriteLine("等待页面元素超时，继续处理");
        }
    }
    
    /// <summary>
    /// 检查是否为防护页面
    /// </summary>
    private async Task<bool> IsProtectionPage(IPage page)
    {
        try
        {
            var title = await page.TitleAsync();
            var bodyText = await page.InnerTextAsync("body");
            
            var protectionIndicators = new[]
            {
                "Just a moment",
                "Checking your browser",
                "DDoS protection",
                "Cloudflare",
                "Please wait",
                "Security check",
                "Bot protection",
                "Verifying you are human"
            };
            
            return protectionIndicators.Any(indicator => 
                title.Contains(indicator, StringComparison.OrdinalIgnoreCase) ||
                bodyText.Contains(indicator, StringComparison.OrdinalIgnoreCase));
        }
        catch
        {
            return false;
        }
    }
    
    /// <summary>
    /// 等待防护页面跳转
    /// </summary>
    private async Task WaitForProtectionBypass(IPage page, int timeout)
    {
        var maxWaitTime = Math.Min(timeout, 30000); // 最多等待30秒
        var checkInterval = 2000; // 每2秒检查一次
        var totalWaited = 0;
        
        while (totalWaited < maxWaitTime)
        {
            await Task.Delay(checkInterval);
            totalWaited += checkInterval;
            
            try
            {
                if (!await IsProtectionPage(page))
                {
                    Console.WriteLine($"防护页面已跳转，等待了 {totalWaited/1000} 秒");
                    // 额外等待确保页面完全加载
                    await Task.Delay(3000);
                    return;
                }
            }
            catch
            {
                // 继续等待
            }
        }
        
        Console.WriteLine("等待防护页面跳转超时");
    }
    
    /// <summary>
    /// 验证内容是否有效
    /// </summary>
    private bool IsContentValid(string title, string content)
    {
        if (string.IsNullOrWhiteSpace(content) || content.Length < 50)
            return false;
            
        if (!string.IsNullOrEmpty(title) && title.Contains("Just a moment"))
            return false;
            
        // 检查是否包含有意义的内容
        var meaningfulPatterns = new[]
        {
            @"[.\u4e00-\u9fff]{20,}", // 中文内容
            @"[a-zA-Z\s]{50,}", // 英文内容
            @"\d+", // 数字内容
        };
        
        return meaningfulPatterns.Any(pattern => Regex.IsMatch(content, pattern));
    }
    
    /// <summary>
    /// 移除不需要的页面元素
    /// </summary>
    private async Task RemoveUnwantedElementsAsync(IPage page)
    {
        try
        {
            await page.EvaluateAsync(@"
                // 移除脚本和样式
                document.querySelectorAll('script, style, noscript').forEach(el => el.remove());
                
                // 移除导航和页脚
                document.querySelectorAll('nav, header, footer').forEach(el => el.remove());
                
                // 移除广告和侧边栏相关元素
                const unwantedSelectors = [
                    '[class*=""ad""]', '[id*=""ad""]', '[class*=""banner""]',
                    '.advertisement', '.ads', '.sidebar', '.menu', '.popup',
                    '#advertisement', '#ads', '#sidebar', '#menu', '#popup',
                    '[class*=""comment""]', '[class*=""social""]', '[class*=""share""]',
                    '.cookie', '.modal', '.overlay'
                ];
                
                unwantedSelectors.forEach(selector => {
                    try {
                        document.querySelectorAll(selector).forEach(el => {
                            // 只移除小的或明显是广告的元素
                            if (el.offsetHeight < 100 || 
                                el.innerText.length < 50 || 
                                el.className.toLowerCase().includes('ad') ||
                                el.id.toLowerCase().includes('ad')) {
                                el.remove();
                            }
                        });
                    } catch (e) {
                        // 忽略选择器错误
                    }
                });
            ");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"移除不需要元素时出错: {ex.Message}");
        }
    }
    
    /// <summary>
    /// 提取主要内容
    /// </summary>
    private async Task<string> ExtractMainContentAsync(IPage page)
    {
        // 按优先级排序的内容选择器
        string[] contentSelectors = {
            "article", 
            "main", 
            "[role='main']", 
            ".content", 
            "#content",
            ".post-content",
            ".entry-content",
            ".article-content",
            ".post", 
            ".entry", 
            ".article",
            ".container",
            ".wrapper"
        };
        
        foreach (string selector in contentSelectors)
        {
            try
            {
                var elements = await page.QuerySelectorAllAsync(selector);
                if (elements.Count > 0)
                {
                    // 选择内容最多的元素
                    string bestContent = "";
                    foreach (var element in elements)
                    {
                        var textContent = await element.InnerTextAsync();
                        if (!string.IsNullOrWhiteSpace(textContent) && textContent.Length > bestContent.Length)
                        {
                            bestContent = textContent;
                        }
                    }
                    
                    if (bestContent.Length > 100)
                    {
                        return bestContent;
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"尝试选择器 {selector} 时出错: {ex.Message}");
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
            var textContent = await page.TextContentAsync("body");
            return textContent ?? "";
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
        text = Regex.Replace(text, @"\n\s*\n\s*\n", "\n\n");
        
        // 移除行首行尾的空白
        var lines = text.Split('\n');
        var cleanedLines = new List<string>();
        
        foreach (var line in lines)
        {
            var trimmedLine = line.Trim();
            if (!string.IsNullOrEmpty(trimmedLine) && trimmedLine.Length > 2)
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
