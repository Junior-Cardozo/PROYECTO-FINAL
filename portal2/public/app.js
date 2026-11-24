"use strict";

const { API_URL, FRONTEND_URL, APP1_URL } = window.APP_CONFIG;
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

// Misma cookie de sesión que la aplicación principal (credentials: include).
function api(ruta, opciones = {}) {
  return fetch(API + ruta, { credentials: "include", ...opciones });
}

function vista(nombre) {
  for (const id of ["vista-cargando", "vista-login", "vista-app"])
    $(id).classList.toggle("oculto", id !== nombre);
  $("zona-usuario").classList.toggle("oculto", nombre !== "vista-app");
}

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
    a.href = `${API}/login/${p.id}?returnUrl=${encodeURIComponent(FRONTEND_URL)}`;
    cont.appendChild(a);
  }
}

function fila(dl, etiqueta, valor) {
  const dt = document.createElement("dt");
  dt.textContent = etiqueta;
  const dd = document.createElement("dd");
  dd.textContent = valor;
  dl.append(dt, dd);
}

function mostrarSesion(u) {
  $("usuario-texto").textContent = `${u.nombre || u.email || "Usuario"} (${u.proveedor})`;
  const dl = $("identidad");
  dl.innerHTML = "";
  fila(dl, "Nombre", u.nombre || "—");
  fila(dl, "Proveedor", u.proveedor);
  fila(dl, "Id", u.id);
  fila(dl, "Correo", u.email || "—");
  fila(dl, "Roles", u.roles && u.roles.length ? u.roles.join(", ") : "Sin roles asignados");
  $("enlace-app1").href = APP1_URL;
  vista("vista-app");
}

async function iniciar() {
  pintarBotonesLogin();
  try {
    const r = await api("/auth/usuario");
    if (r.ok) {
      mostrarSesion(await r.json());
    } else {
      vista("vista-login");
    }
  } catch {
    vista("vista-login");
    $("mensaje-login").textContent =
      "No se pudo conectar con el backend. ¿Está levantado y la API_URL es correcta?";
  }
}

$("btn-logout").addEventListener("click", async () => {
  await api("/logout", { method: "POST" });
  vista("vista-login");
});

iniciar();
