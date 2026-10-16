/* ================= navbar ================= */
function navbar(){
  const P=ST.perfil;
  let links='',user='',search='';
  if(P==='pub'){
    links=[['home','Proyectos'],['servicios','Servicios'],['nosotros','Nosotros']]
      .map(([v,t])=>`<button class="${ST.v===v?'on':''}" onclick="go('${v}')">${t}</button>`).join('')
      // En pantallas angostas la cotización pasa al menú desplegable
      +`<button class="solo-movil ${ST.v==='cotizar'?'on':''}" onclick="cotizarServicio(null)">Solicitar cotización</button>`;
    user=`<button class="btn warn sm nav-cot" style="margin-right:8px" onclick="cotizarServicio(null)">Solicitar cotización</button>
      <button class="btn ghost sm" style="color:#fff;border-color:#4A6072" onclick="go('login')">Ingresar</button>`;
  }else if(P==='adm'){
    const on=v=>ST.v===v?'on':'';
    const rol=ST.usuario.rol;
    const ve=v=>puedeVer(rol,v);
    links=`<button class="${on('tablero')}" onclick="go('tablero')">Tablero</button>
      <button class="${on('proyectos')||(ST.v==='obra'?'on':'')}" onclick="go('proyectos')">Obras</button>
      <button class="${on('personal')||(ST.v==='docpers'?'on':'')}" onclick="go('personal')">Personal</button>
      ${ve('portafolio')?`<span class="ddwrap"><button class="${on('portafolio')||on('servicios-adm')}" onclick="tog('cont',event)">Sitio web ▾</button>
        ${ST.menu==='cont'?`<div class="dd left">
          <button class="item" onclick="go('portafolio')">Portafolio<span class="sub">Obras que se muestran al público</span></button>
          <button class="item" onclick="go('servicios-adm')">Servicios<span class="sub">Catálogo publicado en el sitio</span></button></div>`:''}</span>`:''}
      ${ve('usuarios')?`<button class="${on('usuarios')}" onclick="go('usuarios')">Usuarios</button>`:''}`;
    search=`<div class="navsearch"><span style="color:#6E8496">⌕</span><input placeholder="Buscar obra o colaborador"></div>`;
    user=userMenu(iniciales(ST.usuario.nombreCompleto),ST.usuario.nombreCompleto,rol,true);
  }else{
    const on=v=>ST.v===v?'on':'';
    links=`<button class="${on('c-obra')}" onclick="go('c-obra')">Mi obra</button>
      <button class="${on('c-gastos')}" onclick="go('c-gastos')">Gastos</button>
      <button class="${on('c-cmt')}" onclick="go('c-cmt')">Comentarios</button>`;
    user=userMenu(iniciales(ST.usuario.nombreCompleto),ST.usuario.nombreCompleto,ST.usuario.rol,false);
  }
  return `<header class="nav"><div class="nav-in">
    <a class="brand" onclick="go('${P==='pub'?'home':vistaInicio(ST.usuario.rol)}')" style="cursor:pointer">
      <span class="mk"></span><span><b>M/M Contratistas</b><small>GESTIÓN DE OBRA</small></span></a>
    <button class="iconbtn burger" onclick="ST.nav=!ST.nav;draw()">☰</button>
    <nav class="nav-links ${ST.nav?'':'closed'}" id="nl">${links}</nav>
    <div class="nav-right">${search}${user}</div>
  </div></header>`;
}

function iniciales(nombre){
  const p=(nombre||'').trim().split(/\s+/).filter(Boolean);
  return ((p[0]||'')[0]||'').toUpperCase()+((p[1]||'')[0]||'').toUpperCase();
}

function userMenu(ini,nom,rol,alert){
  return `${alert?`<span class="ddwrap"><button class="iconbtn" onclick="tog('alr',event)" title="Avisos">🔔<span class="badge">3</span></button>
    ${ST.menu==='alr'?`<div class="dd" style="min-width:310px">
      <div class="dh"><span>Documentación del personal</span><b style="color:var(--rust)">3</b></div>
      ${ALERTAS.map(a=>`<button class="item" onclick="go('alertas')">${a[0]}<span class="sub"><span class="tg ${a[2]}" style="font-size:11.5px">${a[1]}</span></span></button>`).join('')}
      <button class="item" onclick="go('alertas')" style="text-align:center;font-family:var(--h)">Ver todas las alertas</button></div>`:''}</span>`:''}
    <span class="ddwrap"><button class="userbtn" onclick="tog('usr',event)">
      <span class="av">${ini}</span><span class="un"><b>${nom}</b><span>${rol}</span></span><span style="color:#8FA3B2">▾</span></button>
      ${ST.menu==='usr'?`<div class="dd">
        <div class="dh"><span>Sesión activa</span></div>
        <button class="item" onclick="go('perfil')">Mi perfil<span class="sub">${nom} · ${rol}</span></button>
        <button class="item" onclick="ST.hu=!ST.hu;ST.menu=null;draw()">${ST.hu?'Ocultar':'Mostrar'} códigos de historia<span class="sub">Trazabilidad con el backlog</span></button>
        <button class="item" onclick="cerrarSesion()" style="color:var(--rust)">Cerrar sesión</button>
      </div>`:''}</span>`;
}

function crumbs(arr){
  return `<div class="crumb"><div class="crumb-in">${arr.map((c,i)=>
    i===arr.length-1?`<b>${c[0]}</b>`:`<button onclick="go('${c[1]}')">${c[0]}</button><span>›</span>`).join('')}</div></div>`;
}

function pie(){
  return `<footer class="foot"><div class="foot-in">
    <div><h4>M/M Contratistas</h4>
      <p style="margin:0;max-width:40ch">Construcción de edificios, remodelación y obras civiles en Lima. Cada obra se entrega con seguimiento en línea para el cliente.</p></div>
    <div><h4>Servicios</h4>${(ST.servicios||[]).map(s=>`<a onclick="verServicioPublico(${s.id})">${esc(s.nombre)}</a>`).join('')}</div>
    <div><h4>Contacto</h4><a>+51 947 258 010</a><a>zapatacontratista@hotmail.com</a><a onclick="cotizarServicio(null)">Solicitar cotización</a><a onclick="go('login')">Acceso de clientes</a></div>
  </div><div class="bot"><span>© 2026 M/M Contratistas S.A.C.</span><span>Prototipo académico · Grupo 6 · USMP</span></div></footer>`;
}

/* ================= elevación ================= */
function elev(){
  const dn=OBRA.hitos.filter(h=>h.pc===100).length,tt=OBRA.hitos.length;
  const f=((dn+(OBRA.hitos[dn]?OBRA.hitos[dn].pc/100:0))/tt)*100;
  return `<div class="elev"><div class="tr">
    <div class="pc">${OBRA.av}% ejecutado · ${dn} de ${tt} hitos cerrados</div>
    <div class="gr"></div><div class="fl" style="width:${f.toFixed(1)}%"></div>
    <div class="cols">${OBRA.hitos.map(h=>`<div class="col ${h.pc===100?'dn':(h.pc>0?'nw':'')}">
      <div class="mk2"></div><div class="nm">${h.n}</div><div class="mt2">${h.pc}% · peso ${h.p}%</div></div>`).join('')}</div>
    <div class="cap"><span>Inicio ${OBRA.ini}</span><span>Fin estimado ${OBRA.fin}</span></div>
  </div></div>`;
}

// Línea de tiempo de hitos con datos del servidor (avance ponderado por el peso de cada hito)
function elevHitos(hitos) {
  const tt = hitos.length;
  if (!tt) return '';
  const dn = hitos.filter(h => h.avancePorcentaje === 100).length;
  const f = ((dn + (hitos[dn] ? hitos[dn].avancePorcentaje / 100 : 0)) / tt) * 100;
  const global = Math.round(hitos.reduce((a, h) => a + (h.avancePorcentaje * h.peso) / 100, 0));
  return `<div class="elev"><div class="tr">
    <div class="pc">${global}% ejecutado · ${dn} de ${tt} hitos cerrados</div>
    <div class="gr"></div><div class="fl" style="width:${f.toFixed(1)}%"></div>
    <div class="cols">${hitos.map(h => `<div class="col ${h.avancePorcentaje === 100 ? 'dn' : (h.avancePorcentaje > 0 ? 'nw' : '')}">
      <div class="mk2"></div><div class="nm">${esc(h.nombre)}</div><div class="mt2">${h.avancePorcentaje}% · peso ${h.peso}%</div></div>`).join('')}</div>
    <div class="cap"><span>Inicio del proyecto</span><span>Entrega</span></div>
  </div></div>`;
}
