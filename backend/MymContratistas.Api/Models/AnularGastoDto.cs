using System.ComponentModel.DataAnnotations;

namespace MymContratistas.Api.Models
{
    public class AnularGastoDto
    {
        [Required]
        [MinLength(3)]
        public string Motivo { get; set; } = string.Empty;
    }
}
