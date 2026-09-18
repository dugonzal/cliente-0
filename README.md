# Cliente 0 — un parque de laboratorio y el sistema que lo gestiona

Escaparate público de un sistema de gestión de red (NMS, FCAPS) y del driver SNMP que lo
alimenta. Todo lo que hay aquí se desarrolló y se midió sobre un parque de laboratorio propio
de 18 nodos. Este repositorio no contiene el producto: contiene lo que su licencia declara
abierto y la evidencia de lo que se midió.

Autor: Duvan González · dugonzal@protonmail.com · https://github.com/dugonzal

Serie de artículos sobre cómo se construyó, paso a paso:
https://dugonzal.github.io/blog/

## Qué hay aquí

- `open-reference/contracts/` — los contratos de cable compartidos entre el agente SNMP y el
  gestor, más los códecs de telemetría (C37.118, IEC 104) y la configuración de sesión SNMP.
  Licencia MIT.
- `open-reference/stub/` — la puerta de licencia, en abierto y ejecutable: comprueba el token
  firmado contra la clave pública del autor y se niega a seguir sin él. No es el producto, es
  la puerta que el producto tiene delante. Licencia MIT.
- `open-reference/harness/SnmpStressDemo/` — el arnés de carga que produjo la tabla de
  rendimiento, copiado tal cual como se ejecutó. Levanta agentes simulados y lanza las
  peticiones por un socket UDP compartido. Licencia MIT.
- `docs/` — dos planes de implementación de monitorización MPLS: qué OID corresponde a qué
  (mapa RFC 3815, 4364, 4382, 8029) y en qué fases se construyó.
- `evidence/` — el informe de interoperabilidad contra net-snmp real, con sus tablas de
  rendimiento, y la tabla de etapas del laboratorio con sus recuentos de pruebas.

El motor de gestión, el driver, el simulador y el guardián de licencia se distribuyen solo
como binarios compilados, con token firmado por el autor; su fuente no se publica. Los ejemplos
de uso tampoco, salvo el arnés de carga de arriba. Si quieres verlos, pídelos: se enseñan en vivo.

## El recorrido

El orden es el que siguió el trabajo, no el que quedaría más bonito.

1. El cliente SNMP. v1, v2c y v3 sobre SharpSnmpLib, con un socket multiplexado y sin
   asignación por petición.
2. La interoperabilidad se probó contra net-snmp 5.9.5, no contra sí mismo. El informe de
   `evidence/` dice qué funcionó y, sobre todo, qué no: `authPriv` con AES-128 no
   interoperaba por una diferencia de derivación de claves.
3. La corrección fue adoptar la extensión de clave localizada de Blumenthal, que es la regla
   que sigue net-snmp, en lugar de la variante Reeder que traía la librería. Re-verificado:
   `authPriv` SHA-256/AES-128 y SHA-512/AES-256, caminata completa del subárbol MPLS por v3 y
   los cinco informes de fallo USM (usuario desconocido, digest incorrecto, nivel de
   seguridad no soportado, descifrado, notInTimeWindow) decodificados a su OID `usmStats`.
4. Un NMS encima. FCAPS sobre 18 nodos por SNMP v3 authPriv, con LLDP para el descubrimiento
   de vecinos. Etapa 11: 237/0 y 106/106 pruebas en verde.
5. Las alarmas como estado, no como mensajes. Histéresis, ventanas de mantenimiento y
   correlación de causa (etapa 17, 27/27 y 152/152).
6. La contabilidad. Ventanas de 15 minutos cerradas e inmutables, con conteo real de lo que
   pasó dentro (etapa 16, 19/19 y 137/137).
7. El histórico. Alarmas, medidas de rendimiento y configuración en SQLite, comprobando que
   sobrevive a la muerte del proceso (etapa 14, 18/18 y 119/119).
8. El canal de escritura. Configuración por SSH, con rollback y relectura para detectar
   deriva semántica (etapa 15, 23/23 y 128/128).
9. La guardia y el aviso. El NMS cierra sus ventanas y periodos solo; el correo sale por SMTP
   de verdad y hay webhook, con cola persistente y reintentos (etapas 19 y 20, 17/17, 164/164
   y 23/23, 207/207).
10. Las redes. MPLS por SNMP un nivel más arriba del que estaba: el árbol de MPLS-LDP vive
    bajo `1.3.6.1.2.1.10.166`, no bajo la rama de interfaces.

## El error que enseñó más

Un equipo que reiniciaba su agente se quedaba inalcanzable para siempre. El driver insistía
con el informe de motor de la sesión anterior y el agente lo rechazaba como
notInTimeWindow; el bucle volvía a usar el mismo informe y nunca salía de ahí. El arreglo es
una línea: al fallar, descartar el informe y volver a descubrirlo. El cliente de net-snmp
nunca sufrió esto porque vuelve a descubrir en cada invocación, y por eso ningún banco de
pruebas anterior lo había visto. Apareció en la etapa 17 con el parque entero delante.

## Números

Interoperabilidad y rendimiento, medidos con el driver compilado. Las filas con los agentes en
el mismo proceso se lanzaron con el arnés de `open-reference/harness/SnmpStressDemo/`; las de
agentes en procesos separados, con un lanzador que no está en este repositorio. La tabla
completa, con las filas que no se reprodujeron y un apartado de lo que **no** mide, está en
`evidence/REAL-TEST-REPORT.md`.

- 120.000 peticiones contra 6 agentes, 256 en vuelo: 38.302 req/s, 0 fallos (14-sep-2026).
- 120.000 peticiones contra 6 agentes, 512 en vuelo: 38.792 req/s, 0 fallos.
- 30.000 peticiones contra 1 agente, 256 en vuelo: 39.746 req/s, 0 fallos.
- El número de agentes no escala la cifra: un solo agente en loopback da lo mismo que seis.
  El techo es el cliente más el loopback, no el parque.

Las mediciones del 1 de septiembre de ese mismo año dieron alrededor de 24.000 req/s. No se
reproducen y el informe explica por qué; quedan escritas porque un número que envejece mal
también es un dato.

Recuentos de pruebas del laboratorio, por etapa:

| etapa | qué demostró | números |
|---|---|---|
| 11 | puente FCAPS↔parque real: 18 nodos por SNMP v3 authPriv, LLDP | 237/0 · 106/106 |
| 12 | fallo vivo: corte real, aviso por trap y lectura por poll, recuperación | corte cerrado |
| 13 | v3 de punta a punta sobre los 18 nodos | 18/18 · 109/109 |
| 14 | histórico en SQLite que sobrevive a la muerte del proceso | 18/18 · 119/119 |
| 15 | canal de escritura por SSH, deriva semántica y rollback con relectura | 23/23 · 128/128 |
| 16 | ventanas de 15 minutos cerradas e inmutables, conteo real | 19/19 · 137/137 |
| 17 | histéresis, ventanas de mantenimiento, correlación de causa | 27/27 · 152/152 |
| 18 | rechazo con nombre, avisos v3 con identidad, alarmas con los contadores del equipo | 162/162 |
| 19 | guardia: cierre de ventanas y periodos por sí solo | 17/17 · 164/164 |
| 20 | aviso: SMTP real y webhook, cola persistente con reintentos | 23/23 · 207/207 |

Etapas cerradas en septiembre de 2026; la última, el día 16.

## Cómo verificarlo

Lo que se puede comprobar sin pedirme nada:

```
dotnet build open-reference/contracts/SnmpContracts.csproj -c Release
```

Compila sin advertencias y sin errores sobre .NET 10. El proyecto de contratos no tiene
dependencia de ninguna librería SNMP a propósito: no puede acoplarse al driver.

Y la puerta de licencia se prueba sin pedirme nada:

```
dotnet run --project open-reference/stub
```

Sin token responde `License check failed: This software is licensed to its authors...` y sale
con código 2; con un token válido imprime sus datos y emite una trama IEC 104 de ejemplo. La
clave pública del autor está incrustada a propósito: verifica y no firma, así que publicarla no
entrega nada. El stub es independiente: no es el guardián del producto ni su fuente.

Y se lee: `evidence/REAL-TEST-REPORT.md` está escrito con la fecha de cada medición y con los
huecos que quedaron.

Lo que no se verifica desde aquí es el producto, porque su fuente no está publicada. Se
enseña funcionando sobre los 18 nodos cuando haga falta.

## Qué no está aquí, y por qué

- El código del motor, del driver, del simulador y del guardián de licencia. Lo que sí está es
  el stub, que reproduce la comprobación de arranque, no el guardián.
- El laboratorio no está aquí: está **en abierto**, entero, en
  [dugonzal/mpls-lab](https://github.com/dugonzal/mpls-lab) — 18 nodos, 17 bancos con su
  verificador y su evidencia.
- Capturas de tráfico y credenciales de cualquier tipo. Se hizo un barrido antes de publicar
  y no hay ninguna.
- Material comercial y de captación, que no es código ni evidencia.

## English

A network management system (NMS, FCAPS) and the SNMP driver behind it, built and measured
against a self-hosted lab of 18 nodes.

This repository is the public showcase, not the product. What is published here is what the
license declares open: the shared wire contracts (MIT), two MPLS monitoring implementation
plans, and the evidence — a real-interoperability report against net-snmp 5.9.5 and the
per-stage test counts of the lab. The management engine, the driver, the simulator and the
license guard ship as compiled binaries with an author-signed token; their source is not
published. One usage example is published anyway, because it is the method behind the numbers:
`open-reference/harness/SnmpStressDemo/`. The lab itself is open at
https://github.com/dugonzal/mpls-lab.

Numbers worth knowing: 120,000 polls across 6 agents at 38,302 req/s with 0 failures
(14-Sep-2026), and 207/207 tests green at the last stage. The report includes the earlier
measurements that no longer reproduce, and says so.

`open-reference/stub/` is the license gate itself, in the open and runnable: it checks an
author-signed token against the embedded public key and refuses to start without one. It is not
the product — it is the door the product sits behind. The public key is embedded on purpose: it
verifies, and it cannot sign.

Contact: dugonzal@protonmail.com · https://github.com/dugonzal
