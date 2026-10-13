using System;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

class Program
{
    private static readonly HttpClient client = new HttpClient();
    private const string OllamaGenerateUrl = "http://localhost:11434/api/generate";

    static async Task Main(string[] args)
    {
        // 1. UPDATE PATH TO YOUR IMAGE FILE
        string imagePath = @"C:\Users\Hassan\Pictures\Codex Image.jpg"; // Set to your real file path

        if (!File.Exists(imagePath))
        {
            Console.WriteLine($"[Error] Image file not found at: {imagePath}");
            return;
        }

        Console.WriteLine("Reading image and converting to Base64 byte arrays...");
        byte[] imageBytes = await File.ReadAllBytesAsync(imagePath);
        string base64Image = Convert.ToBase64String(imageBytes);

        // 2. ASK OLLAMA'S MULTIMODAL MODEL (GEMMA4) TO EXPLAIN WHAT IT SEES
        Console.WriteLine("\n--- Step 1: Querying Ollama (gemma4:12b) Vision System ---");
        string visionPrompt = "Describe what you see in this picture in detail. Mention major elements like trees, sky, or weather.";

        var visionPayload = new
        {
            model = "gemma4:12b", // Using your native 12B model that supports image inputs
            prompt = visionPrompt,
            images = new string[] { base64Image }, // Ollama maps the token matrices natively
            stream = false
        };

        try
        {
            string visionJson = JsonSerializer.Serialize(visionPayload);
            var visionContent = new StringContent(visionJson, Encoding.UTF8, "application/json");

            HttpResponseMessage visionResponse = await client.PostAsync(OllamaGenerateUrl, visionContent);
            visionResponse.EnsureSuccessStatusCode();

            string visionResultJson = await visionResponse.Content.ReadAsStringAsync();
            using JsonDocument visionDoc = JsonDocument.Parse(visionResultJson);
            string sceneDescription = visionDoc.RootElement.GetProperty("response").GetString();

            Console.WriteLine($"\n[Gemma4 Analysis Result]:\n{sceneDescription}\n");

            // 3. HAND OFF CHAT INTERPRETATION TO GPT-OSS:20B
            string userQuestion = "What is in this picture? Describe what you see.";
            Console.WriteLine($"--- Step 2: Handoff to Ollama Core Brain (gpt-oss:20b) ---");
            Console.WriteLine($"Forwarding context to text model...");

            string structuredPrompt = $"Context describing an image provided by a vision processing node:\n\"\"\"\n{sceneDescription}\n\"\"\"\n\nUser Question: {userQuestion}\nAnswer:";

            var finalPayload = new
            {
                model = "gpt-oss:20b",
                prompt = structuredPrompt,
                stream = false
            };

            string finalJson = JsonSerializer.Serialize(finalPayload);
            var finalContent = new StringContent(finalJson, Encoding.UTF8, "application/json");

            HttpResponseMessage finalResponse = await client.PostAsync(OllamaGenerateUrl, finalContent);
            finalResponse.EnsureSuccessStatusCode();

            string finalResultJson = await finalResponse.Content.ReadAsStringAsync();
            using JsonDocument finalDoc = JsonDocument.Parse(finalResultJson);
            string llmResponse = finalDoc.RootElement.GetProperty("response").GetString();

            Console.WriteLine("\n--- Final Ollama Response (gpt-oss:20b) ---");
            Console.WriteLine(llmResponse);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"\n[Error] Pipeline Execution Crash: {ex.Message}");
            Console.WriteLine("Make sure Ollama is open and running in your taskbar on port 11434.");
        }
    }
}
