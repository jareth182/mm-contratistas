namespace MymContratistas.Api.Models
{
    public class ResetPasswordDto
    {
        public string Token { get; set; } = string.Empty;
        public string NuevaPassword { get; set; } = string.Empty;
    }
}