using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Reflection;
using System.Threading.Tasks;
using System.Xml;
using HtmlAgilityPack;
using Microsoft.AspNetCore.Html;
using Microsoft.AspNetCore.Mvc;
using OfficeOpenXml;
using MedicineService.Models;
using Microsoft.EntityFrameworkCore;
using System.Net;
using Microsoft.Azure.Cosmos;

// For more information on enabling MVC for empty projects, visit https://go.microsoft.com/fwlink/?LinkID=397860

namespace MedicineService.Controllers
{
    
    [Route("api/[controller]")]
    [ApiController]
    public class HomeController : Controller
    {
        private readonly MyContext _myContext;
        public HomeController(MyContext myContext)
        {
            _myContext = myContext;
        }



        [HttpGet] // Change to HttpGet attribute
        public async Task<IActionResult> Index()
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

                                 var i= 0;
                               
                                foreach (var drugName in drugNames)
                                {
                                    var myId = i++;
                                    string myIdString = myId.ToString();
                                    var newMedicine = new Medicine
                                    {
                                        MedicineId = Guid.NewGuid().ToString(),
                                        Name = drugName,
                                        Price = GenerateRandomPrice()
                                    };

                                    await _myContext.AddAsync(newMedicine);
                                    await _myContext.SaveChangesAsync();
                                }
  
                            

                             

                                //await _myContext.AddRangeAsync(drugNames);
                                //await _myContext.SaveChangesAsync();
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
            return Ok("Drug names successfully saved to Cosmos DB.");
        }

        private decimal GenerateRandomPrice()
        {
            var random = new Random();
            return Convert.ToDecimal(random.NextDouble() * 100.0);
        }


    }
}

