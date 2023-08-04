using iTextSharp.text;
using iTextSharp.text.pdf;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using System.Text;

namespace Employee_Csv_Generator
{
    public class HomeController : Controller
    {
        const string API_KEY = "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";
        private readonly ILogger<HomeController> _logger;
        static readonly HttpClient client = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(500) // Set a timeout of 500 seconds (5 minutes)
        };

        public HomeController(ILogger<HomeController> logger)
        {
            _logger = logger;
        }

        public IActionResult Index()
        {
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> Get(string prompt, IFormFile file1, IFormFile file2, string filename)
        {
            string inputPrompt = prompt;

            List<string> allData = new List<string>();

            if (file1 != null)
            {
                try
                {
                    // Read and parse the first CSV file
                    using (var reader = new StreamReader(file1.OpenReadStream()))
                    {
                        List<string[]> rows = new List<string[]>();
                        while (!reader.EndOfStream)
                        {
                            string line = await reader.ReadLineAsync();
                            string[] data = line.Split(',');
                            rows.Add(data);
                        }

                        // Filter the data you want to include in the prompt from the CSV file
                        // For example, you can join specific columns or rows to create the input prompt
                        List<string> filteredData = rows.Select(row => string.Join(", ", row)).ToList();
                        allData.AddRange(filteredData);
                    }
                }
                catch (Exception ex)
                {
                    return Json("Error reading the CSV file 1: " + ex.Message);
                }
            }

            if (file2 != null)
            {
                try
                {
                    // Read and parse the second CSV file
                    using (var reader = new StreamReader(file2.OpenReadStream()))
                    {
                        List<string[]> rows = new List<string[]>();
                        while (!reader.EndOfStream)
                        {
                            string line = await reader.ReadLineAsync();
                            string[] data = line.Split(',');
                            rows.Add(data);
                        }

                        // Filter the data you want to include in the prompt from the CSV file
                        // For example, you can join specific columns or rows to create the input prompt
                        List<string> filteredData = rows.Select(row => string.Join(", ", row)).ToList();
                        allData.AddRange(filteredData);
                    }
                }
                catch (Exception ex)
                {
                    return Json("Error reading the CSV file 2: " + ex.Message);
                }
            }

            // Combine all the filtered data and the user-provided prompt to create the final input prompt
            inputPrompt = string.Join("\n", allData) + "\n\n" + prompt;

            if (string.IsNullOrEmpty(inputPrompt))
            {
                return Json("Please provide a prompt.");
            }

            var options = new Dictionary<string, object>
            {
                { "model", "gpt-3.5-turbo" },
                { "max_tokens", 3500 },
                { "temperature", 0.1 }
            };

            client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", API_KEY);

            try
            {
                options["messages"] = new[]
                {
                    new
                    {
                        role = "user",
                        content = inputPrompt
                    }
                };

                var json = JsonConvert.SerializeObject(options);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                var startTime = DateTime.Now;
                var response = await client.PostAsync("https://api.openai.com/v1/chat/completions", content);
                response.EnsureSuccessStatusCode();

                var responseBody = await response.Content.ReadAsStringAsync();
                dynamic jsonResponse = JsonConvert.DeserializeObject(responseBody);
                string result = jsonResponse.choices[0].message.content;
                var endTime = DateTime.Now;
                var responseTime = (endTime - startTime).TotalMilliseconds;

                // Save the data to a CSV file with the specified filename
                string csvContent = $"Generated Response: {result}";
                string filePathCSV = Path.Combine(Path.GetTempPath(), $"{filename}.csv");
                await System.IO.File.WriteAllTextAsync(filePathCSV, csvContent, Encoding.UTF8);

                // Generate PDF using iTextSharp
                string filePathPDF = Path.Combine(Path.GetTempPath(), $"{filename}.pdf");
                using (FileStream fs = new FileStream(filePathPDF, FileMode.Create))
                {
                    Document document = new Document(PageSize.A4);
                    PdfWriter writer = PdfWriter.GetInstance(document, fs);
                    document.Open();
                    document.Add(new Paragraph($"Generated Response:\n\n{result}"));
                    document.Close();
                }

                ViewBag.FileNames = new List<string> { $"{filename}.csv", $"{filename}.pdf" };
                return View("Index");
            }
            catch (Exception ex)
            {
                return Json(ex.Message);
            }
        }

        // Add a new action to download the CSV file for a specific filename
        public IActionResult Download(string fileName)
        {
            if (!string.IsNullOrEmpty(fileName))
            {
                string filePath = Path.Combine(Path.GetTempPath(), fileName);
                if (System.IO.File.Exists(filePath))
                {
                    byte[] fileBytes = System.IO.File.ReadAllBytes(filePath);
                    if (fileName.EndsWith(".csv"))
                    {
                        return File(fileBytes, "text/csv", fileName);
                    }
                    else if (fileName.EndsWith(".pdf"))
                    {
                        return File(fileBytes, "application/pdf", fileName);
                    }
                }
            }
            return NotFound();
        }
    }
}
