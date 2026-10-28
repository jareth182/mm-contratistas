/* ================= vistas ================= */
const V={};

// Imagen pública de un proyecto publicado (no requiere sesión)
const imgPublica=id=>`${API}/api/sitio/imagenes/${id}`;

// Tarjeta de un proyecto publicado (portafolio y proyectos relacionados de un servicio)
const tarjetaProyecto=p=>`<article class="pc2">
  <div class="im">${p.imagenPrincipalId?`<img src="${imgPublica(p.imagenPrincipalId)}" alt="${esc(p.nombre)}" loading="lazy">`:'Fotografía de obra'}</div>
  <div class="bd"><h3>${esc(p.nombre)}</h3>
  <div class="hint" style="margin:3px 0 10px">${esc(p.tipoObra)}${p.ubicacion?` · ${esc(p.ubicacion)}`:''} · ${p.anio}</div>
  <button class="btn ghost sm" onclick="verProyectoPublico(${p.id})">Ver obra</button></div></article>`;

V.home=async()=>{
  let tarjetas='';
  try{
    const r=await fetch(`${API}/api/sitio/proyectos`);
    if(!r.ok) throw new Error();
    const proyectos=await r.json();
    tarjetas=proyectos.length?`<div class="cards">${proyectos.map(tarjetaProyecto).join('')}</div>`
      :'<p class="hint">Pronto publicaremos aquí nuestras obras ejecutadas.</p>';
  }catch(e){
    tarjetas='<div class="msg warn">No se pudo cargar el portafolio en este momento. Intente nuevamente más tarde.</div>';
  }
  return `${navbar()}
 <section class="hero"><div class="hgrid"></div><div class="hero-in">
   <div class="rl"><i></i>CONSTRUCCIÓN DE EDIFICIOS · LIMA</div>
   <h1>Obras que puede seguir día por día</h1>
   <p>Ejecutamos edificaciones multifamiliares, comerciales y remodelaciones. Cada cliente recibe un acceso donde ve el avance, los gastos y los documentos de su obra.</p>
   <button class="btn warn" onclick="cotizarServicio(null)">Solicitar cotización</button>
   <button class="btn ghost" style="color:#fff;border-color:#4A6072;margin-left:8px" onclick="go('servicios')">Ver servicios</button>
 </div></section>
 <div class="page">${ST.msg||''}${cota('Proyectos ejecutados','HU05')}
  ${tarjetas}
 </div>${pie()}`;
};

V['obra-pub']=async()=>{
  let p=null;
  if(ST.proyectoPubId){
    try{
      const r=await fetch(`${API}/api/sitio/proyectos/${ST.proyectoPubId}`);
      if(r.ok) p=await r.json();
    }catch(e){ /* se muestra el aviso de abajo */ }
  }
  if(!p) return `${navbar()}${crumbs([['Proyectos','home'],['Proyecto']])}
 <div class="page"><div class="msg warn">El proyecto que busca no existe o ya no está publicado.</div>
  <button class="btn" onclick="go('home')">Ver todos los proyectos</button></div>${pie()}`;

  ST.galeria=p.imagenes.map(i=>({src:imgPublica(i.id),nombre:i.nombre}));
  const [principal,...resto]=p.imagenes;
  return `${navbar()}${crumbs([['Proyectos','home'],[esc(p.nombre)]])}
 <div class="page"><div class="phead"><div><h1>${esc(p.nombre)}</h1>
   <p>${esc(p.tipoObra)}${p.ubicacion?` · ${esc(p.ubicacion)}`:''}</p></div>
   <button class="btn warn" onclick="cotizarServicio(${p.servicio?p.servicio.id:'null'})">Solicitar cotización</button>${ST.hu?'<span class="hu">HU05</span>':''}</div>
  <div class="g2" style="gap:26px">
   <div>${principal?`<button class="gal-main" onclick="abrirGaleria(0)" title="Ampliar"><img src="${imgPublica(principal.id)}" alt="${esc(principal.nombre)}"></button>
     <div class="thumbs" style="margin-top:8px">${resto.map((i,n)=>`<button class="gal-th" onclick="abrirGaleria(${n+1})" title="Ampliar"><img src="${imgPublica(i.id)}" alt="${esc(i.nombre)}" loading="lazy"></button>`).join('')}</div>`
     :'<div class="th" style="width:100%;height:270px">Fotografía de obra</div>'}</div>
   <div><div class="card"><table><tbody>
     <tr><th style="width:45%">Tipo de obra</th><td>${esc(p.tipoObra)}</td></tr>
     <tr><th>Ubicación</th><td>${esc(p.ubicacion)||'—'}</td></tr>
     <tr><th>Año de ejecución</th><td>${p.anio}</td></tr>
     ${p.servicio?`<tr><th>Servicio</th><td><button class="btn link" onclick="verServicioPublico(${p.servicio.id})">${esc(p.servicio.nombre)}</button></td></tr>`:''}</tbody></table></div>
    ${p.descripcion?`<p style="margin-top:16px;white-space:pre-line">${esc(p.descripcion)}</p>`:''}</div>
  </div></div>${pie()}`;
};

V.servicios=()=>`${navbar()}${crumbs([['Inicio','home'],['Servicios']])}
 <div class="page"><div class="phead"><div><h1>Servicios</h1>
   <p>Trabajamos con constructoras, inversionistas y propietarios directos. Toda obra incluye personal asegurado y equipo de protección.</p></div></div>
   ${ST.hu?'<div class="cota"><span class="t"></span><span class="b"></span><span class="r"></span><span class="hu">HU06</span></div>':''}
   ${(ST.servicios||[]).map(s=>`<div class="srv"><h3>${esc(s.nombre)}</h3><div><p style="margin:0 0 10px">${esc(s.descripcion)}</p>
     <button class="btn ghost sm" onclick="verServicioPublico(${s.id})">Ver detalle</button>
     <button class="btn ghost sm" style="margin-left:6px" onclick="cotizarServicio(${s.id})">Cotizar este servicio</button></div></div>`).join('')
     ||'<p class="hint">Pronto publicaremos aquí nuestro catálogo de servicios.</p>'}
 </div>${pie()}`;

V['servicio-pub']=async()=>{
  let s=null;
  if(ST.servicioPubId){
    try{
      const r=await fetch(`${API}/api/sitio/servicios/${ST.servicioPubId}`);
      if(r.ok) s=await r.json();
    }catch(e){ /* se muestra el aviso de abajo */ }
  }
  if(!s) return `${navbar()}${crumbs([['Servicios','servicios'],['Servicio']])}
 <div class="page"><div class="msg warn">El servicio que busca no existe o ya no está publicado.</div>
  <button class="btn" onclick="go('servicios')">Ver todos los servicios</button></div>${pie()}`;

  const lista=(titulo,items)=>items.length?`<div class="card"><div class="ch"><h4>${titulo}</h4></div><div class="cb">
    <ul style="margin:0;padding-left:18px">${items.map(i=>`<li style="margin:3px 0">${esc(i)}</li>`).join('')}</ul></div></div>`:'';
  return `${navbar()}${crumbs([['Servicios','servicios'],[esc(s.nombre)]])}
 <div class="page"><div class="phead"><div><h1>${esc(s.nombre)}</h1>
   <p style="white-space:pre-line">${esc(s.descripcion)}</p></div>
   <button class="btn warn" onclick="cotizarServicio(${s.id})">Solicitar cotización</button>${ST.hu?'<span class="hu">HU06</span>':''}</div>
  <div class="g2" style="gap:18px">${lista('Beneficios',s.beneficios)}${lista('Características',s.caracteristicas)}</div>
  ${cota('Proyectos relacionados','','h3')}
  ${s.proyectos.length?`<div class="cards">${s.proyectos.map(tarjetaProyecto).join('')}</div>`
    :'<p class="hint">Aún no hay proyectos publicados para este servicio.</p>'}
 </div>${pie()}`;
};

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

V.cotizar=()=>{
  const f=ST.formCotizacion||{nombres:'',telefono:'',correo:'',distrito:'',descripcion:''};
  const servicioElegido=f.servicioId??ST.servicioCotizar;
  const adjuntos=ST.adjuntosCotizacion||[];
  const kb=n=>n<1024*1024?`${Math.max(1,Math.round(n/1024))} KB`:`${(n/1024/1024).toFixed(1)} MB`;
  return `${navbar()}${crumbs([['Inicio','home'],['Solicitar cotización']])}
 <div class="page" style="max-width:720px"><div class="phead"><div><h1>Solicitar cotización</h1>
   <p>Describa el trabajo que necesita. Le respondemos con una propuesta en un plazo de tres días hábiles.</p></div>
   ${ST.hu?'<span class="hu">HU07</span>':''}</div>
  ${ST.msg||''}
  <div class="card"><div class="cb">
   <div class="g2"><div class="f"><label for="cot_nombres">Nombres y apellidos *</label><input id="cot_nombres" value="${esc(f.nombres)}" autocomplete="name" placeholder="Ana Torres Miranda"></div>
     <div class="f"><label for="cot_telefono">Teléfono *</label><input id="cot_telefono" value="${esc(f.telefono)}" type="tel" autocomplete="tel" placeholder="987 654 321"></div></div>
   <div class="f"><label for="cot_correo">Correo electrónico *</label><input id="cot_correo" value="${esc(f.correo)}" type="email" autocomplete="email" placeholder="ana.torres@correo.com"></div>
   <div class="g2"><div class="f"><label for="cot_servicio">Tipo de servicio *</label><select id="cot_servicio"><option value="">Seleccione un servicio</option>
      ${(ST.servicios||[]).map(s=>`<option value="${s.id}" ${String(s.id)===String(servicioElegido)?'selected':''}>${esc(s.nombre)}</option>`).join('')}</select></div>
     <div class="f"><label for="cot_distrito">Distrito *</label><input id="cot_distrito" value="${esc(f.distrito)}" placeholder="San Isidro"></div></div>
   <div class="f"><label for="cot_descripcion">Descripción del trabajo *</label><textarea id="cot_descripcion" placeholder="Ampliación de un tercer piso sobre losa existente, aproximadamente 90 m².">${esc(f.descripcion)}</textarea></div>
   <div class="f"><label>Planos o fotos de referencia</label>
     ${adjuntos.length?`<ul class="adjuntos">${adjuntos.map((a,i)=>`<li><span>${esc(a.name)} <span class="hint">· ${kb(a.size)}</span></span>
       <button class="btn link" onclick="quitarAdjuntoCotizacion(${i})" aria-label="Quitar ${esc(a.name)}">Quitar</button></li>`).join('')}</ul>`:''}
     <input type="file" id="cot_archivos" accept=".pdf,.jpg,.jpeg,.png,application/pdf,image/jpeg,image/png" multiple style="display:none" onchange="agregarAdjuntosCotizacion()">
     ${adjuntos.length<COTIZACION_MAX_ADJUNTOS?'<button class="btn ghost sm" onclick="document.getElementById(\'cot_archivos\').click()">Adjuntar archivos</button>':''}
     <div class="hint" style="margin-top:5px">PDF, JPG o PNG. Hasta 10 MB por archivo y ${COTIZACION_MAX_ADJUNTOS} archivos como máximo.</div></div>
   <button class="btn" id="cot_enviar" onclick="enviarCotizacion()">Enviar solicitud</button>
   <div class="hint" style="margin-top:8px">* Campos obligatorios</div>
  </div></div></div>${pie()}`;
};

V.login=()=>`${navbar()}
 <div class="page" style="max-width:430px;padding-top:50px"><div class="phead"><div><h1>Ingresar</h1>
  <p>Acceso para el personal de M/M Contratistas y para clientes con obra en curso.</p></div>${ST.hu?'<span class="hu">HU01</span>':''}</div>
  ${ST.msg||''}
  <div class="card"><div class="cb">
   <div class="f"><label>Correo electrónico</label><input id="login_correo" type="email" placeholder="nombre@mym.pe" autocomplete="username" onkeydown="if(event.key==='Enter')iniciarSesion()"></div>
   <div class="f"><label>Contraseña</label><input type="password" id="login_password" autocomplete="current-password" onkeydown="if(event.key==='Enter')iniciarSesion()"></div>
   
   <button class="btn" style="width:100%" onclick="iniciarSesion()">Ingresar</button>
   
   <div style="display:flex;justify-content:center;margin-top:12px">
     <button class="btn link" onclick="go('recuperar')">¿Olvidó su contraseña?</button>
   </div>
  </div></div></div>${pie()}`;

V.recuperar=()=>`${navbar()}
 <div class="page" style="max-width:430px;padding-top:50px"><div class="phead"><div><h1>Recuperar contraseña</h1>
  <p>Ingrese su correo y le enviaremos un enlace para crear una contraseña nueva. El enlace vence en 24 horas.</p></div>${ST.hu?'<span class="hu">HU03</span>':''}</div>
  ${ST.msg || ''}
  <div class="card"><div class="cb">
   <div class="f"><label>Correo electrónico</label><input id="rec_correo" type="email" placeholder="nombre@correo.com" onkeydown="if(event.key==='Enter')enviarEnlace()"></div>
   <button type="button" class="btn" style="width:100%" onclick="enviarEnlace()">Enviar enlace</button>
   <div style="display:flex;justify-content:center;margin-top:12px">
     <button type="button" class="btn link" onclick="go('login')">Volver a ingresar</button>
   </div>
  </div></div></div>${pie()}`;

V.nueva_password=async()=>{
  // Se valida el enlace antes de mostrar el formulario (vigente, vencido o ya usado)
  let valido=false;
  if(ST.token_temp){
    try{valido=(await fetch(`${API}/api/auth/validar-token?token=${encodeURIComponent(ST.token_temp)}`)).ok}catch(e){}
  }
  if(!valido) return `${navbar()}
 <div class="page" style="max-width:430px;padding-top:50px"><div class="phead"><div><h1>Nueva contraseña</h1></div></div>
  <div class="msg bad">El enlace de recuperación es inválido o ha expirado.</div>
  <button class="btn" onclick="go('recuperar')">Solicitar un enlace nuevo</button></div>${pie()}`;
  return `${navbar()}
 <div class="page" style="max-width:430px;padding-top:50px"><div class="phead"><div><h1>Nueva contraseña</h1>
  <p>Cree una nueva contraseña para su cuenta.</p></div>${ST.hu?'<span class="hu">HU03</span>':''}</div>
  ${ST.msg || ''}
  <div class="card"><div class="cb">
   <div class="f"><label>Nueva contraseña</label><input type="password" id="reset_pass1" autocomplete="new-password">
    <div class="hint" style="margin-top:4px">Mínimo 8 caracteres.</div></div>
   <div class="f"><label>Confirmar contraseña</label><input type="password" id="reset_pass2" autocomplete="new-password" onkeydown="if(event.key==='Enter')guardarNuevaContrasena()"></div>
   <button class="btn" style="width:100%" onclick="guardarNuevaContrasena()">Guardar</button>
  </div></div></div>${pie()}`;
};

V.perfil=()=>`${navbar()}${crumbs([['Mi perfil']])}
 <div class="page" style="max-width:560px"><div class="phead"><div><h1>Mi perfil</h1>
  <p>Datos de la cuenta con la que inició sesión.</p></div></div>
  ${ST.msg||''}
  <div class="card"><table><tbody>
   <tr><th style="width:40%">Nombre completo</th><td>${ST.usuario.nombreCompleto}</td></tr>
   <tr><th>Correo electrónico</th><td>${ST.usuario.correo}</td></tr>
   <tr><th>Rol</th><td>${ST.usuario.rol}</td></tr></tbody></table></div></div>${pie()}`;

/* ---- admin ---- */
V.tablero=()=>`${navbar()}${crumbs([['Tablero']])}
 <div class="page"><div class="phead"><div><h1>Buenos días, ${ST.usuario.nombreCompleto.split(' ')[0]}</h1>
   <p>Jueves 10 de septiembre de 2026 · 3 obras en ejecución</p></div>
   <button class="btn" onclick="go('proyectos')">Registrar obra</button></div>
  ${ST.msg||''}
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

V.usuarios=async()=>{
  let filas='',roles=[];
  try{
    const [ru,rr]=await Promise.all([apiFetch(`${API}/api/usuarios`),apiFetch(`${API}/api/usuarios/roles`)]);
    if(!ru.ok||!rr.ok) throw new Error();
    ST.usuariosCache=await ru.json(); roles=await rr.json();
    filas=ST.usuariosCache.map(u=>`<tr>
     <td><b>${esc(u.nombreCompleto)}</b></td><td>${esc(u.correo)}</td><td>${esc(u.rol)}</td>
     <td>${u.estado==='Activo'?'<span class="tg ok">Activo</span>':'<span class="tg mt">Inactivo</span>'}</td>
     <td style="width:170px;white-space:nowrap"><button class="btn link" onclick="editarUsuario(${u.id})">Editar</button> ·
      ${u.estado==='Activo'?`<button class="btn link" onclick="cambiarEstadoUsuario(${u.id},false)">Desactivar</button>`
        :`<button class="btn link" onclick="cambiarEstadoUsuario(${u.id},true)">Activar</button>`}</td></tr>`).join('')
      || '<tr><td colspan="5" style="text-align:center;color:var(--concrete)">No hay usuarios registrados.</td></tr>';
  }catch(e){
    filas='<tr><td colspan="5" style="text-align:center;color:var(--rust)">Error al cargar los usuarios desde el servidor.</td></tr>';
  }
  const f=ST.formUsuario;
  const form=f?`<div class="card" style="margin-top:20px" id="form_usuario"><div class="ch"><h4>${f.id?'Editar usuario':'Nuevo usuario'}</h4></div><div class="cb">
    <div class="g2"><div class="f"><label>Nombres</label><input id="usu_nombres" value="${esc(f.nombres)}"></div>
     <div class="f"><label>Apellidos</label><input id="usu_apellidos" value="${esc(f.apellidos)}"></div></div>
    <div class="g2"><div class="f"><label>Correo electrónico</label><input id="usu_correo" type="email" value="${esc(f.correo)}" ${f.id?'disabled':''} placeholder="nombre@correo.com">
      ${f.id?'':'<div class="hint" style="margin-top:4px">Se enviará un enlace a este correo para que defina su contraseña.</div>'}</div>
     <div class="f"><label>Rol</label><select id="usu_rol"><option value="">Seleccione un rol</option>
      ${roles.map(r=>`<option value="${r.id}" ${String(f.rolId)===String(r.id)?'selected':''}>${esc(r.nombre)}</option>`).join('')}</select></div></div>
    <button class="btn" onclick="guardarUsuario()">${f.id?'Guardar cambios':'Registrar usuario'}</button>
    <button class="btn ghost" style="margin-left:8px" onclick="cancelarUsuario()">Cancelar</button>
   </div></div>`:'';
  return `${navbar()}${crumbs([['Usuarios']])}
 <div class="page"><div class="phead"><div><h1>Usuarios</h1><p>Cuentas con acceso a la plataforma y el rol que define qué puede ver cada una.</p></div>
  <button class="btn" onclick="abrirFormularioUsuario()">Nuevo usuario</button></div>
  ${ST.hu?'<div class="cota"><span class="t"></span><span class="b"></span><span class="r"></span><span class="hu">HU04</span></div>':''}
  ${ST.msg||''}
  <div class="card"><table><thead><tr><th>Nombre</th><th>Correo</th><th>Rol</th><th>Estado</th><th></th></tr></thead><tbody>
  ${filas}
  </tbody></table></div>${form}</div>`;
};

const TIPOS_OBRA=['Edificio multifamiliar','Vivienda unifamiliar','Obra comercial','Obra industrial','Remodelación','Obra civil menor'];

V.portafolio=async()=>{
  let filas='',proyectos=[];
  try{
    const r=await apiFetch(`${API}/api/portafolio`);
    if(!r.ok) throw new Error();
    proyectos=await r.json();
    ST.portafolioCache=proyectos;
    filas=proyectos.map(p=>`<tr><td><b>${esc(p.nombre)}</b></td><td>${esc(p.tipoObra)}</td><td>${p.anio}</td>
     <td>${p.estado==='Publicado'?'<span class="tg ok">Publicado</span>':'<span class="tg mt">Borrador</span>'}</td>
     <td style="width:290px;white-space:nowrap"><button class="btn link" onclick="editarProyecto(${p.id})">Editar</button> ·
      <button class="btn link" onclick="cambiarPublicacion(${p.id},${p.estado==='Publicado'?'false':'true'})">${p.estado==='Publicado'?'Despublicar':'Publicar'}</button> ·
      <button class="btn link" style="color:var(--rust)" onclick="eliminarProyecto(${p.id})">Eliminar</button></td></tr>`).join('')
      ||'<tr><td colspan="5" style="text-align:center;color:var(--concrete)">Aún no hay proyectos en el portafolio.</td></tr>';
  }catch(e){
    filas='<tr><td colspan="5" style="text-align:center;color:var(--rust)">Error al cargar el portafolio desde el servidor.</td></tr>';
  }

  const f=ST.formProyecto;
  let serviciosAdm=[];
  if(f){ try{ const rs=await apiFetch(`${API}/api/servicios`); if(rs.ok) serviciosAdm=await rs.json(); }catch(e){} }
  const form=f?`<div class="card" style="margin-top:20px" id="form_proyecto"><div class="ch"><h4>${f.id?'Editar proyecto':'Nuevo proyecto'}</h4>
    <span class="hint">Se guarda como borrador hasta que lo publique.</span></div><div class="cb">
    <div class="g2"><div class="f"><label>Nombre del proyecto</label><input id="port_nombre" value="${esc(f.nombre)}" placeholder="Condominio Las Palmeras"></div>
     <div class="f"><label>Tipo de obra</label><select id="port_tipo"><option value="">Seleccione un tipo</option>
      ${TIPOS_OBRA.map(t=>`<option ${f.tipoObra===t?'selected':''}>${t}</option>`).join('')}</select></div>
     <div class="f"><label>Ubicación</label><input id="port_ubicacion" value="${esc(f.ubicacion)}" placeholder="Chorrillos, Lima"></div>
     <div class="f"><label>Año de ejecución</label><input id="port_anio" type="number" min="1990" value="${esc(f.anio)}" placeholder="2025"></div></div>
    <div class="f"><label>Servicio relacionado</label><select id="port_servicio"><option value="">Sin servicio</option>
      ${serviciosAdm.map(s=>`<option value="${s.id}" ${String(f.servicioId)===String(s.id)?'selected':''}>${esc(s.nombre)}${s.estado==='Oculto'?' (oculto)':''}</option>`).join('')}</select>
      <div class="hint" style="margin-top:4px">El proyecto aparecerá en «Proyectos relacionados» de ese servicio.</div></div>
    <div class="f"><label>Descripción</label><textarea id="port_descripcion" placeholder="Edificio de cinco niveles con veinte departamentos.">${esc(f.descripcion)}</textarea></div>
    <button class="btn" onclick="guardarProyecto()">${f.id?'Guardar cambios':'Guardar borrador'}</button>
    <button class="btn ghost" style="margin-left:8px" onclick="cancelarProyecto()">Cancelar</button>
   </div></div>`:'';

  // Las imágenes se gestionan sobre un proyecto ya guardado
  const sel=f&&f.id?proyectos.find(p=>p.id===f.id):null;
  const imagenes=sel?`<div class="card" style="margin-top:20px"><div class="ch"><h4>Imágenes · ${esc(sel.nombre)}</h4></div>
   <div class="cb"><div class="thumbs">
    ${sel.imagenes.map(i=>`<div class="th" style="width:150px;height:130px;padding:0;position:relative;overflow:hidden;${i.esPrincipal?'border:2px solid var(--safety)':''}">
      <img data-img-id="${i.id}" alt="${esc(i.nombre)}" style="width:100%;height:100%;object-fit:cover">
      <div style="position:absolute;left:0;right:0;bottom:0;background:rgba(10,25,41,.82);color:#fff;font-size:11.5px;padding:3px 5px;display:flex;justify-content:space-between;gap:4px">
       ${i.esPrincipal?'<b>Principal</b>':`<button class="btn link" style="color:#fff;font-size:11.5px" onclick="marcarImagenPrincipal(${i.id})">Hacer principal</button>`}
       <button class="btn link" style="color:#fff;font-size:11.5px" onclick="eliminarImagenProyecto(${i.id})">Quitar</button></div></div>`).join('')}
    <div><input type="file" id="port_archivos" accept=".jpg,.jpeg,.png,image/jpeg,image/png" multiple style="display:none" onchange="subirImagenesProyecto(${sel.id})">
     <button class="btn ghost sm" onclick="document.getElementById('port_archivos').click()">Subir imágenes</button></div></div>
    <div class="hint" style="margin-top:7px">JPG o PNG hasta 5 MB. La imagen marcada como principal se usa como miniatura en el sitio.</div>
  </div></div>`:'';

  return `${navbar()}${crumbs([['Sitio web'],['Portafolio']])}
 <div class="page"><div class="phead"><div><h1>Portafolio</h1><p>Obras que se muestran en el sitio público. Solo las publicadas son visibles para los visitantes.</p></div>
  <button class="btn" onclick="nuevoProyecto()">Nuevo proyecto</button></div>
  ${ST.hu?'<div class="cota"><span class="t"></span><span class="b"></span><span class="r"></span><span class="hu">HU08</span></div>':''}
  ${ST.msg||''}
  <div class="card"><table><thead><tr><th>Nombre</th><th>Tipo de obra</th><th>Año</th><th>Estado</th><th></th></tr></thead><tbody>
  ${filas}
  </tbody></table></div>${form}${imagenes}</div>`;
};

V['servicios-adm']=async()=>{
  let filas='',servicios=[];
  try{
    const r=await apiFetch(`${API}/api/servicios`);
    if(!r.ok) throw new Error();
    servicios=await r.json();
    ST.serviciosAdmCache=servicios;
    filas=servicios.map(s=>`<tr><td class="num">${s.orden}</td><td><b>${esc(s.nombre)}</b><div class="hint">${esc(s.descripcion)}</div></td>
     <td>${s.estado==='Publicado'?'<span class="tg ok">Publicado</span>':'<span class="tg mt">Oculto</span>'}</td>
     <td style="width:150px;white-space:nowrap"><button class="btn link" onclick="editarServicio(${s.id})">Editar</button> ·
      <button class="btn link" style="color:var(--rust)" onclick="confirmarEliminarServicio(${s.id})">Eliminar</button></td></tr>`).join('')
      ||'<tr><td colspan="4" style="text-align:center;color:var(--concrete)">Aún no hay servicios en el catálogo.</td></tr>';
  }catch(e){
    filas='<tr><td colspan="4" style="text-align:center;color:var(--rust)">Error al cargar los servicios desde el servidor.</td></tr>';
  }

  const f=ST.formServicio;
  const form=f?`<div class="card" style="margin-top:20px" id="form_servicio"><div class="ch"><h4>${f.id?'Editar servicio':'Nuevo servicio'}</h4></div><div class="cb">
    <div class="g2"><div class="f"><label>Nombre del servicio</label><input id="srv_nombre" value="${esc(f.nombre)}" placeholder="Construcción de edificios"></div>
     <div class="g2" style="gap:0 12px"><div class="f"><label>Orden</label><input id="srv_orden" type="number" min="1" value="${esc(f.orden)}"></div>
      <div class="f"><label>Estado</label><select id="srv_estado"><option ${f.estado==='Publicado'?'selected':''}>Publicado</option><option ${f.estado==='Oculto'?'selected':''}>Oculto</option></select></div></div></div>
    <div class="f"><label>Descripción breve</label><textarea id="srv_descripcion" placeholder="Se muestra en el listado del catálogo.">${esc(f.descripcion)}</textarea></div>
    <div class="g2"><div class="f"><label>Beneficios</label><textarea id="srv_beneficios" placeholder="Uno por línea">${esc(f.beneficios)}</textarea></div>
     <div class="f"><label>Características</label><textarea id="srv_caracteristicas" placeholder="Una por línea">${esc(f.caracteristicas)}</textarea></div></div>
    <div class="hint" style="margin-bottom:10px">El orden define la posición en el sitio; los demás servicios se reacomodan solos.</div>
    <button class="btn" onclick="guardarServicio()">${f.id?'Guardar cambios':'Registrar servicio'}</button>
    <button class="btn ghost" style="margin-left:8px" onclick="cancelarServicio()">Cancelar</button>
   </div></div>`:'';

  return `${navbar()}${crumbs([['Sitio web'],['Servicios']])}
 <div class="page"><div class="phead"><div><h1>Servicios</h1><p>Catálogo que aparece en el sitio público. El orden define cómo se listan.</p></div>
  <button class="btn" onclick="nuevoServicio()">Nuevo servicio</button></div>
  ${ST.hu?'<div class="cota"><span class="t"></span><span class="b"></span><span class="r"></span><span class="hu">HU09</span></div>':''}
  ${ST.msg||''}
  <div class="card"><table><thead><tr><th class="num" style="width:70px">Orden</th><th>Servicio</th><th>Estado</th><th></th></tr></thead><tbody>
  ${filas}
  </tbody></table></div>${form}</div>`;
};

const ESPECIALIDADES=['Maestro de Obra','Operario','Oficial','Ayudante','Peón'];

// Filas del listado de personal; se vuelven a pintar al escribir en el buscador sin redibujar la página
function filasPersonal(lista){
  return lista.map(t=>`<tr><td><b>${esc(t.nombreCompleto)}</b></td><td>${esc(t.tipoDocumento)} ${esc(t.documento)}</td>
   <td>${esc(t.especialidad)}</td><td>${esc(t.telefono)||'—'}</td><td class="num">${S(t.jornalDiario)}</td>
   <td>${t.estado==='Activo'?'<span class="tg ok">Activo</span>':'<span class="tg mt">Inactivo</span>'}</td>
   <td style="width:170px;white-space:nowrap"><button class="btn link" onclick="editarTrabajador(${t.id})">Editar</button> ·
    ${t.estado==='Activo'?`<button class="btn link" onclick="cambiarEstadoTrabajador(${t.id},false)">Desactivar</button>`
      :`<button class="btn link" onclick="cambiarEstadoTrabajador(${t.id},true)">Activar</button>`}</td></tr>`).join('')
    ||'<tr><td colspan="7" style="text-align:center;color:var(--concrete)">No hay colaboradores que coincidan.</td></tr>';
}

V.personal=async()=>{
  let personal=[],error=false;
  try{
    const r=await apiFetch(`${API}/api/personal`);
    if(!r.ok) throw new Error();
    personal=await r.json();
  }catch(e){ error=true; }
  ST.personalCache=personal;
  const activos=personal.filter(t=>t.estado==='Activo').length;

  const f=ST.formTrabajador;
  let historial='';
  if(f&&f.id){
    try{
      const r=await apiFetch(`${API}/api/personal/${f.id}/historial`);
      const obras=r.ok?await r.json():[];
      historial=`<div class="card" style="margin-top:16px"><div class="ch"><h4>Historial de obras asignadas</h4></div>
       <table><thead><tr><th>Obra</th><th>Especialidad</th><th class="num">Jornal</th><th>Ingreso</th></tr></thead><tbody>
       ${obras.map(o=>`<tr><td>${o.obraId?`<button class="btn link" onclick="abrirObra(${o.obraId})">${esc(o.obra)}</button>`:esc(o.obra)}
         ${o.estadoObra==='Finalizado'?' <span class="tg mt">Finalizada</span>':''}</td>
         <td>${esc(o.especialidad)}</td><td class="num">${S(o.jornal)}</td><td>${esc(o.fechaIngreso)}</td></tr>`).join('')
         ||'<tr><td colspan="4" style="text-align:center;color:var(--concrete)">Aún no ha sido asignado a ninguna obra.</td></tr>'}
       </tbody></table></div>`;
    }catch(e){ /* sin historial */ }
  }
  const form=f?`<div class="card" style="margin-top:20px" id="form_trabajador"><div class="ch"><h4>${f.id?'Editar colaborador':'Nuevo colaborador'}</h4></div><div class="cb">
    <div class="g2"><div class="f"><label>Nombres</label><input id="trab_nombres" value="${esc(f.nombres)}"></div>
     <div class="f"><label>Apellidos</label><input id="trab_apellidos" value="${esc(f.apellidos)}"></div>
     <div class="g2" style="gap:0 12px"><div class="f"><label>Documento</label><select id="trab_tipo">
       <option ${f.tipoDocumento==='DNI'?'selected':''}>DNI</option><option value="CE" ${f.tipoDocumento==='CE'?'selected':''}>CE</option></select></div>
      <div class="f"><label>Número</label><input id="trab_documento" value="${esc(f.documento)}" inputmode="numeric"></div></div>
     <div class="f"><label>Especialidad</label><select id="trab_especialidad"><option value="">Seleccione</option>
       ${ESPECIALIDADES.map(e=>`<option ${f.especialidad===e?'selected':''}>${e}</option>`).join('')}</select></div>
     <div class="f"><label>Teléfono</label><input id="trab_telefono" type="tel" value="${esc(f.telefono)}" placeholder="987 654 321"></div>
     <div class="f"><label>Jornal diario (S/)</label><input id="trab_jornal" type="number" min="1" step="0.01" value="${esc(f.jornalDiario)}"></div></div>
    <button class="btn" onclick="guardarTrabajador()">${f.id?'Guardar cambios':'Registrar colaborador'}</button>
    <button class="btn ghost" style="margin-left:8px" onclick="cancelarTrabajador()">Cancelar</button>
    ${historial}
   </div></div>`:'';

  return `${navbar()}${crumbs([['Personal']])}
 <div class="page"><div class="phead"><div><h1>Personal</h1><p>Colaboradores de obra con su documento, especialidad y jornal diario.</p></div>
  <button class="btn" onclick="nuevoTrabajador()">Nuevo colaborador</button></div>
  ${ST.hu?'<div class="cota"><span class="t"></span><span class="b"></span><span class="r"></span><span class="hu">HU10</span></div>':''}
  ${ST.msg||''}
  <div class="card"><div class="ch"><input id="buscar_personal" placeholder="Buscar por nombre, documento o especialidad" oninput="filtrarPersonal()" style="flex:1;max-width:320px;padding:6px 10px;border:1px solid var(--line);font-family:var(--b)">
   <span class="hint">${personal.length} registrados · ${activos} activos</span></div>
  <div style="overflow-x:auto"><table><thead><tr><th>Nombres y apellidos</th><th>Documento</th><th>Especialidad</th><th>Teléfono</th><th class="num">Jornal</th><th>Estado</th><th></th></tr></thead>
  <tbody id="tabla_personal">${error?'<tr><td colspan="7" style="text-align:center;color:var(--rust)">Error al cargar el personal desde el servidor.</td></tr>':filasPersonal(personal)}</tbody></table></div></div>
  ${form}</div>`;
};

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
      const respuesta = await apiFetch(API + '/api/obras');
      if (!respuesta.ok) throw new Error();
      const obrasAPI = await respuesta.json();

      if (obrasAPI.length === 0) {
          filas = `<tr><td colspan="5" style="text-align:center;color:var(--concrete)">No hay obras registradas en la base de datos.</td></tr>`;
      } else {
          filas = obrasAPI.map(p => `<tr><td><b>${esc(p.nombre)}</b></td><td>${esc(p.cliente)}</td>
           <td>${p.estado === 'Finalizado' ? '<span class="tg mt">Finalizado</span>' : '<span class="tg ok">En ejecución</span>'}</td>
           <td><div style="display:flex;align-items:center;gap:9px"><div class="bar" style="flex:1"><i style="width:${p.avancePorcentaje}%"></i></div>
            <span style="font-variant-numeric:tabular-nums">${p.avancePorcentaje}%</span></div></td>
           <td style="width:90px"><button class="btn ghost sm" onclick="abrirObra(${p.id})">Abrir</button></td></tr>`).join('');
      }
  } catch (error) {
      filas = `<tr><td colspan="5" style="text-align:center;color:var(--rust)">Error de conexión con el servidor.</td></tr>`;
  }

  const f = ST.formObra;
  const cliente = f && f.cliente
    ? `<div style="display:flex;align-items:center;gap:10px;padding:8px 10px;border:1px solid var(--line);background:var(--surface)">
        <span><b>${esc(f.cliente.nombre)}</b> · ${esc(f.cliente.tipoDocumento)} ${esc(f.cliente.documento)}</span>
        <button class="btn link" onclick="cambiarClienteObra()">Cambiar</button></div>`
    : `<input id="obra_cliente_q" autocomplete="off" placeholder="Escriba el nombre o el documento del cliente" oninput="buscarClientes()">
       <div id="cli_resultados"></div>
       <div class="hint" style="margin-top:4px">¿El cliente no está registrado? <button class="btn link" onclick="abrirModalCliente()">Registrar nuevo cliente</button></div>`;
  const form = f ? `<div class="card" style="margin-top:22px" id="form_obra"><div class="ch"><h4>Registrar nuevo proyecto</h4>${ST.hu?'<span class="hu">HU11</span>':''}</div><div class="cb">
     <div class="g2"><div class="f"><label>Nombre del proyecto</label><input id="nuevo_nombre" value="${esc(f.nombre)}" placeholder="Edificio multifamiliar Los Cedros"></div>
      <div class="f"><label>Ubicación</label><input id="nueva_ubic" value="${esc(f.ubicacion)}" placeholder="Santiago de Surco, Lima"></div>
      <div class="f"><label>Fecha de inicio</label><input id="nueva_inicio" type="date" value="${esc(f.fechaInicio)}"></div>
      <div class="f"><label>Fecha estimada de fin</label><input id="nueva_fin" type="date" value="${esc(f.fechaFin)}"></div></div>
     <div class="f"><label>Cliente</label>${cliente}</div>
     <div class="f"><label>Descripción</label><textarea id="nueva_desc" placeholder="Edificio de cinco niveles con veinte departamentos y sótano.">${esc(f.descripcion)}</textarea></div>
     <div class="hint" style="margin-bottom:10px">Al guardar, el proyecto queda «En Ejecución» y el cliente recibe acceso a su panel de seguimiento.</div>
     <button class="btn" onclick="guardarObra()">Guardar</button>
     <button class="btn ghost" style="margin-left:8px" onclick="cancelarObra()">Cancelar</button>
    </div></div>` : '';

  return `${navbar()}${crumbs([['Obras']])}
   <div class="page"><div class="phead"><div><h1>Obras</h1><p>Expediente digital de cada proyecto: hitos, cuadrilla, documentos y control económico.</p></div>
    <button class="btn" onclick="abrirFormularioObra()">Nuevo proyecto</button></div>
    ${ST.hu?'<div class="cota"><span class="t"></span><span class="b"></span><span class="r"></span><span class="hu">HU11</span></div>':''}
    ${ST.msg||''}
    <div class="card"><table><thead><tr><th>Obra</th><th>Cliente</th><th>Estado</th><th style="width:200px">Avance</th><th></th></tr></thead><tbody>
    ${filas}
    </tbody></table></div>${form}</div>`;
};

/* ---- obra con pestañas ---- */
const TABS=[['hitos','Hitos','HU12 · HU13'],['cuad','Cuadrilla','HU14'],['docs','Documentos','HU15'],
 ['pres','Presupuesto','HU16'],['gastos','Gastos','HU17'],['pagos','Pagos a personal','HU18'],['econ','Control económico','HU19']];
 
V.obra = async () => {
  if (!ST.tab) ST.tab = 'hitos';
  const t = TABS.find(x => x[0] === ST.tab) || TABS[0];
  
  // Sin una obra elegida se vuelve al listado
  if (!ST.obraId) { ST.v = 'proyectos'; return V.proyectos(); }

  let obra = null;
  try {
    const respuesta = await apiFetch(API + '/api/obras');
    obra = (await respuesta.json()).find(o => o.id === ST.obraId) || null;
  } catch (e) { /* se muestra el aviso de abajo */ }
  if (!obra) { ST.obraId = null; ST.v = 'proyectos'; ST.msg = '<div class="msg bad">No se encontró la obra seleccionada.</div>'; return V.proyectos(); }

  const contenidoPestana = await TC[ST.tab]();

  return `${navbar()}${crumbs([['Obras','proyectos'],[esc(obra.nombre)]])}
   <div class="page" style="padding-bottom:4px"><div class="phead" style="margin-bottom:12px"><div>
     <h1>${esc(obra.nombre)}</h1><p>${esc(obra.cliente)}</p></div>
     <div style="display:flex;gap:7px;align-items:center"><span class="tg ${obra.estado === 'Finalizado' ? 'mt' : 'ok'}">${obra.estado === 'Finalizado' ? 'Finalizado' : 'En ejecución'}</span>
     <span class="tg mt">${obra.avancePorcentaje}% ejecutado</span>
     ${obra.estado === 'Finalizado'
       ? (obra.fechaCierre ? `<span class="hint">Cierre real: ${fmtFecha(obra.fechaCierre)}</span>` : '')
       : '<button class="btn ghost sm" onclick="abrirFinalizarObra()">Finalizar obra</button>'}</div></div>
     ${ST.finalizando && obra.estado !== 'Finalizado' ? `<div class="card" style="margin-bottom:12px"><div class="cb" style="display:flex;gap:12px;align-items:flex-end;flex-wrap:wrap">
       <div class="f" style="margin:0"><label>Fecha real de cierre</label>
        <input id="fin_fecha" type="date" value="${new Date().toLocaleDateString('en-CA')}" max="${new Date().toLocaleDateString('en-CA')}" ${obra.fechaInicio ? `min="${obra.fechaInicio}"` : ''}></div>
       <button class="btn" onclick="finalizarObra(${obra.id})">Confirmar cierre</button>
       <button class="btn ghost" onclick="cancelarFinalizarObra()">Cancelar</button></div></div>` : ''}</div>
   <div class="tabs"><div class="tabs-in">${TABS.map(x=>`<button class="${x[0]===ST.tab?'on':''}" onclick="tab('${x[0]}')">${x[1]}</button>`).join('')}</div></div>
   <div class="page">${cota(t[1],t[2])}${ST.msg||''}${contenidoPestana}</div>`;
};

const TC={};

// Detalle de un hito: datos, botón «Registrar Avance», formulario e historial de avances (HU13)
async function detalleHito(h) {
  let filasHistorial = '';
  try {
    const respuesta = await apiFetch(`${API}/api/obras/hitos/${h.id}/avances`);
    const avances = respuesta.ok ? await respuesta.json() : [];
    filasHistorial = avances.length ? avances.map(a => `<tr>
        <td style="white-space:nowrap">${fmtFechaHora(a.fecha)}</td>
        <td class="num">${a.porcentaje}%</td>
        <td>${esc(a.descripcion)}</td>
        <td>${esc(a.responsable)}</td>
        <td><div class="thumbs">${a.fotos.length
          ? a.fotos.map(f => `<img class="th" data-foto-avance="${f.id}" alt="${esc(f.nombre)}" title="${esc(f.nombre)}" style="object-fit:cover;cursor:pointer" onclick="ampliarFotoAvance(this)">`).join('')
          : '<span class="hint">Sin fotografías</span>'}</div></td>
      </tr>`).join('')
      : '<tr><td colspan="5" style="text-align:center;color:var(--concrete)">Aún no se registraron avances en este hito.</td></tr>';
  } catch (error) {
    filasHistorial = '<tr><td colspan="5" style="text-align:center;color:var(--rust)">Error al cargar el historial de avances.</td></tr>';
  }

  const completado = h.estado === 'Completado';
  const estado = completado ? '<span class="tg ok">Completado</span>' : h.estado === 'En proceso' ? '<span class="tg wn">En proceso</span>' : '<span class="tg mt">Pendiente</span>';
  const hoy = new Date().toLocaleDateString('es-PE', { day: '2-digit', month: '2-digit', year: 'numeric' });
  const f = ST.formAvance || { porcentaje: '', descripcion: '' };

  const formulario = `<div class="card" style="margin-top:16px" id="form_avance"><div class="ch"><h4>Registrar avance</h4></div><div class="cb">
      <div class="g3">
        <div class="f"><label>Porcentaje de avance (${h.avancePorcentaje + 1} a 100)</label>
          <input id="avance_porcentaje" type="number" min="${h.avancePorcentaje + 1}" max="100" value="${esc(f.porcentaje)}"></div>
        <div class="f"><label>Fecha</label><input value="${hoy}" disabled></div>
        <div class="f"><label>Responsable</label><input value="${esc(ST.usuario ? ST.usuario.nombreCompleto : '')}" disabled></div>
      </div>
      <div class="f"><label>Descripción del trabajo realizado</label>
        <textarea id="avance_descripcion" maxlength="1000">${esc(f.descripcion)}</textarea></div>
      <div class="f"><label>Fotografías (JPG o PNG, hasta 5 archivos de 5 MB)</label>
        <input type="file" id="avance_fotos" multiple accept="image/png, image/jpeg" onchange="mostrarFotosAvance(this)">
        <span id="avance_nombres" class="hint"></span></div>
      <div id="avance_error"></div>
      <button class="btn sm" onclick="guardarAvance(${h.id})">Guardar avance</button>
      <button class="btn ghost sm" style="margin-left:8px" onclick="cancelarAvance()">Cancelar</button>
    </div></div>`;

  return `<div class="card" style="margin-top:20px" id="detalle_hito">
    <div class="ch"><h4>Detalle del hito · <span style="color:var(--safety)">${esc(h.nombre)}</span></h4>${ST.hu ? '<span class="hu">HU13</span>' : ''}</div>
    <div class="cb">
      <div class="g3">
        <div class="f"><label>Estado</label>${estado}</div>
        <div class="f"><label>Peso en la obra</label>${h.peso}%</div>
        <div class="f"><label>Fecha planificada · real</label>${fmtFecha(h.fechaPlanificada)} · ${fmtFecha(h.fechaReal)}</div>
      </div>
      <div class="f"><label>Avance del hito: ${h.avancePorcentaje}%</label><div class="bar"><i style="width:${h.avancePorcentaje}%"></i></div></div>
      ${completado ? '<p class="hint">El hito está completado y ya no admite nuevos avances.</p>'
        : (ST.registrandoAvance ? '' : '<button class="btn sm" onclick="abrirFormularioAvance()">Registrar Avance</button>')}
      <button class="btn ghost sm" style="margin-left:8px" onclick="cerrarDetalleHito()">Cerrar detalle</button>
      ${ST.registrandoAvance && !completado ? formulario : ''}
      <h4 style="margin-top:22px">Historial de avances</h4>
      <table><thead><tr><th>Fecha</th><th class="num">Avance</th><th>Descripción</th><th>Responsable</th><th>Fotografías</th></tr></thead><tbody>
      ${filasHistorial}
      </tbody></table>
    </div>
  </div>`;
}

TC.hitos = async () => {
  let filas = '';
  let detalle = '';
  let lineaDeTiempo = '';
  let resumenPesos = '';
  let hitosReales = [];

  try {
    const respuesta = await apiFetch(API + '/api/obras');
    const obras = await respuesta.json();
    const obraActual = obras.find(o => o.id === ST.obraId) || null;
    hitosReales = obraActual && obraActual.hitos ? obraActual.hitos : [];
    ST.hitosCache = hitosReales;

    // Suma acumulada de pesos (debe llegar a 100 %)
    const acumulado = hitosReales.reduce((a, h) => a + h.peso, 0);
    const estadoPesos = acumulado === 100 ? 'ok' : (acumulado > 100 ? 'bad' : 'warn');
    const textoPesos = acumulado === 100 ? 'La planificación está completa.'
      : `Faltan ${100 - acumulado}% por asignar para completar el 100%.`;
    resumenPesos = `<div class="msg ${estadoPesos}" style="margin-top:20px"><b>Suma de pesos: ${acumulado}% de 100%.</b> ${hitosReales.length ? textoPesos : 'Aún no hay hitos definidos.'}
      <div class="bar" style="margin-top:8px"><i style="width:${Math.min(acumulado, 100)}%"></i></div></div>`;

    if (hitosReales.length === 0) {
      filas = `<tr><td colspan="8" style="text-align:center;color:var(--concrete)">No hay hitos registrados en esta obra.</td></tr>`;
    } else {
      lineaDeTiempo = elevHitos(hitosReales);

      filas = hitosReales.map(h => {
        const pendiente = h.estado === 'Pendiente' && h.avancePorcentaje === 0;

        return `<tr>
          <td class="num">${h.orden}</td>
          <td><b>${esc(h.nombre)}</b></td>
          <td class="num">${h.peso}%</td>
          <td>${fmtFecha(h.fechaPlanificada)}</td>
          <td>${fmtFecha(h.fechaReal)}</td>
          <td>${h.estado === 'Completado' ? '<span class="tg ok">Completado</span>' : h.estado === 'En proceso' ? '<span class="tg wn">En proceso</span>' : '<span class="tg mt">Pendiente</span>'}</td>
          <td class="num">${h.avancePorcentaje}%</td>
          <td style="white-space:nowrap;width:260px">
            ${pendiente ? `<button class="btn link" onclick="editarHito(${h.id})">Editar</button> ·
            <button class="btn link" onclick="eliminarHito(${h.id})">Eliminar</button> · ` : ''}
            <button class="btn link" onclick="seleccionarHito(${h.id})">Ver detalle</button>
          </td>
        </tr>`;
      }).join('');

      const seleccionado = hitosReales.find(h => h.id === ST.hitoEditando);
      if (seleccionado) detalle = await detalleHito(seleccionado);
    }
  } catch (error) {
    filas = `<tr><td colspan="8" style="text-align:center;color:var(--rust)">Error al cargar los hitos desde el servidor.</td></tr>`;
  }

  const f = ST.formHito || { id: null, nombre: '', peso: '', fecha: '' };

  return `${lineaDeTiempo}
   ${resumenPesos}
   <div class="card" style="margin-top:20px" id="form_hito"><div class="ch"><h4>${f.id ? 'Editar hito' : 'Agregar hito'}</h4></div><div class="cb">
    <div class="g3">
     <div class="f"><label>Nombre del hito</label><input id="nuevo_hito_nombre" placeholder="Ej. Vaciado de losa" value="${esc(f.nombre)}"></div>
     <div class="f"><label>Peso (%)</label><input id="nuevo_hito_peso" type="number" min="1" max="100" placeholder="15" value="${esc(f.peso)}"></div>
     <div class="f"><label>Fecha planificada</label><input id="nuevo_hito_fecha" type="date" value="${esc(f.fecha)}"></div>
    </div>
    <button class="btn sm" onclick="guardarHito(${ST.obraId})">${f.id ? 'Guardar cambios' : 'Guardar hito'}</button>
    ${f.id ? '<button class="btn ghost sm" style="margin-left:8px" onclick="cancelarHito()">Cancelar</button>' : ''}
   </div></div>

   <div class="card" style="margin-top:20px"><div class="ch"><span class="hint">Listado de hitos del proyecto</span></div>
   <table><thead><tr><th class="num" style="width:60px">Orden</th><th>Hito</th><th class="num">Peso</th><th>Planificada</th><th>Real</th><th>Estado</th><th class="num">Avance</th><th></th></tr></thead><tbody>
   ${filas}
   </tbody></table></div>

   ${detalle}
   <p class="hint" style="margin-top:14px">Solo se pueden editar o eliminar los hitos pendientes, sin avances registrados.</p>`;
};

TC.cuad = async () => {
  let filas = '';
  let activos = 0, retirados = 0, finalizada = false;
  try {
    const respuesta = await apiFetch(API + '/api/obras');
    const obras = await respuesta.json();
    const obraActual = obras.find(o => o.id === ST.obraId) || null;
    finalizada = !!obraActual && obraActual.estado === 'Finalizado';
    // Primero quienes siguen en obra; los retirados quedan como historial de participación
    const cuadrilla = (obraActual && obraActual.cuadrilla ? obraActual.cuadrilla : [])
      .slice().sort((x, y) => (x.estado === 'Activo' ? 0 : 1) - (y.estado === 'Activo' ? 0 : 1) || y.id - x.id);
    activos = cuadrilla.filter(c => c.estado === 'Activo').length;
    retirados = cuadrilla.length - activos;

    if (cuadrilla.length === 0) {
      filas = `<tr><td colspan="6" style="text-align:center;color:var(--concrete)">No hay colaboradores asignados a esta obra.</td></tr>`;
    } else {
      filas = cuadrilla.map(c => {
        const activo = c.estado === 'Activo';
        return `<tr${activo ? '' : ' style="color:var(--concrete)"'}>
        <td><b>${esc(c.nombreCompleto)}</b></td>
        <td>${esc(c.especialidad)}</td>
        <td class="num">${S(c.jornal)}</td>
        <td>${esc(c.fechaIngresoObra)}</td>
        <td>${activo ? '<span class="tg ok">Activo</span>' : `<span class="tg mt">Retirado el ${fmtFecha(c.fechaSalida)}</span>`}</td>
        <td style="width:150px">${activo ? `<button class="btn link" onclick="registrarSalida(${c.id})">Registrar salida</button>` : ''}</td>
      </tr>`;
      }).join('');
    }
  } catch (error) {
    filas = `<tr><td colspan="6" style="text-align:center;color:var(--rust)">Error al cargar la cuadrilla desde el servidor.</td></tr>`;
  }

  return `
   <div class="card"><div class="ch"><span class="hint">${activos} colaboradores en obra${retirados ? ` · ${retirados} con salida registrada` : ''}</span>
    <button class="btn sm" onclick="abrirModalCuadrilla()" ${finalizada ? 'disabled title="La obra está finalizada"' : ''}>Asignar Colaborador</button>${ST.hu ? '<span class="hu">HU14</span>' : ''}</div>
   <table><thead><tr><th>Colaborador</th><th>Especialidad</th><th class="num">Jornal</th><th>Fecha de ingreso</th><th>Estado</th><th></th></tr></thead><tbody>
   ${filas}
   </tbody></table></div>`;
};

TC.docs = async () => {
  let filas = '';
  let total = 0, visibles = 0;
  try {
    const respuesta = await apiFetch(API + '/api/obras');
    const obras = await respuesta.json();
    const obraActual = obras.find(o => o.id === ST.obraId) || null;
    const docsReales = obraActual && obraActual.documentos ? obraActual.documentos : [];
    total = docsReales.length;
    visibles = docsReales.filter(d => d.visibleCliente).length;

    if (total === 0) {
      filas = `<tr><td colspan="5" style="text-align:center;color:var(--concrete)">No hay documentos cargados en esta obra.</td></tr>`;
    } else {
      filas = docsReales.slice().sort((a, b) => b.id - a.id).map(d => `<tr>
        <td><b>${esc(d.nombre)}</b></td>
        <td><span class="tg mt">${esc(d.clasificacion)}</span></td>
        <td>${esc(d.fechaCarga)}</td>
        <td>${d.visibleCliente ? '<span class="tg ok">Sí</span>' : '<span class="tg mt">No — uso interno</span>'}</td>
        <td style="width:330px;white-space:nowrap">
          <button class="btn link" onclick="descargarDocumento(${d.id})">Descargar</button> ·
          <button class="btn link" onclick="cambiarVisibilidadDocumento(${d.id}, ${!d.visibleCliente})">${d.visibleCliente ? 'Ocultar al cliente' : 'Mostrar al cliente'}</button> ·
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
       <input type="file" id="nuevo_doc_archivo" style="display:none" accept=".pdf,.jpg,.jpeg,.png,application/pdf,image/png,image/jpeg" onchange="document.getElementById('nombre_doc').innerText = this.files[0] ? this.files[0].name : ''">
       <button class="btn ghost sm" onclick="document.getElementById('nuevo_doc_archivo').click()">Seleccionar archivo</button>
       <span id="nombre_doc" class="hint" style="margin-left: 10px;"></span>
     </div>
     <div class="f"><label>Clasificación</label><select id="nuevo_doc_clasif">
      <option>Contrato</option><option>Permiso</option><option>Plano</option><option>Comprobante</option><option>Otro</option>
     </select></div>
     <div class="f"><label>Visibilidad</label>
      <label style="display:flex;gap:8px;align-items:center;font-weight:normal;margin-top:6px"><input type="checkbox" id="nuevo_doc_vis" style="width:auto"> Visible para el cliente</label>
      <div class="hint" style="margin-top:4px">Sin marcar, el documento es de uso interno.</div></div>
    </div>
    <div id="doc_error"></div>
    <button class="btn sm" onclick="subirDocumento(${ST.obraId})">Subir Documento</button>${ST.hu ? '<span class="hu">HU15</span>' : ''}
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
  const editandoId = ST.partidaEditando || null;
  let edicion = { nombre: '', descripcion: '', monto: '' };

  try {
    const respuesta = await apiFetch(`${API}/api/obras/${ST.obraId}/presupuesto`);
    const presupuesto = await respuesta.json();
    const partidas = presupuesto.partidas || [];
    totalPartidas = partidas.length;
    montoTotal = presupuesto.montoTotal || 0;

    if (totalPartidas === 0) {
      filas = `<tr><td colspan="4" style="text-align:center;color:var(--concrete)">No hay partidas presupuestales registradas.</td></tr>`;
    } else {
      filas = partidas.map(p => {
        if (editandoId === p.id) edicion = { nombre: p.nombre, descripcion: p.descripcion, monto: p.montoAsignado };
        const conGastos = p.gastosAsociados > 0;
        return `<tr>
          <td><b>${esc(p.nombre)}</b></td>
          <td>${esc(p.descripcion) || '<span class="hint">—</span>'}</td>
          <td class="num">${S(p.montoAsignado)}</td>
          <td style="width:190px;white-space:nowrap">
            <button class="btn link" onclick="seleccionarPartida(${p.id})">Editar</button> ·
            ${conGastos
              ? `<span class="hint" title="Tiene ${p.gastosAsociados} gasto(s) asociado(s); no se puede eliminar">Con gastos</span>`
              : `<button class="btn link" onclick="eliminarPartida(${p.id})">Eliminar</button>`}
          </td>
        </tr>`;
      }).join('');
    }
  } catch (error) {
    filas = `<tr><td colspan="4" style="text-align:center;color:var(--rust)">Error al cargar el presupuesto desde el servidor.</td></tr>`;
  }

  return `
   <div class="card" style="margin-bottom:20px">
    <div class="ch"><h4>${editandoId ? 'Editar partida presupuestal' : 'Nueva partida'}</h4>${ST.hu ? '<span class="hu">HU16</span>' : ''}</div>
    <div class="cb">
     <div class="g3">
      <div class="f">
        <label>Partida</label>
        <input id="partida_nombre" maxlength="100" placeholder="Ej. Movimiento de tierras" value="${esc(edicion.nombre)}">
      </div>
      <div class="f">
        <label>Descripción (opcional)</label>
        <input id="partida_descripcion" maxlength="300" placeholder="Ej. Excavación y nivelación" value="${esc(edicion.descripcion)}">
      </div>
      <div class="f">
        <label>Monto (S/)</label>
        <input id="partida_monto" type="number" min="0.01" step="0.01" placeholder="48000" value="${esc(edicion.monto)}">
      </div>
     </div>
     <div id="partida_error"></div>
     <button class="btn sm" onclick="${editandoId ? `actualizarPartida(${editandoId})` : `agregarPartidaFormulario(${ST.obraId})`}">
        ${editandoId ? 'Guardar cambios' : 'Agregar Partida'}
     </button>
     ${editandoId ? `<button class="btn ghost sm" style="margin-left:8px" onclick="cancelarEdicionPartida()">Cancelar</button>` : ''}
    </div>
   </div>

   <div class="card"><div class="ch"><span class="hint">${totalPartidas} partidas registradas</span></div>
   <table><thead><tr><th>Partida</th><th>Descripción</th><th class="num">Monto (S/)</th><th></th></tr></thead><tbody>
   ${filas}
   <tr style="background:#F5F7F8"><td colspan="2"><b>Monto total presupuestado</b></td><td class="num"><b>${S(montoTotal)}</b></td><td></td></tr>
   </tbody></table>
   <p class="hint" style="margin:10px 14px">Una partida con gastos asociados no se puede eliminar, y su monto no puede bajar de lo ya gastado.</p></div>`;
};

TC.gastos = async () => {
  let filas = '';
  let opcionesPartidas = '';
  
  try {
    const respuesta = await apiFetch(API + '/api/obras');
    const obras = await respuesta.json();
    const obraActual = obras.find(o => o.id === ST.obraId) || null;
    
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
    const respuesta = await apiFetch(API + '/api/obras');
    const obras = await respuesta.json();
    const obraActual = obras.find(o => o.id === ST.obraId) || null;
    
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
    const respuesta = await apiFetch(API + '/api/obras');
    const obras = await respuesta.json();
    const obraActual = obras.find(o => o.id === ST.obraId) || null;
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
V['c-obra']=async()=>{
  let obra=null;
  try{
    const r=await apiFetch(`${API}/api/cliente/obras`);
    if(r.ok){const l=await r.json();obra=l[0]||null}
  }catch(e){}
  if(!obra) return `${navbar()}${crumbs([['Mi obra']])}
   <div class="page"><div class="phead"><div><h1>Mi obra</h1></div></div>
   ${ST.msg||''}
   <div class="msg warn">Aún no tiene una obra asignada. Si cree que es un error, comuníquese con M/M Contratistas.</div></div>`;
  const hitos=obra.hitos;
  return `${navbar()}${crumbs([['Mi obra']])}
 <div class="page"><div class="phead"><div><h1>${esc(obra.nombre)}</h1>
  <p>${esc(obra.cliente)}</p></div>
  <div style="display:flex;gap:7px;align-items:center"><span class="tg ${obra.estado==='Finalizado'?'mt':'ok'}">${obra.estado==='Finalizado'?'Finalizado':'En ejecución'}</span>${ST.hu?'<span class="hu">HU20</span>':''}</div></div>
  ${ST.msg||''}
 ${elevHitos(hitos)}
 <div class="card" style="margin-top:20px"><div class="ch"><h4>Etapas de la obra</h4></div>
  <table><thead><tr><th>Hito</th><th>Estado</th><th>Fecha planificada</th><th>Fecha real</th></tr></thead><tbody>
  ${hitos.map(h=>`<tr><td><b>${esc(h.nombre)}</b></td>
   <td>${h.estado==='Completado'?'<span class="tg ok">Completado</span>':h.estado==='En proceso'?'<span class="tg wn">En proceso</span>':'<span class="tg mt">Pendiente</span>'}</td>
   <td>${fmtFecha(h.fechaPlanificada)}</td><td>${fmtFecha(h.fechaReal)}</td></tr>`).join('')||'<tr><td colspan="4" style="text-align:center;color:var(--concrete)">El equipo aún no ha definido los hitos de su obra.</td></tr>'}
  </tbody></table></div>
 <div class="card" style="margin-top:20px"><div class="ch"><h4>Estructura · avance del 10/09/2026</h4>
   <button class="btn ghost sm" onclick="go('c-cmt')">Comentar</button></div>
  <div class="cb"><p style="margin-top:0">Vaciado de losa del cuarto nivel y desencofrado de columnas del tercero.</p>
   <div class="thumbs"><div class="th">Losa nivel 4</div><div class="th">Columnas</div><div class="th">Vista general</div></div></div></div>
 <div class="card" style="margin-top:20px"><div class="ch"><h4>Documentos de mi obra</h4></div>
  <table><thead><tr><th>Documento</th><th>Clasificación</th><th>Fecha de carga</th><th></th></tr></thead><tbody>
  ${(obra.documentos||[]).map(d=>`<tr><td><b>${esc(d.nombre)}</b></td><td><span class="tg mt">${esc(d.clasificacion)}</span></td><td>${esc(d.fechaCarga)}</td>
   <td style="width:110px"><button class="btn link" onclick="descargarArchivo('${API}/api/cliente/documentos/${d.id}/descargar')">Descargar</button></td></tr>`).join('')||'<tr><td colspan="4" style="text-align:center;color:var(--concrete)">El equipo aún no ha compartido documentos de su obra.</td></tr>'}
  </tbody></table></div>
 </div>`;
};

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