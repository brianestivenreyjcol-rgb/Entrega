// CDM Auditorías Calidad — comportamiento de las páginas.
//
// Sin JavaScript todo funciona con enlaces y un formulario GET. Con él:
// - Filtros: cada casilla de un desplegable filtra al marcarla (el desplegable sigue abierto
//   para marcar más) y cada fecha al cambiarla. Los enlaces con data-parcial (Día/Semana/Mes,
//   clic en un sector o un auditor, borrar filtros) tampoco recargan: se pide la página con la
//   cabecera X-Parcial y se sustituye #informe.
// - Fichas: cada <title> de las gráficas («Etiqueta · Serie valor · …») y el title de las
//   piezas marcadas se convierte en una ficha al pasar el ratón, con guía vertical.
// - Tablas table.ranking[data-mapa]: se ordenan al pulsar la cabecera y se colorean con el
//   semáforo según th[data-sentido] (1 más es mejor, -1 más es peor, 0 sin color).
// - Cifras de la tira de indicadores: cuentan desde cero al cargar la página y, al filtrar,
//   pasan del valor anterior al nuevo (las animaciones de entrada solo se ven al cargar).
// - GAIA Formación (/gaia): el color de la página sigue a la marca filtrada (#informe[data-marca-pagina] pasa a <html data-marca>
//   en las recargas parciales) y las gráficas con muchos días llevan un selector Semana / Día ([data-vista-boton]).
// - CDM No solución (/nosolucion) usa todo lo anterior y además: el buscador de Equipos
//   (form[data-parcial]), «Copiar ID» de las llamadas de ejemplo ([data-copiar]) y, mientras se
//   traen los datos de BigQuery, la recarga sola de la página ([data-recargar-en]).
// - Cabecera del portal: Imprimir (window.print), Presentar (pantalla completa del contenido, sin panel) y, en el panel de filtros,
//   plegar a un raíl vertical (el estado se recuerda en localStorage, con try/catch). La miga de la página actual
//   (#informe[data-miga]) se actualiza al pasar de pestaña sin recargar.
// - Indicador + volumen (_IndicadorVolumenGaia): las pestañas de indicador ([data-indicador-boton]) y el selector Semana / Día
//   ([data-ind-vista]) enseñan el bloque [data-indicador][data-vista] elegido; todas vienen pintadas del servidor.
// - Ranking tipológico: pulsar una fila con [data-hijos] despliega sus filas hijas (tr[data-padre]).
// Con «reducir movimiento» no se anima nada.
(() => {
  'use strict';

  const sinMovimiento = window.matchMedia('(prefers-reduced-motion: reduce)');
  const raiz = document.documentElement;

  // ---------------------------------------------------------------------------------------
  // Carga parcial
  // ---------------------------------------------------------------------------------------

  let peticion = null;

  /**
   * Pide la página con X-Parcial y sustituye #informe.
   * @param {string} url
   * @param {{ empujar?: boolean, reabrir?: { campo: string, busqueda: string, scroll: number, foco: string | null } | null }} [opciones]
   *   reabrir: el desplegable que estaba abierto al marcar una casilla, para dejarlo igual.
   */
  async function cargar(url, { empujar = true, reabrir = null } = {}) {
    const informe = document.getElementById('informe');
    if (!informe) { location.href = url; return; }

    peticion?.abort();
    peticion = new AbortController();
    raiz.classList.add('cargando');
    const masFiltrosAbierto = informe.querySelector('.mas-filtros')?.open;
    const cifrasAntes = [...informe.querySelectorAll('.resumen-cifra')].map(el => Number(el.dataset.contar));

    try {
      const r = await fetch(url, { headers: { 'X-Parcial': '1' }, signal: peticion.signal });
      if (!r.ok) throw new Error('HTTP ' + r.status);
      const molde = document.createElement('template');
      molde.innerHTML = await r.text();
      const nuevo = molde.content.querySelector('#informe');
      if (!nuevo) throw new Error('Respuesta sin informe');

      // Al filtrar no se repiten las animaciones de entrada.
      nuevo.classList.add('actualizado');
      const mas = nuevo.querySelector('.mas-filtros');
      if (mas && masFiltrosAbierto) mas.open = true;

      ocultarFicha();
      informe.replaceWith(nuevo);
      // El color de la página sigue a la marca filtrada (GAIA): el servidor la deja en #informe y aquí pasa a <html>.
      const marca = nuevo.dataset.marcaPagina;
      if (marca) raiz.dataset.marca = marca;
      const miga = nuevo.dataset.miga;
      const migaActual = document.querySelector('.miga-actual');
      if (miga && migaActual) migaActual.textContent = miga;
      preparar(nuevo, cifrasAntes);
      if (reabrir) reabrirDesplegable(nuevo, reabrir);
      if (nuevo.dataset.titulo) document.title = nuevo.dataset.titulo;
      if (empujar) history.pushState({ informe: true }, '', url);
    } catch (e) {
      if (e.name === 'AbortError') return;
      location.href = url; // Si falla, recarga normal.
      return;
    }
    raiz.classList.remove('cargando');
  }

  function urlDe(form) {
    // Las fechas que coinciden con el borde del calendario no se envían: así la URL sigue
    // sirviendo cuando lleguen datos nuevos (el rango se amplía solo, como en el PBI).
    // En No solución el rango por defecto no es el calendario entero sino los últimos 30 días
    // (data-defecto), y la fecha inicial depende de la final: con data-fechas-juntas solo se
    // quitan si las dos son las de por defecto.
    const desde = form.querySelector('input[name=desde]');
    const hasta = form.querySelector('input[name=hasta]');
    const bordes = {
      desde: desde?.dataset.defecto ?? desde?.min,
      hasta: hasta?.dataset.defecto ?? hasta?.max,
    };
    if (form.hasAttribute('data-fechas-juntas') && !(desde?.value === bordes.desde && hasta?.value === bordes.hasta)) {
      bordes.desde = bordes.hasta = undefined;
    }
    const p = new URLSearchParams();
    for (const [clave, valor] of new FormData(form)) {
      if (valor === '' || (bordes[clave] !== undefined && valor === bordes[clave])) continue;
      p.append(clave, valor);
    }
    const q = p.toString();
    return form.getAttribute('action') + (q ? '?' + q : '');
  }

  /** Envía los filtros. Con «desplegable», ese se vuelve a abrir tal cual tras la carga. */
  function enviar(form, desplegable = null) {
    let reabrir = null;
    if (desplegable) {
      const activo = document.activeElement;
      reabrir = {
        campo: desplegable.dataset.campo,
        busqueda: desplegable.querySelector('.buscar-opcion')?.value ?? '',
        scroll: desplegable.querySelector('.opciones')?.scrollTop ?? 0,
        foco: activo instanceof HTMLInputElement && activo.type === 'checkbox' && desplegable.contains(activo) ? activo.value : null,
      };
    }
    form.querySelectorAll('details.multi[open]').forEach(d => { if (d !== desplegable) d.open = false; });
    cargar(urlDe(form), { reabrir });
  }

  let reabriendo = false;

  function reabrirDesplegable(contenedor, { campo, busqueda, scroll, foco }) {
    const d = contenedor.querySelector(`details.multi[data-campo="${CSS.escape(campo)}"]`);
    if (!d) return;
    // Si estaba dentro de «Más filtros», que se vea.
    const mas = d.closest('.mas-filtros');
    if (mas) mas.open = true;
    reabriendo = true;
    d.open = true;
    const buscar = d.querySelector('.buscar-opcion');
    if (buscar && busqueda) {
      buscar.value = busqueda;
      filtrarOpciones(buscar);
    }
    colocarDesplegable(d);
    const opciones = d.querySelector('.opciones');
    if (opciones) opciones.scrollTop = scroll;
    const casilla = foco === null ? null : [...d.querySelectorAll('input[type=checkbox]')].find(c => c.value === foco);
    (casilla ?? buscar ?? d.querySelector('summary'))?.focus({ preventScroll: true });
  }

  /** Abre el desplegable hacia arriba si no cabe debajo, y ajusta el alto de la lista. */
  function colocarDesplegable(d) {
    d.classList.remove('hacia-arriba');
    const panel = d.querySelector('.desplegable');
    const opciones = d.querySelector('.opciones');
    if (!panel || !opciones) return;
    opciones.style.maxHeight = '';
    const caja = d.querySelector('summary').getBoundingClientRect();
    const alto = panel.getBoundingClientRect().height;
    const cabecera = document.querySelector('.cabecera')?.getBoundingClientRect().bottom ?? 0;
    const abajo = window.innerHeight - caja.bottom - 12;
    const arriba = caja.top - cabecera - 12;
    if (alto > abajo && arriba > abajo) d.classList.add('hacia-arriba');
    const disponible = Math.max(abajo, arriba);
    if (alto > disponible) {
      const resto = alto - opciones.getBoundingClientRect().height;
      opciones.style.maxHeight = Math.max(120, disponible - resto) + 'px';
    }
  }

  const sinTildes = s => s.normalize('NFD').replace(/[̀-ͯ]/g, '').toLowerCase().trim();

  /** Filtra las opciones del desplegable mientras se escribe (sin tildes ni mayúsculas). */
  function filtrarOpciones(buscar) {
    const texto = sinTildes(buscar.value);
    const lista = buscar.closest('.desplegable').querySelector('.opciones');
    let visibles = 0;
    lista.querySelectorAll('label.opcion').forEach(l => {
      const nombre = sinTildes(l.querySelector('.opcion-texto')?.textContent ?? l.textContent);
      l.hidden = texto !== '' && !nombre.includes(texto);
      if (!l.hidden) visibles++;
    });
    let aviso = lista.querySelector('.sin-coincidencias');
    if (!aviso) {
      aviso = document.createElement('p');
      aviso.className = 'sin-opciones sin-coincidencias';
      lista.appendChild(aviso);
    }
    aviso.textContent = `Sin coincidencias para «${buscar.value.trim()}»`;
    aviso.hidden = visibles > 0;
  }

  document.addEventListener('click', e => {
    const enlace = e.target.closest('a[data-parcial]');
    if (enlace && e.button === 0 && !e.ctrlKey && !e.metaKey && !e.shiftKey && !e.altKey) {
      e.preventDefault();
      cargar(enlace.href);
      return;
    }

    // Semana / Día de una gráfica: muestra el bloque [data-vista] elegido dentro de su [data-vistas].
    const vista = e.target.closest('[data-vista-boton]');
    if (vista) {
      const caja = vista.closest('[data-vistas]');
      caja?.querySelectorAll('[data-vista-boton]').forEach(b => b.classList.toggle('activo', b === vista));
      caja?.querySelectorAll('[data-vista]').forEach(b => { b.hidden = b.dataset.vista !== vista.dataset.vistaBoton; });
      return;
    }

    if (e.target.closest('[data-imprimir]')) { window.print(); return; }
    if (e.target.closest('[data-presentar]')) { presentar(true); return; }
    if (e.target.closest('[data-presentar-salir]')) { presentar(false); return; }
    if (e.target.closest('[data-panel-plegar]')) { plegarPanel(true); return; }
    if (e.target.closest('[data-panel-abrir]')) { plegarPanel(false); return; }

    // Navegar a otra página (sin recarga parcial): la barra de carga se ve mientras llega.
    const destino = e.target.closest('a[href]');
    if (destino && !destino.hasAttribute('data-parcial') && e.button === 0 && !e.ctrlKey && !e.metaKey && !e.shiftKey && !e.altKey
        && destino.origin === location.origin && !destino.hasAttribute('download') && destino.target !== '_blank'
        && destino.pathname + destino.search !== location.pathname + location.search && !/\/(csv|descargar)$/.test(destino.pathname)) {
      raiz.classList.add('cargando');
    }

    const boton = e.target.closest('[data-indicador-boton], [data-ind-vista]');
    if (boton) {
      const caja = boton.closest('[data-indicadores]');
      if (caja) {
        if (boton.dataset.indicadorBoton !== undefined) caja.dataset.indicador = boton.dataset.indicadorBoton;
        else caja.dataset.vista = boton.dataset.indVista;
        mostrarIndicador(caja);
      }
      return;
    }

    const fila = e.target.closest('tr[data-hijos]');
    if (fila && !e.target.closest('a, button')) {
      const abierta = fila.classList.toggle('abierta');
      fila.setAttribute('aria-expanded', String(abierta));
      fila.closest('tbody').querySelectorAll(`tr[data-padre="${CSS.escape(fila.dataset.hijos)}"]`).forEach(h => { h.hidden = !abierta; });
      return;
    }

    const limpiar = e.target.closest('[data-limpiar]');
    if (limpiar) {
      const d = limpiar.closest('details');
      d.querySelectorAll('input[type=checkbox]').forEach(c => { c.checked = false; });
      enviar(d.closest('form'), d);
      return;
    }

    const ordenar = e.target.closest('table.ranking .ordenar');
    if (ordenar) {
      ordenarTabla(ordenar.closest('th'));
      return;
    }

    const copiar = e.target.closest('[data-copiar]');
    if (copiar) {
      copiarTexto(copiar);
      return;
    }

    // Clic fuera: cierra los desplegables abiertos.
    document.querySelectorAll('details.multi[open]').forEach(d => { if (!d.contains(e.target)) d.open = false; });
  });

  // «toggle» no burbujea: se escucha en fase de captura.
  document.addEventListener('toggle', e => {
    const d = e.target;
    if (!(d instanceof HTMLDetailsElement) || !d.matches('details.multi') || !d.open) return;
    document.querySelectorAll('details.multi[open]').forEach(o => { if (o !== d) o.open = false; });
    colocarDesplegable(d);
    // Al abrirlo la persona (no al reabrirlo tras filtrar), el foco va al buscador.
    if (!reabriendo) d.querySelector('.buscar-opcion')?.focus();
    reabriendo = false;
  }, true);

  document.addEventListener('submit', e => {
    const form = e.target;
    // Formularios GET sueltos que solo cambian #informe (el buscador de Equipos).
    if (form.matches('form[data-parcial]')) {
      e.preventDefault();
      cargar(urlDe(form));
      return;
    }
    if (!form.matches('[data-tablero-filtros]')) return;
    e.preventDefault();
    enviar(form, form.querySelector('details.multi[open]'));
  });

  /** «Copiar ID»: al portapapeles, y si el navegador no deja (http sin localhost), a la antigua. */
  async function copiarTexto(boton) {
    const texto = boton.dataset.copiar;
    let hecho = false;
    try {
      await navigator.clipboard.writeText(texto);
      hecho = true;
    } catch {
      const area = document.createElement('textarea');
      area.value = texto;
      area.setAttribute('readonly', '');
      area.style.position = 'fixed';
      area.style.opacity = '0';
      document.body.appendChild(area);
      area.select();
      try { hecho = document.execCommand('copy'); } catch { hecho = false; }
      area.remove();
    }
    const original = boton.dataset.textoOriginal ?? boton.innerHTML;
    boton.dataset.textoOriginal = original;
    boton.textContent = hecho ? 'Copiado' : 'Selecciónalo a mano';
    clearTimeout(boton._temporizador);
    boton._temporizador = setTimeout(() => { boton.innerHTML = original; }, 1600);
  }

  // Mientras No solución prepara los datos, la página se vuelve a pedir sola cada pocos segundos.
  let recarga = null;
  function programarRecarga(contenedor) {
    clearTimeout(recarga);
    const aviso = contenedor.querySelector('[data-recargar-en]');
    if (!aviso) return;
    const segundos = Number(aviso.dataset.recargarEn) || 10;
    recarga = setTimeout(() => cargar(location.href, { empujar: false }), segundos * 1000);
  }

  document.addEventListener('change', e => {
    const campo = e.target;
    if (!campo.closest?.('[data-tablero-filtros]')) return;
    if (campo.matches('input[type=date]') && campo.value) enviar(campo.form);
    if (campo.matches('details.multi input[type=checkbox]')) enviar(campo.form, campo.closest('details.multi'));
  });

  document.addEventListener('input', e => {
    if (e.target.matches('.buscar-opcion')) filtrarOpciones(e.target);
  });

  document.addEventListener('keydown', e => {
    // Intro en el buscador no envía el formulario: se filtra al marcar las casillas.
    if (e.key === 'Enter' && e.target.matches?.('.buscar-opcion')) {
      e.preventDefault();
      return;
    }
    if (e.key !== 'Escape') return;
    ocultarFicha();
    document.querySelectorAll('details.multi[open]').forEach(d => {
      d.open = false;
      d.querySelector('summary')?.focus();
    });
  });

  window.addEventListener('popstate', () => {
    if (document.getElementById('informe')) cargar(location.href, { empujar: false });
  });

  /** Indicador + volumen: enseña la figura del indicador y la vista elegidos y pone en su sitio las pestañas y el título. */
  function mostrarIndicador(caja) {
    const ind = caja.dataset.indicador;
    const vista = caja.dataset.vista;
    caja.querySelectorAll('[data-indicador][data-vista]').forEach(f => { f.hidden = !(f.dataset.indicador === ind && f.dataset.vista === vista); });
    caja.querySelectorAll('[data-indicador-boton]').forEach(b => b.classList.toggle('activo', b.dataset.indicadorBoton === ind));
    caja.querySelectorAll('[data-ind-vista]').forEach(b => b.classList.toggle('activo', b.dataset.indVista === vista));
    const nombre = caja.querySelector(`[data-indicador-boton="${CSS.escape(ind)}"]`)?.textContent.trim();
    const titulo = caja.closest('.tarjeta')?.querySelector('[data-ind-nombre]');
    if (titulo && nombre) titulo.textContent = nombre;
  }

  // ---------------------------------------------------------------------------------------
  // Panel de filtros plegable, Presentar y barra de carga al navegar
  // ---------------------------------------------------------------------------------------

  /** Pliega el panel a un raíl vertical (o lo despliega) y recuerda la elección; tema.js la lee al cargar. */
  function plegarPanel(plegar) {
    if (plegar) raiz.dataset.panel = 'plegado'; else delete raiz.dataset.panel;
    try { localStorage.setItem('cdm-panel', plegar ? 'plegado' : 'abierto'); } catch { /* sin almacenamiento */ }
  }

  /** Presentar: el contenido a pantalla completa, sin panel ni botones. Se sale con el botón, con Esc o saliendo de la pantalla completa. */
  function presentar(entrar) {
    if (entrar) {
      raiz.dataset.presentacion = '';
      raiz.requestFullscreen?.().catch(() => { /* si no deja, queda la vista limpia sin pantalla completa */ });
    } else {
      delete raiz.dataset.presentacion;
      if (document.fullscreenElement) document.exitFullscreen?.().catch(() => { });
    }
  }
  document.addEventListener('fullscreenchange', () => {
    if (!document.fullscreenElement) delete raiz.dataset.presentacion;
  });
  // Imprimir siempre en claro: con el tema oscuro, el texto claro saldría invisible sobre el papel.
  let temaAntes = null;
  window.addEventListener('beforeprint', () => {
    if (raiz.dataset.tema === 'oscuro') { temaAntes = 'oscuro'; raiz.dataset.tema = 'claro'; }
  });
  window.addEventListener('afterprint', () => {
    if (temaAntes) { raiz.dataset.tema = temaAntes; temaAntes = null; }
  });
  // Al volver con «atrás» desde otra página (caché del navegador), la barra de carga no se queda a medias.
  window.addEventListener('pageshow', () => raiz.classList.remove('cargando'));

  // ---------------------------------------------------------------------------------------
  // Fichas al pasar el ratón
  // ---------------------------------------------------------------------------------------

  const ficha = document.createElement('div');
  ficha.className = 'ficha';
  ficha.setAttribute('role', 'tooltip');
  document.body.appendChild(ficha);
  let objetivo = null;

  // «Etiqueta · Serie valor · …»: la primera parte es el título y cada una de las demás, una
  // fila «Serie | valor» (el valor empieza en la primera cifra o en «—»).
  function pintarFicha(texto) {
    const [titulo, ...resto] = texto.split(' · ');
    ficha.replaceChildren();
    const t = document.createElement('div');
    t.className = 'ficha-titulo';
    t.textContent = titulo;
    ficha.appendChild(t);
    for (const parte of resto) {
      const fila = document.createElement('div');
      fila.className = 'ficha-fila';
      const m = parte.match(/^(.*?)\s+([-+−]?[\d—].*)$/);
      const nombre = document.createElement('span');
      nombre.textContent = m ? m[1] : parte;
      fila.appendChild(nombre);
      if (m) {
        const valor = document.createElement('b');
        valor.textContent = m[2];
        fila.appendChild(valor);
      }
      ficha.appendChild(fila);
    }
  }

  function colocarFicha(x, y) {
    const margen = 14;
    const r = ficha.getBoundingClientRect();
    let izq = x + margen;
    let arriba = y + margen;
    if (izq + r.width > window.innerWidth - 8) izq = x - r.width - margen;
    if (arriba + r.height > window.innerHeight - 8) arriba = y - r.height - margen;
    ficha.style.left = Math.max(8, izq) + 'px';
    ficha.style.top = Math.max(8, arriba) + 'px';
  }

  function ocultarFicha() {
    ficha.classList.remove('visible');
    if (objetivo) quitarFoco(objetivo);
    objetivo = null;
  }

  // Guía vertical y desvanecido del resto de periodos en las gráficas por tiempo.
  function ponerFoco(el) {
    const plano = el.closest('.plano');
    if (!plano || el.dataset.i === undefined) return;
    plano.classList.add('con-foco');
    plano.querySelectorAll(`[data-i="${el.dataset.i}"]`).forEach(x => x.classList.add('foco'));
    let guia = plano.querySelector(':scope > .guia');
    if (!guia) {
      guia = document.createElement('span');
      guia.className = 'guia';
      plano.appendChild(guia);
    }
    guia.style.left = (parseFloat(el.dataset.x) * 100) + '%';
    guia.hidden = false;
  }

  function quitarFoco(el) {
    const plano = el.closest('.plano');
    if (!plano) return;
    plano.classList.remove('con-foco');
    plano.querySelectorAll('.foco').forEach(x => x.classList.remove('foco'));
    const guia = plano.querySelector(':scope > .guia');
    if (guia) guia.hidden = true;
  }

  document.addEventListener('pointermove', e => {
    const el = e.target.closest?.('[data-ficha]');
    if (el !== objetivo) {
      if (objetivo) quitarFoco(objetivo);
      objetivo = el;
      if (!el) { ficha.classList.remove('visible'); return; }
      pintarFicha(el.dataset.ficha);
      ponerFoco(el);
      ficha.classList.add('visible');
    }
    if (el) colocarFicha(e.clientX, e.clientY);
  });
  document.addEventListener('pointerleave', ocultarFicha);
  window.addEventListener('scroll', ocultarFicha, { passive: true });

  // Pasa los <title> de SVG y los title de las piezas a data-ficha (así no sale además el
  // globo del navegador).
  function prepararFichas(contenedor) {
    contenedor.querySelectorAll('svg title').forEach(t => {
      t.parentElement.dataset.ficha = t.textContent.trim();
      t.remove();
    });
    contenedor.querySelectorAll('.resumen-dato[title], .hbarra[title], .apilada[title], .anillo[title]').forEach(el => {
      el.dataset.ficha = el.title;
      el.removeAttribute('title');
    });
  }

  // ---------------------------------------------------------------------------------------
  // Tablas que se ordenan y colorean
  // ---------------------------------------------------------------------------------------

  const comparar = new Intl.Collator('es', { sensitivity: 'base', numeric: true }).compare;

  function valorDe(celda) {
    const v = celda?.dataset.valor ?? celda?.textContent ?? '';
    const n = v.trim() === '' ? NaN : Number(v);
    return Number.isNaN(n) ? v : n;
  }

  function ordenarTabla(th) {
    const tabla = th.closest('table');
    const columna = [...th.parentElement.children].indexOf(th);
    const ascendente = th.getAttribute('aria-sort') === 'descending';
    tabla.querySelectorAll('th[aria-sort]').forEach(x => x.removeAttribute('aria-sort'));
    th.setAttribute('aria-sort', ascendente ? 'ascending' : 'descending');

    const cuerpo = tabla.tBodies[0];
    const filas = [...cuerpo.rows];
    filas.sort((a, b) => {
      // Las filas con pocos datos y la de totales se quedan siempre al final.
      const ra = a.classList.contains('total') ? 2 : a.classList.contains('pocas') ? 1 : 0;
      const rb = b.classList.contains('total') ? 2 : b.classList.contains('pocas') ? 1 : 0;
      if (ra !== rb) return ra - rb;
      const va = valorDe(a.cells[columna]);
      const vb = valorDe(b.cells[columna]);
      const vacioA = va === '' || (typeof va === 'number' && Number.isNaN(va));
      const vacioB = vb === '' || (typeof vb === 'number' && Number.isNaN(vb));
      if (vacioA !== vacioB) return vacioA ? 1 : -1;
      const r = typeof va === 'number' && typeof vb === 'number' ? va - vb : comparar(String(va), String(vb));
      return ascendente ? r : -r;
    });
    cuerpo.append(...filas);
  }

  function colorearTabla(tabla) {
    const cabeceras = [...tabla.tHead.rows[0].cells];
    cabeceras.forEach((th, columna) => {
      const sentido = Number(th.dataset.sentido || 0);
      if (!sentido) return;
      const conValor = f => {
        const c = f.cells[columna];
        return c && c.dataset.valor !== undefined && c.dataset.valor !== '' && !Number.isNaN(Number(c.dataset.valor)) ? c : null;
      };
      const filas = [...tabla.tBodies[0].rows].filter(f => !f.classList.contains('total'));
      // La escala sale de las filas principales; las hijas del ranking tipológico (tr.hija) se colorean con la misma.
      const valores = filas.filter(f => !f.classList.contains('hija')).map(conValor).filter(Boolean).map(c => Number(c.dataset.valor));
      if (valores.length < 2) return;
      const min = Math.min(...valores);
      const max = Math.max(...valores);
      if (max === min) return;
      filas.map(conValor).filter(Boolean).forEach(c => {
        let t = (Number(c.dataset.valor) - min) / (max - min);
        if (sentido < 0) t = 1 - t;
        c.classList.add(t >= 2 / 3 ? 'mapa-verde' : t >= 1 / 3 ? 'mapa-ambar' : 'mapa-rojo');
      });
    });
  }

  /** Barrita de volumen (td.barra-volumen): su largo (--v, de 0 a 1) es la cifra frente a la mayor de su columna. */
  function ponerBarras(tabla) {
    const filas = [...(tabla.tBodies[0]?.rows ?? [])];
    const columnas = new Set();
    filas.forEach(f => [...f.cells].forEach((c, i) => { if (c.classList.contains('barra-volumen')) columnas.add(i); }));
    columnas.forEach(i => {
      const celdas = filas.map(f => f.cells[i]).filter(c => c?.classList.contains('barra-volumen'));
      const mayor = Math.max(0, ...celdas.map(c => Number(c.dataset.valor)).filter(Number.isFinite));
      celdas.forEach(c => {
        const v = Number(c.dataset.valor);
        c.style.setProperty('--v', mayor > 0 && Number.isFinite(v) ? String(Math.max(0.02, v / mayor)) : '0');
      });
    });
  }

  // ---------------------------------------------------------------------------------------
  // Cifras que cuentan
  // ---------------------------------------------------------------------------------------

  const entero = new Intl.NumberFormat('es-ES', { maximumFractionDigits: 0, useGrouping: 'always' });
  const porcentaje = new Intl.NumberFormat('es-ES', { minimumFractionDigits: 2, maximumFractionDigits: 2, useGrouping: 'always' });
  const suavizar = t => 1 - Math.pow(1 - t, 3);

  /** @param {number[]} [antes] las cifras que había antes de filtrar: se cuenta desde ellas. */
  function contarCifras(contenedor, antes = []) {
    if (sinMovimiento.matches) return;
    contenedor.querySelectorAll('.resumen-cifra[data-contar]').forEach((el, i) => {
      const final = Number(el.dataset.contar);
      if (el.dataset.contar === '' || Number.isNaN(final)) return;
      const inicial = Number.isFinite(antes[i]) ? antes[i] : 0;
      if (inicial === final) return;
      const textoFinal = el.textContent;
      const esPorcentaje = el.dataset.formato === 'porcentaje';
      const inicio = performance.now();
      const duracion = 780;
      function paso(ahora) {
        if (!el.isConnected) return;
        const t = Math.min(1, (ahora - inicio) / duracion);
        const v = inicial + (final - inicial) * suavizar(t);
        el.textContent = t >= 1 ? textoFinal : esPorcentaje ? porcentaje.format(v * 100) + ' %' : entero.format(v);
        if (t < 1) requestAnimationFrame(paso);
      }
      requestAnimationFrame(paso);
    });
  }

  // ---------------------------------------------------------------------------------------

  function preparar(contenedor, cifrasAntes = []) {
    prepararFichas(contenedor);
    contenedor.querySelectorAll('table.ranking[data-mapa]').forEach(colorearTabla);
    contenedor.querySelectorAll('table.ranking').forEach(ponerBarras);
    contarCifras(contenedor, cifrasAntes);
    programarRecarga(contenedor);
  }

  preparar(document);
})();
