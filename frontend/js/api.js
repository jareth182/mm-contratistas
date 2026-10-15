/* ================= api y sesión ================= */
const API = 'http://localhost:5067';
const SESION_KEY = 'mym_sesion';

// Vistas que requieren sesión y roles que pueden verlas
const VISTAS_PERSONAL = ['tablero', 'proyectos', 'obra', 'personal', 'docpers', 'alertas'];
const VISTAS_SITIO = ['portafolio', 'servicios-adm'];
const VISTAS_CLIENTE = ['c-obra', 'c-gastos', 'c-cmt'];
const PERMISOS = {
  'Administrador': [...VISTAS_PERSONAL, ...VISTAS_SITIO, 'usuarios', 'perfil'],
  'Gerente de Operaciones': [...VISTAS_PERSONAL, ...VISTAS_SITIO, 'perfil'],
  'Supervisor de Obra': [...VISTAS_PERSONAL, 'perfil'],
  'Cliente': [...VISTAS_CLIENTE, 'perfil']
};
const VISTAS_PRIVADAS = Object.values(PERMISOS).flat();

function sesionLeer() {
  try { return JSON.parse(sessionStorage.getItem(SESION_KEY)); } catch (e) { return null; }
}
function sesionGuardar(datos) {
  try { sessionStorage.setItem(SESION_KEY, JSON.stringify(datos)); } catch (e) { /* sin almacenamiento */ }
}
function sesionBorrar() {
  try { sessionStorage.removeItem(SESION_KEY); } catch (e) { /* sin almacenamiento */ }
}
function tokenActual() {
  const s = sesionLeer();
  return s ? s.token : null;
}
function puedeVer(rol, vista) {
  return (PERMISOS[rol] || []).includes(vista);
}
function vistaInicio(rol) {
  return rol === 'Cliente' ? 'c-obra' : 'tablero';
}

// fetch que adjunta el token; si el servidor responde 401 con una sesión abierta, la sesión venció
async function apiFetch(url, opciones = {}) {
  const headers = new Headers(opciones.headers || {});
  const token = tokenActual();
  if (token) headers.set('Authorization', `Bearer ${token}`);
  const respuesta = await fetch(url, { ...opciones, headers });
  if (respuesta.status === 401 && token && typeof sesionExpirada === 'function') sesionExpirada();
  return respuesta;
}

// Descarga un archivo protegido (window.open no puede enviar el token)
async function descargarArchivo(url, nombreSugerido) {
  try {
    const respuesta = await apiFetch(url);
    if (!respuesta.ok) { say('bad', 'No se pudo descargar el archivo.'); return; }
    const blob = await respuesta.blob();
    const enlace = document.createElement('a');
    enlace.href = URL.createObjectURL(blob);
    const cabecera = respuesta.headers.get('Content-Disposition') || '';
    const coincidencia = /filename\*?=(?:UTF-8'')?"?([^";]+)"?/i.exec(cabecera);
    enlace.download = coincidencia ? decodeURIComponent(coincidencia[1]) : (nombreSugerido || 'archivo');
    document.body.appendChild(enlace);
    enlace.click();
    enlace.remove();
    URL.revokeObjectURL(enlace.href);
  } catch (e) {
    say('bad', 'Error de conexión con el servidor.');
  }
}

// Escapa texto que viene del servidor antes de insertarlo en el HTML
function esc(texto) {
  return String(texto ?? '').replace(/[&<>"']/g, c => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;' }[c]));
}

// 2026-12-01 -> 01/12/2026
function fmtFecha(iso) {
  const m = /^(\d{4})-(\d{2})-(\d{2})/.exec(iso || '');
  return m ? `${m[3]}/${m[2]}/${m[1]}` : '—';
}
