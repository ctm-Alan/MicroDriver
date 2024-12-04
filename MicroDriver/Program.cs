using System.Text;
using System.Text.RegularExpressions;

namespace MicroDriver;

internal abstract class Program
{
    static async Task Main(string[] args)
    {
        var path = args.Length > 0 ? args[0] : Directory.GetCurrentDirectory();

        if (File.Exists(path) && Path.GetExtension(path) == ".http")
        {
            DisplayMessage($"Executing file: {path}", ConsoleColor.Green);
            await ExecuteHttpFile(await File.ReadAllTextAsync(path, Encoding.UTF8));
        }
        else if (Directory.Exists(path))
        {
            var httpFiles = Directory.GetFiles(path, "*.http");
            if (httpFiles.Length == 0)
            {
                DisplayMessage("No .http files found in the specified directory.", ConsoleColor.Red);
                return;
            }

            var selectedFile = ShowFileSelectionMenu(httpFiles);
            DisplayMessage($"Executing file: {selectedFile}", ConsoleColor.Green);
            await ExecuteHttpFile(await File.ReadAllTextAsync(selectedFile, Encoding.UTF8));
        }
        else
        {
            DisplayMessage("Invalid path or file type. Please provide a valid directory or .http file.", ConsoleColor.Red);
        }
    }

    private static string ShowFileSelectionMenu(string[] files)
    {
        var selectedIndex = 0;
        ConsoleKey key;

        do
        {
            Console.Clear();
            DisplayMessage("Use the arrow keys to navigate and press Enter to select a file:", ConsoleColor.Cyan);

            for (var i = 0; i < files.Length; i++)
            {
                if (i == selectedIndex)
                {
                    HighlightText(Path.GetFileName(files[i]));
                }
                else
                {
                    Console.WriteLine(Path.GetFileName(files[i]));
                }
            }

            key = Console.ReadKey(true).Key;

            selectedIndex = key switch
            {
                ConsoleKey.UpArrow => (selectedIndex > 0) ? selectedIndex - 1 : files.Length - 1,
                ConsoleKey.DownArrow => (selectedIndex + 1) % files.Length,
                _ => selectedIndex
            };

        } while (key != ConsoleKey.Enter);

        return files[selectedIndex];
    }

    static async Task ExecuteHttpFile(string fileContent)
{
    var requestLineRegex = new Regex(@"^(GET|POST|PUT|DELETE|PATCH) (.+)$", RegexOptions.Multiline);
    var headerRegex = new Regex(@"^([\w-]+): (.+)$", RegexOptions.Multiline);

    // Extract request line
    var requestLineMatch = requestLineRegex.Match(fileContent);
    if (!requestLineMatch.Success)
    {
        DisplayMessage("Invalid HTTP file format.", ConsoleColor.Red);
        return;
    }

    string method = requestLineMatch.Groups[1].Value;
    string url = requestLineMatch.Groups[2].Value;

    // Create the request message
    var requestMessage = new HttpRequestMessage(new HttpMethod(method), url);

    // Extract headers
    var headers = new Dictionary<string, string>();
    foreach (Match match in headerRegex.Matches(fileContent))
    {
        string headerKey = match.Groups[1].Value;
        string headerValue = match.Groups[2].Value;
        headers[headerKey] = headerValue;
    }

    // Extract body
    string body = string.Empty;
    int bodyIndex = fileContent.IndexOf("\n\n", StringComparison.Ordinal);
    if (bodyIndex != -1)
    {
        body = fileContent[(bodyIndex + 2)..].Trim();
    }

    // Add the body to the request if it exists
    if (!string.IsNullOrWhiteSpace(body))
    {
        var content = new StringContent(body, Encoding.UTF8);

        // Check for Content-Type in headers and apply it to the body
        if (headers.TryGetValue("Content-Type", out var contentType))
        {
            content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(contentType);
            headers.Remove("Content-Type"); // Avoid adding Content-Type to general headers
        }

        requestMessage.Content = content;
    }

    // Add remaining headers to the request
    foreach (var header in headers)
    {
        requestMessage.Headers.TryAddWithoutValidation(header.Key, header.Value);
    }

    using var client = new HttpClient();

    DisplayMessage($"Executing {method} {url}", ConsoleColor.Green);

    // Send the request and get the response
    var response = await client.SendAsync(requestMessage);

    DisplayMessage($"Status: {response.StatusCode}", ConsoleColor.Yellow);
    string responseBody = await response.Content.ReadAsStringAsync();
    DisplayMessage("Response:", ConsoleColor.Blue);
    Console.WriteLine(responseBody);
}
    
    static void DisplayMessage(string message, ConsoleColor color)
    {
        Console.ForegroundColor = color;
        Console.WriteLine(message);
        Console.ResetColor();
    }

    static void HighlightText(string text)
    {
        Console.BackgroundColor = ConsoleColor.DarkCyan;
        Console.ForegroundColor = ConsoleColor.White;
        Console.WriteLine(text);
        Console.ResetColor();
    }
}