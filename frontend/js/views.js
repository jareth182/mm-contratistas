/* ================= vistas ================= */
const V={};

V.home=()=>`${navbar()}
 <section class="hero"><div class="hgrid"></div><div class="hero-in">
   <div class="rl"><i></i>CONSTRUCCIÓN DE EDIFICIOS · LIMA</div>
   <h1>Obras que puede seguir día por día</h1>
   <p>Ejecutamos edificaciones multifamiliares, comerciales y remodelaciones. Cada cliente recibe un acceso donde ve el avance, los gastos y los documentos de su obra.</p>
   <button class="btn warn" onclick="go('cotizar')">Solicitar cotización</button>
   <button class="btn ghost" style="color:#fff;border-color:#4A6072;margin-left:8px" onclick="go('servicios')">Ver servicios</button>
 </div></section>
 <div class="page">${cota('Proyectos ejecutados','HU05')}
  <div class="cards">${PORT.filter(p=>p.e==='Publicado').map(p=>`<article class="pc2">
    <div class="im">Fotografía de obra</div><div class="bd"><h3>${p.n}</h3>
    <div class="hint" style="margin:3px 0 10px">${p.t} · ${p.u} · ${p.a}</div>
    <button class="btn ghost sm" onclick="go('obra-pub')">Ver obra</button></div></article>`).join('')}</div>
 </div>${pie()}`;

V['obra-pub']=()=>`${navbar()}${crumbs([['Proyectos','home'],['Condominio Las Palmeras']])}
 <div class="page"><div class="phead"><div><h1>Condominio Las Palmeras</h1>
   <p>Edificio de cinco niveles con veinte departamentos, estacionamiento en sótano y áreas comunes en azotea. Entregado dentro del plazo contratado.</p></div>
   <button class="btn warn" onclick="go('cotizar')">Solicitar cotización</button></div>
  <div class="g2" style="gap:26px">
   <div><div class="th" style="width:100%;height:270px">Fotografía principal</div>
     <div class="thumbs" style="margin-top:8px"><div class="th">Fachada</div><div class="th">Interior</div><div class="th">Azotea</div></div></div>
   <div class="card"><table><tbody>
     <tr><th style="width:45%">Tipo de obra</th><td>Edificio multifamiliar</td></tr>
     <tr><th>Ubicación</th><td>Chorrillos, Lima</td></tr>
     <tr><th>Año de ejecución</th><td>2025</td></tr>
     <tr><th>Servicio</th><td>Construcción de edificios</td></tr>
     <tr><th>Duración</th><td>14 meses</td></tr></tbody></table></div>
  </div></div>${pie()}`;

V.servicios=()=>`${navbar()}${crumbs([['Inicio','home'],['Servicios']])}
 <div class="page"><div class="phead"><div><h1>Servicios</h1>
   <p>Trabajamos con constructoras, inversionistas y propietarios directos. Toda obra incluye personal asegurado y equipo de protección.</p></div></div>
   ${ST.hu?'<div class="cota"><span class="t"></span><span class="b"></span><span class="r"></span><span class="hu">HU06</span></div>':''}
   ${SERV.map(s=>`<div class="srv"><h3>${s.n}</h3><div><p style="margin:0 0 10px">${s.d}</p>
     <button class="btn ghost sm" onclick="go('cotizar')">Cotizar este servicio</button></div></div>`).join('')}
 </div>${pie()}`;

V.nosotros=()=>`${navbar()}${crumbs([['Inicio','home'],['Nosotros']])}
 <div class="page"><div class="phead"><div><h1>Nosotros</h1>
   <p>Empresa contratista con experiencia en la gestión integral de obras, desde la negociación del presupuesto hasta la entrega final.</p></div></div>
  <div class="g2" style="gap:24px">
   <div class="card"><div class="ch"><h4>Misión</h4></div><div class="cb">
     Ser una empresa donde cada persona esté comprometida con la excelencia, buscando beneficios reales para la empresa, la comunidad y el medio ambiente, y satisfaciendo ampliamente a nuestros clientes.</div></div>
   <div class="card"><div class="ch"><h4>Visión</h4></div><div class="cb">
     Ser una empresa contratista reconocida por su solidez humana y técnica, apreciada por sus clientes, soporte para su gente y respetuosa del medio en el que vivimos.</div></div>
  </div>
  <div class="kpi" style="margin-top:22px">
   <div><div class="v">14</div><div class="l">Colaboradores en planilla</div></div>
   <div><div class="v">3</div><div class="l">Obras en ejecución</div></div>
   <div><div class="v">100%</div><div class="l">Personal con SCTR vigente</div></div>
   <div><div class="v">2018</div><div class="l">Año de fundación</div></div>
  </div></div>${pie()}`;

V.cotizar=()=>`${navbar()}${crumbs([['Inicio','home'],['Solicitar cotización']])}
 <div class="page" style="max-width:720px"><div class="phead"><div><h1>Solicitar cotización</h1>
   <p>Describa el trabajo que necesita. Le respondemos con una propuesta en un plazo de tres días hábiles.</p></div>
   ${ST.hu?'<span class="hu">HU07</span>':''}</div>
  ${ST.msg||''}
  <div class="card"><div class="cb">
   <div class="g2"><div class="f"><label>Nombres y apellidos</label><input placeholder="Ana Torres Miranda"></div>
     <div class="f"><label>Teléfono</label><input placeholder="987 654 321"></div></div>
   <div class="f"><label>Correo electrónico</label><input placeholder="ana.torres@correo.com"></div>
   <div class="g2"><div class="f"><label>Tipo de servicio</label><select>${SERV.map(s=>`<option>${s.n}</option>`).join('')}</select></div>
     <div class="f"><label>Distrito</label><input placeholder="San Isidro"></div></div>
   <div class="f"><label>Descripción del trabajo</label><textarea placeholder="Ampliación de un tercer piso sobre losa existente, aproximadamente 90 m²."></textarea></div>
   <div class="f"><label>Planos o fotos de referencia</label><div class="thumbs">
     <span class="th doc">plano-nivel-3.pdf</span><span class="th doc">fachada.jpg</span>
     <button class="btn ghost sm">Adjuntar archivos</button></div>
     <div class="hint" style="margin-top:5px">PDF, JPG o PNG. Hasta 10 MB por archivo.</div></div>
   <button class="btn" onclick="say('ok','Su solicitud fue enviada correctamente. Código SC-2026-0184. Nos comunicaremos con usted a la brevedad.')">Enviar solicitud</button>
  </div></div></div>${pie()}`;

V.login=()=>`${navbar()}
 <div class="page" style="max-width:430px;padding-top:50px"><div class="phead"><div><h1>Ingresar</h1>
  <p>Acceso para el personal de M/M Contratistas y para clientes con obra en curso.</p></div>${ST.hu?'<span class="hu">HU01</span>':''}</div>
  ${ST.msg||''}
  <div class="card"><div class="cb">
   <!-- Agregamos los ID para capturar los datos -->
   <div class="f"><label>Correo electrónico</label><input id="login_correo" value="marco.zapata@mym.pe"></div>
   <div class="f"><label>Contraseña</label><input type="password" id="login_password" value="123"></div>
   
   <button class="btn" style="width:100%" onclick="iniciarSesion()">Ingresar</button>
   
   <div style="display:flex;justify-content:center;margin-top:12px">
     <button class="btn link" onclick="go('recuperar')">¿Olvidó su contraseña?</button>
   </div>
  </div></div></div>${pie()}`;

V.recuperar=()=>`${navbar()}
 <div class="page" style="max-width:430px;padding-top:50px"><div class="phead"><div><h1>Recuperar contraseña</h1>
  <p>Le enviaremos un enlace para crear una contraseña nueva. El enlace vence en 24 horas.</p></div>${ST.hu?'<span class="hu">HU03</span>':''}</div>
  ${ST.msg||''}
  <div class="card"><div class="cb"><div class="f"><label>Correo electrónico</label><input placeholder="nombre@correo.com"></div>
   <button class="btn" style="width:100%" onclick="say('ok','Si el correo se encuentra registrado, recibirá un enlace de recuperación en los próximos minutos.')">Enviar enlace</button>
   <button class="btn link" style="margin-top:11px" onclick="go('login')">Volver a ingresar</button>
  </div></div></div>${pie()}`;

/* ---- admin ---- */
V.tablero=()=>`${navbar()}${crumbs([['Tablero']])}
 <div class="page"><div class="phead"><div><h1>Buenos días, Marco</h1>
   <p>Jueves 10 de septiembre de 2026 · 3 obras en ejecución</p></div>
   <button class="btn" onclick="go('proyectos')">Registrar obra</button></div>
  <div class="kpi">
   <div><div class="v">3</div><div class="l">Obras en ejecución</div></div>
   <div><div class="v">14</div><div class="l">Colaboradores activos</div></div>
   <div><div class="v" style="color:var(--ambar)">2</div><div class="l">Documentos por vencer</div></div>
   <div><div class="v" style="color:var(--rust)">1</div><div class="l">Documentos vencidos</div></div>
  </div>
  <div class="msg warn" style="margin-top:20px"><b>Documentación del personal.</b>
   Saúl Quinto Palomino tiene el SCTR vencido y no puede asignarse a una obra.
   <button class="btn ghost sm" style="margin-left:8px" onclick="go('alertas')">Ver alertas</button></div>
  ${cota('Obras en ejecución','HU11','h3')}
  <div class="card"><table><thead><tr><th>Obra</th><th>Cliente</th><th style="width:210px">Avance</th><th></th></tr></thead><tbody>
   ${PROY.filter(p=>p.e==='En Ejecución').map(p=>`<tr><td><b>${p.n}</b></td><td>${p.c}</td>
     <td><div style="display:flex;align-items:center;gap:9px"><div class="bar" style="flex:1"><i style="width:${p.a}%"></i></div>
     <span style="font-variant-numeric:tabular-nums">${p.a}%</span></div></td>
     <td style="width:90px"><button class="btn ghost sm" onclick="go('obra')">Abrir</button></td></tr>`).join('')}
  </tbody></table></div>
  ${cota('Últimos movimientos','','h3')}
  <div class="card"><table><tbody>
   <tr><td style="width:110px" class="hint">Hoy 09:14</td><td>Avance de <b>Estructura</b> actualizado a 55% — Los Cedros</td></tr>
   <tr><td class="hint">Ayer 16:02</td><td>Gasto registrado: Cemento Sol tipo I — ${S(5940)}</td></tr>
   <tr><td class="hint">Ayer 11:37</td><td>Nueva solicitud de cotización — SC-2026-0183, Santiago de Surco</td></tr>
   <tr><td class="hint">07/09</td><td>Comentario del cliente en el hito <b>Estructura</b></td></tr>
  </tbody></table></div></div>`;

V.usuarios=()=>`${navbar()}${crumbs([['Usuarios']])}
 <div class="page"><div class="phead"><div><h1>Usuarios</h1><p>Cuentas con acceso a la plataforma y el rol que define qué puede ver cada una.</p></div>
  <button class="btn" onclick="say('ok','Usuario registrado correctamente. Se envió el enlace para definir la contraseña.')">Nuevo usuario</button></div>
  ${ST.hu?'<div class="cota"><span class="t"></span><span class="b"></span><span class="r"></span><span class="hu">HU04</span></div>':''}
  ${ST.msg||''}
  <div class="card"><table><thead><tr><th>Nombre</th><th>Correo</th><th>Rol</th><th>Estado</th><th></th></tr></thead><tbody>
  ${[['Maribel Minaya Arellano','maribel.minaya@mym.pe','Administrador','Activo'],
     ['Marco Zapata Neyra','marco.zapata@mym.pe','Gerente de Operaciones','Activo'],
     ['Rómulo Ccahuana Ayala','romulo.ccahuana@mym.pe','Supervisor de Obra','Activo'],
     ['Inversiones Del Río S.A.C.','contacto@delrio.pe','Cliente','Activo'],
     ['Jazmín Zapata Minaya','jazmin.zapata@mym.pe','Administrador','Inactivo']].map(u=>`<tr>
   <td><b>${u[0]}</b></td><td>${u[1]}</td><td>${u[2]}</td>
   <td>${u[3]==='Activo'?'<span class="tg ok">Activo</span>':'<span class="tg mt">Inactivo</span>'}</td>
   <td style="width:150px;white-space:nowrap"><button class="btn link">Editar</button> · <button class="btn link">Desactivar</button></td></tr>`).join('')}
  </tbody></table></div></div>`;

V.portafolio=()=>`${navbar()}${crumbs([['Sitio web'],['Portafolio']])}
 <div class="page"><div class="phead"><div><h1>Portafolio</h1><p>Obras que se muestran en el sitio público. Solo las publicadas son visibles para los visitantes.</p></div>
  <button class="btn" onclick="say('ok','Proyecto guardado correctamente.')">Nuevo proyecto</button></div>
  ${ST.hu?'<div class="cota"><span class="t"></span><span class="b"></span><span class="r"></span><span class="hu">HU08</span></div>':''}
  ${ST.msg||''}
  <div class="card"><table><thead><tr><th>Nombre</th><th>Tipo de obra</th><th>Año</th><th>Estado</th><th></th></tr></thead><tbody>
  ${PORT.map(p=>`<tr><td><b>${p.n}</b></td><td>${p.t}</td><td>${p.a}</td>
   <td>${p.e==='Publicado'?'<span class="tg ok">Publicado</span>':'<span class="tg mt">Borrador</span>'}</td>
   <td style="width:190px;white-space:nowrap"><button class="btn link">Editar</button> ·
   <button class="btn link" onclick="say('ok','${p.e==='Publicado'?'Proyecto retirado del portafolio público.':'Proyecto publicado correctamente.'}')">${p.e==='Publicado'?'Despublicar':'Publicar'}</button></td></tr>`).join('')}
  </tbody></table></div>
  <div class="card" style="margin-top:20px"><div class="ch"><h4>Imágenes · Condominio Las Palmeras</h4></div>
   <div class="cb"><div class="thumbs"><div class="th" style="border:2px solid var(--safety)">Principal</div>
    <div class="th">Fachada</div><div class="th">Interior</div><button class="btn ghost sm">Subir imágenes</button></div>
    <div class="hint" style="margin-top:7px">JPG o PNG hasta 5 MB. La imagen marcada se usa como miniatura en el sitio.</div>
  </div></div></div>`;

V['servicios-adm']=()=>`${navbar()}${crumbs([['Sitio web'],['Servicios']])}
 <div class="page"><div class="phead"><div><h1>Servicios</h1><p>Catálogo que aparece en el sitio público. El orden define cómo se listan.</p></div>
  <button class="btn" onclick="say('ok','Servicio registrado correctamente.')">Nuevo servicio</button></div>
  ${ST.hu?'<div class="cota"><span class="t"></span><span class="b"></span><span class="r"></span><span class="hu">HU09</span></div>':''}
  ${ST.msg||''}
  <div class="card"><table><thead><tr><th class="num" style="width:70px">Orden</th><th>Servicio</th><th>Descripción</th><th></th></tr></thead><tbody>
  ${SERV.map((s,i)=>`<tr><td class="num">${i+1}</td><td><b>${s.n}</b></td><td class="hint">${s.d}</td>
   <td style="width:150px;white-space:nowrap"><button class="btn link">Editar</button> · <button class="btn link">Eliminar</button></td></tr>`).join('')}
  </tbody></table></div></div>`;

V.personal=()=>`${navbar()}${crumbs([['Personal']])}
 <div class="page"><div class="phead"><div><h1>Personal</h1><p>Colaboradores de obra con su especialidad, jornal y estado de la documentación obligatoria.</p></div>
  <button class="btn">Nuevo colaborador</button></div>
  ${ST.hu?'<div class="cota"><span class="t"></span><span class="b"></span><span class="r"></span><span class="hu">HU10</span></div>':''}
  <div class="card"><div class="ch"><input placeholder="Buscar por nombre o especialidad" style="flex:1;max-width:320px;padding:6px 10px;border:1px solid var(--line);font-family:var(--b)">
   <span class="hint">6 registrados · 5 activos</span></div>
  <table><thead><tr><th>Nombres y apellidos</th><th>Documento</th><th>Especialidad</th><th class="num">Jornal</th><th>Documentación</th><th>Estado</th></tr></thead><tbody>
  ${PERS.map(p=>`<tr><td><button class="btn link" onclick="go('docpers')"><b>${p.n}</b></button></td><td>${p.d}</td><td>${p.e}</td>
   <td class="num">${S(p.j)}</td><td>${dt(p.dc)}</td>
   <td>${p.s==='Activo'?'<span class="tg ok">Activo</span>':'<span class="tg mt">Inactivo</span>'}</td></tr>`).join('')}
  </tbody></table></div></div>`;

V.docpers=()=>`${navbar()}${crumbs([['Personal','personal'],['Julio Ramos Bellido']])}
 <div class="page"><div class="phead"><div><h1>Julio Ramos Bellido</h1>
   <p>Oficial · DNI 40219974 · Jornal ${S(140)} · Asignado a Edificio multifamiliar Los Cedros</p></div>${ST.hu?'<span class="hu">HU23</span>':''}</div>
  ${ST.msg||''}
  <div class="g2" style="gap:18px">
   <div class="card"><div class="ch"><h4>SCTR</h4><span class="tg wn">Vence en 14 días</span></div><div class="cb">
    <div class="g2"><div class="f"><label>Fecha de inicio</label><input value="24/09/2025"></div>
     <div class="f"><label>Fecha de vencimiento</label><input value="24/09/2026"></div></div>
    <div class="thumbs"><span class="th doc">sctr-ramos-2025.pdf</span></div>
    <button class="btn sm" style="margin-top:12px" onclick="say('ok','Vigencia registrada correctamente.')">Registrar renovación</button></div></div>
   <div class="card"><div class="ch"><h4>Examen médico ocupacional</h4><span class="tg ok">Vigente</span></div><div class="cb">
    <div class="g2"><div class="f"><label>Fecha de inicio</label><input value="12/01/2026"></div>
     <div class="f"><label>Fecha de vencimiento</label><input value="12/01/2027"></div></div>
    <div class="thumbs"><span class="th doc">emo-ramos-2026.pdf</span></div>
    <button class="btn sm" style="margin-top:12px" onclick="say('ok','Vigencia registrada correctamente.')">Registrar renovación</button></div></div>
  </div>
  <div class="card" style="margin-top:20px"><div class="ch"><h4>Vigencias anteriores</h4></div>
   <table><thead><tr><th>Documento</th><th>Vigencia</th><th>Archivo</th></tr></thead><tbody>
    <tr><td>SCTR</td><td>24/09/2024 – 24/09/2025</td><td><span class="th doc">sctr-ramos-2024.pdf</span></td></tr>
    <tr><td>Examen médico ocupacional</td><td>10/01/2025 – 10/01/2026</td><td><span class="th doc">emo-ramos-2025.pdf</span></td></tr>
  </tbody></table></div></div>`;

V.alertas=()=>`${navbar()}${crumbs([['Personal','personal'],['Alertas de documentación']])}
 <div class="page"><div class="phead"><div><h1>Alertas de documentación</h1>
   <p>Colaboradores con SCTR o examen médico ocupacional vencido o próximo a vencer.</p></div>${ST.hu?'<span class="hu">HU24</span>':''}</div>
  <div class="msg warn">Se envió el aviso diario por correo con 3 colaboradores afectados.</div>
  <div class="card"><table><thead><tr><th>Colaborador</th><th>Documento</th><th>Vence</th><th class="num">Días</th><th>Estado</th><th></th></tr></thead><tbody>
   <tr><td><b>Saúl Quinto Palomino</b></td><td>SCTR</td><td>18/08/2026</td><td class="num" style="color:var(--rust)">−23</td><td>${dt('Vencido')}</td>
    <td><button class="btn ghost sm" onclick="go('docpers')">Registrar renovación</button></td></tr>
   <tr><td><b>Julio Ramos Bellido</b></td><td>SCTR</td><td>24/09/2026</td><td class="num">14</td><td>${dt('Por vencer')}</td>
    <td><button class="btn ghost sm" onclick="go('docpers')">Registrar renovación</button></td></tr>
   <tr><td><b>Nilo Achahui Mamani</b></td><td>Examen médico ocupacional</td><td>02/10/2026</td><td class="num">22</td><td>${dt('Por vencer')}</td>
    <td><button class="btn ghost sm" onclick="go('docpers')">Registrar renovación</button></td></tr>
  </tbody></table></div>
  <p class="hint" style="margin-top:12px">Un colaborador con documentación vencida no puede asignarse a una cuadrilla.</p></div>`;

V.proyectos = async () => {
  let filas = '';
  try {
      const respuesta = await fetch('http://localhost:5067/api/obras');
      const obrasAPI = await respuesta.json();

      if (obrasAPI.length === 0) {
          filas = `<tr><td colspan="5" style="text-align:center;color:var(--concrete)">No hay obras registradas en la base de datos.</td></tr>`;
      } else {
          filas = obrasAPI.map(p => `<tr><td><b>${p.nombre}</b></td><td>${p.cliente}</td>
           <td>${p.estado === 'Finalizado' ? '<span class="tg mt">Finalizado</span>' : '<span class="tg ok">En ejecución</span>'}</td>
           <td><div style="display:flex;align-items:center;gap:9px"><div class="bar" style="flex:1"><i style="width:${p.avancePorcentaje}%"></i></div>
            <span style="font-variant-numeric:tabular-nums">${p.avancePorcentaje}%</span></div></td>
           <td style="width:90px"><button class="btn ghost sm" onclick="go('obra')">Abrir</button></td></tr>`).join('');
      }
  } catch (error) {
      filas = `<tr><td colspan="5" style="text-align:center;color:var(--rust)">Error de conexión con el servidor.</td></tr>`;
  }

  return `${navbar()}${crumbs([['Obras']])}
   <div class="page"><div class="phead"><div><h1>Obras</h1><p>Expediente digital de cada proyecto: hitos, cuadrilla, documentos y control económico.</p></div>
    <button class="btn" onclick="say('ok','Proyecto registrado correctamente. El cliente ya tiene acceso a su panel de seguimiento.')">Registrar obra</button></div>
    ${ST.hu?'<div class="cota"><span class="t"></span><span class="b"></span><span class="r"></span><span class="hu">HU11</span></div>':''}
    ${ST.msg||''}
    <div class="card"><table><thead><tr><th>Obra</th><th>Cliente</th><th>Estado</th><th style="width:200px">Avance</th><th></th></tr></thead><tbody>
    ${filas}
    </tbody></table></div>
    <div class="card" style="margin-top:22px"><div class="ch"><h4>Registrar nueva obra</h4>${ST.hu?'<span class="hu">HU11</span>':''}</div><div class="cb">
     <div class="g2"><div class="f"><label>Nombre del proyecto</label><input id="nuevo_nombre" placeholder="Edificio multifamiliar Los Cedros"></div>
      <div class="f"><label>Ubicación</label><input id="nueva_ubic" placeholder="Santiago de Surco, Lima"></div>
      <div class="f"><label>Fecha de inicio</label><input value="02/03/2026"></div>
      <div class="f"><label>Fecha estimada de fin</label><input value="28/11/2026"></div></div>
     <div class="f"><label>Cliente</label><input id="nuevo_cliente" placeholder="Escriba el nombre o el documento del cliente">
      <div class="hint" style="margin-top:4px">¿El cliente no está registrado? <button class="btn link">Registrar nuevo cliente</button></div></div>
     <div class="f"><label>Descripción</label><textarea placeholder="Edificio de cinco niveles con veinte departamentos y sótano."></textarea></div>
     <button class="btn" onclick="guardarObra()">Guardar</button>
    </div></div></div>`;
};

/* ---- obra con pestañas ---- */
const TABS=[['hitos','Hitos','HU12 · HU13'],['cuad','Cuadrilla','HU14'],['docs','Documentos','HU15'],
 ['pres','Presupuesto','HU16'],['gastos','Gastos','HU17'],['pagos','Pagos a personal','HU18'],['econ','Control económico','HU19']];
 
V.obra = async () => {
  if (!ST.tab) ST.tab = 'hitos';
  const t = TABS.find(x => x[0] === ST.tab) || TABS[0];
  
  const contenidoPestana = await TC[ST.tab]();

  return `${navbar()}${crumbs([['Obras','proyectos'],[OBRA.nombre]])}
   <div class="page" style="padding-bottom:4px"><div class="phead" style="margin-bottom:12px"><div>
     <h1>${OBRA.nombre}</h1><p>${OBRA.cliente} · ${OBRA.ubic} · ${OBRA.ini} a ${OBRA.fin}</p></div>
     <div style="display:flex;gap:7px;align-items:center"><span class="tg ok">En ejecución</span>
     <span class="tg mt">${OBRA.av}% ejecutado</span></div></div></div>
   <div class="tabs"><div class="tabs-in">${TABS.map(x=>`<button class="${x[0]===ST.tab?'on':''}" onclick="tab('${x[0]}')">${x[1]}</button>`).join('')}</div></div>
   <div class="page">${cota(t[1],t[2])}${ST.msg||''}${contenidoPestana}</div>`;
};

const TC={};

TC.hitos = async () => {
  let filas = '';
  let nombreHitoSeleccionado = 'Seleccione un hito de la tabla';
  let idHitoSeleccionado = null;
  let lineaDeTiempo = '';

  try {
    const respuesta = await fetch('http://localhost:5067/api/obras');
    const obras = await respuesta.json();
    const obraActual = obras.length > 0 ? obras[0] : null;
    const hitosReales = obraActual && obraActual.hitos ? obraActual.hitos : [];

    if (hitosReales.length === 0) {
      filas = `<tr><td colspan="8" style="text-align:center;color:var(--concrete)">No hay hitos registrados en esta obra.</td></tr>`;
    } else {
      const cerrados = hitosReales.filter(h => h.avancePorcentaje === 100).length;
      const totalHitos = hitosReales.length;
      
      let avanceGlobal = 0;
      hitosReales.forEach(h => { avanceGlobal += (h.avancePorcentaje * h.peso) / 100; });
      avanceGlobal = Math.round(avanceGlobal);

      const anchoBarra = totalHitos > 0 ? ((cerrados + (hitosReales[cerrados] ? hitosReales[cerrados].avancePorcentaje / 100 : 0)) / totalHitos) * 100 : 0;

      lineaDeTiempo = `<div class="elev"><div class="tr">
        <div class="pc">${avanceGlobal}% ejecutado · ${cerrados} de ${totalHitos} hitos cerrados</div>
        <div class="gr"></div><div class="fl" style="width:${anchoBarra.toFixed(1)}%"></div>
        <div class="cols">${hitosReales.map(h => `<div class="col ${h.avancePorcentaje === 100 ? 'dn' : (h.avancePorcentaje > 0 ? 'nw' : '')}">
          <div class="mk2"></div><div class="nm">${h.nombre}</div><div class="mt2">${h.avancePorcentaje}% · peso ${h.peso}%</div></div>`).join('')}</div>
        <div class="cap"><span>Inicio del proyecto</span><span>Conectado a PostgreSQL</span></div>
      </div></div>`;

      filas = hitosReales.map((h, i) => {
        if (ST.hitoEditando && ST.hitoEditando === h.id) {
            nombreHitoSeleccionado = h.nombre;
            idHitoSeleccionado = h.id;
        }

        return `<tr>
          <td class="num">${i + 1}</td>
          <td><b>${h.nombre}</b></td>
          <td class="num">${h.peso}%</td>
          <td>${h.fechaPlanificada || '—'}</td>
          <td>${h.fechaReal || '—'}</td>
          <td>${h.estado === 'Completado' ? '<span class="tg ok">Completado</span>' : h.estado === 'En proceso' ? '<span class="tg wn">En proceso</span>' : '<span class="tg mt">Pendiente</span>'}</td>
          <td class="num">${h.avancePorcentaje}%</td>
          <td style="white-space:nowrap;width:150px">
            ${h.avancePorcentaje < 100 ? `<button class="btn link" onclick="seleccionarHito(${h.id})">Registrar avance</button>` : '<span class="hint">—</span>'}
          </td>
        </tr>`;
      }).join('');
    }
  } catch (error) {
    filas = `<tr><td colspan="8" style="text-align:center;color:var(--rust)">Error al cargar los hitos desde el servidor.</td></tr>`;
  }

  return `${lineaDeTiempo}
   <div class="card" style="margin-top:20px"><div class="ch"><h4>Nuevo Hito</h4></div><div class="cb">
    <div class="g3">
     <div class="f"><label>Nombre del hito</label><input id="nuevo_hito_nombre" placeholder="Ej. Vaciado de losa"></div>
     <div class="f"><label>Peso (%)</label><input id="nuevo_hito_peso" type="number" placeholder="15"></div>
     <div class="f"><label>Fecha planificada</label><input id="nuevo_hito_fecha" placeholder="DD/MM/YYYY"></div>
    </div>
    <button class="btn sm" onclick="guardarHito(1)">Guardar hito</button>
   </div></div>
   
   <div class="card" style="margin-top:20px"><div class="ch"><span class="hint">Listado de hitos del proyecto</span></div>
   <table><thead><tr><th class="num" style="width:60px">Orden</th><th>Hito</th><th class="num">Peso</th><th>Planificada</th><th>Real</th><th>Estado</th><th class="num">Avance</th><th></th></tr></thead><tbody>
   ${filas}
   </tbody></table></div>

   <div class="card" style="margin-top:20px">
    <div class="ch">
      <h4>Registrar avance · <span style="color:var(--safety)">${nombreHitoSeleccionado}</span></h4>
      ${ST.hu?'<span class="hu">HU13</span>':''}
    </div>
    <div class="cb">
      <div class="g2">
        <div class="f">
          <label>Porcentaje de avance (0 a 100)</label>
          <input id="avance_porcentaje" type="number" value="0" ${!idHitoSeleccionado ? 'disabled' : ''}>
        </div>
        <div class="f">
          <label>Fecha</label>
          <input value="14/09/2026" ${!idHitoSeleccionado ? 'disabled' : ''}>
        </div>
      </div>
      <div class="f">
        <label>Descripción del trabajo realizado</label>
        <textarea id="avance_descripcion" ${!idHitoSeleccionado ? 'disabled' : ''}></textarea>
      </div>
      <div class="f">
        <label>Fotografías</label>
        <div class="thumbs">
          <input type="file" id="avance_foto" style="display:none" accept="image/png, image/jpeg" onchange="document.getElementById('nombre_foto').innerText = this.files[0] ? this.files[0].name : ''">
          <button class="btn ghost sm" onclick="document.getElementById('avance_foto').click()" ${!idHitoSeleccionado ? 'disabled' : ''}>Adjuntar fotografías</button>
          <span id="nombre_foto" class="hint" style="margin-left: 10px;"></span>
        </div>
      </div>
      <button class="btn" onclick="actualizarAvanceDesdeFormulario(${idHitoSeleccionado})" ${!idHitoSeleccionado ? 'disabled' : ''}>
        Guardar avance
      </button>
    </div>
   </div>
   <div class="msg bad" style="margin-top:18px">No se puede eliminar un hito con avances o gastos asociados.</div>`;
};

TC.cuad = async () => {
  let filas = '';
  let total = 0;
  try {
    const respuesta = await fetch('http://localhost:5067/api/obras');
    const obras = await respuesta.json();
    const obraActual = obras.length > 0 ? obras[0] : null;
    const cuadrillaReal = obraActual && obraActual.cuadrilla ? obraActual.cuadrilla : [];
    total = cuadrillaReal.length;

    if (total === 0) {
      filas = `<tr><td colspan="6" style="text-align:center;color:var(--concrete)">No hay colaboradores asignados a esta obra.</td></tr>`;
    } else {
      filas = cuadrillaReal.map(c => `<tr>
        <td><b>${c.nombreCompleto}</b></td>
        <td>${c.especialidad}</td>
        <td class="num">${S(c.jornal)}</td>
        <td>${c.fechaIngresoObra}</td>
        <td>${dt(c.estadoSctr)}</td>
        <td style="width:150px"><button class="btn link" onclick="retirarColaborador(${c.id})">Registrar salida</button></td>
      </tr>`).join('');
    }
  } catch (error) {
    filas = `<tr><td colspan="6" style="text-align:center;color:var(--rust)">Error al cargar la cuadrilla desde el servidor.</td></tr>`;
  }

  return `
   <div class="card" style="margin-bottom:20px"><div class="ch"><h4>Asignar nuevo colaborador</h4></div><div class="cb">
    <div class="g3">
     <div class="f"><label>Nombres y apellidos</label><input id="nuevo_colab_nombre" placeholder="Ej. Juan Pérez"></div>
     <div class="f"><label>Especialidad</label><select id="nuevo_colab_esp">
      <option>Maestro de Obra</option><option>Operario</option><option>Oficial</option><option>Ayudante</option><option>Peón</option>
     </select></div>
     <div class="f"><label>Jornal diario (S/)</label><input id="nuevo_colab_jornal" type="number" placeholder="120"></div>
    </div>
    <button class="btn sm" onclick="asignarColaborador(1)">Asignar colaborador</button>
   </div></div>

   <div class="card"><div class="ch"><span class="hint">${total} colaboradores en obra</span></div>
   <table><thead><tr><th>Colaborador</th><th>Especialidad</th><th class="num">Jornal</th><th>Ingreso a obra</th><th>Documentación</th><th></th></tr></thead><tbody>
   ${filas}
   </tbody></table></div>`;
};

TC.docs = async () => {
  let filas = '';
  let total = 0, visibles = 0;
  try {
    const respuesta = await fetch('http://localhost:5067/api/obras');
    const obras = await respuesta.json();
    const obraActual = obras.length > 0 ? obras[0] : null;
    const docsReales = obraActual && obraActual.documentos ? obraActual.documentos : [];
    total = docsReales.length;
    visibles = docsReales.filter(d => d.visibleCliente).length;

    if (total === 0) {
      filas = `<tr><td colspan="5" style="text-align:center;color:var(--concrete)">No hay documentos cargados en esta obra.</td></tr>`;
    } else {
      filas = docsReales.map(d => `<tr>
        <td><b>${d.nombre}</b></td>
        <td><span class="tg mt">${d.clasificacion}</span></td>
        <td>${d.fechaCarga}</td>
        <td>${d.visibleCliente ? '<span class="tg ok">Sí</span>' : '<span class="tg mt">No — uso interno</span>'}</td>
        <td style="width:170px;white-space:nowrap">
          <button class="btn link" onclick="descargarDocumento(${d.id})">Descargar</button> · 
          <button class="btn link" onclick="eliminarDocumento(${d.id})">Eliminar</button>
        </td>
      </tr>`).join('');
    }
  } catch (error) {
    filas = `<tr><td colspan="5" style="text-align:center;color:var(--rust)">Error al cargar documentos desde el servidor.</td></tr>`;
  }

  return `
   <div class="card" style="margin-bottom:20px"><div class="ch"><h4>Subir nuevo documento</h4></div><div class="cb">
    <div class="g3">
     <div class="f"><label>Archivo</label>
       <input type="file" id="nuevo_doc_archivo" style="display:none" onchange="document.getElementById('nombre_doc').innerText = this.files[0] ? this.files[0].name : ''">
       <button class="btn ghost sm" onclick="document.getElementById('nuevo_doc_archivo').click()">Seleccionar archivo</button>
       <span id="nombre_doc" class="hint" style="margin-left: 10px;"></span>
     </div>
     <div class="f"><label>Clasificación</label><select id="nuevo_doc_clasif">
      <option>Contrato</option><option>Permiso</option><option>Plano</option><option>Comprobante</option><option>Otro</option>
     </select></div>
     <div class="f"><label>Visibilidad</label><select id="nuevo_doc_vis">
      <option value="true">Visible para el cliente</option>
      <option value="false">Oculto (Uso interno)</option>
     </select></div>
    </div>
    <button class="btn sm" onclick="subirDocumento(1)">Subir documento</button>
    <p class="hint" style="margin-top:12px">Clasificaciones: Contrato, Permiso, Plano, Comprobante y Otro. PDF, JPG o PNG hasta 10 MB.</p>
   </div></div>

   <div class="card"><div class="ch"><span class="hint">${total} documentos · ${visibles} visibles para el cliente</span></div>
   <table><thead><tr><th>Documento</th><th>Clasificación</th><th>Fecha de carga</th><th>Visible para el cliente</th><th></th></tr></thead><tbody>
   ${filas}
   </tbody></table></div>`;
};

TC.pres = async () => {
  let filas = '';
  let totalPartidas = 0;
  let montoTotal = 0;
  let editandoId = ST.partidaEditando || null;
  let editNombre = '', editMonto = '';
  
  try {
    const respuesta = await fetch('http://localhost:5067/api/obras');
    const obras = await respuesta.json();
    const obraActual = obras.length > 0 ? obras[0] : null;
    const partidasReales = obraActual && obraActual.partidas ? obraActual.partidas : [];
    totalPartidas = partidasReales.length;

    if (totalPartidas === 0) {
      filas = `<tr><td colspan="3" style="text-align:center;color:var(--concrete)">No hay partidas presupuestales registradas.</td></tr>`;
    } else {
      filas = partidasReales.map(p => {
        montoTotal += p.montoAsignado;
        if (editandoId === p.id) { editNombre = p.nombre; editMonto = p.montoAsignado; }
        return `<tr>
          <td><b>${p.nombre}</b></td>
          <td class="num">${S(p.montoAsignado)}</td>
          <td style="width:160px;white-space:nowrap">
            <button class="btn link" onclick="seleccionarPartida(${p.id})">Editar</button> · 
            <button class="btn link" onclick="eliminarPartida(${p.id})">Eliminar</button>
          </td>
        </tr>`;
      }).join('');
    }
  } catch (error) {
    filas = `<tr><td colspan="3" style="text-align:center;color:var(--rust)">Error al cargar el presupuesto desde el servidor.</td></tr>`;
  }

  return `
   <div class="card" style="margin-bottom:20px">
    <div class="ch"><h4>${editandoId ? 'Editar partida presupuestal' : 'Nueva partida'}</h4></div>
    <div class="cb">
     <div class="g2">
      <div class="f">
        <label>Nombre de la partida</label>
        <input id="partida_nombre" placeholder="Ej. Movimiento de tierras" value="${editNombre}">
      </div>
      <div class="f">
        <label>Monto asignado (S/)</label>
        <input id="partida_monto" type="number" placeholder="48000" value="${editMonto}">
      </div>
     </div>
     <button class="btn sm" onclick="${editandoId ? `actualizarPartida(${editandoId})` : `agregarPartidaFormulario(1)`}">
        ${editandoId ? 'Guardar cambios' : 'Agregar partida'}
     </button>
     ${editandoId ? `<button class="btn ghost sm" onclick="cancelarEdicionPartida()">Cancelar</button>` : ''}
    </div>
   </div>

   <div class="card"><div class="ch"><span class="hint">${totalPartidas} partidas registradas</span></div>
   <table><thead><tr><th>Partida</th><th class="num">Monto asignado</th><th></th></tr></thead><tbody>
   ${filas}
   <tr style="background:#F5F7F8"><td><b>Monto total presupuestado</b></td><td class="num"><b>${S(montoTotal)}</b></td><td></td></tr>
   </tbody></table></div>`;
};

TC.gastos = async () => {
  let filas = '';
  let opcionesPartidas = '';
  
  try {
    const respuesta = await fetch('http://localhost:5067/api/obras');
    const obras = await respuesta.json();
    const obraActual = obras.length > 0 ? obras[0] : null;
    
    const gastosReales = obraActual && obraActual.gastos ? obraActual.gastos : [];
    const partidasReales = obraActual && obraActual.partidas ? obraActual.partidas : [];

    const gastosInvertidos = [...gastosReales].reverse();

    if (gastosInvertidos.length === 0) {
      filas = `<tr><td colspan="7" style="text-align:center;color:var(--concrete)">No hay gastos registrados.</td></tr>`;
    } else {
      filas = gastosInvertidos.map(g => `<tr style="${g.estado === 'Anulado' ? 'color:var(--concrete)' : ''}">
        <td>${g.fecha}</td><td>${g.concepto}</td><td>${g.partidaNombre}</td><td>${g.proveedor}</td>
        <td class="num">${S(g.monto)}</td>
        <td>${g.estado === 'Anulado' ? '<span class="tg bd">Anulado</span>' : '<span class="tg ok">Registrado</span>'}</td>
        <td style="width:130px; line-height:2;">
          ${g.comprobanteUrl ? `<button class="btn link sm" onclick="descargarComprobante(${g.id})">Comprobante</button>` : '<span class="hint">Sin comprobante</span>'}
          ${g.estado !== 'Anulado' ? `<br><button class="btn link sm" style="color:var(--rust)" onclick="anularGasto(${g.id})">Anular gasto</button>` : ''}
        </td>
      </tr>`).join('');
    }

    if(partidasReales.length === 0) {
        opcionesPartidas = '<option value="">Debe crear partidas en Presupuesto primero</option>';
    } else {
        opcionesPartidas = partidasReales.map(p => {
            const gastado = gastosReales.filter(g => g.partidaNombre === p.nombre && g.estado !== 'Anulado').reduce((sum, g) => sum + g.monto, 0);
            const saldo = p.montoAsignado - gastado;
            return `<option value="${p.nombre}">${p.nombre} — saldo ${S(saldo)}</option>`;
        }).join('');
    }
  } catch (error) {
    filas = `<tr><td colspan="7" style="text-align:center;color:var(--rust)">Error al cargar los gastos desde el servidor.</td></tr>`;
  }

  return `<div class="card"><div class="ch"><span class="hint">Del más reciente al más antiguo</span>
   <button class="btn sm" onclick="document.getElementById('form_gasto').scrollIntoView({behavior: 'smooth'})">Nuevo gasto</button></div>
   <table><thead><tr><th>Fecha</th><th>Concepto</th><th>Partida</th><th>Proveedor</th><th class="num">Monto</th><th>Estado</th><th></th></tr></thead><tbody>
   ${filas}
   </tbody></table></div>
   
   <div id="form_gasto" class="card" style="margin-top:20px"><div class="ch"><h4>Nuevo gasto</h4>${ST.hu?'<span class="hu">HU17</span>':''}</div><div class="cb">
    <div class="g3"><div class="f"><label>Fecha</label><input id="gasto_fecha" value="14/09/2026"></div>
     <div class="f"><label>Tipo de comprobante</label><select id="gasto_tipo"><option>Factura</option><option>Boleta</option><option>Recibo por honorarios</option><option>Otro</option></select></div>
     <div class="f"><label>Número</label><input id="gasto_numero" placeholder="F001-004512"></div></div>
    <div class="f"><label>Concepto</label><input id="gasto_concepto" placeholder="Cemento Sol tipo I — 180 bolsas"></div>
    <div class="g3"><div class="f"><label>Proveedor</label><input id="gasto_proveedor" placeholder="Distribuidora Andina"></div>
     <div class="f"><label>Partida</label><select id="gasto_partida">${opcionesPartidas}</select></div>
     <div class="f"><label>Monto</label><input id="gasto_monto" type="number" placeholder="5940.00"></div></div>
    <div class="f"><label>Comprobante</label><div class="thumbs">
     <input type="file" id="gasto_archivo" style="display:none" accept="image/*,.pdf" onchange="document.getElementById('nombre_comprobante').innerText = this.files[0] ? this.files[0].name : ''">
     <button class="btn ghost sm" onclick="document.getElementById('gasto_archivo').click()">Adjuntar comprobante</button>
     <span id="nombre_comprobante" class="hint" style="margin-left:10px;"></span>
     </div></div>
    <button class="btn" onclick="registrarGasto(1, true)">Registrar</button>
    <button class="btn ghost" onclick="registrarGasto(1, false)">Registrar sin comprobante</button>
   </div></div>`;
};

TC.pagos = async () => {
  let filas = '';
  let opcionesColaboradores = '';

  try {
    const respuesta = await fetch('http://localhost:5067/api/obras');
    const obras = await respuesta.json();
    const obraActual = obras.length > 0 ? obras[0] : null;
    
    const pagosReales = obraActual && obraActual.pagos ? obraActual.pagos : [];
    const cuadrillaReal = obraActual && obraActual.cuadrilla ? obraActual.cuadrilla : [];

    const pagosInvertidos = [...pagosReales].reverse();

    if (pagosInvertidos.length === 0) {
      filas = `<tr><td colspan="6" style="text-align:center;color:var(--concrete)">No hay pagos registrados.</td></tr>`;
    } else {
      filas = pagosInvertidos.map(p => `<tr>
        <td><b>${p.colaboradorNombre}</b></td>
        <td>${p.periodo}</td>
        <td class="num">${S(p.monto)}</td>
        <td>${p.fechaPago}</td>
        <td><span class="tg ok">Registrado</span></td>
        <td style="width:130px">
          ${p.constanciaUrl ? `<button class="btn link sm" onclick="descargarConstancia(${p.id})">constancia</button>` : '<span class="hint">Sin archivo</span>'}
        </td>
      </tr>`).join('');
    }

    if(cuadrillaReal.length === 0) {
        opcionesColaboradores = '<option value="">Debe asignar personal en Cuadrilla primero</option>';
    } else {
        opcionesColaboradores = cuadrillaReal.map(c => `<option value="${c.nombreCompleto}">${c.nombreCompleto} — ${c.especialidad}</option>`).join('');
    }
  } catch (error) {
    filas = `<tr><td colspan="6" style="text-align:center;color:var(--rust)">Error al cargar los pagos desde el servidor.</td></tr>`;
  }

  const hoy = new Date().toLocaleDateString('es-PE', { day: '2-digit', month: '2-digit', year: 'numeric' });

  return `<div class="card"><div class="ch"><span class="hint">Imputados a la partida Mano de obra</span>
   <button class="btn sm" onclick="document.getElementById('form_pago').scrollIntoView({behavior: 'smooth'})">Nuevo pago</button></div>
   <table><thead><tr><th>Colaborador</th><th>Periodo pagado</th><th class="num">Monto</th><th>Fecha de pago</th><th>Estado</th><th></th></tr></thead><tbody>
   ${filas}
   </tbody></table></div>
   <p class="hint">Solo aparecen los colaboradores asignados a la cuadrilla de esta obra.</p>

   <div id="form_pago" class="card" style="margin-top:20px"><div class="ch"><h4>Registrar nuevo pago</h4>${ST.hu?'<span class="hu">HU18</span>':''}</div><div class="cb">
    <div class="g2">
     <div class="f"><label>Colaborador</label><select id="pago_colaborador">${opcionesColaboradores}</select></div>
     <div class="f"><label>Periodo (Ej. 11/08 - 16/08)</label><input id="pago_periodo" placeholder="DD/MM - DD/MM" oninput="formatearPeriodo(this)"></div>
    </div>
    <div class="g2">
     <div class="f"><label>Monto a pagar (S/)</label><input id="pago_monto" type="number" placeholder="1080.00"></div>
     <div class="f"><label>Fecha de pago</label><input id="pago_fecha" value="${hoy}"></div>
    </div>
    <div class="f"><label>Constancia de transferencia / Recibo</label><div class="thumbs">
     <input type="file" id="pago_archivo" style="display:none" accept="image/*,.pdf" onchange="document.getElementById('nombre_constancia').innerText = this.files[0] ? this.files[0].name : ''">
     <button class="btn ghost sm" onclick="document.getElementById('pago_archivo').click()">Adjuntar constancia</button>
     <span id="nombre_constancia" class="hint" style="margin-left:10px;"></span>
    </div></div>
    <button class="btn" onclick="registrarPago(1, true)">Registrar pago</button>
    <button class="btn ghost" onclick="registrarPago(1, false)">Registrar sin constancia</button>
   </div></div>`;
};

TC.econ = async () => {
  let filas = '';
  let tp = 0, tg = 0;
  
  try {
    const respuesta = await fetch('http://localhost:5067/api/obras');
    const obras = await respuesta.json();
    const obraActual = obras.length > 0 ? obras[0] : null;
    const partidasReales = obraActual && obraActual.partidas ? obraActual.partidas : [];
    const gastosReales = obraActual && obraActual.gastos ? obraActual.gastos : [];

    tp = partidasReales.reduce((sum, p) => sum + p.montoAsignado, 0);
    
    filas = partidasReales.map(p => {
      const gastado = gastosReales.filter(g => g.partidaNombre === p.nombre && g.estado !== 'Anulado').reduce((sum, g) => sum + g.monto, 0);
      tg += gastado;
      const pc = p.montoAsignado > 0 ? Math.round(gastado / p.montoAsignado * 100) : 0;
      const sl = p.montoAsignado - gastado;
      const cl = pc > 100 ? 'ov' : (pc >= 90 ? 'nr' : '');
      return `<tr><td><b>${p.nombre}</b> ${pc>100?'<span class="tg bd">Excedida</span>':''}</td>
       <td class="num">${S(p.montoAsignado)}</td><td class="num">${S(gastado)}</td>
       <td class="num" style="${sl<0?'color:var(--rust);font-weight:600':''}">${S(sl)}</td>
       <td><div style="display:flex;align-items:center;gap:9px"><div class="bar ${cl}" style="flex:1"><i style="width:${Math.min(pc,100)}%"></i></div>
       <span style="font-variant-numeric:tabular-nums;min-width:38px;text-align:right">${pc}%</span></div></td></tr>`;
    }).join('');
  } catch (error) {
    filas = `<tr><td colspan="5" style="text-align:center;color:var(--rust)">Error de conexión con el servidor.</td></tr>`;
  }

  return `<div class="kpi" style="margin-bottom:20px">
  <div><div class="v">${S(tp)}</div><div class="l">Presupuesto total</div></div>
  <div><div class="v">${S(tg)}</div><div class="l">Gasto acumulado</div></div>
  <div><div class="v" style="color:var(--verde)">${S(tp-tg)}</div><div class="l">Saldo disponible</div></div>
  <div><div class="v">${tp > 0 ? Math.round(tg/tp*100) : 0}%</div><div class="l">Ejecutado</div></div></div>
 <div class="card"><div class="ch"><span class="hint">Los gastos anulados no se consideran</span>
  <button class="btn ghost sm" onclick="exportarControlEconomico()">Exportar a Excel</button></div>
 <table><thead><tr><th>Partida</th><th class="num">Presupuestado</th><th class="num">Gastado</th><th class="num">Saldo</th><th style="width:160px">Ejecutado</th></tr></thead><tbody>
 ${filas}
 <tr style="background:#F5F7F8"><td><b>Total</b></td><td class="num"><b>${S(tp)}</b></td><td class="num"><b>${S(tg)}</b></td>
  <td class="num"><b>${S(tp-tg)}</b></td><td></td></tr>
 </tbody></table></div>
 <p class="hint" style="margin-top:12px">Los porcentajes se calculan automáticamente en base al registro de gastos.</p>`;
};

/* ---- cliente ---- */
V['c-obra']=()=>`${navbar()}${crumbs([['Mi obra']])}
 <div class="page"><div class="phead"><div><h1>${OBRA.nombre}</h1>
  <p>${OBRA.ubic} · Inicio ${OBRA.ini} · Fin estimado ${OBRA.fin}</p></div>
  <div style="display:flex;gap:7px;align-items:center"><span class="tg ok">En ejecución</span>${ST.hu?'<span class="hu">HU20</span>':''}</div></div>
 ${elev()}
 <div class="card" style="margin-top:20px"><div class="ch"><h4>Etapas de la obra</h4></div>
  <table><thead><tr><th>Hito</th><th>Estado</th><th>Fecha planificada</th><th>Fecha real</th></tr></thead><tbody>
  ${OBRA.hitos.map(h=>`<tr><td><b>${h.n}</b></td>
   <td>${h.e==='Completado'?'<span class="tg ok">Completado</span>':h.e==='En proceso'?'<span class="tg wn">En proceso</span>':'<span class="tg mt">Pendiente</span>'}</td>
   <td>${h.pl}</td><td>${h.rl}</td></tr>`).join('')}
  </tbody></table></div>
 <div class="card" style="margin-top:20px"><div class="ch"><h4>Estructura · avance del 10/09/2026</h4>
   <button class="btn ghost sm" onclick="go('c-cmt')">Comentar</button></div>
  <div class="cb"><p style="margin-top:0">Vaciado de losa del cuarto nivel y desencofrado de columnas del tercero.</p>
   <div class="thumbs"><div class="th">Losa nivel 4</div><div class="th">Columnas</div><div class="th">Vista general</div></div></div></div>
 <div class="card" style="margin-top:20px"><div class="ch"><h4>Documentos de mi obra</h4></div>
  <div class="cb"><div class="thumbs">${OBRA.docs.filter(d=>d.v).map(d=>`<span class="th doc">${d.n}</span>`).join('')}</div></div></div>
 </div>`;

V['c-gastos']=()=>{const t=OBRA.part.reduce((a,p)=>a+p.gs,0);
 return `${navbar()}${crumbs([['Mi obra','c-obra'],['Gastos']])}
 <div class="page"><div class="phead"><div><h1>En qué se ha invertido</h1>
  <p>Detalle de los gastos de su obra con el comprobante de cada uno.</p></div>${ST.hu?'<span class="hu">HU21</span>':''}</div>
 <div class="kpi" style="margin-bottom:20px">
  <div><div class="v">${S(t)}</div><div class="l">Invertido a la fecha</div></div>
  <div><div class="v">${OBRA.av}%</div><div class="l">Avance de obra</div></div></div>
 <div class="card"><div class="ch"><h4>Resumen por partida</h4></div>
  <table><tbody>${OBRA.part.filter(p=>p.gs>0).map(p=>`<tr><td><b>${p.n}</b></td><td class="num">${S(p.gs)}</td></tr>`).join('')}
  </tbody></table></div>
 <div class="card" style="margin-top:20px"><div class="ch"><h4>Detalle · Concreto y acero</h4></div>
  <table><thead><tr><th>Fecha</th><th>Concepto</th><th>Proveedor</th><th class="num">Monto</th><th></th></tr></thead><tbody>
  ${OBRA.gastos.filter(g=>g.pa==='Concreto y acero'&&g.e!=='Anulado').map(g=>`<tr><td>${g.f}</td><td>${g.c}</td><td>${g.p}</td>
   <td class="num">${S(g.m)}</td><td style="width:150px"><button class="btn link">Ver comprobante</button></td></tr>`).join('')}
 </tbody></table></div>
 <p class="hint" style="margin-top:12px">No se muestran los pagos individuales al personal ni el margen de la empresa.</p></div>`;};

V['c-cmt']=()=>`${navbar()}${crumbs([['Mi obra','c-obra'],['Comentarios']])}
 <div class="page" style="max-width:800px"><div class="phead"><div><h1>Comentarios · Estructura</h1>
  <p>Sus consultas sobre este avance quedan registradas junto con la respuesta del equipo de obra.</p></div>${ST.hu?'<span class="hu">HU22</span>':''}</div>
 ${ST.msg||''}
 <div class="card"><div class="cb">
  <div class="cmt"><b>Inversiones Del Río</b><time>02/09/2026 · 10:24</time>
   <p style="margin:5px 0 0">¿La losa del cuarto nivel se vació completa o quedó un paño pendiente?</p></div>
  <div class="cmt emp"><b>Marco Zapata</b><time>02/09/2026 · 15:40</time>
   <p style="margin:5px 0 0">Se vació completa. El paño sin acabado que se ve en la foto es la zona de la escalera, que va en el siguiente frente.</p></div>
  <div class="cmt"><b>Inversiones Del Río</b><time>08/09/2026 · 09:12</time>
   <p style="margin:5px 0 0">Perfecto, gracias. ¿Siguen dentro del plazo para cerrar estructura?</p></div>
 </div></div>
 <div class="card" style="margin-top:16px"><div class="cb">
  <div class="f"><label>Escribir un comentario</label><textarea placeholder="Su comentario sobre este hito"></textarea>
   <div class="hint" style="margin-top:4px">Hasta 500 caracteres.</div></div>
  <button class="btn" onclick="say('ok','Comentario enviado correctamente.')">Enviar</button></div></div>
 </div>`;