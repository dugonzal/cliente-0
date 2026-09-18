# El arnés de los polls

`Program.cs` está copiado **tal cual** del arnés que produjo la tabla de rendimiento de
[`evidence/REAL-TEST-REPORT.md`](../../evidence/REAL-TEST-REPORT.md), sección 4. No está reescrito
para la ocasión: es el mismo código que se ejecutó.

Qué hace, en tres frases: levanta `--agents` agentes SNMP simulados, pide el árbol de OIDs a uno
de ellos para tener una lista de objetivos, y lanza `--count` peticiones sobre **un solo socket UDP
multiplexado** con un máximo de `--parallel` en vuelo. Al terminar imprime el tiempo, los aciertos,
los fallos y el caudal.

```bash
dotnet run -- --count 120000 --agents 6 --parallel 256
```

La salida tiene esta forma (las cifras son las de la tabla del informe, no las de tu máquina):

```
Pooling 120.000 SNMP polls -> 6 agents over 1 shared socket (max 256 in flight)
Done in 3,13s: 120.000 ok, 0 failed => 38.302 responses/s
=> throughput: 38302 req/s (single UDP socket, 6 agents)
```

## Qué filas de la tabla reproduce este arnés

Levanta los agentes **dentro del mismo proceso**, así que corresponde a estas dos filas:

| polls | agentes | dónde corren | en vuelo | req/s | fallos |
|---|---|---|---|---|---|
| 120.000 | 6 | mismo proceso | 256 | 36.109 | 0 |
| 120.000 | 6 | mismo proceso | 512 | 30.252 | 0 |

```bash
dotnet run -- --count 120000 --agents 6 --parallel 256
dotnet run -- --count 120000 --agents 6 --parallel 512
```

Los números no van a coincidir dígito a dígito con los de la tabla: dependen de la máquina y el
techo está en el cliente y el loopback (36–40k req/s), no en los agentes. Lo que se sostiene entre
máquinas es la **relación**: subir las peticiones en vuelo de 256 a 512 dentro del mismo proceso
cuesta alrededor de un 16 %, y añadir agentes no sube el caudal.

Las otras tres filas de la tabla se midieron con los agentes levantados como **procesos separados**,
y ese lanzador no está en el repositorio del producto. Quien quiera verlo, se levanta y se enseña.

## Hasta dónde se puede comprobar esto

Se puede **leer** el método entero: los tres parámetros, cómo se reparten los objetivos, y que el
transporte es un socket compartido y no un socket por petición. Y se puede comprobar que el informe
es coherente con lo que este código hace.

No se puede **ejecutar** sin el driver y el simulador, que son privados: las dos referencias del
`.csproj` apuntan ahí. Está escrito en el README de [`evidence/`](../../evidence/README.md) con el
mismo criterio: esto es una demostración que se inspecciona, no una que se repite.
