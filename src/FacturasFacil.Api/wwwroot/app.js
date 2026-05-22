/* ────────────────────────────────────────────────────────────
   FacturasFacil — app.js
   SPA ligera: routing, auth JWT, upload, historial, SAT, planes
   ──────────────────────────────────────────────────────────── */

const API = '';   // mismo origen; cambiar si el API está en otro servidor

/* ── Estado global ───────────────────────────────── */
let token    = localStorage.getItem('ff_token')    || null;
let userInfo = JSON.parse(localStorage.getItem('ff_user') || 'null');
let selectedFiles = [];

/* ── Routing ─────────────────────────────────────── */
function showPage(name) {
  document.querySelectorAll('.page').forEach(p => p.classList.add('hidden'));
  const page = document.getElementById(`page-${name}`);
  if (page) page.classList.remove('hidden');

  // Limpiar errores al cambiar página
  document.querySelectorAll('.alert-error, .alert-success')
    .forEach(el => el.classList.add('hidden'));

  if (name === 'dashboard') loadDashboard();
  if (name === 'historial')  loadHistorial();
  if (name === 'planes')     loadPlanes();
  window.scrollTo(0, 0);
}

function updateNav() {
  const loggedIn = !!token;
  setVis('navDashboard', loggedIn);
  setVis('navHistorial',  loggedIn);
  setVis('navValidar',    loggedIn);
  setVis('navLogout',     loggedIn);
  setVis('navLogin',      !loggedIn);
  setVis('navRegister',   !loggedIn);
}

function setVis(id, show) {
  const el = document.getElementById(id);
  if (!el) return;
  if (show) el.classList.remove('hidden');
  else      el.classList.add('hidden');
}

/* ── API helper ─────────────────────────────────── */
async function apiFetch(path, opts = {}) {
  if (token) {
    opts.headers = { ...(opts.headers || {}), Authorization: `Bearer ${token}` };
  }
  const res = await fetch(API + path, opts);
  if (res.status === 401) { logout(); return null; }
  return res;
}

// Parsea JSON de forma segura; si el servidor devuelve HTML (error 500)
// devuelve un objeto con el mensaje de error en lugar de lanzar excepción
async function safeJson(res) {
  const ct = res.headers.get('content-type') || '';
  if (ct.includes('application/json')) return res.json();
  const text = await res.text();
  try { return JSON.parse(text); }
  catch {
    return { error: `Error del servidor (${res.status})${text ? ': ' + text.substring(0, 300) : ''}` };
  }
}

/* ── AUTH ────────────────────────────────────────── */
async function login() {
  const email    = document.getElementById('loginEmail').value.trim();
  const password = document.getElementById('loginPassword').value;
  const errEl    = document.getElementById('loginError');
  errEl.classList.add('hidden');

  if (!email || !password) { showError(errEl, 'Completa todos los campos.'); return; }

  setLoading('btnLogin', true, 'Entrando...');
  try {
    const res  = await fetch(`${API}/api/auth/login`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ email, password })
    });
    if (res.status === 401) { showError(errEl, 'Correo o contraseña incorrectos.'); return; }
    const data = await safeJson(res);
    if (!res.ok) { showError(errEl, data.error || 'Error al iniciar sesión.'); return; }
    saveSession(data);
    showPage('dashboard');
  } catch (err) { showError(errEl, `Error de conexión: ${err.message || err}`); }
  finally  { setLoading('btnLogin', false, 'Entrar'); }
}

async function register() {
  const nombre   = document.getElementById('regNombre').value.trim();
  const empresa  = document.getElementById('regEmpresa').value.trim();
  const email    = document.getElementById('regEmail').value.trim();
  const password = document.getElementById('regPassword').value;
  const errEl    = document.getElementById('registerError');
  errEl.classList.add('hidden');

  if (!nombre || !email || !password)
    { showError(errEl, 'Nombre, email y contraseña son obligatorios.'); return; }
  if (password.length < 8)
    { showError(errEl, 'La contraseña debe tener al menos 8 caracteres.'); return; }

  setLoading('btnRegister', true, 'Creando cuenta...');
  try {
    const res  = await fetch(`${API}/api/auth/register`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ nombreCompleto: nombre, empresa: empresa || null, email, password })
    });
    const data = await safeJson(res);
    if (!res.ok) {
      const msg = data.errores ? data.errores.join(', ') : (data.error || 'Error al registrar.');
      showError(errEl, msg); return;
    }
    saveSession(data);
    showPage('dashboard');
  } catch (err) { showError(errEl, `Error de conexión: ${err.message || err}`); }
  finally  { setLoading('btnRegister', false, 'Crear cuenta gratis'); }
}

function logout() {
  token    = null; userInfo = null;
  localStorage.removeItem('ff_token');
  localStorage.removeItem('ff_user');
  selectedFiles = [];
  updateNav();
  showPage('home');
}

function saveSession(data) {
  token    = data.token;
  userInfo = data;
  localStorage.setItem('ff_token', token);
  localStorage.setItem('ff_user',  JSON.stringify(data));
  updateNav();
}

/* ── REFRESH USER ───────────────────────────────── */
async function refreshUserInfo() {
  const res = await apiFetch('/api/auth/me');
  if (!res || !res.ok) return;
  const data = await res.json();
  // Mezclar datos frescos en userInfo conservando el token
  userInfo = { ...userInfo, ...data, plan: data.plan, planId: data.planId, suscripcionVence: data.suscripcionVence };
  localStorage.setItem('ff_user', JSON.stringify(userInfo));
}

/* ── DASHBOARD ──────────────────────────────────── */
async function loadDashboard() {
  // Refrescar plan desde servidor (puede haber cambiado por pago)
  await refreshUserInfo();

  // Nombre y plan
  if (userInfo) {
    document.getElementById('welcomeNombre').textContent = `¡Hola, ${userInfo.nombreCompleto?.split(' ')[0]}!`;
    document.getElementById('welcomePlan').textContent   = userInfo.plan || 'Gratis';
  }

  // Uso actual
  const res = await apiFetch('/api/uso/actual');
  if (res && res.ok) {
    const uso  = await res.json();
    const pct  = uso.esIlimitado ? 0 : Math.min(uso.porcentajeUso, 100);
    document.getElementById('usoFacturas').textContent = uso.facturasUsadas;
    document.getElementById('usoLimite').textContent   = uso.esIlimitado ? '∞' : uso.limiteFacturasMes;
    const bar = document.getElementById('usoBarFill');
    bar.style.width = uso.esIlimitado ? '5%' : `${pct}%`;
    bar.classList.toggle('warning', pct >= 70 && pct < 90);
    bar.classList.toggle('danger',  pct >= 90);
  }

  // Historial rápido
  loadHistorialQuick();
}

async function loadHistorialQuick() {
  const container = document.getElementById('historialQuick');
  const res = await apiFetch('/api/facturas/historial?pagina=1&porPagina=3');
  if (!res || !res.ok) { container.innerHTML = '<p class="text-muted">Sin descargas aún.</p>'; return; }
  const data = await res.json();
  renderHistorialList(container, data, true);
}

/* ── HISTORIAL ───────────────────────────────────── */
async function loadHistorial() {
  const container = document.getElementById('historialTabla');
  container.innerHTML = '<p class="text-muted">Cargando...</p>';
  const res = await apiFetch('/api/facturas/historial?pagina=1&porPagina=50');
  if (!res || !res.ok) { container.innerHTML = '<p class="text-muted">Error al cargar.</p>'; return; }
  const data = await res.json();
  renderHistorialList(container, data, false);
}

function renderHistorialList(container, items, esQuick) {
  if (!items.length) { container.innerHTML = '<p class="text-muted">Aún no has generado ningún Excel.</p>'; return; }
  const rows = items.map(i => `
    <tr>
      <td>${formatFecha(i.fechaGeneracion)}</td>
      <td>${i.totalFacturas} facturas</td>
      <td>${i.totalArchivosSubidos} archivo${i.totalArchivosSubidos !== 1 ? 's' : ''}</td>
      <td>${formatBytes(i.tamanioBytes)}</td>
      <td><button class="hist-dl-btn" onclick="descargarHistorial(${i.id},'${i.nombreArchivo}')">⬇ Descargar</button></td>
    </tr>`).join('');

  container.innerHTML = `
    <table class="hist-table">
      <thead><tr><th>Fecha</th><th>Facturas</th><th>Archivos</th><th>Tamaño</th><th></th></tr></thead>
      <tbody>${rows}</tbody>
    </table>`;
}

async function descargarHistorial(id, nombre) {
  const res = await apiFetch(`/api/facturas/historial/${id}/descargar`);
  if (!res || !res.ok) { alert('El archivo ya no existe en el servidor.'); return; }
  const blob = await res.blob();
  triggerDownload(blob, nombre);
}

/* ── UPLOAD / PROCESAMIENTO ──────────────────────── */
function filesSelected(e)   { addFiles(Array.from(e.target.files)); }
function dragOver(e)        { e.preventDefault(); document.getElementById('dropZone').classList.add('active'); }
function dragLeave()        { document.getElementById('dropZone').classList.remove('active'); }
function dropFiles(e)       { e.preventDefault(); dragLeave(); addFiles(Array.from(e.dataTransfer.files)); }

function addFiles(newFiles) {
  const permitidos = newFiles.filter(f =>
    f.name.toLowerCase().endsWith('.zip') || f.name.toLowerCase().endsWith('.rar'));
  selectedFiles = [...selectedFiles, ...permitidos];
  renderFileList();
}

function removeFile(idx) {
  selectedFiles.splice(idx, 1);
  renderFileList();
}

function renderFileList() {
  const container = document.getElementById('fileList');
  const btn       = document.getElementById('btnProcesar');

  if (!selectedFiles.length) {
    container.classList.add('hidden');
    btn.classList.add('hidden');
    return;
  }

  container.classList.remove('hidden');
  btn.classList.remove('hidden');
  container.innerHTML = selectedFiles.map((f, i) => `
    <div class="file-item">
      <div class="file-item-name">📦 <span>${f.name}</span>
        <span class="text-muted">(${formatBytes(f.size)})</span>
      </div>
      <button class="file-remove" onclick="removeFile(${i})" title="Quitar">✕</button>
    </div>`).join('');
}

async function procesar() {
  if (!selectedFiles.length) return;

  const errEl     = document.getElementById('uploadError');
  const successEl = document.getElementById('uploadSuccess');
  const progress  = document.getElementById('uploadProgress');
  const fill      = document.getElementById('progressFill');
  const text      = document.getElementById('progressText');

  errEl.classList.add('hidden');
  successEl.classList.add('hidden');
  progress.classList.remove('hidden');
  document.getElementById('btnProcesar').disabled = true;

  // Animación de progreso simulada
  let pct = 0;
  const interval = setInterval(() => {
    pct = Math.min(pct + 4, 85);
    fill.style.width = `${pct}%`;
    text.textContent = pct < 30 ? 'Subiendo archivos...' : pct < 60 ? 'Procesando XMLs...' : 'Generando Excel...';
  }, 200);

  try {
    const form = new FormData();
    selectedFiles.forEach(f => form.append('files', f));

    const res = await apiFetch('/api/facturas/procesar', { method: 'POST', body: form });

    clearInterval(interval);
    fill.style.width = '100%';

    if (!res) return;

    if (res.status === 402) {
      const data = await safeJson(res);
      showError(errEl, `${data.error} <a href="#" onclick="showPage('planes')">Ver planes →</a>`);
      return;
    }

    if (!res.ok) {
      const data = await safeJson(res);
      showError(errEl, data.error || `Error del servidor (${res.status}). Revisa los logs.`);
      return;
    }

    const blob     = await res.blob();
    const dispHdr  = res.headers.get('content-disposition') || '';
    const match    = dispHdr.match(/filename="?([^"]+)"?/);
    const filename = match ? match[1] : `Facturas_${Date.now()}.xlsx`;

    triggerDownload(blob, filename);

    successEl.innerHTML = `✅ Excel generado con éxito: <strong>${filename}</strong>`;
    successEl.classList.remove('hidden');
    selectedFiles = [];
    renderFileList();

    // Refrescar dashboard
    setTimeout(loadDashboard, 800);

  } catch (err) {
    clearInterval(interval);
    showError(errEl, `Error de conexión: ${err.message || err}`);
  } finally {
    progress.classList.add('hidden');
    fill.style.width = '0%';
    document.getElementById('btnProcesar').disabled = false;
  }
}

/* ── VALIDACIÓN SAT ─────────────────────────────── */
async function validarSat() {
  const uuid     = document.getElementById('satUuid').value.trim();
  const emisor   = document.getElementById('satEmisor').value.trim().toUpperCase();
  const receptor = document.getElementById('satReceptor').value.trim().toUpperCase();
  const total    = document.getElementById('satTotal').value.trim();
  const resultEl = document.getElementById('satResult');

  if (!uuid || !emisor || !receptor || !total) {
    resultEl.innerHTML = '<div class="sat-error"><div class="sat-estado">⚠️ Completa todos los campos</div></div>';
    resultEl.classList.remove('hidden');
    return;
  }

  setLoading('btnValidar', true, 'Consultando SAT...');
  resultEl.classList.add('hidden');

  try {
    const res = await apiFetch('/api/facturas/sat/validar', {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ uuid, rfcEmisor: emisor, rfcReceptor: receptor, total })
    });
    if (!res) return;
    const data = await res.json();

    const esVigente   = data.estado === 'Vigente';
    const esCancelado = data.estado === 'Cancelado';
    const cls         = esVigente ? 'sat-vigente' : esCancelado ? 'sat-cancelado' : 'sat-error';
    const ico         = esVigente ? '✅' : esCancelado ? '❌' : '⚠️';

    resultEl.innerHTML = `
      <div class="${cls}">
        <div class="sat-estado">${ico} ${data.estado}</div>
        <div class="sat-detalle">Código: ${data.codigoEstatus}</div>
        ${data.esCancelable ? `<div class="sat-detalle">¿Cancelable?: ${data.esCancelable}</div>` : ''}
        ${data.estatusCancelacion ? `<div class="sat-detalle">Estatus cancelación: ${data.estatusCancelacion}</div>` : ''}
        <div class="sat-detalle" style="margin-top:.5rem;word-break:break-all">UUID: ${data.uuid}</div>
      </div>`;
    resultEl.classList.remove('hidden');
  } catch {
    resultEl.innerHTML = '<div class="sat-error"><div class="sat-estado">⚠️ Error de conexión</div></div>';
    resultEl.classList.remove('hidden');
  } finally {
    setLoading('btnValidar', false, 'Consultar SAT');
  }
}

/* ── PLANES ─────────────────────────────────────── */
async function loadPlanes() {
  const grid = document.getElementById('planesGrid');
  try {
    // Refrescar info del usuario para tener planId y suscripcionVence actualizados
    if (token) await refreshUserInfo();

    const res  = await fetch(`${API}/api/planes`);
    const data = await res.json();

    const planActualId = userInfo?.planId ?? 0;
    const vence = userInfo?.suscripcionVence ? new Date(userInfo.suscripcionVence) : null;
    const diasRestantes = vence ? Math.ceil((vence - new Date()) / (1000*60*60*24)) : null;
    const proxRenovar = diasRestantes !== null && diasRestantes <= 7;

    grid.innerHTML = data.map((p, i) => {
      const gratis   = p.precioMensual === 0;
      const popular  = i === 1;
      const esMiPlan = token && p.id === planActualId;
      const limite   = p.limiteFacturasMes === -1 ? 'Ilimitadas' : `${p.limiteFacturasMes.toLocaleString('es-MX')} facturas/mes`;
      const features = p.caracteristicas.map(c => `<li>${c}</li>`).join('');

      let btnLabel, btnAction, btnClass, badgeHtml = '';

      if (esMiPlan) {
        badgeHtml = '<div class="plan-badge-actual">✅ Tu plan actual</div>';
        if (vence) {
          const fechaStr = vence.toLocaleDateString('es-MX', {day:'2-digit',month:'short',year:'numeric'});
          badgeHtml += `<div class="plan-vence">Renueva el ${fechaStr}${proxRenovar ? ' ⚠️' : ''}</div>`;
        }
        if (proxRenovar || gratis) {
          btnLabel = 'Plan actual'; btnAction = ''; btnClass = 'btn-disabled';
        } else {
          btnLabel = '🔄 Renovar plan'; btnAction = `contratarPlan(${p.id})`; btnClass = 'btn-outline';
        }
      } else if (!token) {
        btnLabel = gratis ? 'Comenzar gratis' : 'Registrarse';
        btnAction = `showPage('register')`; btnClass = 'btn-primary';
      } else {
        btnLabel = gratis ? 'Bajar a Gratis' : 'Cambiar a este plan';
        btnAction = gratis ? '' : `contratarPlan(${p.id})`;
        btnClass  = gratis ? 'btn-disabled' : 'btn-primary';
      }

      return `
        <div class="plan-card ${popular ? 'popular' : ''} ${esMiPlan ? 'plan-activo' : ''}">
          ${badgeHtml}
          <div class="plan-nombre">${p.nombre}</div>
          <div class="plan-precio">
            ${gratis ? 'Gratis' : `$${p.precioMensual.toLocaleString('es-MX')}`}
            ${gratis ? '' : '<span>/mes MXN</span>'}
          </div>
          <div class="plan-limite">📊 ${limite}</div>
          <ul class="plan-features">${features}</ul>
          <button class="btn-primary plan-btn ${btnClass}"
            ${btnAction ? `onclick="${btnAction}"` : 'disabled'}>${btnLabel}</button>
        </div>`;
    }).join('');
  } catch {
    grid.innerHTML = '<p class="text-muted text-center">Error cargando planes.</p>';
  }
}

async function contratarPlan(planId) {
  try {
    const res  = await apiFetch('/api/pagos/checkout', {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ planId })
    });
    if (!res) return; // 401 → logout ya ejecutado
    const data = await safeJson(res);
    if (!res.ok) { alert(`⚠️ ${data.error || 'Error al iniciar el pago.'}`); return; }
    if (data.url) window.location.href = data.url;
    else alert('No se recibió URL de pago. Verifica la configuración de Stripe.');
  } catch (err) {
    alert(`Error de conexión: ${err.message || err}`);
  }
}

/* ── UTILIDADES ─────────────────────────────────── */
function triggerDownload(blob, filename) {
  const url = URL.createObjectURL(blob);
  const a   = document.createElement('a');
  a.href    = url; a.download = filename;
  document.body.appendChild(a); a.click();
  document.body.removeChild(a);
  URL.revokeObjectURL(url);
}

function formatFecha(iso) {
  return new Date(iso).toLocaleString('es-MX', {
    day: '2-digit', month: '2-digit', year: 'numeric',
    hour: '2-digit', minute: '2-digit'
  });
}

function formatBytes(bytes) {
  if (bytes < 1024)        return `${bytes} B`;
  if (bytes < 1024 * 1024) return `${(bytes / 1024).toFixed(1)} KB`;
  return `${(bytes / 1024 / 1024).toFixed(2)} MB`;
}

function showError(el, msg) {
  el.innerHTML = msg;
  el.classList.remove('hidden');
}

function setLoading(id, loading, text) {
  const btn = document.getElementById(id);
  if (!btn) return;
  btn.disabled    = loading;
  btn.textContent = text;
}

/* ── INIT ────────────────────────────────────────── */
updateNav();

// Detectar regreso desde Stripe (success_url trae ?session_id=...)
(async () => {
  const params = new URLSearchParams(window.location.search);
  if (params.has('session_id') && token) {
    // Limpiar la URL sin recargar la página
    window.history.replaceState({}, '', '/');
    // Refrescar datos del usuario desde el servidor (el webhook ya actualizó el plan)
    await refreshUserInfo();
    showPage('dashboard');
    // Mostrar banner de éxito
    const banner = document.createElement('div');
    banner.className = 'alert-success';
    banner.style.cssText = 'position:fixed;top:70px;left:50%;transform:translateX(-50%);z-index:999;padding:1rem 2rem;border-radius:8px;box-shadow:0 4px 12px rgba(0,0,0,.2)';
    banner.textContent = '🎉 ¡Pago exitoso! Tu plan ha sido actualizado.';
    document.body.appendChild(banner);
    setTimeout(() => banner.remove(), 5000);
  } else if (token) {
    showPage('dashboard');
  }
})();
