namespace MymContratistas.Api.Models
{
    public class Obra
    {
        public int Id { get; set; }
        public string Nombre { get; set; } = string.Empty;
        public string Cliente { get; set; } = string.Empty;
        public string Estado { get; set; } = "En Ejecución";
        public int AvancePorcentaje { get; set; }

        // Nueva relación: Una obra contiene una lista de hitos
        public List<Hito> Hitos { get; set; } = new List<Hito>();
        public List<Colaborador> Cuadrilla { get; set; } = new List<Colaborador>();
        public List<Documento> Documentos { get; set; } = new List<Documento>(); 
        public List<Partida> Partidas { get; set; } = new List<Partida>();
        public List<Gasto> Gastos { get; set; } = new List<Gasto>(); 
        public List<Pago> Pagos { get; set; } = new List<Pago>(); 
    }

}