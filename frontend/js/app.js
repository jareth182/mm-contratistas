// Sesión restaurada desde el almacenamiento (si el usuario recarga la página)
ST.usuario = (sesionLeer() || {}).usuario || null;
ST.perfil = ST.usuario ? (ST.usuario.rol === 'Cliente' ? 'cli' : 'adm') : 'pub';

/* ================= control ================= */
function go(v){ST.formUsuario=null;ST.formHito=null;ST.formProyecto=null;ST.formObra=null;ST.finalizando=false;ST.v=v;ST.msg=null;ST.menu=null;ST.nav=false;draw();window.scrollTo({top:0})}
function abrirObra(id){ST.obraId=id;ST.formHito=null;ST.hitoEditando=null;ST.tab='hitos';go('obra')}
function tab(t){ST.tab=t;ST.msg=null;draw()}
function tog(m,e){e.stopPropagation();ST.menu=ST.menu===m?null:m;draw()}
function say(t,x){ST.msg=`<div class="msg ${t}">${x}</div>`;draw();
  const m=document.querySelector('.msg');if(m)m.scrollIntoView({block:'center'})}

// Control de acceso: vistas privadas solo con sesión y según el rol
function guardarAcceso() {
  const rol = ST.usuario ? ST.usuario.rol : null;
  ST.perfil = rol ? (rol === 'Cliente' ? 'cli' : 'adm') : 'pub';

  if (VISTAS_PRIVADAS.includes(ST.v)) {
    if (!rol) {
      ST.v = 'login';
      if (!ST.msg) ST.msg = '<div class="msg warn">Inicie sesión para continuar.</div>';
    } else if (!puedeVer(rol, ST.v)) {
      ST.v = vistaInicio(rol);
      ST.msg = '<div class="msg bad">No tiene permisos para acceder a esa sección.</div>';
    }
  } else if (rol && ST.v === 'login') {
    ST.v = vistaInicio(rol);
  }
}

async function draw(){
  guardarAcceso();
  const vista = V[ST.v] || V.home;
  document.getElementById('app').innerHTML = await vista();
  location.hash = ST.v === 'obra-pub' && ST.proyectoPubId ? `obra-pub?id=${ST.proyectoPubId}` : ST.v;
  cargarImagenesProtegidas();
}

// Las imágenes del portafolio exigen sesión: no pueden ir directo en un <img src>, se piden con el token
const imagenesCargadas = new Map();
async function cargarImagenesProtegidas() {
  for (const img of document.querySelectorAll('img[data-img-id]')) {
    const id = img.dataset.imgId;
    try {
      if (!imagenesCargadas.has(id)) {
        const r = await apiFetch(`${API}/api/portafolio/imagenes/${id}`);
        if (!r.ok) continue;
        imagenesCargadas.set(id, URL.createObjectURL(await r.blob()));
      }
      img.src = imagenesCargadas.get(id);
    } catch (e) { /* la miniatura queda vacía */ }
  }
}

// --- OBRAS ---

function abrirFormularioObra() {
  ST.formObra = { nombre: '', ubicacion: '', fechaInicio: '', fechaFin: '', descripcion: '', cliente: null };
  ST.msg = null;
  draw();
  setTimeout(() => { const f = document.getElementById('form_obra'); if (f) f.scrollIntoView({ behavior: 'smooth' }); }, 50);
}

function cancelarObra() {
  ST.formObra = null;
  ST.msg = null;
  draw();
}

// Conserva lo escrito antes de volver a dibujar la pantalla
function leerFormularioObra() {
  const f = ST.formObra;
  if (!f) return f;
  const valor = id => { const e = document.getElementById(id); return e ? e.value : null; };
  f.nombre = valor('nuevo_nombre') ?? f.nombre;
  f.ubicacion = valor('nueva_ubic') ?? f.ubicacion;
  f.fechaInicio = valor('nueva_inicio') ?? f.fechaInicio;
  f.fechaFin = valor('nueva_fin') ?? f.fechaFin;
  f.descripcion = valor('nueva_desc') ?? f.descripcion;
  return f;
}

function cambiarClienteObra() {
  leerFormularioObra();
  ST.formObra.cliente = null;
  draw();
}

function seleccionarClienteObra(cliente) {
  leerFormularioObra();
  ST.formObra.cliente = cliente;
  draw();
}

// Búsqueda de cliente por nombre o documento: solo se actualiza la lista, para no perder el foco del campo
let temporizadorClientes = null;
function buscarClientes() {
  clearTimeout(temporizadorClientes);
  temporizadorClientes = setTimeout(async () => {
    const entrada = document.getElementById('obra_cliente_q');
    const caja = document.getElementById('cli_resultados');
    if (!entrada || !caja) return;
    const texto = entrada.value.trim();
    if (texto.length < 2) { caja.innerHTML = ''; return; }
    try {
      const r = await apiFetch(`${API}/api/clientes?q=${encodeURIComponent(texto)}`);
      if (!r.ok) throw new Error();
      const lista = await r.json();
      ST.resultadosClientes = lista;
      caja.innerHTML = lista.length
        ? `<div style="border:1px solid var(--line);background:var(--surface);max-height:190px;overflow:auto">${lista.map((c, i) =>
            `<button class="item" style="display:block;width:100%;text-align:left;padding:7px 10px" onclick="seleccionarClienteObra(ST.resultadosClientes[${i}])">
              <b>${esc(c.nombre)}</b><span class="sub" style="display:block">${esc(c.tipoDocumento)} ${esc(c.documento)}</span></button>`).join('')}</div>`
        : '<div class="hint" style="margin-top:4px">Sin coincidencias. Puede registrarlo como cliente nuevo.</div>';
    } catch (e) {
      caja.innerHTML = '<div class="hint" style="margin-top:4px;color:var(--rust)">No se pudo buscar clientes.</div>';
    }
  }, 250);
}

// Ventana emergente «Registrar Nuevo Cliente»; al guardar, el cliente queda vinculado al proyecto en curso
function abrirModalCliente() {
  leerFormularioObra();
  let capa = document.getElementById('modal_cliente');
  if (!capa) {
    capa = document.createElement('div');
    capa.id = 'modal_cliente';
    document.body.appendChild(capa);
  }
  capa.style.cssText = 'position:fixed;inset:0;background:rgba(10,25,41,.6);display:flex;align-items:center;justify-content:center;z-index:900;overflow:auto';
  capa.innerHTML = `<div class="card" style="max-width:480px;width:92%;margin:20px"><div class="ch"><h4>Registrar nuevo cliente</h4></div><div class="cb">
    <div id="mcli_msg"></div>
    <div class="f"><label>Nombre o razón social</label><input id="mcli_nombre" placeholder="Inversiones Del Río S.A.C."></div>
    <div class="g2"><div class="f"><label>Tipo de documento</label><select id="mcli_tipo"><option>RUC</option><option>DNI</option></select></div>
     <div class="f"><label>Número de documento</label><input id="mcli_doc" inputmode="numeric" placeholder="20512345678"></div></div>
    <div class="g2"><div class="f"><label>Correo electrónico</label><input id="mcli_correo" type="email" placeholder="contacto@empresa.pe">
      <div class="hint" style="margin-top:4px">Con él se habilita el panel de seguimiento del cliente.</div></div>
     <div class="f"><label>Teléfono</label><input id="mcli_tel" placeholder="987 654 321"></div></div>
    <div style="display:flex;gap:8px;margin-top:8px">
     <button class="btn" onclick="guardarClienteModal()">Registrar y vincular</button>
     <button class="btn ghost" onclick="cerrarModalCliente()">Cancelar</button></div>
  </div></div>`;
  setTimeout(() => { const e = document.getElementById('mcli_nombre'); if (e) e.focus(); }, 30);
}

function cerrarModalCliente() {
  const capa = document.getElementById('modal_cliente');
  if (capa) capa.remove();
}

async function guardarClienteModal() {
  const aviso = texto => { document.getElementById('mcli_msg').innerHTML = `<div class="msg bad">${esc(texto)}</div>`; };
  const nombre = document.getElementById('mcli_nombre').value.trim();
  const tipo = document.getElementById('mcli_tipo').value;
  const documento = document.getElementById('mcli_doc').value.trim();
  const correo = document.getElementById('mcli_correo').value.trim();
  const telefono = document.getElementById('mcli_tel').value.trim();

  if (!nombre) return aviso('El nombre o razón social es obligatorio.');
  if (!/^\d+$/.test(documento) || documento.length !== (tipo === 'DNI' ? 8 : 11))
    return aviso(tipo === 'DNI' ? 'El DNI debe tener 8 dígitos.' : 'El RUC debe tener 11 dígitos.');
  if (correo && !/^\S+@\S+\.\S+$/.test(correo)) return aviso('Ingrese un correo electrónico válido.');

  try {
    const r = await apiFetch(`${API}/api/clientes`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ nombre, tipoDocumento: tipo, documento, correo, telefono })
    });
    const data = await r.json();
    if (!r.ok) return aviso(data.mensaje || 'No se pudo registrar el cliente.');

    cerrarModalCliente();
    ST.formObra.cliente = data.cliente;
    say('ok', data.mensaje + ' Quedó vinculado al proyecto.');
  } catch (e) {
    aviso('Error de conexión con el servidor.');
  }
}

async function guardarObra() {
  const f = leerFormularioObra();
  const nombre = f.nombre.trim();

  if (!nombre) return say('bad', 'El nombre del proyecto es obligatorio.');
  if (!f.cliente) return say('bad', 'Busque y seleccione un cliente, o registre uno nuevo.');
  if (!f.fechaInicio) return say('bad', 'La fecha de inicio es obligatoria.');
  if (f.fechaFin && f.fechaFin < f.fechaInicio) return say('bad', 'La fecha estimada de fin no puede ser anterior a la de inicio.');

  try {
    const respuesta = await apiFetch(API + '/api/obras', {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({
        nombre,
        clienteId: f.cliente.id,
        ubicacion: f.ubicacion.trim(),
        descripcion: f.descripcion.trim(),
        fechaInicio: f.fechaInicio,
        fechaFinEstimada: f.fechaFin || null
      })
    });
    const data = await respuesta.json();

    if (respuesta.ok) {
      ST.formObra = null;
      say(data.panelHabilitado ? 'ok' : 'warn', data.mensaje);
    } else if (respuesta.status !== 401) {
      say('bad', data.mensaje || 'Ocurrió un error al guardar el proyecto.');
    }
  } catch (error) {
    say('bad', 'Error de conexión con el servidor.');
  }
}

// Cierre del proyecto con fecha real (HU11-T6)
function abrirFinalizarObra() {
  ST.finalizando = true;
  ST.msg = null;
  draw();
}

function cancelarFinalizarObra() {
  ST.finalizando = false;
  draw();
}

async function finalizarObra(obraId) {
  const fecha = document.getElementById('fin_fecha').value;
  if (!fecha) return say('bad', 'Indique la fecha real de cierre.');
  if (!confirm('¿Finalizar la obra? Quedará marcada como Finalizada con la fecha de cierre indicada.')) return;

  try {
    const respuesta = await apiFetch(`${API}/api/obras/${obraId}/finalizar`, {
      method: 'PUT',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ fechaCierre: fecha })
    });
    const data = await respuesta.json();
    if (respuesta.ok) {
      ST.finalizando = false;
      say('ok', data.mensaje);
    } else if (respuesta.status !== 401) {
      say('bad', data.mensaje || 'No se pudo finalizar la obra.');
    }
  } catch (e) {
    say('bad', 'Error de conexión con el servidor.');
  }
}

// --- HITOS ---

function capturarFormHito() {
  const f = ST.formHito || { id: null };
  f.nombre = document.getElementById('nuevo_hito_nombre').value.trim();
  f.peso = document.getElementById('nuevo_hito_peso').value;
  f.fecha = document.getElementById('nuevo_hito_fecha').value;
  ST.formHito = f;
  return f;
}

function editarHito(id) {
  const h = (ST.hitosCache || []).find(x => x.id === id);
  if (!h) return;
  ST.formHito = { id: h.id, nombre: h.nombre, peso: h.peso, fecha: h.fechaPlanificada || '' };
  ST.msg = null;
  draw();
  setTimeout(() => { const el = document.getElementById('form_hito'); if (el) el.scrollIntoView({ behavior: 'smooth' }); }, 80);
}

function cancelarHito() {
  ST.formHito = null;
  ST.msg = null;
  draw();
}

async function guardarHito(obraId) {
  const f = capturarFormHito();
  const peso = parseInt(f.peso);

  if (!f.nombre) return say('bad', 'El nombre del hito es obligatorio.');
  if (isNaN(peso) || peso < 1 || peso > 100) return say('bad', 'El peso debe ser un número entre 1 y 100.');

  const datos = { nombre: f.nombre, peso: peso, fechaPlanificada: f.fecha || null };

  try {
    const respuesta = await apiFetch(f.id ? `${API}/api/obras/hitos/${f.id}/definicion` : `${API}/api/obras/${obraId}/hitos`, {
      method: f.id ? 'PUT' : 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify(datos)
    });

    if (respuesta.ok) {
      const edicion = !!f.id;
      ST.formHito = null;
      say('ok', edicion ? 'Hito actualizado correctamente.' : 'Hito registrado correctamente.');
    } else if (respuesta.status !== 401) {
      say('bad', (await respuesta.text()) || 'Ocurrió un error al guardar el hito.');
    }
  } catch (error) {
    say('bad', 'Error de conexión con el servidor.');
  }
}

async function eliminarHito(id) {
  const h = (ST.hitosCache || []).find(x => x.id === id);
  if (!confirm(`¿Eliminar el hito "${h ? h.nombre : ''}"?`)) return;

  try {
    const respuesta = await apiFetch(`${API}/api/obras/hitos/${id}`, { method: 'DELETE' });
    if (respuesta.ok) {
      if (ST.formHito && ST.formHito.id === id) ST.formHito = null;
      say('ok', 'Hito eliminado correctamente.');
    } else if (respuesta.status !== 401) {
      say('bad', (await respuesta.text()) || 'No se pudo eliminar el hito.');
    }
  } catch (error) {
    say('bad', 'Error de conexión con el servidor.');
  }
}

function seleccionarHito(id) {
  ST.hitoEditando = id;
  draw();
  setTimeout(() => {
    window.scrollTo({ top: document.body.scrollHeight, behavior: 'smooth' });
  }, 100);
}

async function actualizarAvanceDesdeFormulario(hitoId) {
  if (!hitoId) return;

  const entrada = document.getElementById('avance_porcentaje').value;
  const descripcion = document.getElementById('avance_descripcion').value;
  const fotoInput = document.getElementById('avance_foto');
  
  let nuevoAvance = parseInt(entrada);
  
  if (isNaN(nuevoAvance) || nuevoAvance < 0 || nuevoAvance > 100) {
    say('bad', 'Por favor, ingrese un número válido entre 0 y 100 en el formulario.');
    return;
  }

  const formData = new FormData();
  formData.append("nuevoAvance", nuevoAvance);
  formData.append("descripcion", descripcion);
  
  if (fotoInput.files.length > 0) {
    formData.append("foto", fotoInput.files[0]);
  }

  try {
    const respuesta = await apiFetch(`${API}/api/obras/hitos/${hitoId}`, {
      method: 'PUT',
      body: formData 
    });

    if (respuesta.ok) {
      say('ok', 'El avance y los archivos se guardaron correctamente.');
      ST.hitoEditando = null;
      draw();
      window.scrollTo({ top: 0, behavior: 'smooth' });
    } else {
      say('bad', 'Ocurrió un error al guardar los archivos en el servidor.');
    }
  } catch (error) {
    say('bad', 'Error de conexión con el servidor.');
  }
}

// --- CUADRILLA ---

async function asignarColaborador(obraId) {
  const nombre = document.getElementById('nuevo_colab_nombre').value;
  const especialidad = document.getElementById('nuevo_colab_esp').value;
  const jornalStr = document.getElementById('nuevo_colab_jornal').value;

  if (!nombre || !jornalStr) {
    say('bad', 'El nombre y el jornal son obligatorios.');
    return;
  }

  const hoy = new Date();
  const fechaActual = hoy.toLocaleDateString('es-PE', { day: '2-digit', month: '2-digit', year: 'numeric' });

  const nuevoColaborador = {
    nombreCompleto: nombre,
    dni: "00000000",
    especialidad: especialidad,
    jornal: parseFloat(jornalStr),
    estadoSctr: "Vigente",
    fechaIngresoObra: fechaActual
  };

  try {
    const respuesta = await apiFetch(`${API}/api/obras/${obraId}/cuadrilla`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify(nuevoColaborador)
    });

    if (respuesta.ok) {
      say('ok', 'Colaborador asignado correctamente a la cuadrilla.');
      draw(); 
    } else {
      say('bad', 'Ocurrió un error al asignar el colaborador.');
    }
  } catch (error) {
    say('bad', 'Error de conexión con el servidor.');
  }
}

async function retirarColaborador(colabId) {
  if (!confirm("¿Está seguro de registrar la salida de este colaborador?")) return;

  try {
    const respuesta = await apiFetch(`${API}/api/obras/cuadrilla/${colabId}`, {
      method: 'DELETE'
    });

    if (respuesta.ok) {
      say('ok', 'Salida del colaborador registrada correctamente.');
      draw(); 
    } else {
      say('bad', 'Ocurrió un error al retirar al colaborador.');
    }
  } catch (error) {
    say('bad', 'Error de conexión con el servidor.');
  }
}

// --- DOCUMENTOS ---

async function subirDocumento(obraId) {
  const archivoInput = document.getElementById('nuevo_doc_archivo');
  const clasificacion = document.getElementById('nuevo_doc_clasif').value;
  const visible = document.getElementById('nuevo_doc_vis').value;

  if (archivoInput.files.length === 0) {
    say('bad', 'Debe seleccionar un archivo para subir.');
    return;
  }

  const formData = new FormData();
  formData.append("archivo", archivoInput.files[0]);
  formData.append("clasificacion", clasificacion);
  formData.append("visibleCliente", visible);

  try {
    const respuesta = await apiFetch(`${API}/api/obras/${obraId}/documentos`, {
      method: 'POST',
      body: formData
    });

    if (respuesta.ok) {
      say('ok', 'Documento cargado correctamente en la base de datos.');
      draw(); 
    } else {
      say('bad', 'Ocurrió un error al subir el documento.');
    }
  } catch (error) {
    say('bad', 'Error de conexión con el servidor.');
  }
}

async function eliminarDocumento(docId) {
  if (!confirm("¿Está seguro de eliminar este documento de la obra?")) return;

  try {
    const respuesta = await apiFetch(`${API}/api/obras/documentos/${docId}`, {
      method: 'DELETE'
    });

    if (respuesta.ok) {
      say('ok', 'Documento eliminado correctamente.');
      draw(); 
    } else {
      say('bad', 'Ocurrió un error al eliminar el documento.');
    }
  } catch (error) {
    say('bad', 'Error de conexión con el servidor.');
  }
}

function descargarDocumento(docId) {
  descargarArchivo(`${API}/api/obras/documentos/${docId}/descargar`);
}

// --- PRESUPUESTO / PARTIDAS ---

async function agregarPartidaFormulario(obraId) {
  const nombre = document.getElementById('partida_nombre').value;
  const montoStr = document.getElementById('partida_monto').value;
  
  if (!nombre || nombre.trim() === "" || !montoStr) {
    say('bad', 'Ambos campos son obligatorios.');
    return;
  }
  
  const monto = parseFloat(montoStr);
  if (isNaN(monto) || monto <= 0) {
    say('bad', 'Por favor, ingrese un monto válido mayor a 0.');
    return;
  }

  const nuevaPartida = {
    nombre: nombre,
    montoAsignado: monto
  };

  try {
    const respuesta = await apiFetch(`${API}/api/obras/${obraId}/partidas`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify(nuevaPartida)
    });

    if (respuesta.ok) {
      say('ok', 'Partida agregada correctamente.');
      draw(); 
    } else {
      say('bad', 'Ocurrió un error al guardar la partida.');
    }
  } catch (error) {
    say('bad', 'Error de conexión con el servidor.');
  }
}

function seleccionarPartida(id) {
  ST.partidaEditando = id;
  draw();
  window.scrollTo({ top: 0, behavior: 'smooth' });
}

function cancelarEdicionPartida() {
  ST.partidaEditando = null;
  draw();
}

async function actualizarPartida(partidaId) {
  const nombre = document.getElementById('partida_nombre').value;
  const montoStr = document.getElementById('partida_monto').value;
  const monto = parseFloat(montoStr);

  if (!nombre || isNaN(monto) || monto <= 0) {
    say('bad', 'Datos inválidos. Revise los campos.');
    return;
  }

  const partidaActualizada = {
    nombre: nombre,
    montoAsignado: monto
  };

  try {
    const respuesta = await apiFetch(`${API}/api/obras/partidas/${partidaId}`, {
      method: 'PUT',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify(partidaActualizada)
    });

    if (respuesta.ok) {
      say('ok', 'Partida actualizada correctamente.');
      ST.partidaEditando = null;
      draw(); 
    } else {
      say('bad', 'Ocurrió un error al actualizar.');
    }
  } catch (error) {
    say('bad', 'Error de conexión con el servidor.');
  }
}

async function eliminarPartida(partidaId) {
  if (!confirm("¿Está seguro de eliminar esta partida del presupuesto?")) return;

  try {
    const respuesta = await apiFetch(`${API}/api/obras/partidas/${partidaId}`, {
      method: 'DELETE'
    });

    if (respuesta.ok) {
      say('ok', 'Partida eliminada correctamente.');
      draw(); 
    } else {
      say('bad', 'Ocurrió un error al eliminar la partida.');
    }
  } catch (error) {
    say('bad', 'Error de conexión con el servidor.');
  }
}

// --- GASTOS ---

async function registrarGasto(obraId, requiereComprobante) {
  const fecha = document.getElementById('gasto_fecha').value;
  const tipo = document.getElementById('gasto_tipo').value;
  const numero = document.getElementById('gasto_numero').value;
  const concepto = document.getElementById('gasto_concepto').value;
  const proveedor = document.getElementById('gasto_proveedor').value;
  const partida = document.getElementById('gasto_partida').value;
  const montoStr = document.getElementById('gasto_monto').value;
  const archivoInput = document.getElementById('gasto_archivo');

  if (!partida) {
     say('bad', 'Debe registrar al menos una Partida en el presupuesto antes de agregar gastos.');
     return;
  }

  if (!concepto || !montoStr) {
    say('bad', 'El concepto y el monto son obligatorios.');
    return;
  }
  
  if (requiereComprobante && archivoInput.files.length === 0) {
    say('bad', 'Debe adjuntar la imagen o el archivo del comprobante para registrar el gasto.');
    return;
  }

  const monto = parseFloat(montoStr);
  if (isNaN(monto) || monto <= 0) {
    say('bad', 'Monto inválido. Debe ser mayor a 0.');
    return;
  }

  const formData = new FormData();
  formData.append("fecha", fecha);
  formData.append("tipo", tipo);
  formData.append("numero", numero);
  formData.append("concepto", concepto);
  formData.append("proveedor", proveedor);
  formData.append("partidaNombre", partida);
  formData.append("monto", monto);
  
  if (archivoInput.files.length > 0) {
    formData.append("archivo", archivoInput.files[0]);
  }

  try {
    const respuesta = await apiFetch(`${API}/api/obras/${obraId}/gastos`, {
      method: 'POST',
      body: formData
    });

    if (respuesta.ok) {
      say('ok', 'Gasto registrado correctamente.');
      draw(); 
    } else {
      say('bad', 'Ocurrió un error al registrar el gasto.');
    }
  } catch (error) {
    say('bad', 'Error de conexión con el servidor.');
  }
}

function descargarComprobante(gastoId) {
  descargarArchivo(`${API}/api/obras/gastos/${gastoId}/descargar`);
}

async function anularGasto(gastoId) {
  if (!confirm("¿Está seguro de que desea anular este gasto? El monto se devolverá al saldo de la partida.")) return;

  try {
    const respuesta = await apiFetch(`${API}/api/obras/gastos/${gastoId}/anular`, {
      method: 'PUT'
    });

    if (respuesta.ok) {
      say('ok', 'Gasto anulado correctamente.');
      draw(); 
    } else {
      say('bad', 'Ocurrió un error al anular el gasto.');
    }
  } catch (error) {
    say('bad', 'Error de conexión con el servidor.');
  }
}

// --- PAGOS A PERSONAL ---

async function registrarPago(obraId, requiereConstancia) {
  const colaborador = document.getElementById('pago_colaborador').value;
  const periodo = document.getElementById('pago_periodo').value;
  const montoStr = document.getElementById('pago_monto').value;
  const fecha = document.getElementById('pago_fecha').value;
  const archivoInput = document.getElementById('pago_archivo');

  if (!colaborador) {
     say('bad', 'Debe registrar al menos un trabajador en Cuadrilla antes de pagar.');
     return;
  }

  if (!periodo || !montoStr) {
    say('bad', 'El periodo y el monto son obligatorios.');
    return;
  }
  
  if (requiereConstancia && archivoInput.files.length === 0) {
    say('bad', 'Debe adjuntar la constancia para registrar el pago.');
    return;
  }

  const monto = parseFloat(montoStr);
  if (isNaN(monto) || monto <= 0) {
    say('bad', 'Monto inválido. Debe ser mayor a 0.');
    return;
  }

  const formData = new FormData();
  formData.append("colaboradorNombre", colaborador);
  formData.append("periodo", periodo);
  formData.append("monto", monto);
  formData.append("fechaPago", fecha);
  
  if (archivoInput.files.length > 0) {
    formData.append("archivo", archivoInput.files[0]);
  }

  try {
    const respuesta = await apiFetch(`${API}/api/obras/${obraId}/pagos`, {
      method: 'POST',
      body: formData
    });

    if (respuesta.ok) {
      say('ok', 'Pago registrado correctamente.');
      draw(); 
    } else {
      say('bad', 'Ocurrió un error al registrar el pago.');
    }
  } catch (error) {
    say('bad', 'Error de conexión con el servidor.');
  }
}

function descargarConstancia(pagoId) {
  descargarArchivo(`${API}/api/obras/pagos/${pagoId}/descargar`);
}

function formatearPeriodo(input) {
  let v = input.value.replace(/\D/g, ''); 
  if (v.length > 8) v = v.substring(0, 8); 
  
  let out = '';
  if (v.length > 0) out += v.substring(0, 2);
  if (v.length > 2) out += '/' + v.substring(2, 4);
  if (v.length > 4) out += ' - ' + v.substring(4, 6);
  if (v.length > 6) out += '/' + v.substring(6, 8);
  
  input.value = out;
}

// --- CONTROL ECONÓMICO (EXPORTAR CON FORMATO EXCEL) ---

function exportarControlEconomico() {
  const tablaOriginal = document.querySelector('.card table');
  if (!tablaOriginal) {
    say('bad', 'No hay datos para exportar.');
    return;
  }

  let html = `
    <html xmlns:o="urn:schemas-microsoft-com:office:office" xmlns:x="urn:schemas-microsoft-com:office:excel" xmlns="http://www.w3.org/TR/REC-html40">
    <head>
      <meta charset="utf-8">
      <style>
        table { border-collapse: collapse; width: 100%; font-family: Arial, sans-serif; }
        th { background-color: #0A1929; color: #ffffff; font-weight: bold; border: 1px solid #dddddd; padding: 10px; text-align: left; }
        td { border: 1px solid #dddddd; padding: 8px; }
        .num { text-align: right; }
        .total td { font-weight: bold; background-color: #F5F7F8; }
      </style>
    </head>
    <body>
      <table>
  `;

  const filas = tablaOriginal.querySelectorAll('tr');
  filas.forEach((fila, index) => {
    const esTotal = fila.innerText.includes('Total') && index > 0;
    html += `<tr class="${esTotal ? 'total' : ''}">`;
    
    const celdas = fila.querySelectorAll('th, td');
    celdas.forEach(celda => {
      let texto = celda.innerText.replace(/(\r\n|\n|\r)/gm, " ").replace(/Excedida/g, '').trim();
      let esNumero = celda.classList.contains('num');
      let tag = index === 0 ? 'th' : 'td';
      
      html += `<${tag} ${esNumero ? 'class="num"' : ''}>${texto}</${tag}>`;
    });
    html += '</tr>';
  });

  html += `
      </table>
    </body>
    </html>
  `;

  const blob = new Blob([html], { type: 'application/vnd.ms-excel' });
  const url = window.URL.createObjectURL(blob);
  
  const a = document.createElement('a');
  a.href = url;
  
  const hoy = new Date().toISOString().split('T')[0];
  a.download = `Control_Economico_${hoy}.xls`;
  
  document.body.appendChild(a);
  a.click();
  document.body.removeChild(a);
  window.URL.revokeObjectURL(url);
}

// --- AUTENTICACIÓN (HU-01 y HU-03) ---

function cerrarSesion(mensaje) {
  sesionBorrar();
  ST.usuario = null;
  ST.menu = null;
  ocultarAvisoInactividad();
  ST.v = 'home';
  ST.msg = `<div class="msg ok">${mensaje || 'Sesión cerrada correctamente.'}</div>`;
  draw();
  window.scrollTo({ top: 0 });
}

// El servidor rechazó el token o se agotó el tiempo: vuelve al inicio de sesión
function sesionExpirada(mensaje) {
  if (!ST.usuario) return;
  sesionBorrar();
  ST.usuario = null;
  ST.menu = null;
  ocultarAvisoInactividad();
  ST.v = 'login';
  ST.msg = `<div class="msg warn">${mensaje || 'Su sesión expiró. Inicie sesión nuevamente.'}</div>`;
  draw();
}

// --- CONTROL DE INACTIVIDAD (HU-02) ---
// Aviso a los 25 min sin actividad y cierre automático a los 30 min
const INACTIVIDAD = { avisoMs: 25 * 60 * 1000, cierreMs: 30 * 60 * 1000, renovarCadaMs: 10 * 60 * 1000 };
let ultimaActividad = Date.now();
let ultimaRenovacion = Date.now();
let avisoVisible = false;

function registrarActividad() {
  if (!ST.usuario || avisoVisible) return; // con el aviso abierto solo cuentan sus botones
  ultimaActividad = Date.now();
  if (ultimaActividad - ultimaRenovacion > INACTIVIDAD.renovarCadaMs) renovarSesion();
}

async function renovarSesion() {
  ultimaRenovacion = Date.now(); // evita llamadas repetidas mientras responde
  try {
    const respuesta = await apiFetch(API + '/api/auth/renovar', { method: 'POST' });
    if (!respuesta.ok) return;
    const data = await respuesta.json();
    sesionGuardar({ token: data.token, expira: data.expira, usuario: data.usuario });
    return true;
  } catch (e) { /* sin conexión: se reintentará con la siguiente actividad */ }
}

function mostrarAvisoInactividad() {
  avisoVisible = true;
  let capa = document.getElementById('aviso_inactividad');
  if (!capa) {
    capa = document.createElement('div');
    capa.id = 'aviso_inactividad';
    document.body.appendChild(capa);
  }
  capa.style.cssText = 'position:fixed;inset:0;background:rgba(10,25,41,.6);display:flex;align-items:center;justify-content:center;z-index:1000';
  capa.innerHTML = `<div class="card" style="max-width:400px;width:90%"><div class="cb">
    <h3 style="margin-bottom:8px">¿Sigue ahí?</h3>
    <p>Su sesión se cerrará por inactividad en <b id="aviso_segundos"></b>.</p>
    <div style="display:flex;gap:8px;margin-top:16px">
      <button class="btn" onclick="continuarSesion()">Continuar</button>
      <button class="btn ghost" onclick="cerrarSesion()">Cerrar sesión</button></div></div></div>`;
  actualizarCuentaRegresiva();
}

function actualizarCuentaRegresiva() {
  const el = document.getElementById('aviso_segundos');
  if (!el) return;
  const restante = Math.max(0, Math.ceil((INACTIVIDAD.cierreMs - (Date.now() - ultimaActividad)) / 1000));
  el.textContent = `${Math.floor(restante / 60)}:${String(restante % 60).padStart(2, '0')}`;
}

function ocultarAvisoInactividad() {
  avisoVisible = false;
  const capa = document.getElementById('aviso_inactividad');
  if (capa) capa.style.display = 'none';
}

async function continuarSesion() {
  const renovada = await renovarSesion();
  if (!renovada) return sesionExpirada('Su sesión expiró por inactividad. Inicie sesión nuevamente.');
  ocultarAvisoInactividad();
  ultimaActividad = Date.now();
}

function revisarInactividad() {
  if (!ST.usuario) { if (avisoVisible) ocultarAvisoInactividad(); return; }
  const inactivo = Date.now() - ultimaActividad;
  if (inactivo >= INACTIVIDAD.cierreMs) {
    sesionExpirada('Su sesión se cerró por inactividad. Inicie sesión nuevamente.');
  } else if (inactivo >= INACTIVIDAD.avisoMs) {
    if (!avisoVisible) mostrarAvisoInactividad(); else actualizarCuentaRegresiva();
  }
}

['click', 'keydown', 'mousemove', 'scroll', 'touchstart'].forEach(ev =>
  document.addEventListener(ev, registrarActividad, { passive: true }));
setInterval(revisarInactividad, 1000);

async function iniciarSesion() {
  const correo = document.getElementById('login_correo').value;
  const password = document.getElementById('login_password').value;

  if (!correo || !password) {
    say('bad', 'Debe ingresar su correo y contraseña.');
    return;
  }

  try {
    const respuesta = await apiFetch(API + '/api/auth/login', {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ correo: correo, password: password })
    });

    const data = await respuesta.json();

    if (respuesta.ok) {
      sesionGuardar({ token: data.token, expira: data.expira, usuario: data.usuario });
      ST.usuario = data.usuario;
      ST.msg = null;
      ultimaActividad = ultimaRenovacion = Date.now();
      go(vistaInicio(data.usuario.rol));
    } else {
      say('bad', data.mensaje);
    }
  } catch (error) {
    say('bad', 'Error al conectar con el servidor.');
  }
}

async function enviarEnlace() {
  const correo = document.getElementById('rec_correo').value.trim();
  if (!correo || !correo.includes('@')) return say('bad', 'Ingrese un correo electrónico válido.');

  try {
    const res = await fetch(API + '/api/auth/recuperar', {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ correo: correo })
    });
    const data = await res.json();
    say(res.ok ? 'ok' : 'bad', data.mensaje);
  } catch (e) {
    say('bad', 'Error al conectar con el servidor.');
  }
}

async function guardarNuevaContrasena() {
  const token = ST.token_temp;
  const pass1 = document.getElementById('reset_pass1').value;
  const pass2 = document.getElementById('reset_pass2').value;

  if (!token) return say('bad', 'No se encontró el código de seguridad. Por favor, abra el enlace desde su correo.');
  if (pass1.length < 8) return say('bad', 'La contraseña debe tener al menos 8 caracteres.');
  if (pass1 !== pass2) return say('bad', 'Las contraseñas no coinciden.');

  try {
    const res = await fetch(API + '/api/auth/restablecer', {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ token: token, nuevaPassword: pass1 })
    });
    const data = await res.json();

    if (res.ok) {
      ST.token_temp = null;
      ST.v = 'login';
      ST.msg = '<div class="msg ok">Contraseña actualizada correctamente. Ya puede iniciar sesión.</div>';
      draw();
    } else {
      say('bad', data.mensaje);
    }
  } catch (e) {
    say('bad', 'Error de conexión con el servidor.');
  }
}

// --- USUARIOS Y ROLES (HU-04) ---

function abrirFormularioUsuario() {
  ST.formUsuario = { id: null, nombres: '', apellidos: '', correo: '', rolId: '' };
  ST.msg = null;
  draw();
  setTimeout(() => { const f = document.getElementById('form_usuario'); if (f) f.scrollIntoView({ behavior: 'smooth' }); }, 50);
}

function editarUsuario(id) {
  const u = (ST.usuariosCache || []).find(x => x.id === id);
  if (!u) return;
  ST.formUsuario = { id: u.id, nombres: u.nombres, apellidos: u.apellidos, correo: u.correo, rolId: u.rolId };
  ST.msg = null;
  draw();
  setTimeout(() => { const f = document.getElementById('form_usuario'); if (f) f.scrollIntoView({ behavior: 'smooth' }); }, 50);
}

function cancelarUsuario() {
  ST.formUsuario = null;
  ST.msg = null;
  draw();
}

async function guardarUsuario() {
  const f = ST.formUsuario;
  // Se conserva lo escrito por si hay que volver a mostrar el formulario con un error
  f.nombres = document.getElementById('usu_nombres').value.trim();
  f.apellidos = document.getElementById('usu_apellidos').value.trim();
  f.correo = document.getElementById('usu_correo').value.trim();
  f.rolId = document.getElementById('usu_rol').value;

  if (!f.nombres) return say('bad', 'Los nombres son obligatorios.');
  if (!f.id && (!f.correo || !f.correo.includes('@'))) return say('bad', 'Ingrese un correo electrónico válido.');
  if (!f.rolId) return say('bad', 'Seleccione un rol para el usuario.');

  try {
    const respuesta = await apiFetch(f.id ? `${API}/api/usuarios/${f.id}` : `${API}/api/usuarios`, {
      method: f.id ? 'PUT' : 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ nombres: f.nombres, apellidos: f.apellidos, correo: f.correo, rolId: parseInt(f.rolId) })
    });
    const data = await respuesta.json();
    if (respuesta.ok) {
      // Si el administrador cambió su propio nombre, se actualiza la barra superior
      if (f.id && ST.usuario && f.id === ST.usuario.id) {
        ST.usuario.nombreCompleto = data.usuario.nombreCompleto;
        const sesion = sesionLeer();
        if (sesion) sesionGuardar({ ...sesion, usuario: ST.usuario });
      }
      ST.formUsuario = null;
      say(data.correoEnviado === false ? 'warn' : 'ok', data.mensaje);
    } else if (respuesta.status !== 401) {
      say('bad', data.mensaje || 'No se pudo guardar el usuario.');
    }
  } catch (e) {
    say('bad', 'Error de conexión con el servidor.');
  }
}

async function cambiarEstadoUsuario(id, activar) {
  const u = (ST.usuariosCache || []).find(x => x.id === id);
  if (!activar && !confirm(`¿Desactivar a ${u ? u.nombreCompleto : 'este usuario'}? No podrá ingresar, pero se conservarán sus registros.`)) return;

  try {
    const respuesta = await apiFetch(`${API}/api/usuarios/${id}/estado`, {
      method: 'PUT',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ activo: activar })
    });
    const data = await respuesta.json();
    if (respuesta.ok) say('ok', data.mensaje);
    else if (respuesta.status !== 401) say('bad', data.mensaje || 'No se pudo cambiar el estado.');
  } catch (e) {
    say('bad', 'Error de conexión con el servidor.');
  }
}

// --- PORTAFOLIO (HU-08) ---

const IMAGEN_MAX_BYTES = 5 * 1024 * 1024;

function nuevoProyecto() {
  ST.formProyecto = { id: null, nombre: '', tipoObra: '', ubicacion: '', anio: new Date().getFullYear(), descripcion: '' };
  ST.msg = null;
  draw();
  setTimeout(() => { const f = document.getElementById('form_proyecto'); if (f) f.scrollIntoView({ behavior: 'smooth' }); }, 50);
}

function editarProyecto(id) {
  const p = (ST.portafolioCache || []).find(x => x.id === id);
  if (!p) return;
  ST.formProyecto = { id: p.id, nombre: p.nombre, tipoObra: p.tipoObra, ubicacion: p.ubicacion, anio: p.anio, descripcion: p.descripcion };
  ST.msg = null;
  draw();
  setTimeout(() => { const f = document.getElementById('form_proyecto'); if (f) f.scrollIntoView({ behavior: 'smooth' }); }, 50);
}

function cancelarProyecto() {
  ST.formProyecto = null;
  ST.msg = null;
  draw();
}

async function guardarProyecto() {
  const f = ST.formProyecto;
  // Se conserva lo escrito por si hay que volver a mostrar el formulario con un error
  f.nombre = document.getElementById('port_nombre').value.trim();
  f.tipoObra = document.getElementById('port_tipo').value;
  f.ubicacion = document.getElementById('port_ubicacion').value.trim();
  f.anio = document.getElementById('port_anio').value;
  f.descripcion = document.getElementById('port_descripcion').value.trim();

  if (!f.nombre) return say('bad', 'El nombre del proyecto es obligatorio.');
  if (!f.tipoObra) return say('bad', 'Seleccione el tipo de obra.');
  if (!f.anio) return say('bad', 'Ingrese el año de ejecución.');

  try {
    const respuesta = await apiFetch(f.id ? `${API}/api/portafolio/${f.id}` : `${API}/api/portafolio`, {
      method: f.id ? 'PUT' : 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ nombre: f.nombre, tipoObra: f.tipoObra, ubicacion: f.ubicacion, anio: parseInt(f.anio), descripcion: f.descripcion })
    });
    const data = await respuesta.json();
    if (respuesta.ok) {
      // Tras guardar queda abierto el proyecto para poder subir sus imágenes
      ST.formProyecto = { ...f, id: data.proyecto.id };
      say('ok', data.mensaje + ' Ahora puede subir sus imágenes.');
    } else if (respuesta.status !== 401) {
      say('bad', data.mensaje || 'No se pudo guardar el proyecto.');
    }
  } catch (e) {
    say('bad', 'Error de conexión con el servidor.');
  }
}

async function cambiarPublicacion(id, publicar) {
  try {
    const respuesta = await apiFetch(`${API}/api/portafolio/${id}/${publicar ? 'publicar' : 'despublicar'}`, { method: 'PUT' });
    const data = await respuesta.json();
    if (respuesta.ok) say('ok', data.mensaje);
    else if (respuesta.status !== 401) say('bad', data.mensaje || 'No se pudo cambiar el estado del proyecto.');
  } catch (e) {
    say('bad', 'Error de conexión con el servidor.');
  }
}

async function eliminarProyecto(id) {
  const p = (ST.portafolioCache || []).find(x => x.id === id);
  if (!confirm(`¿Eliminar «${p ? p.nombre : 'este proyecto'}» y todas sus imágenes? Esta acción no se puede deshacer.`)) return;
  try {
    const respuesta = await apiFetch(`${API}/api/portafolio/${id}`, { method: 'DELETE' });
    const data = await respuesta.json();
    if (respuesta.ok) {
      if (ST.formProyecto && ST.formProyecto.id === id) ST.formProyecto = null;
      say('ok', data.mensaje);
    } else if (respuesta.status !== 401) {
      say('bad', data.mensaje || 'No se pudo eliminar el proyecto.');
    }
  } catch (e) {
    say('bad', 'Error de conexión con el servidor.');
  }
}

async function subirImagenesProyecto(id) {
  const entrada = document.getElementById('port_archivos');
  const archivos = Array.from(entrada.files);
  if (archivos.length === 0) return;

  for (const a of archivos) {
    if (!/\.(jpe?g|png)$/i.test(a.name)) return say('bad', `«${esc(a.name)}» no es válido. Solo se aceptan imágenes JPG o PNG.`);
    if (a.size > IMAGEN_MAX_BYTES) return say('bad', `«${esc(a.name)}» supera el máximo de 5 MB.`);
  }

  const datos = new FormData();
  archivos.forEach(a => datos.append('archivos', a));
  try {
    const respuesta = await apiFetch(`${API}/api/portafolio/${id}/imagenes`, { method: 'POST', body: datos });
    const data = await respuesta.json();
    if (respuesta.ok) say('ok', data.mensaje);
    else if (respuesta.status !== 401) say('bad', data.mensaje || 'No se pudieron subir las imágenes.');
  } catch (e) {
    say('bad', 'Error de conexión con el servidor.');
  }
}

async function marcarImagenPrincipal(imagenId) {
  try {
    const respuesta = await apiFetch(`${API}/api/portafolio/imagenes/${imagenId}/principal`, { method: 'PUT' });
    const data = await respuesta.json();
    if (respuesta.ok) say('ok', data.mensaje);
    else if (respuesta.status !== 401) say('bad', data.mensaje || 'No se pudo marcar la imagen.');
  } catch (e) {
    say('bad', 'Error de conexión con el servidor.');
  }
}

async function eliminarImagenProyecto(imagenId) {
  if (!confirm('¿Eliminar esta imagen del proyecto?')) return;
  try {
    const respuesta = await apiFetch(`${API}/api/portafolio/imagenes/${imagenId}`, { method: 'DELETE' });
    const data = await respuesta.json();
    if (respuesta.ok) say('ok', data.mensaje);
    else if (respuesta.status !== 401) say('bad', data.mensaje || 'No se pudo eliminar la imagen.');
  } catch (e) {
    say('bad', 'Error de conexión con el servidor.');
  }
}

// --- PORTAFOLIO PÚBLICO (HU-05) ---

function verProyectoPublico(id) {
  ST.proyectoPubId = id;
  go('obra-pub');
}

// Galería ampliada con navegación (botones, flechas del teclado y Esc)
function abrirGaleria(indice) {
  if (!ST.galeria || !ST.galeria.length) return;
  ST.galeriaIndice = indice;
  let capa = document.getElementById('galeria');
  if (!capa) {
    capa = document.createElement('div');
    capa.id = 'galeria';
    capa.className = 'lightbox';
    capa.setAttribute('role', 'dialog');
    capa.setAttribute('aria-modal', 'true');
    capa.addEventListener('click', e => { if (e.target === capa) cerrarGaleria(); });
    document.body.appendChild(capa);
    document.addEventListener('keydown', teclaGaleria);
  }
  pintarGaleria();
}

function pintarGaleria() {
  const capa = document.getElementById('galeria');
  if (!capa) return;
  const total = ST.galeria.length;
  const actual = ST.galeria[ST.galeriaIndice];
  capa.innerHTML = `<button class="lb-cerrar" onclick="cerrarGaleria()" aria-label="Cerrar">✕</button>
    ${total > 1 ? '<button class="lb-nav lb-ant" onclick="moverGaleria(-1)" aria-label="Anterior">‹</button>' : ''}
    <figure><img src="${actual.src}" alt="${esc(actual.nombre)}"><figcaption>${ST.galeriaIndice + 1} / ${total}</figcaption></figure>
    ${total > 1 ? '<button class="lb-nav lb-sig" onclick="moverGaleria(1)" aria-label="Siguiente">›</button>' : ''}`;
}

function moverGaleria(paso) {
  const total = ST.galeria.length;
  ST.galeriaIndice = (ST.galeriaIndice + paso + total) % total;
  pintarGaleria();
}

function cerrarGaleria() {
  const capa = document.getElementById('galeria');
  if (capa) capa.remove();
  document.removeEventListener('keydown', teclaGaleria);
}

function teclaGaleria(e) {
  if (e.key === 'Escape') cerrarGaleria();
  else if (e.key === 'ArrowLeft') moverGaleria(-1);
  else if (e.key === 'ArrowRight') moverGaleria(1);
}

// --- ARRANQUE ---

document.addEventListener('click',()=>{if(ST.menu){ST.menu=null;draw()}});
let h = location.hash.replace('#','');
if (h.includes('?')) {
  // El token de recuperación viaja en el enlace del correo: se guarda y se limpia la URL
  const params = new URLSearchParams(h.split('?')[1]);
  if (params.get('token')) ST.token_temp = params.get('token');
  if (params.get('id')) ST.proyectoPubId = parseInt(params.get('id'));
  h = h.split('?')[0];
}

if (h && V[h]) ST.v = h;

// Arrancar la plataforma
draw();