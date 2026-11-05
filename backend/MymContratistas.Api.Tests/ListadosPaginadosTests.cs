using System.Collections;
using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using MymContratistas.Api.Controllers;
using MymContratistas.Api.Models;
using MymContratistas.Api.Services;
using Xunit;
using static MymContratistas.Api.Tests.Respuesta;

namespace MymContratistas.Api.Tests
{
    // Pruebas de HT-07: paginación, filtros estándar e índices en los listados de gastos y pagos
    public sealed class ListadosPaginadosTests : PruebaConBaseDeDatos
    {
        private readonly ObrasController _obras;
        private readonly int _obraId, _obraAjenaId, _usuarioCliente;

        public ListadosPaginadosTests()
        {
            var invitacion = new InvitacionService(new CorreoDePrueba(), new ConfigurationBuilder().Build(), NullLogger<InvitacionService>.Instance);
            _obras = new ObrasController(Db, new PasswordHasher<Usuario>(), invitacion, Notificaciones.Silenciosa(Db));

            var c1 = new Cliente { Nombre = "Del Río", Documento = "20512345678", TipoDocumento = "RUC" };
            var c2 = new Cliente { Nombre = "Otra Empresa", Documento = "20999999999", TipoDocumento = "RUC" };
            Db.Clientes.AddRange(c1, c2);
            Db.SaveChanges();
            var obra = new Obra { Nombre = "Los Cedros", Cliente = c1.Nombre, ClienteId = c1.Id };
            var ajena = new Obra { Nombre = "Torre Ajena", Cliente = c2.Nombre, ClienteId = c2.Id };
            Db.Obras.AddRange(obra, ajena);
            var rol = Db.Roles.Single(r => r.Nombre == NombresRol.Cliente).Id;
            var u = new Usuario { Nombres = "Del Río", Correo = "c@mail.com", RolId = rol, PasswordHash = "x", ClienteId = c1.Id };
            Db.Usuarios.Add(u);
            Db.SaveChanges();
            (_obraId, _obraAjenaId, _usuarioCliente) = (obra.Id, ajena.Id, u.Id);
        }

        // Fecha aaaa-mm-dd -> dd/MM/yyyy, como se guardan
        private static string F(int mes, int dia) => $"{dia:00}/{mes:00}/2026";

        private void Gasto(string fecha, string concepto, decimal monto = 100m, string partida = "Concreto", string proveedor = "Andina", string numero = "F001-1", string estado = "Registrado", int? obra = null)
        {
            Db.Gastos.Add(new Gasto { ObraId = obra ?? _obraId, Fecha = fecha, Concepto = concepto, Proveedor = proveedor, NumeroComprobante = numero, PartidaNombre = partida, Monto = monto, Estado = estado, ComprobanteUrl = "c.pdf", Archivo = "c.pdf" });
            Db.SaveChanges();
        }

        private void Pago(string fechaPago, string colaborador, decimal monto = 100m, string estado = "Registrado", string periodo = "01/08 – 06/08")
        {
            Db.Pagos.Add(new Pago { ObraId = _obraId, ColaboradorNombre = colaborador, Periodo = periodo, Monto = monto, FechaPago = fechaPago, Estado = estado });
            Db.SaveChanges();
        }

        private void MuchosGastos(int cantidad)
        {
            for (var i = 1; i <= cantidad; i++)
                Db.Gastos.Add(new Gasto { ObraId = _obraId, Fecha = F(1 + (i % 12), 1 + (i % 28)), Concepto = $"Gasto {i:000}", Proveedor = "P", PartidaNombre = "Concreto", Monto = i, Estado = "Registrado" });
            Db.SaveChanges();
        }

        private static List<object> Elementos(IActionResult r, string propiedad) =>
            ((IEnumerable)Valor(((ObjectResult)r).Value!, propiedad)).Cast<object>().ToList();

        private static object Cuerpo(IActionResult r) => ((ObjectResult)r).Value!;
        private static object Pag(IActionResult r) => Valor(Cuerpo(r), "paginacion");
        private static IEnumerable<string> Conceptos(IActionResult r) => Elementos(r, "gastos").Select(g => (string)Valor(g, "concepto"));

        // ---------- Gastos (personal) ----------

        [Fact]
        public async Task SinFiltros_devuelveLaPrimeraPaginaDe20_conLosTotales()
        {
            MuchosGastos(45);

            var r = await _obras.ListarGastos(_obraId);

            Assert.Equal(20, Elementos(r, "gastos").Count);
            Assert.Equal((1, 20, 45, 3), (Valor(Pag(r), "pagina"), Valor(Pag(r), "tamano"), Valor(Pag(r), "total"), Valor(Pag(r), "totalPaginas")));
        }

        [Fact]
        public async Task LasPaginas_siguenElOrdenPorFechaDelMasRecienteAlMasAntiguo_sinRepetirNiOmitir()
        {
            MuchosGastos(45);
            var vistos = new List<string>();
            var fechas = new List<DateTime>();

            for (var p = 1; p <= 3; p++)
            {
                var r = await _obras.ListarGastos(_obraId, new FiltroListado { Pagina = p });
                vistos.AddRange(Conceptos(r));
                fechas.AddRange(Elementos(r, "gastos").Select(g => DateTime.ParseExact((string)Valor(g, "fecha"), "dd/MM/yyyy", System.Globalization.CultureInfo.InvariantCulture)));
            }

            Assert.Equal(45, vistos.Distinct().Count());                              // ninguno repetido ni omitido
            Assert.Equal(fechas.OrderByDescending(f => f), fechas);                   // orden global, no solo dentro de cada página
        }

        [Fact]
        public async Task ElTamano_seRespeta_yTieneUnMaximoDe100()
        {
            MuchosGastos(130);

            Assert.Equal(5, Elementos(await _obras.ListarGastos(_obraId, new FiltroListado { Tamano = 5 }), "gastos").Count);
            var enorme = await _obras.ListarGastos(_obraId, new FiltroListado { Tamano = 100000 });
            Assert.Equal(100, Elementos(enorme, "gastos").Count);
            Assert.Equal(100, Valor(Pag(enorme), "tamano"));
            Assert.Equal(1, Elementos(await _obras.ListarGastos(_obraId, new FiltroListado { Tamano = 0 }), "gastos").Count);
        }

        [Fact]
        public async Task UnaPaginaFueraDeRango_seLlevaALaUltima()
        {
            MuchosGastos(45);

            var r = await _obras.ListarGastos(_obraId, new FiltroListado { Pagina = 99 });

            Assert.Equal(3, Valor(Pag(r), "pagina"));
            Assert.Equal(5, Elementos(r, "gastos").Count);
            Assert.Equal(1, Valor(Pag(await _obras.ListarGastos(_obraId, new FiltroListado { Pagina = -3 })), "pagina"));
        }

        [Fact]
        public async Task SinGastos_devuelveUnaPaginaVacia()
        {
            var r = await _obras.ListarGastos(_obraId);

            Assert.Empty(Elementos(r, "gastos"));
            Assert.Equal((1, 0, 1), (Valor(Pag(r), "pagina"), Valor(Pag(r), "total"), Valor(Pag(r), "totalPaginas")));
        }

        [Fact]
        public async Task Filtro_porEstado_sinImportarMayusculas_yRechazaUnEstadoDesconocido()
        {
            Gasto(F(8, 1), "vigente"); Gasto(F(8, 2), "anulado", estado: "Anulado");

            Assert.Equal(new[] { "anulado" }, Conceptos(await _obras.ListarGastos(_obraId, new FiltroListado { Estado = "anulado" })));
            Assert.Equal(new[] { "vigente" }, Conceptos(await _obras.ListarGastos(_obraId, new FiltroListado { Estado = "REGISTRADO" })));
            Assert.Equal(2, Elementos(await _obras.ListarGastos(_obraId, new FiltroListado { Estado = " " }), "gastos").Count);
            Assert.Equal(400, Estado(await _obras.ListarGastos(_obraId, new FiltroListado { Estado = "Cancelado" })));
        }

        [Fact]
        public async Task Filtro_porPartida_esExacto()
        {
            Gasto(F(8, 1), "a", partida: "Concreto"); Gasto(F(8, 2), "b", partida: "Encofrado"); Gasto(F(8, 3), "c", partida: "Concreto y acero");

            Assert.Equal(new[] { "b" }, Conceptos(await _obras.ListarGastos(_obraId, new FiltroListado { Partida = "Encofrado" })));
            Assert.Equal(new[] { "a" }, Conceptos(await _obras.ListarGastos(_obraId, new FiltroListado { Partida = "Concreto" })));
        }

        [Fact]
        public async Task Filtro_porTexto_buscaEnConceptoProveedorYNumero_sinImportarMayusculas()
        {
            Gasto(F(8, 1), "Cemento Sol", proveedor: "Andina", numero: "F001-1");
            Gasto(F(8, 2), "Fierro", proveedor: "Aceros CEMENTERA", numero: "F001-2");
            Gasto(F(8, 3), "Arena", proveedor: "Cantera", numero: "B777-9");

            Assert.Equal(new[] { "Fierro", "Cemento Sol" }, Conceptos(await _obras.ListarGastos(_obraId, new FiltroListado { Q = "  CEMENT " })));
            Assert.Equal(new[] { "Arena" }, Conceptos(await _obras.ListarGastos(_obraId, new FiltroListado { Q = "b777" })));
            Assert.Empty(Conceptos(await _obras.ListarGastos(_obraId, new FiltroListado { Q = "no existe" })));
        }

        [Fact]
        public async Task LaBusqueda_noInterpretaComodinesNiInyeccion()
        {
            Gasto(F(8, 1), "Cemento 100%"); Gasto(F(8, 2), "Otro");

            Assert.Equal(new[] { "Cemento 100%" }, Conceptos(await _obras.ListarGastos(_obraId, new FiltroListado { Q = "100%" })));
            Assert.Empty(Conceptos(await _obras.ListarGastos(_obraId, new FiltroListado { Q = "'; DROP TABLE Gastos; --" })));
            Assert.Equal(2, await Db.Gastos.CountAsync());
        }

        [Fact]
        public async Task Filtro_porFechas_esInclusivo_yAceptaAmbosFormatos()
        {
            Gasto(F(7, 31), "julio"); Gasto(F(8, 1), "inicio"); Gasto(F(8, 15), "medio"); Gasto(F(8, 31), "fin"); Gasto(F(9, 1), "septiembre");

            var agosto = await _obras.ListarGastos(_obraId, new FiltroListado { Desde = "2026-08-01", Hasta = "2026-08-31" });
            var igual = await _obras.ListarGastos(_obraId, new FiltroListado { Desde = "01/08/2026", Hasta = "31/08/2026" });

            Assert.Equal(new[] { "fin", "medio", "inicio" }, Conceptos(agosto));
            Assert.Equal(Conceptos(agosto), Conceptos(igual));
            Assert.Equal(new[] { "septiembre", "fin" }, Conceptos(await _obras.ListarGastos(_obraId, new FiltroListado { Desde = "2026-08-31" })));
            Assert.Equal(new[] { "inicio", "julio" }, Conceptos(await _obras.ListarGastos(_obraId, new FiltroListado { Hasta = "2026-08-01" })));
        }

        [Fact]
        public async Task Filtro_porFechasInvalidas_o_invertidas_devuelve400()
        {
            Assert.Equal(400, Estado(await _obras.ListarGastos(_obraId, new FiltroListado { Desde = "ayer" })));
            Assert.Equal(400, Estado(await _obras.ListarGastos(_obraId, new FiltroListado { Hasta = "2026-13-45" })));
            var invertidas = await _obras.ListarGastos(_obraId, new FiltroListado { Desde = "2026-09-01", Hasta = "2026-08-01" });
            Assert.Equal(400, Estado(invertidas));
            Assert.Contains("posterior", (string)((ObjectResult)invertidas).Value!);
            Assert.Equal(400, Estado(await _obras.ListarGastos(_obraId, new FiltroListado { Q = new string('a', 101) })));
        }

        [Fact]
        public async Task UnGastoConFechaIlegible_apareceSinFiltroDeFechas_peroNoDentroDeUnRango()
        {
            Gasto("sin fecha", "raro"); Gasto(F(8, 5), "normal");

            Assert.Equal(2, Elementos(await _obras.ListarGastos(_obraId), "gastos").Count);
            Assert.Equal(new[] { "normal" }, Conceptos(await _obras.ListarGastos(_obraId, new FiltroListado { Desde = "2026-01-01" })));
        }

        [Fact]
        public async Task LosFiltrosSeCombinan_yLaPaginacionCuentaSoloLoFiltrado()
        {
            for (var i = 1; i <= 30; i++) Gasto(F(8, i % 28 + 1), $"Cemento {i:00}", partida: i % 2 == 0 ? "Concreto" : "Encofrado", monto: 10);
            for (var i = 1; i <= 10; i++) Gasto(F(8, i), $"Fierro {i:00}", partida: "Concreto", monto: 10);

            var r = await _obras.ListarGastos(_obraId, new FiltroListado { Q = "cemento", Partida = "Concreto", Tamano = 10, Pagina = 2 });

            Assert.Equal(15, Valor(Pag(r), "total"));
            Assert.Equal(2, Valor(Pag(r), "totalPaginas"));
            Assert.Equal(5, Elementos(r, "gastos").Count);
            Assert.All(Conceptos(r), c => Assert.StartsWith("Cemento", c));
        }

        [Fact]
        public async Task ElResumen_cuentaTodoLoFiltrado_yLaMontoVigenteIgnoraLosAnulados()
        {
            Gasto(F(8, 1), "uno", monto: 100); Gasto(F(8, 2), "dos", monto: 250); Gasto(F(8, 3), "anulado", monto: 999, estado: "Anulado");

            var r = await _obras.ListarGastos(_obraId, new FiltroListado { Tamano = 1 });

            var resumen = Valor(Cuerpo(r), "resumen");
            Assert.Equal(3, Valor(resumen, "cantidad"));
            Assert.Equal(350m, Valor(resumen, "montoVigente"));
            Assert.Single(Elementos(r, "gastos"));
        }

        [Fact]
        public async Task LasPartidas_noDependenDeLosFiltros()
        {
            Db.Partidas.AddRange(new Partida { ObraId = _obraId, Nombre = "Concreto", MontoAsignado = 1000 }, new Partida { ObraId = _obraId, Nombre = "Encofrado", MontoAsignado = 500 });
            Db.SaveChanges();
            Gasto(F(8, 1), "a", monto: 100, partida: "Concreto");

            var r = await _obras.ListarGastos(_obraId, new FiltroListado { Partida = "Encofrado" });

            Assert.Empty(Elementos(r, "gastos"));
            Assert.Equal(2, Elementos(r, "partidas").Count);
        }

        [Fact]
        public async Task LosGastosDeOtraObra_nuncaAparecen()
        {
            Gasto(F(8, 1), "propio"); Gasto(F(8, 1), "ajeno", obra: _obraAjenaId);

            Assert.Equal(new[] { "propio" }, Conceptos(await _obras.ListarGastos(_obraId, new FiltroListado { Q = "o" })));
            Assert.Equal(404, Estado(await _obras.ListarGastos(99999, new FiltroListado())));
        }

        // ---------- Pagos (personal) ----------

        [Fact]
        public async Task Pagos_paginanFiltranYOrdenanPorFechaDePago()
        {
            for (var i = 1; i <= 25; i++) Pago(F(8, i), i % 5 == 0 ? $"Elmer {i}" : $"Rómulo {i}", estado: i == 7 ? "Anulado" : "Registrado");

            var p1 = await _obras.ListarPagos(_obraId);
            var p2 = await _obras.ListarPagos(_obraId, new FiltroListado { Pagina = 2 });
            Assert.Equal(20, Elementos(p1, "pagos").Count);
            Assert.Equal(5, Elementos(p2, "pagos").Count);
            Assert.Equal((25, 2), (Valor(Pag(p1), "total"), Valor(Pag(p1), "totalPaginas")));
            Assert.Equal("25/08/2026", Valor(Elementos(p1, "pagos")[0], "fechaPago"));        // el más reciente primero

            Assert.Equal(5, Valor(Pag(await _obras.ListarPagos(_obraId, new FiltroListado { Q = "elmer" })), "total"));
            Assert.Equal(1, Valor(Pag(await _obras.ListarPagos(_obraId, new FiltroListado { Estado = "Anulado" })), "total"));
            Assert.Equal(3, Valor(Pag(await _obras.ListarPagos(_obraId, new FiltroListado { Desde = "2026-08-10", Hasta = "2026-08-12" })), "total"));
            Assert.Equal(400, Estado(await _obras.ListarPagos(_obraId, new FiltroListado { Estado = "x" })));
            Assert.Equal(404, Estado(await _obras.ListarPagos(99999)));
        }

        [Fact]
        public async Task Pagos_elResumenSumaLoFiltradoSinLosAnulados()
        {
            Pago(F(8, 1), "A", monto: 100); Pago(F(8, 2), "B", monto: 200); Pago(F(8, 3), "C", monto: 700, estado: "Anulado");

            var resumen = Valor(Cuerpo(await _obras.ListarPagos(_obraId, new FiltroListado { Tamano = 1 })), "resumen");

            Assert.Equal(3, Valor(resumen, "cantidad"));
            Assert.Equal(300m, Valor(resumen, "montoVigente"));
        }

        // ---------- Gastos (cliente) ----------

        private ClienteController Panel() => new(Db, Notificaciones.Silenciosa(Db))
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity(new[] { new Claim(ClaimTypes.NameIdentifier, _usuarioCliente.ToString()), new Claim(ClaimTypes.Name, "Del Río") }, "Prueba")) }
            }
        };

        [Fact]
        public async Task Cliente_elDetallePagina_peroElResumenYElTotalAbarcanTodo()
        {
            for (var i = 1; i <= 30; i++) Gasto(F(8, i % 28 + 1), $"Cemento {i:00}", monto: 10, partida: i <= 20 ? "Concreto" : "Encofrado");

            var r = await Panel().GastosDeObra(_obraId, new FiltroListado { Partida = "Encofrado", Tamano = 4 });

            Assert.Equal(300m, Valor(Cuerpo(r), "total"));
            Assert.Equal(30, Valor(Cuerpo(r), "cantidad"));
            var resumen = Elementos(r, "resumen");
            Assert.Equal(new[] { "Concreto", "Encofrado" }, resumen.Select(p => (string)Valor(p, "partida")));
            Assert.Equal(200m, Valor(resumen[0], "total"));
            Assert.Equal(4, Elementos(r, "gastos").Count);
            Assert.Equal((10, 3), (Valor(Pag(r), "total"), Valor(Pag(r), "totalPaginas")));
            Assert.Equal(10, Valor(Valor(Cuerpo(r), "filtrado"), "cantidad"));
            Assert.Equal(100m, Valor(Valor(Cuerpo(r), "filtrado"), "subtotal"));
        }

        [Fact]
        public async Task Cliente_filtraPorTextoYFechas_yOrdenaDelMasRecienteAlMasAntiguo()
        {
            Gasto(F(8, 1), "Cemento viejo"); Gasto(F(8, 20), "Cemento nuevo"); Gasto(F(9, 5), "Cemento septiembre"); Gasto(F(8, 10), "Fierro");

            var r = await Panel().GastosDeObra(_obraId, new FiltroListado { Q = "cemento", Desde = "2026-08-01", Hasta = "2026-08-31" });

            Assert.Equal(new[] { "Cemento nuevo", "Cemento viejo" }, Conceptos(r));
        }

        [Fact]
        public async Task Cliente_nuncaVeGastosAnulados_aunqueLosPidaPorEstado()
        {
            Gasto(F(8, 1), "vigente"); Gasto(F(8, 2), "anulado", estado: "Anulado");

            var r = await Panel().GastosDeObra(_obraId, new FiltroListado { Estado = "Anulado" });

            Assert.Equal(new[] { "vigente" }, Conceptos(r));
            Assert.Equal(1, Valor(Cuerpo(r), "cantidad"));
        }

        [Fact]
        public async Task Cliente_filtrosInvalidos_devuelven400_peroUnaObraAjenaSiguiendoSiendo403()
        {
            Assert.Equal(400, Estado(await Panel().GastosDeObra(_obraId, new FiltroListado { Desde = "x" })));
            Assert.Equal(403, Estado(await Panel().GastosDeObra(_obraAjenaId, new FiltroListado { Desde = "x" })));    // primero la autorización
            Assert.Equal(403, Estado(await Panel().GastosDeObra(_obraAjenaId, new FiltroListado { Q = "ajeno" })));
        }

        [Fact]
        public async Task Cliente_laPaginacionNoPermiteVerGastosDeOtraObra()
        {
            Gasto(F(8, 1), "propio"); Gasto(F(8, 1), "ajeno", obra: _obraAjenaId);

            var r = await Panel().GastosDeObra(_obraId, new FiltroListado { Tamano = 100 });

            Assert.Equal(new[] { "propio" }, Conceptos(r));
        }

        // ---------- Filtro: validación y rangos ----------

        [Theory]
        [InlineData("2026-08-15", "2026-08-15", true)]
        [InlineData("2026-08-14", "2026-08-15", false)]
        [InlineData("2026-08-16", "2026-08-15", true)]
        public void Rango_esInclusivoEnAmbosExtremos(string fecha, string limite, bool dentroDeDesde)
        {
            var f = DateTime.Parse(fecha);
            var l = DateTime.Parse(limite);
            Assert.Equal(dentroDeDesde, FiltroListado.EnRango(f, l, null));
            Assert.Equal(f <= l, FiltroListado.EnRango(f, null, l));
        }

        [Fact]
        public void SinFiltroDeFechas_todoEntra_inclusoLoIlegible_peroConFiltroNo()
        {
            Assert.True(FiltroListado.EnRango(DateTime.MinValue, null, null));
            Assert.False(FiltroListado.EnRango(DateTime.MinValue, new DateTime(2026, 1, 1), null));
            Assert.False(FiltroListado.EnRango(DateTime.MinValue, null, new DateTime(2026, 1, 1)));
        }

        // ---------- Índices ----------

        [Theory]
        [InlineData(typeof(Gasto), "ObraId,Estado")]
        [InlineData(typeof(Gasto), "ObraId,PartidaNombre")]
        [InlineData(typeof(Pago), "ObraId,Estado")]
        [InlineData(typeof(Pago), "ObraId,PartidaNombre")]
        [InlineData(typeof(Hito), "ObraId,Orden")]
        [InlineData(typeof(AvanceHito), "HitoId,Fecha")]
        [InlineData(typeof(ComentarioHito), "HitoId,Fecha")]
        [InlineData(typeof(Notificacion), "UsuarioId,Canal,Leida,Fecha")]
        public void LosListados_tienenSuIndiceCompuesto(Type entidad, string columnas)
        {
            var indices = Db.Model.FindEntityType(entidad)!.GetIndexes().Select(i => string.Join(",", i.Properties.Select(p => p.Name))).ToList();

            Assert.Contains(columnas, indices);
        }

        [Fact]
        public async Task LosIndices_existenEnLaBaseDeDatos()
        {
            var conexion = Db.Database.GetDbConnection();
            await using var comando = conexion.CreateCommand();
            comando.CommandText = "SELECT name FROM sqlite_master WHERE type='index' AND tbl_name IN ('Gastos','Pagos')";
            var nombres = new List<string>();
            await using (var lector = await comando.ExecuteReaderAsync())
                while (await lector.ReadAsync()) nombres.Add(lector.GetString(0));

            Assert.Contains("IX_Gastos_ObraId_Estado", nombres);
            Assert.Contains("IX_Gastos_ObraId_PartidaNombre", nombres);
            Assert.Contains("IX_Pagos_ObraId_Estado", nombres);
        }
    }
}
