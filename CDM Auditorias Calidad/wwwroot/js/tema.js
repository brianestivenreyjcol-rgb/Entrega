// Tema claro / oscuro (guía de estilos, 2.3). Pone data-tema="claro|oscuro" en <html> y
// data-tema-preferido="auto|claro|oscuro" (lo que eligió la persona; se guarda en el
// navegador). Se carga en <head>, antes de la hoja de estilos, para que no parpadee. También recuerda si el panel de
// filtros está plegado (clave cdm-panel).
(() => {
  'use strict';

  const CLAVE = 'cdm-tema';
  const ORDEN = ['auto', 'claro', 'oscuro'];
  const NOMBRES = { auto: 'automático', claro: 'claro', oscuro: 'oscuro' };
  const sistemaOscuro = window.matchMedia('(prefers-color-scheme: dark)');

  function leer() {
    try {
      const v = localStorage.getItem(CLAVE);
      return ORDEN.includes(v) ? v : 'auto';
    } catch {
      return 'auto'; // Navegador sin almacenamiento: siempre automático.
    }
  }

  function guardar(valor) {
    try { localStorage.setItem(CLAVE, valor); } catch { /* sin almacenamiento */ }
  }

  function aplicar(preferido) {
    const raiz = document.documentElement;
    raiz.dataset.temaPreferido = preferido;
    raiz.dataset.tema = preferido === 'auto' ? (sistemaOscuro.matches ? 'oscuro' : 'claro') : preferido;

    const siguiente = ORDEN[(ORDEN.indexOf(preferido) + 1) % ORDEN.length];
    const texto = `Tema ${NOMBRES[preferido]}. Pulsa para pasar a ${NOMBRES[siguiente]}.`;
    document.querySelectorAll('[data-boton-tema]').forEach(b => {
      b.setAttribute('aria-label', texto);
      b.title = texto;
      const rotulo = b.querySelector('[data-tema-texto]');
      if (rotulo) rotulo.textContent = 'Tema: ' + NOMBRES[preferido];
    });
  }

  aplicar(leer());

  // Panel de filtros plegado a un raíl (lo pliega site.js): se recuerda y se aplica aquí para que no parpadee al cargar.
  try {
    if (localStorage.getItem('cdm-panel') === 'plegado') document.documentElement.dataset.panel = 'plegado';
  } catch { /* sin almacenamiento: siempre desplegado */ }

  sistemaOscuro.addEventListener('change', () => { if (leer() === 'auto') aplicar('auto'); });

  document.addEventListener('click', e => {
    if (!e.target.closest('[data-boton-tema]')) return;
    const siguiente = ORDEN[(ORDEN.indexOf(leer()) + 1) % ORDEN.length];
    guardar(siguiente);
    aplicar(siguiente);
  });

  // Los botones aún no existen al cargar el script: se rotulan cuando ya está la página.
  document.addEventListener('DOMContentLoaded', () => aplicar(leer()));
})();
