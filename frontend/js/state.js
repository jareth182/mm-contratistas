/* ================= datos ================= */
const OBRA={nombre:'Edificio multifamiliar Los Cedros',cliente:'Inversiones Del Río S.A.C.',
 ubic:'Santiago de Surco, Lima',ini:'02/03/2026',fin:'28/11/2026',av:47,
 hitos:[{n:'Excavación',p:10,e:'Completado',pl:'20/03/2026',rl:'18/03/2026',pc:100},
  {n:'Cimentación',p:20,e:'Completado',pl:'05/05/2026',rl:'09/05/2026',pc:100},
  {n:'Estructura',p:30,e:'En proceso',pl:'15/08/2026',rl:'—',pc:55},
  {n:'Albañilería',p:20,e:'Pendiente',pl:'30/09/2026',rl:'—',pc:0},
  {n:'Acabados',p:15,e:'Pendiente',pl:'10/11/2026',rl:'—',pc:0},
  {n:'Entrega',p:5,e:'Pendiente',pl:'28/11/2026',rl:'—',pc:0}],
 part:[{n:'Movimiento de tierras',pr:48000,gs:46200},{n:'Concreto y acero',pr:186000,gs:171400},
  {n:'Mano de obra',pr:142000,gs:98750},{n:'Encofrado',pr:54000,gs:52600},
  {n:'Instalaciones',pr:76000,gs:12300},{n:'Acabados',pr:94000,gs:0}],
 gastos:[{f:'22/08/2026',c:'Cemento Sol tipo I — 180 bolsas',p:'Distribuidora Andina',pa:'Concreto y acero',m:5940,e:'Registrado'},
  {f:'19/08/2026',c:'Alquiler de encofrado metálico',p:'Encofrados Perú',pa:'Encofrado',m:8200,e:'Registrado'},
  {f:'14/08/2026',c:'Fierro corrugado 1/2" — 4 t',p:'Aceros Lima',pa:'Concreto y acero',m:14800,e:'Registrado'},
  {f:'11/08/2026',c:'Flete de material',p:'Transportes Quispe',pa:'Concreto y acero',m:1200,e:'Anulado'},
  {f:'07/08/2026',c:'Arena gruesa — 12 m³',p:'Cantera Jicamarca',pa:'Movimiento de tierras',m:2640,e:'Registrado'}],
 pagos:[{c:'Rómulo Ccahuana Ayala',pe:'11/08 – 16/08',m:1080,f:'17/08/2026'},
  {c:'Elmer Huatuco Rojas',pe:'11/08 – 16/08',m:960,f:'17/08/2026'},
  {c:'Julio Ramos Bellido',pe:'11/08 – 16/08',m:840,f:'17/08/2026'},
  {c:'Rómulo Ccahuana Ayala',pe:'04/08 – 09/08',m:1080,f:'10/08/2026'}],
 cuad:[{n:'Rómulo Ccahuana Ayala',e:'Maestro de Obra',j:180,i:'02/03/2026',d:'Vigente'},
  {n:'Elmer Huatuco Rojas',e:'Operario',j:160,i:'09/03/2026',d:'Vigente'},
  {n:'Julio Ramos Bellido',e:'Oficial',j:140,i:'09/03/2026',d:'Por vencer'},
  {n:'Wilmer Tapia Sulca',e:'Ayudante',j:110,i:'06/04/2026',d:'Vigente'}],
 docs:[{n:'Contrato de obra firmado',c:'Contrato',f:'28/02/2026',v:1},
  {n:'Licencia de edificación',c:'Permiso',f:'05/03/2026',v:1},
  {n:'Planos estructurales',c:'Plano',f:'05/03/2026',v:1},
  {n:'Cotización de acero',c:'Otro',f:'12/08/2026',v:0}]};
const PERS=[{n:'Rómulo Ccahuana Ayala',d:'41258963',e:'Maestro de Obra',j:180,s:'Activo',dc:'Vigente'},
 {n:'Elmer Huatuco Rojas',d:'44870125',e:'Operario',j:160,s:'Activo',dc:'Vigente'},
 {n:'Julio Ramos Bellido',d:'40219974',e:'Oficial',j:140,s:'Activo',dc:'Por vencer'},
 {n:'Wilmer Tapia Sulca',d:'47336208',e:'Ayudante',j:110,s:'Activo',dc:'Vigente'},
 {n:'Saúl Quinto Palomino',d:'09884471',e:'Operario',j:160,s:'Activo',dc:'Vencido'},
 {n:'Nilo Achahui Mamani',d:'46012558',e:'Peón',j:95,s:'Inactivo',dc:'Por vencer'}];
const PROY=[{n:'Edificio multifamiliar Los Cedros',c:'Inversiones Del Río S.A.C.',e:'En Ejecución',a:47},
 {n:'Remodelación oficinas Miraflores',c:'Estudio Vargas & Asoc.',e:'En Ejecución',a:78},
 {n:'Ampliación almacén Lurín',c:'Comercial Pacífico E.I.R.L.',e:'En Ejecución',a:12},
 {n:'Casa de playa Asia',c:'Familia Benavides',e:'Finalizado',a:100}];

/* ================= estado ================= */
let ST={v:'home',tab:'hitos',perfil:'pub',hu:false,menu:null,nav:false,msg:null};
const S=n=>'S/ '+n.toLocaleString('es-PE',{minimumFractionDigits:2,maximumFractionDigits:2});
const dt=d=>d==='Vigente'?'<span class="tg ok">Vigente</span>':d==='Por vencer'?'<span class="tg wn">Por vencer</span>':'<span class="tg bd">Vencido</span>';
const cota=(t,hu,g='h2')=>`<div class="cota"><span class="t"></span><span class="b"></span><${g}>${t}</${g}><span class="r"></span>${hu&&ST.hu?`<span class="hu">${hu}</span>`:''}</div>`;
const ALERTAS=[['Saúl Quinto Palomino','SCTR vencido hace 23 días','bd'],
 ['Julio Ramos Bellido','SCTR vence en 14 días','wn'],
 ['Nilo Achahui Mamani','Examen médico vence en 22 días','wn']];