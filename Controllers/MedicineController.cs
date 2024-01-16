using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MedicineService.Models;
using HtmlAgilityPack;
using OfficeOpenXml;

[Route("api/[controller]")]
[ApiController]
public class MedicineController : ControllerBase
{
    private readonly CosmosClient _cosmosClient;
    private readonly Container _container;

    public MedicineController(IConfiguration configuration)
    {
        // Retrieve Cosmos DB settings from configuration
        string cosmosEndpoint = configuration["CosmosDb:EndpointUri"];
        string cosmosKey = configuration["CosmosDb:PrimaryKey"];
        string databaseName = configuration["CosmosDb:DatabaseId"];
        string containerName = configuration["CosmosDb:ContainerId"];

        // Create CosmosClient and Container instances
        _cosmosClient = new CosmosClient(cosmosEndpoint, cosmosKey);
        _container = _cosmosClient.GetContainer(databaseName, containerName);
    }

    [HttpGet]
    public async Task<IActionResult> GetAllMedicines(int page = 1, int pageSize = 10)
    {
        try
        {
            // Calculate the number of items to skip based on the page number and page size
            int itemsToSkip = (page - 1) * pageSize;

            var query = new QueryDefinition("SELECT * FROM c OFFSET @skip LIMIT @pageSize")
                .WithParameter("@skip", itemsToSkip)
                .WithParameter("@pageSize", pageSize);

            var iterator = _container.GetItemQueryIterator<Medicine>(query);

            List<Medicine> medicines = new List<Medicine>();
            while (iterator.HasMoreResults)
            {
                var response = await iterator.ReadNextAsync();
                medicines.AddRange(response.ToList());
            }

            return Ok(medicines);
        }
        catch (Exception ex)
        {
            return StatusCode(500, $"An error occurred: {ex.Message}");
        }
    }


    [HttpGet("SearchByName")]
    public async Task<IActionResult> SearchMedicineByName(string searchTerm)
    {
        try
        {
            // Use a parameterized query to perform case-insensitive search
            var query = new QueryDefinition("SELECT * FROM c WHERE CONTAINS(LOWER(c.name), @searchTerm)")
                .WithParameter("@searchTerm", searchTerm.ToLower());

            var iterator = _container.GetItemQueryIterator<Medicine>(query);

            List<Medicine> matchingMedicines = new List<Medicine>();
            while (iterator.HasMoreResults)
            {
                var response = await iterator.ReadNextAsync();
                matchingMedicines.AddRange(response.ToList());
            }

            return Ok(matchingMedicines);
        }
        catch (Exception ex)
        {
            return StatusCode(500, $"An error occurred: {ex.Message}");
        }
    }


    [HttpGet("{medicineId}")]
    public async Task<IActionResult> GetMedicineById(string medicineId)
    {
        try
        {
            var query = new QueryDefinition("SELECT * FROM c WHERE c.id = @id")
                .WithParameter("@id", medicineId);

            var iterator = _container.GetItemQueryIterator<Medicine>(query);

            while (iterator.HasMoreResults)
            {
                var response = await iterator.ReadNextAsync();
                var medicine = response.FirstOrDefault();

                if (medicine != null)
                {
                    return Ok(medicine);
                }
            }

            return NotFound($"Medicine with ID {medicineId} not found");
        }
        catch (Exception ex)
        {
            return StatusCode(500, $"An error occurred: {ex.Message}");
        }
    }





    [HttpGet("DeleteAndPopulate")]
    public async Task<IActionResult> DeleteAndPopulate()
    {
        try
        {
            // Delete all items in the container
            var query = new QueryDefinition("SELECT * FROM c");
            var iterator = _container.GetItemQueryIterator<dynamic>(query);

            while (iterator.HasMoreResults)
            {
                var response = await iterator.ReadNextAsync();
                foreach (var item in response)
                {
                    await _container.DeleteItemAsync<dynamic>(item.id.ToString(), new PartitionKey(item.medicineid.ToString()));
                }
            }

            // Call the Index method to populate the container with new items
            return await LoadMedicineDataFromExcel();
        }
        catch (Exception ex)
        {
            return StatusCode(500, $"An error occurred: {ex.Message}");
        }
    }


   // [HttpGet("LoadMedicineDataFromExcel")]
    private async Task<IActionResult> LoadMedicineDataFromExcel()
    {
        string baseUrl = "https://www.titck.gov.tr";
        string url = $"{baseUrl}/dinamikmodul/43";


        // EPPlus lisans contextini ayarla
        ExcelPackage.LicenseContext = LicenseContext.NonCommercial;


        using (var httpClient = new HttpClient())
        {
            try
            {
                var response = await httpClient.GetAsync(url);

                if (response.IsSuccessStatusCode)
                {
                    // Read the HTML content from the response
                    string content = await response.Content.ReadAsStringAsync();

                    // Parse HTML content with HtmlAgilityPack
                    var htmlDocument = new HtmlDocument();
                    htmlDocument.LoadHtml(content);

                    // Find the table with id "myTable"
                    var myTableDiv = htmlDocument.DocumentNode.SelectSingleNode("//table[@id='myTable']");

                    // Find the first a tag inside the div
                    var firstATag = myTableDiv?.SelectSingleNode(".//a");

                    if (firstATag != null)
                    {
                        // Get the href value of the first a tag
                        string excelLink = firstATag.GetAttributeValue("href", "");
                        Console.WriteLine($"Excel link: {excelLink}");

                        // Download and parse the Excel file
                        var excelResponse = await httpClient.GetAsync(excelLink);
                        excelResponse.EnsureSuccessStatusCode();

                        using (var memoryStream = new MemoryStream(await excelResponse.Content.ReadAsByteArrayAsync()))
                        using (var package = new ExcelPackage(memoryStream))
                        {

                            var worksheet = package.Workbook.Worksheets[0];

                            // Get all drug names in column A starting from row 4
                            var startRow = 4;
                            var endRow = worksheet.Dimension.End.Row;
                            var drugNames = worksheet.Cells[startRow, 1, endRow, 1].Select(cell => cell.Text);


                            foreach (var drugName in drugNames)
                            {
                                var newMedicine = new Medicine
                                {
                                    MedicineId = Guid.NewGuid().ToString(),
                                    Name = drugName,
                                    Price = GenerateRandomPrice()
                                };

                                await _container.CreateItemAsync(newMedicine, new PartitionKey(newMedicine.MedicineId));
                            }


                            Console.WriteLine($"Drug Names: {string.Join(", ", drugNames)}");

                        }

                    }
                    else
                    {
                        Console.WriteLine("No 'a' tag found inside the table with id 'myTable'.");
                    }

                }
                else
                {
                    Console.WriteLine($"Failed to retrieve HTML. Status code: {response.StatusCode}");
                }
            }
            catch (Exception e)
            {
                // Log the exception or handle it as needed
                Console.WriteLine($"Error while saving the item: {e.Message}");

                // Return an IActionResult with detailed error information
                return StatusCode(500, new
                {
                    error = $"An error occurred: {e.Message}",
                    innerError = e.InnerException?.Message
                });
            }
        }

        // Use IActionResult and return a response
        return Ok("Drug names successfully saved to DB.");
    }

    private decimal GenerateRandomPrice()
    {
        var random = new Random();
        double randomValue = random.NextDouble() * (400.0 - 30.0) + 30.0;
        decimal roundedValue = Math.Round((decimal)randomValue, 3);
        return roundedValue;
    }

}

