using System.Collections;
using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using MymContratistas.Api.Controllers;
using MymContratistas.Api.Models;
using Xunit;
using static MymContratistas.Api.Tests.Respuesta;

namespace MymContratistas.Api.Tests
{
    // Pruebas funcionales y de privacidad de HU21: gastos con comprobantes para el cliente (HU21-T6)
    public sealed class GastosClienteTests : PruebaConBaseDeDatos
    {
        private readonly int _usuarioId, _obraId, _usuarioAjenoId, _obraAjenaId;
        private readonly int _gastoCementoId, _gastoAnuladoId, _gastoAjenoId;

        public GastosClienteTests()
        {
            var rol = Db.Roles.Single(r => r.Nombre == NombresRol.Cliente);

            (int usuario, int obra) CrearCliente(string nombre, string documento, string obraNombre)
            {
                var cliente = new Cliente { Nombre = nombre, Documento = documento, TipoDocumento = "RUC" };
                Db.Clientes.Add(cliente);
                Db.SaveChanges();
                var obra = new Obra { Nombre = obraNombre, Cliente = nombre, ClienteId = cliente.Id, AvancePorcentaje = 30 };
                Db.Obras.Add(obra);
                var usuario = new Usuario { Nombres = nombre, Correo = $"{documento}@cliente.pe", RolId = rol.Id, PasswordHash = "x", ClienteId = cliente.Id };
                Db.Usuarios.Add(usuario);
                Db.SaveChanges();
                return (usuario.Id, obra.Id);
            }

            Gasto NuevoGasto(int obraId, string fecha, string concepto, string partida, decimal monto, string estado = "Registrado")
            {
                var archivo = Guid.NewGuid().ToString("N") + ".pdf";
                Directory.CreateDirectory(Path.Combine(Carpeta, "Uploads", "gastos"));
                File.WriteAllBytes(Path.Combine(Carpeta, "Uploads", "gastos", archivo), Archivos.Pdf());
                var g = new Gasto
                {
                    ObraId = obraId, Fecha = fecha, Concepto = concepto, Proveedor = "Proveedor " + concepto, PartidaNombre = partida,
                    Monto = monto, Estado = estado, TipoComprobante = "Factura", NumeroComprobante = "F001-" + concepto.Length,
                    ComprobanteUrl = concepto + ".pdf", Archivo = archivo,
                    MotivoAnulacion = estado == "Anulado" ? "Factura duplicada reservada" : null
                };
                Db.Gastos.Add(g);
                Db.SaveChanges();
                return g;
            }

            (_usuarioId, _obraId) = CrearCliente("Inversiones Del Río", "20512345678", "Los Cedros");
            (_usuarioAjenoId, _obraAjenaId) = CrearCliente("Otra Empresa", "20999999999", "Torre Ajena");

            _gastoCementoId = NuevoGasto(_obraId, "22/08/2026", "Cemento", "Concreto y acero", 5940m).Id;
            NuevoGasto(_obraId, "14/08/2026", "Fierro", "Concreto y acero", 14800m);
            NuevoGasto(_obraId, "19/08/2026", "Encofrado", "Encofrado", 8200m);
            _gastoAnuladoId = NuevoGasto(_obraId, "11/08/2026", "Flete", "Concreto y acero", 1200m, "Anulado").Id;
            _gastoAjenoId = NuevoGasto(_obraAjenaId, "01/08/2026", "Gasto ajeno", "Acabados", 777m).Id;

            // Pagos al personal y presupuesto: nunca deben llegar al cliente
            Db.Partidas.Add(new Partida { ObraId = _obraId, Nombre = "Concreto y acero", MontoAsignado = 186000m });
            Db.Pagos.Add(new Pago { ObraId = _obraId, ColaboradorNombre = "Rómulo Ccahuana Ayala", Periodo = "11/08 – 16/08", Monto = 1080m, FechaPago = "17/08/2026" });
            Db.SaveChanges();
        }

        private ClienteController Panel(int? usuarioId = null, string nombre = "Inversiones Del Río") => new(Db, Notificaciones.Silenciosa())
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity(new[]
                    {
                        new Claim(ClaimTypes.NameIdentifier, (usuarioId ?? _usuarioId).ToString()),
                        new Claim(ClaimTypes.Name, nombre)
                    }, "Prueba"))
                }
            }
        };

        private static void AssertSinPermiso(IActionResult r)
        {
            Assert.Equal(403, Estado(r));
            Assert.Equal(ClienteController.MensajeSinPermiso, (string)((ObjectResult)r).Value!);
        }

        private static object Cuerpo(IActionResult r) => ((ObjectResult)r).Value!;

        private static List<object> Elementos(object origen, string propiedad) =>
            ((IEnumerable)Valor(origen, propiedad)).Cast<object>().ToList();

        // ---- Resumen acumulado por partida (T1) ----

        [Fact]
        public async Task Resumen_acumulaLosGastosVigentesPorPartida()
        {
            var r = await Panel().GastosDeObra(_obraId);

            Assert.Equal(200, Estado(r));
            var resumen = Elementos(Cuerpo(r), "resumen");
            Assert.Equal(new[] { "Concreto y acero", "Encofrado" }, resumen.Select(p => (string)Valor(p, "partida")));
            Assert.Equal(20740m, Valor(resumen[0], "total"));
            Assert.Equal(2, Valor(resumen[0], "cantidad"));
            Assert.Equal(8200m, Valor(resumen[1], "total"));
            Assert.Equal(28940m, Valor(Cuerpo(r), "total"));
        }

        // ---- Detalle de gastos (T2) ----

        [Fact]
        public async Task Detalle_incluyeFechaConceptoProveedorYMonto_delMasRecienteAlMasAntiguo()
        {
            var gastos = Elementos(Cuerpo(await Panel().GastosDeObra(_obraId)), "gastos");

            Assert.Equal(new[] { "Cemento", "Encofrado", "Fierro" }, gastos.Select(g => (string)Valor(g, "concepto")));
            Assert.Equal("22/08/2026", Valor(gastos[0], "fecha"));
            Assert.Equal("Proveedor Cemento", Valor(gastos[0], "proveedor"));
            Assert.Equal("Concreto y acero", Valor(gastos[0], "partida"));
            Assert.Equal(5940m, Valor(gastos[0], "monto"));
        }

        [Fact]
        public async Task ObraSinGastos_devuelveListasVacias()
        {
            var nueva = new Obra { Nombre = "Sin gastos", Cliente = "Inversiones Del Río", ClienteId = Db.Usuarios.Find(_usuarioId)!.ClienteId };
            Db.Obras.Add(nueva);
            await Db.SaveChangesAsync();

            var cuerpo = Cuerpo(await Panel().GastosDeObra(nueva.Id));

            Assert.Equal(0m, Valor(cuerpo, "total"));
            Assert.Empty(Elementos(cuerpo, "resumen"));
            Assert.Empty(Elementos(cuerpo, "gastos"));
        }

        // ---- Comprobante protegido (T3) ----

        [Fact]
        public async Task Comprobante_delPropioProyecto_seEntregaConSuNombreOriginal()
        {
            var archivo = Assert.IsType<PhysicalFileResult>(await Panel().ComprobanteDeGasto(_gastoCementoId));

            Assert.Equal("application/pdf", archivo.ContentType);
            Assert.Equal("Cemento.pdf", archivo.FileDownloadName);
        }

        [Fact]
        public async Task Comprobante_deOtroCliente_oInexistente_devuelve403ConMensajeDePermisos()
        {
            AssertSinPermiso(await Panel().ComprobanteDeGasto(_gastoAjenoId));
            AssertSinPermiso(await Panel().ComprobanteDeGasto(99999));
        }

        [Fact]
        public async Task Comprobante_sinArchivoFisico_devuelve404()
        {
            foreach (var f in Directory.GetFiles(Path.Combine(Carpeta, "Uploads", "gastos"))) File.Delete(f);

            Assert.IsType<NotFoundObjectResult>(await Panel().ComprobanteDeGasto(_gastoCementoId));
        }

        [Fact]
        public async Task Comprobante_deGastoAnterior_seLeeDeLaCarpetaUploads()
        {
            Directory.CreateDirectory(Path.Combine(Carpeta, "Uploads"));
            File.WriteAllBytes(Path.Combine(Carpeta, "Uploads", "antiguo.pdf"), Archivos.Pdf());
            var antiguo = new Gasto { ObraId = _obraId, Fecha = "01/01/2026", Concepto = "Antiguo", PartidaNombre = "Otros", Monto = 10m, ComprobanteUrl = "antiguo.pdf" };
            Db.Gastos.Add(antiguo);
            await Db.SaveChangesAsync();

            Assert.IsType<PhysicalFileResult>(await Panel().ComprobanteDeGasto(antiguo.Id));
        }

        // ---- Información que el cliente no debe ver (T4) ----

        [Fact]
        public async Task LaRespuesta_noExponePresupuestoPagosAlPersonalNiMotivosDeAnulacion()
        {
            var json = JsonSerializer.Serialize(Cuerpo(await Panel().GastosDeObra(_obraId)));

            foreach (var prohibido in new[] { "Rómulo", "Ccahuana", "colaborador", "periodo", "montoAsignado", "saldo", "presupuest", "margen",
                                              "motivo", "Factura duplicada", "archivo", "1080", "186000" })
                Assert.DoesNotContain(prohibido, json, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public async Task LosPagosAlPersonal_noSumanAlTotalNiAlResumen()
        {
            var cuerpo = Cuerpo(await Panel().GastosDeObra(_obraId));

            Assert.Equal(28940m, Valor(cuerpo, "total")); // sin los 1,080 pagados al personal
            Assert.DoesNotContain(Elementos(cuerpo, "resumen"), p => (string)Valor(p, "partida") == Pago.PartidaManoDeObra);
        }

        // ---- Gastos anulados (T5) ----

        [Fact]
        public async Task LosGastosAnulados_noSeListanNiSeSuman()
        {
            var cuerpo = Cuerpo(await Panel().GastosDeObra(_obraId));

            Assert.DoesNotContain(Elementos(cuerpo, "gastos"), g => (string)Valor(g, "concepto") == "Flete");
            Assert.Equal(20740m, Valor(Elementos(cuerpo, "resumen")[0], "total")); // no incluye los 1,200 anulados
        }

        [Fact]
        public async Task AlAnularUnGasto_desapareceDeLaVistaYDeLosMontos()
        {
            var cemento = await Db.Gastos.FindAsync(_gastoCementoId);
            cemento!.Estado = "Anulado";
            cemento.MotivoAnulacion = "Error de digitación";
            cemento.FechaAnulacion = DateTime.UtcNow;
            await Db.SaveChangesAsync();

            var cuerpo = Cuerpo(await Panel().GastosDeObra(_obraId));

            Assert.Equal(23000m, Valor(cuerpo, "total")); // Fierro 14,800 + Encofrado 8,200
            Assert.DoesNotContain(Elementos(cuerpo, "gastos"), g => (string)Valor(g, "concepto") == "Cemento");
            Assert.IsType<NotFoundObjectResult>(await Panel().ComprobanteDeGasto(_gastoCementoId));
        }

        [Fact]
        public async Task Comprobante_deUnGastoAnulado_nuncaSeEntrega()
        {
            Assert.IsType<NotFoundObjectResult>(await Panel().ComprobanteDeGasto(_gastoAnuladoId));
        }

        // ---- Aislamiento entre clientes (T6) ----

        [Fact]
        public async Task GastosDeUnProyectoAjeno_oInexistente_devuelve403()
        {
            AssertSinPermiso(await Panel().GastosDeObra(_obraAjenaId));
            AssertSinPermiso(await Panel().GastosDeObra(99999));
        }

        [Fact]
        public async Task CadaClienteVeSoloSusGastos()
        {
            var ajeno = Panel(_usuarioAjenoId, "Otra Empresa");

            var gastos = Elementos(Cuerpo(await ajeno.GastosDeObra(_obraAjenaId)), "gastos");

            Assert.Equal("Gasto ajeno", Valor(Assert.Single(gastos), "concepto"));
            AssertSinPermiso(await ajeno.GastosDeObra(_obraId));
            AssertSinPermiso(await ajeno.ComprobanteDeGasto(_gastoCementoId));
        }

        [Fact]
        public async Task UnaCuentaSinFicha_noVeGastosNiComprobantes()
        {
            var rol = Db.Roles.Single(r => r.Nombre == NombresRol.Cliente);
            var huerfano = new Usuario { Nombres = "Sin Ficha", Correo = "sin@ficha.pe", RolId = rol.Id, PasswordHash = "x" };
            Db.Usuarios.Add(huerfano);
            await Db.SaveChangesAsync();
            var panel = Panel(huerfano.Id, "Sin Ficha");

            AssertSinPermiso(await panel.GastosDeObra(_obraId));
            AssertSinPermiso(await panel.ComprobanteDeGasto(_gastoCementoId));
        }
    }
}
