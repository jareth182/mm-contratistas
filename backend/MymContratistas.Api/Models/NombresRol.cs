namespace MymContratistas.Api.Models
{
    public static class NombresRol
    {
        public const string Administrador = "Administrador";
        public const string Gerente = "Gerente de Operaciones";
        public const string Supervisor = "Supervisor de Obra";
        public const string Cliente = "Cliente";

        // Roles internos de M/M Contratistas (todo menos el cliente)
        public const string Personal = Administrador + "," + Gerente + "," + Supervisor;
    }
}
