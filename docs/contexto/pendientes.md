# Pendientes y decisiones abiertas (detalle)

> Parte del contexto de CDM Auditorías Calidad: el resumen está en `CONTEXTO_IA.md` (raíz del
> proyecto). Las referencias a «sección N» son de cuando todo estaba en un solo fichero:
> 1–2 → `power-bi.md` · 3 y 3 bis → `decisiones.md` · 3 ter → `no-solucion.md` ·
> 4–5 → `estructura-y-ejecucion.md` · 6 → `verificacion.md` · 7 → `pendientes.md` · 8 → `bitacora.md`.

## 7. Pendientes y decisiones abiertas

- **Causa de la no solución**: publicada el 06-10-2026. Revisar con el usuario el umbral de la rúbrica
  (1 de 7) y, de vez en cuando, las etiquetas que siguen en «Otro» (`ClasificadorEtiquetas`).

- **CDM No solución en el 5180**: publicado (caché en `publicacion\datos`). Al publicar un cambio de
  formato del cubo, copiar la caché de `App_Data` a `publicacion\datos` para no esperar a BigQuery.
  Para lo siguiente: cerrar la ventana «CDM Auditorias Calidad (5180)», `publicar.cmd`, `arrancar.cmd`.
- **No solución depende del DSN `BQCOL` de usuario** (la cuenta de Google del usuario), igual que
  en ranking-mvc. En la torre nueva hay que volver a crearlo. Para entregar la web haría falta una
  cuenta de servicio de BigQuery.
- **Dos copias de No solución** (aquí y en ranking-mvc): un arreglo en una no llega a la otra. Si
  ranking-mvc deja de usarse, esta pasa a ser la única.

- **Producción (5180)**: al día con la versión actual desde el 02-10-2026 (el usuario pidió una
  sola dirección que funcione: `http://localhost:5180/general`). Para los próximos cambios:
  cerrar la ventana «CDM Auditorias Calidad (5180)», `publicar.cmd` y otra vez `arrancar.cmd`.
  Un `publicar.cmd` con la web en marcha falla al copiar la DLL (y deja copiados algunos
  ficheros sueltos antes de fallar).
- **Credenciales personales**: la web usa el login de SQL del usuario (el de `DB_2` de
  ranking-mvc). Para entregarla, pedir una cuenta de servicio con SELECT en
  `Reporting.WO.AuditoriasWhatsapp/Jazztel/Orange`,
  `ModulosIceberg.Calidad.PlantillaCalidadUnificada` y
  `Planificacion.Nomina.(NominaAntiguedad, Nomina_User_Avaya, PD_Usuarios)`.
- **Acceso**: no hay inicio de sesión. Decidir si hace falta (ranking-mvc tiene uno).
- **SOLARIS**: la guía es de la plataforma SOLARIS · GAIA. Si esta web pasa a formar parte de
  ella, falta su logotipo (`_MarcaSolaris`) en la portada y quizá el login de SOLARIS.
- **Histórico: 3 meses atrás + el mes en curso (pedido del usuario el 02-10-2026)**.
  - ICEBERG: el usuario cambió él mismo en `Consultas/Auditorias.sql` el filtro de `- 2` a
    `- 3`: hoy, desde el 01/07 (hay datos desde el 04/07). **Activo** en desarrollo y en
    producción desde el 02-10-2026: 20.350 auditorías del 04/07 al 02/10, julio = 5.009 (solo
    ICEBERG), igual que con SQL directo.
    - Por qué al principio no salía: la web leía la copia de `bin\…\Consultas`, que la
      compilación no había refrescado. Ahora lee la de la carpeta de la aplicación en cada
      carga y el `.csproj` copia las consultas siempre (`Always`).
    - En producción (la versión anterior, que aún lee de su carpeta de binarios) se sustituyó
      solo `publicacion\app\Consultas\Auditorias.sql` por el nuevo y se pulsó «Actualizar»; no
      se reinició nada. Para volver a la anterior (`- 2`), está en git:
      `git show 2caa2bd:"CDM Auditorias Calidad/Consultas/Auditorias.sql"`.
  - WEB (WhatsApp, Jazztel, Orange): las tablas de origen empiezan el 01/08/2026, no hay julio.
    **El usuario va a pedir que las tablas WEB traigan 4 meses y avisará**: no investigar más
    hasta entonces. La consulta no filtra fechas en WEB, así que en cuanto el origen tenga
    julio, saldrá; si luego trae más de 4 meses, habría que poner a WEB la misma ventana.
  - Visto de paso (sin usar): `Reporting.WO.Auditoria_Grupo1/3/5` y `Auditoria_YGMMKRTV` tienen
    auditorías de julio, pero son otros formularios, no están en el PBI y `Grupo3` llega hasta
    el 08/09 (se solaparía con las tablas nuevas). La vista `WO.vw_auditorias` está rota (usa
    `Auditoria_Grupo4`, que no existe).
- Cuidado al comparar agosto con julio mientras WEB no traiga julio: julio solo tiene ICEBERG,
  así que la variación de agosto frente a julio sale inflada (con el filtro Base = ICEBERG es
  justa).
- Ideas no hechas: exportar también legajo e ID de llamada en el Excel.
