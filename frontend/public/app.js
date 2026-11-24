"use strict";

const { API_URL, FRONTEND_URL } = window.APP_CONFIG;
const API = API_URL.replace(/\/$/, "");

const PROVEEDORES = [
  { id: "google",  nombre: "Google" },
  { id: "discord", nombre: "Discord" },
  { id: "spotify", nombre: "Spotify" },
  { id: "twitch",  nombre: "Twitch" },
  { id: "dropbox", nombre: "Dropbox" },
  { id: "slack",   nombre: "Slack" },
  { id: "tiktok",  nombre: "TikTok" }
];

const $ = (id) => document.getElementById(id);

// Todas las peticiones llevan la cookie de sesión (credentials: include).
function api(ruta, opciones = {}) {
  return fetch(API + ruta, { credentials: "include", ...opciones });
}

function esc(valor) {
  const d = document.createElement("div");
  d.textContent = valor ?? "";
  return d.innerHTML;
}

function fecha(valor) {
  return valor ? new Date(valor).toLocaleString() : "—";
}

/* ---------------- Login ---------------- */

function pintarBotonesLogin() {
  const cont = $("botones-login");
  cont.innerHTML = "";
  for (const p of PROVEEDORES) {
    const a = document.createElement("a");
    a.className = "btn btn-prov";
    const logo = document.createElement("img");
    logo.src = `img/${p.id}.svg`;
    logo.alt = "";
    const txt = document.createElement("span");
    txt.textContent = `Continuar con ${p.nombre}`;
    a.append(logo, txt);
    // El backend completa el acceso con el proveedor y regresa a esta app.
    a.href = `${API}/login/${p.id}?returnUrl=${encodeURIComponent(FRONTEND_URL)}`;
    cont.appendChild(a);
  }
}

function mostrarLogin(mensaje) {
  $("vista-login").classList.remove("oculto");
  $("vista-app").classList.add("oculto");
  $("zona-usuario").classList.add("oculto");
  $("mensaje-login").textContent = mensaje || "";
}

function mostrarApp(usuario) {
  $("vista-login").classList.add("oculto");
  $("vista-app").classList.remove("oculto");
  $("zona-usuario").classList.remove("oculto");
  $("usuario-texto").textContent = `${usuario.nombre || usuario.email || "Usuario"} (${usuario.proveedor})`;

  const roles = usuario.roles.length ? usuario.roles.join(", ") : "Sin roles asignados";
  $("identidad").innerHTML = `
    <dt>Nombre</dt><dd>${esc(usuario.nombre) || "—"}</dd>
    <dt>Correo</dt><dd>${esc(usuario.email) || "—"}</dd>
    <dt>Proveedor</dt><dd>${esc(usuario.proveedor)}</dd>
    <dt>Id</dt><dd>${esc(usuario.id)}</dd>
    <dt>Roles</dt><dd>${esc(roles)}</dd>`;
}

async function cargarUsuario() {
  try {
    const r = await api("/auth/usuario");
    if (r.status === 401) return mostrarLogin();
    if (!r.ok) return mostrarLogin(`Error del backend (${r.status}).`);
    const u = await r.json();
    // El backend serializa en camelCase
    mostrarApp({
      id: u.id, nombre: u.nombre, email: u.email,
      proveedor: u.proveedor, roles: u.roles || []
    });
    cargarEstadoBackend();
    cargarCatalogo();
    iniciarMetricas();
    if (u.roles && u.roles.includes("Administrador")) cargarUsuarios();
    else $("tarjeta-usuarios").classList.add("oculto");
  } catch {
    mostrarLogin("No se pudo conectar con el backend. ¿Está levantado y la API_URL es correcta?");
  }
}

async function cerrarSesion() {
  detenerMetricas();
  await api("/logout", { method: "POST" });
  $("detalle-reporte").innerHTML = "";
  $("tarjeta-usuarios").classList.add("oculto");
  // Comprobación: sin sesión, un recurso protegido debe responder 401
  const r = await api("/api/reportes");
  mostrarLogin(r.status === 401
    ? "Sesión cerrada. El recurso protegido ahora responde 401 (sin acceso)."
    : "Sesión cerrada.");
}

/* ---------------- Backend / reportes ---------------- */

async function cargarEstadoBackend() {
  try {
    const r = await api("/health");
    const h = await r.json();
    $("estado-backend").innerHTML =
      `<span class="exito">● En línea</span> — instancia <b>${esc(h.instancia)}</b>`;
  } catch {
    $("estado-backend").innerHTML = '<span class="error">● Sin conexión</span>';
  }
}

async function cargarCatalogo() {
  const cont = $("lista-reportes");
  cont.innerHTML = "";
  const r = await api("/api/reportes");
  if (!r.ok) { cont.textContent = `No se pudo cargar el catálogo (${r.status}).`; return; }
  const reportes = await r.json();

  for (const rep of reportes) {
    const b = document.createElement("button");
    b.className = "btn reporte-btn" + (rep.permitido ? "" : " bloqueado");
    b.textContent = (rep.permitido ? "" : "🔒 ") + rep.titulo;
    // Se deja el botón activo a propósito: el backend decide (401/403).
    b.onclick = () => abrirReporte(rep);
    cont.appendChild(b);
  }
}

async function abrirReporte(rep) {
  const cont = $("detalle-reporte");
  cont.innerHTML = '<p class="aviso">Cargando…</p>';

  const meta = `<p class="meta"><b>Autor:</b> ${esc(rep.autor)} · <b>Tablas:</b> ${esc(rep.tablas.join(", "))}
    · <b>Endpoint:</b> ${esc(rep.endpoint)} · <b>Permisos:</b> ${esc(rep.permisos.join(" o "))}<br>${esc(rep.proposito)}</p>`;

  const r = await api(rep.endpoint);

  if (r.status === 401) { cont.innerHTML = meta + '<p class="error">401 — No hay sesión activa.</p>'; return; }
  if (r.status === 403) { cont.innerHTML = meta + '<p class="error">403 — Tu usuario no tiene los permisos requeridos para este reporte.</p>'; return; }
  if (!r.ok) { cont.innerHTML = meta + `<p class="error">Error ${r.status}.</p>`; return; }

  const datos = await r.json();
  cont.innerHTML = `<h3>${esc(datos.reporte)}</h3>` + meta + RENDER[rep.clave](datos);
}

/* ---------------- Render de cada reporte ---------------- */

function kpis(items) {
  return `<div class="kpis">${items.map(([v, t]) => `<div class="kpi"><b>${esc(v)}</b><span>${esc(t)}</span></div>`).join("")}</div>`;
}

function tabla(cabeceras, filas) {
  if (!filas.length) return '<p class="aviso">Sin datos.</p>';
  return `<div class="tabla-wrap"><table><thead><tr>${cabeceras.map(c => `<th>${esc(c)}</th>`).join("")}</tr></thead>
    <tbody>${filas.map(f => `<tr>${f.map(c => `<td>${esc(c)}</td>`).join("")}</tr>`).join("")}</tbody></table></div>`;
}

const conteo = (lista) => tabla(["Nombre", "Cantidad"], lista.map(x => [x.nombre, x.cantidad]));

const RENDER = {
  servidores: (d) =>
    kpis([[d.resumen.totalServidores, "Servidores"], [d.resumen.servidoresDisponibles, "Disponibles"],
          [d.resumen.servidoresCaidos, "Caídos"], [d.resumen.sinMonitoreo, "Sin monitoreo"]]) +
    tabla(["Servidor", "IP", "Tipo", "Sistema", "Monitoreos", "% Disp.", "Resp. prom. (ms)", "Último", "Estado"],
      d.servidores.map(s => [s.nombre, s.direccionIp, s.tipo,
        `${s.sistemaOperativo} ${s.versionSistema ?? ""}`.trim(), s.totalMonitoreos,
        s.porcentajeDisponibilidad ?? "—", s.tiempoRespuestaPromedioMs ?? "—",
        fecha(s.ultimoMonitoreo),
        s.ultimoEstadoDisponible === null ? "—" : (s.ultimoEstadoDisponible ? "Disponible" : "Caído")])),

  incidentes: (d) =>
    kpis([[d.totalIncidentes, "Incidentes"]]) +
    "<h3>Por estado</h3>" + conteo(d.porEstado) +
    "<h3>Por prioridad</h3>" + conteo(d.porPrioridad) +
    "<h3>Por tipo</h3>" + conteo(d.porTipo) +
    "<h3>Por servidor</h3>" + conteo(d.porServidor) +
    "<h3>Más recientes</h3>" +
    tabla(["Id", "Título", "Servidor", "Tipo", "Prioridad", "Estado", "Fecha"],
      d.recientes.map(i => [i.id, i.titulo, i.servidor, i.tipo, i.prioridad, i.estado, fecha(i.fechaRegistro)])),

  alertas: (d) =>
    kpis([[d.totalAlertas, "Alertas activas"], [d.notificaciones.total, "Notificaciones"],
          [d.notificaciones.enviadas, "Enviadas"], [d.notificaciones.pendientes, "Pendientes"]]) +
    "<h3>Alertas por tipo</h3>" + conteo(d.porTipo) +
    "<h3>Alertas por servidor</h3>" + conteo(d.porServidor) +
    "<h3>Notificaciones por canal</h3>" +
    tabla(["Canal", "Total", "Enviadas"], d.notificaciones.porCanal.map(c => [c.nombre, c.cantidad, c.enviadas])) +
    "<h3>Últimos movimientos</h3>" +
    tabla(["Alerta", "Servidor", "Acción", "Detalle", "Fecha"],
      d.ultimosMovimientos.map(m => [m.alerta, m.servidor, m.accion, m.detalle ?? "—", fecha(m.fechaRegistro)]))
};

/* ---------------- Métricas (Prometheus) ---------------- */

let temporizadorMetricas = null;

function detenerMetricas() {
  if (temporizadorMetricas) clearInterval(temporizadorMetricas);
  temporizadorMetricas = null;
}

function iniciarMetricas() {
  detenerMetricas();
  actualizarMetricas();
  temporizadorMetricas = setInterval(actualizarMetricas, 10000);
}

function bytes(n) {
  if (n == null) return "—";
  const u = ["B", "KB", "MB", "GB", "TB"];
  let i = 0;
  while (n >= 1024 && i < u.length - 1) { n /= 1024; i++; }
  return `${n.toFixed(1)} ${u[i]}`;
}

function duracion(seg) {
  if (seg == null) return "—";
  const d = Math.floor(seg / 86400), h = Math.floor(seg % 86400 / 3600), m = Math.floor(seg % 3600 / 60);
  return `${d}d ${h}h ${m}m`;
}

function medidor(titulo, pct, detalle) {
  const v = pct == null ? 0 : Math.min(100, pct);
  const clase = v >= 85 ? "alta" : v >= 65 ? "media" : "";
  return `<div class="medidor"><div class="titulo">${esc(titulo)}</div>
    <div class="valor">${pct == null ? "—" : pct + " %"}</div>
    <div class="barra-fondo"><div class="barra-valor ${clase}" style="width:${v}%"></div></div>
    <div class="detalle">${esc(detalle)}</div></div>`;
}

function tarjetaDato(titulo, valor, detalle) {
  return `<div class="medidor"><div class="titulo">${esc(titulo)}</div>
    <div class="valor">${valor}</div><div class="detalle">${esc(detalle || "")}</div></div>`;
}

function grafica(titulo, puntos) {
  if (!puntos || puntos.length < 2) {
    return `<div class="grafica"><div class="titulo aviso">${esc(titulo)}</div><p class="aviso">Reuniendo datos… (Prometheus necesita 1–2 minutos)</p></div>`;
  }
  const W = 300, H = 90;
  const xs = (i) => (i / (puntos.length - 1)) * W;
  const ys = (v) => H - (Math.min(100, Math.max(0, v)) / 100) * H;
  const linea = puntos.map((p, i) => `${xs(i).toFixed(1)},${ys(p.v).toFixed(1)}`).join(" ");
  const ultimo = puntos[puntos.length - 1].v;
  return `<div class="grafica"><div class="titulo aviso">${esc(titulo)} — ahora ${ultimo} %</div>
    <svg viewBox="0 0 ${W} ${H}" preserveAspectRatio="none">
      <line class="eje" x1="0" y1="${H / 2}" x2="${W}" y2="${H / 2}"/>
      <polygon class="area" points="0,${H} ${linea} ${W},${H}"/>
      <polyline class="linea" points="${linea}"/>
    </svg></div>`;
}

async function actualizarMetricas() {
  const cont = $("metricas");
  try {
    const r = await api("/api/metricas/servidor");

    if (r.status === 401) return detenerMetricas();
    if (r.status === 403) {
      detenerMetricas();
      cont.innerHTML = '<p class="error">403 — Tu usuario no tiene permiso para ver las métricas (requiere Administrador o AnalistaServidores).</p>';
      return;
    }
    if (r.status === 503) {
      cont.innerHTML = '<p class="error">Prometheus no está disponible.</p>';
      return;
    }
    if (!r.ok) { cont.innerHTML = `<p class="error">Error ${r.status}.</p>`; return; }

    const m = await r.json();
    const hr = await api("/api/metricas/historial?minutos=15");
    const h = hr.ok ? await hr.json() : { cpu: [], ram: [] };
    const or = await api("/api/metricas/objetivos");
    const objetivos = or.ok ? await or.json() : [];

    const estado = m.disponible === null ? "—"
      : m.disponible ? '<span class="exito">● En línea</span>' : '<span class="error">● Caído</span>';

    cont.innerHTML = `
      <div class="medidores">
        ${medidor("CPU", m.cpuPorcentaje, `${m.nucleosCpu ?? "?"} núcleos · carga 1m: ${m.cargaPromedio1m ?? "—"}`)}
        ${medidor("Memoria RAM", m.ramPorcentaje, `${bytes(m.ramUsadaBytes)} de ${bytes(m.ramTotalBytes)}`)}
        ${medidor("Disco (/)", m.discoPorcentaje, `${bytes(m.discoUsadoBytes)} de ${bytes(m.discoTotalBytes)}`)}
        ${tarjetaDato("Tiempo de actividad", duracion(m.uptimeSegundos), "desde el último arranque")}
        ${tarjetaDato("Disponibilidad", estado, "según Prometheus (up)")}
      </div>
      <div class="graficas">
        ${grafica("CPU (últimos 15 min)", h.cpu)}
        ${grafica("RAM (últimos 15 min)", h.ram)}
      </div>
      <div class="objetivos"><b>Objetivos de Prometheus:</b>
        ${objetivos.map(o => `${esc(o.job)} (${o.estado === "up" ? "✔" : "✖"})`).join(" · ") || "—"}
        · actualizado ${fecha(m.actualizadoUtc)}</div>`;
  } catch {
    cont.innerHTML = '<p class="error">No se pudo consultar las métricas.</p>';
  }
}

/* ---------------- Administración de usuarios y roles ---------------- */

async function cargarUsuarios() {
  const tarjeta = $("tarjeta-usuarios");
  tarjeta.classList.remove("oculto");

  const [ru, rr] = await Promise.all([api("/api/usuarios"), api("/api/usuarios/roles")]);
  if (!ru.ok || !rr.ok) { $("tabla-usuarios").textContent = `No se pudo cargar (${ru.status}).`; return; }

  const usuarios = await ru.json();
  const roles = await rr.json();

  $("tabla-usuarios").innerHTML = `<table><thead><tr><th>Usuario</th><th>Proveedor</th><th>Último acceso</th><th>Activo</th>
    <th>Roles</th></tr></thead><tbody>${usuarios.map(u => `
      <tr data-id="${u.id}">
        <td>${esc(u.nombre || u.email || "(sin nombre)")}<br><span class="aviso">${esc(u.proveedorId)}</span></td>
        <td>${esc(u.proveedor)}</td>
        <td>${fecha(u.ultimoAcceso)}</td>
        <td><input type="checkbox" class="chk-activo" ${u.activo ? "checked" : ""}></td>
        <td>${roles.map(r => `<label title="${esc(r.descripcion)}"><input type="checkbox" class="chk-rol" value="${esc(r.nombre)}"
              ${u.roles.includes(r.nombre) ? "checked" : ""}> ${esc(r.nombre)}</label>`).join("")}</td>
      </tr>`).join("")}</tbody></table>`;

  $("tabla-usuarios").querySelectorAll("tr[data-id]").forEach(fila => {
    const id = fila.dataset.id;

    fila.querySelectorAll(".chk-rol").forEach(chk => chk.addEventListener("change", async () => {
      const seleccion = [...fila.querySelectorAll(".chk-rol:checked")].map(c => c.value);
      const r = await api(`/api/usuarios/${id}/roles`, {
        method: "PUT", headers: { "Content-Type": "application/json" },
        body: JSON.stringify({ roles: seleccion })
      });
      await resultadoUsuarios(r, "Roles actualizados.");
    }));

    fila.querySelector(".chk-activo").addEventListener("change", async (e) => {
      const r = await api(`/api/usuarios/${id}/activo`, {
        method: "PUT", headers: { "Content-Type": "application/json" },
        body: JSON.stringify({ activo: e.target.checked })
      });
      await resultadoUsuarios(r, "Estado actualizado.");
    });
  });
}

async function resultadoUsuarios(r, textoOk) {
  const msg = $("mensaje-usuarios");
  if (r.ok) { msg.className = "exito"; msg.textContent = textoOk; return; }
  let texto = `Error ${r.status}.`;
  try { texto = (await r.json()).mensaje || texto; } catch {}
  msg.className = "error";
  msg.textContent = texto;
  cargarUsuarios();   // vuelve al estado real guardado
}

/* ---------------- Inicio ---------------- */

$("btn-logout").addEventListener("click", cerrarSesion);
pintarBotonesLogin();
cargarUsuario();
