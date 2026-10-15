// Solo actualizamos la variable ST que ya viene de views.js
ST.usuario = null;
ST.perfil = 'pub';

// Si no hay una vista definida al cargar, lo mandamos al login (o home)
if (!ST.v) {
  ST.v = 'login'; 
}
/* ================= control ================= */
function go(v){ST.v=v;ST.msg=null;ST.menu=null;ST.nav=false;draw();window.scrollTo({top:0})}
function tab(t){ST.tab=t;ST.msg=null;draw()}
function perfil(p,v){ST.perfil=p;ST.v=v;ST.menu=null;ST.msg=null;draw();window.scrollTo({top:0})}
function tog(m,e){e.stopPropagation();ST.menu=ST.menu===m?null:m;draw()}
function say(t,x){ST.msg=`<div class="msg ${t}">${x}</div>`;draw();
  const m=document.querySelector('.msg');if(m)m.scrollIntoView({block:'center'})}

async function draw(){
  const vista = V[ST.v] || V.home;
  document.getElementById('app').innerHTML = await vista();
  location.hash = ST.v;
}

// --- OBRAS ---

async function guardarObra() {
  const nombre = document.getElementById('nuevo_nombre').value;
  const cliente = document.getElementById('nuevo_cliente').value;
  
  if (!nombre || !cliente) {
    say('bad', 'El nombre del proyecto y el cliente son obligatorios.');
    return;
  }

  const nuevaObra = {
    nombre: nombre,
    cliente: cliente,
    estado: 'En Ejecución',
    avancePorcentaje: 0
  };

  try {
    const respuesta = await fetch('http://localhost:5067/api/obras', {
      method: 'POST',
      headers: {
        'Content-Type': 'application/json'
      },
      body: JSON.stringify(nuevaObra)
    });

    if (respuesta.ok) {
      say('ok', 'Proyecto registrado correctamente en la base de datos.');
      
      document.getElementById('nuevo_nombre').value = '';
      document.getElementById('nueva_ubic').value = '';
      document.getElementById('nuevo_cliente').value = '';
      
      setTimeout(() => {
        go('proyectos');
      }, 1000);
    } else {
      say('bad', 'Ocurrió un error al guardar en la base de datos.');
    }
  } catch (error) {
    say('bad', 'Error de conexión con el servidor.');
  }
}

// --- HITOS ---

async function guardarHito(obraId) {
  const nombre = document.getElementById('nuevo_hito_nombre').value;
  const peso = parseInt(document.getElementById('nuevo_hito_peso').value);
  const fechaPlanificada = document.getElementById('nuevo_hito_fecha').value;
  
  if (!nombre || isNaN(peso)) {
    say('bad', 'El nombre y el peso del hito son obligatorios.');
    return;
  }

  const nuevoHito = {
    nombre: nombre,
    peso: peso,
    fechaPlanificada: fechaPlanificada,
    estado: 'Pendiente',
    avancePorcentaje: 0
  };

  try {
    const respuesta = await fetch(`http://localhost:5067/api/obras/${obraId}/hitos`, {
      method: 'POST',
      headers: {
        'Content-Type': 'application/json'
      },
      body: JSON.stringify(nuevoHito)
    });

    if (respuesta.ok) {
      say('ok', 'Hito registrado correctamente en PostgreSQL.');
      draw();
    } else {
      say('bad', 'Ocurrió un error al guardar el hito.');
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
    const respuesta = await fetch(`http://localhost:5067/api/obras/hitos/${hitoId}`, {
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
    const respuesta = await fetch(`http://localhost:5067/api/obras/${obraId}/cuadrilla`, {
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
    const respuesta = await fetch(`http://localhost:5067/api/obras/cuadrilla/${colabId}`, {
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
    const respuesta = await fetch(`http://localhost:5067/api/obras/${obraId}/documentos`, {
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
    const respuesta = await fetch(`http://localhost:5067/api/obras/documentos/${docId}`, {
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
  window.open(`http://localhost:5067/api/obras/documentos/${docId}/descargar`, '_blank');
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
    const respuesta = await fetch(`http://localhost:5067/api/obras/${obraId}/partidas`, {
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
    const respuesta = await fetch(`http://localhost:5067/api/obras/partidas/${partidaId}`, {
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
    const respuesta = await fetch(`http://localhost:5067/api/obras/partidas/${partidaId}`, {
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
    const respuesta = await fetch(`http://localhost:5067/api/obras/${obraId}/gastos`, {
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
  window.open(`http://localhost:5067/api/obras/gastos/${gastoId}/descargar`, '_blank');
}

async function anularGasto(gastoId) {
  if (!confirm("¿Está seguro de que desea anular este gasto? El monto se devolverá al saldo de la partida.")) return;

  try {
    const respuesta = await fetch(`http://localhost:5067/api/obras/gastos/${gastoId}/anular`, {
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
    const respuesta = await fetch(`http://localhost:5067/api/obras/${obraId}/pagos`, {
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
  window.open(`http://localhost:5067/api/obras/pagos/${pagoId}/descargar`, '_blank');
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

async function iniciarSesion() {
  const correo = document.getElementById('login_correo').value;
  const password = document.getElementById('login_password').value;

  if (!correo || !password) {
    say('bad', 'Debe ingresar su correo y contraseña.');
    return;
  }

  try {
    const respuesta = await fetch('http://localhost:5067/api/auth/login', {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ correo: correo, password: password })
    });

    const data = await respuesta.json();

    if (respuesta.ok) {
      ST.usuario = data;
      
      if (data.rol === 'Cliente') {
        perfil('cli', 'c-obra');
      } else {
        perfil('adm', 'tablero');
      }
    } else {
      say('bad', data.mensaje);
    }
  } catch (error) {
    say('bad', 'Error al conectar con el servidor.');
  }
}

async function enviarEnlace() {
  const correo = document.getElementById('rec_correo').value;
  if (!correo) return say('bad', 'Ingrese un correo electrónico válido.');

  // Captura la ruta sin importar en qué computadora estés
  const urlBase = window.location.href.split('#')[0]; 

  try {
    const res = await fetch('http://localhost:5067/api/auth/recuperar', {
      method: 'POST',
      headers: { 
        'Content-Type': 'application/json',
        'Frontend-Url': urlBase // Lo enviamos oculto por aquí para no romper C#
      },
      body: JSON.stringify({ correo: correo }) 
    });
    
    const data = await res.json();
    if (res.ok) {
      say('ok', data.mensaje);
    } else {
      say('bad', data.mensaje);
    }
  } catch (e) {
    say('bad', 'Error al conectar con el servidor.');
  }
}

async function guardarNuevaContrasena() {
  // 1. Extraemos el token secreto directamente de la URL que abrió el correo
  let token = "";
  if (window.location.hash.includes('?token=')) {
    token = window.location.hash.split('?token=')[1];
  } else {
    token = document.getElementById('reset_token')?.value; 
  }

  const pass1 = document.getElementById('reset_pass1').value;
  const pass2 = document.getElementById('reset_pass2').value;

  if (!token) return say('bad', 'No se encontró el código de seguridad. Por favor, abra el enlace desde su correo.');
  if (pass1.length < 8) return say('bad', 'La contraseña debe tener al menos 8 caracteres.');
  if (pass1 !== pass2) return say('bad', 'Las contraseñas no coinciden.');

  try {
    const res = await fetch('http://localhost:5067/api/auth/restablecer', {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ token: token, nuevaPassword: pass1 })
    });
    const data = await res.json();

    if (res.ok) {
      say('ok', 'Contraseña actualizada correctamente. Redirigiendo...');
      setTimeout(() => go('login'), 2000);
    } else {
      say('bad', data.mensaje);
    }
  } catch (e) {
    say('bad', 'Error de conexión.');
  }
}

// --- ARRANQUE ---

document.addEventListener('click',()=>{if(ST.menu){ST.menu=null;draw()}});
let h = location.hash.replace('#','');
if (h.includes('?')) h = h.split('?')[0]; // Separa el token para que el sistema reconozca la vista

if(h&&V[h]){ST.v=h;ST.perfil=h.startsWith('c-')?'cli':['tablero','proyectos','obra','personal','docpers','alertas','usuarios','portafolio','servicios-adm'].includes(h)?'adm':'pub'}

// Arrancar la plataforma
draw();