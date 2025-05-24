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
        _playwright = await Playwright.CreateAsync();
        
        // Docker 容器中的 Chromium 启动配置
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
                "--disable-default-apps"
            }
        });
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
            try
            {
                await page.WaitForLoadStateAsync(LoadState.NetworkIdle, new PageWaitForLoadStateOptions
                {
                    Timeout = Math.Min(timeout, 10000) // 最多等待10秒
                });
            }
            catch (TimeoutException)
            {
                // 网络空闲等待超时，继续处理
                Console.WriteLine("网络空闲等待超时，继续处理页面内容");
            }
            
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
        try
        {
            await page.EvaluateAsync(@"
                // 移除脚本和样式
                document.querySelectorAll('script, style, noscript').forEach(el => el.remove());
                
                // 移除导航和页脚
                document.querySelectorAll('nav, header, footer').forEach(el => el.remove());
                
                // 移除广告相关元素
                const adSelectors = [
                    '[class*=""ad""]', '[id*=""ad""]', '[class*=""banner""]',
                    '.advertisement', '.ads', '.sidebar', '.menu',
                    '#advertisement', '#ads', '#sidebar', '#menu'
                ];
                
                adSelectors.forEach(selector => {
                    try {
                        document.querySelectorAll(selector).forEach(el => {
                            if (el.offsetHeight < 100 || el.innerText.length < 50) {
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
        // 尝试多种选择器来获取主要内容
        string[] contentSelectors = {
            "article", "main", "[role='main']", ".content", "#content",
            ".post", ".entry", ".article", ".container", ".wrapper"
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