# Skill y agente «frontend-solaris»

Para que Claude haga el front de las webs SOLARIS · GAIA y CDM (vistas Razor, CSS, gráficas)
siguiendo la guía de estilos: piezas que ya existen, colores con variables, tema claro y oscuro,
móvil a 375 px y sin librerías externas.

| Fichero | Qué es |
|---|---|
| `frontend-solaris.skill` | La skill: instrucciones, la guía de estilos, recetas de piezas y dos scripts (revisar vistas y capturas en claro y oscuro). |
| `agente-frontend-solaris.md` | El agente: un ayudante de Claude Code que solo toca el front y trabaja con la skill. |

## Instalar en Claude Code (escritorio o terminal)

1. **Skill**: el `.skill` es un zip. Descomprímelo dentro de la carpeta de skills de tu usuario, de
   forma que quede `C:\Users\<tu usuario>\.claude\skills\frontend-solaris\SKILL.md`.
2. **Agente**: copia `agente-frontend-solaris.md` como
   `C:\Users\<tu usuario>\.claude\agents\frontend-solaris.md`.
3. Abre una conversación nueva (las skills y agentes se cargan al empezar).

Los scripts necesitan Python 3 y Microsoft Edge (para las capturas). No hace falta instalar nada más.

## Instalar en claude.ai

Ajustes → Capacidades → Skills → subir `frontend-solaris.skill` (si tu organización lo permite).
En claude.ai no hay agentes: con la skill basta.

## Usarla

- **Sola**: pide algo del front con normalidad («pon etiquetas a esta gráfica», «que la tarjeta se
  vea bien en oscuro», «añade un filtro de marca como el de Auditorías») y Claude carga la skill.
  También puedes forzarla escribiendo `/frontend-solaris` delante de lo que pidas.
- **Con el agente**: «usa el agente frontend-solaris para…». Hace el trabajo aparte y te devuelve un
  resumen de qué cambió y qué comprobó.

## Mantenerla al día

La skill lleva una copia de la guía (`references/guia-de-estilos.md`). Si la guía del proyecto
cambia, vuelve a copiarla ahí y regenera el `.skill`. Si el proyecto tiene su propia guía, la skill
le da prioridad a la del proyecto.
