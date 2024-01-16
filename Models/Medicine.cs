using System.ComponentModel.DataAnnotations;
using Newtonsoft.Json;

namespace MedicineService.Models
{
    public class Medicine
    {
        [JsonProperty("id")]
        public string Id { get; set; } = Guid.NewGuid().ToString();

        [JsonProperty("medicineid")]
        public required string MedicineId { get; set; }

        [JsonProperty("name")]
        public required string Name { get; set; }

        [JsonProperty("price")]
        public required decimal Price { get; set; }


    }
}
