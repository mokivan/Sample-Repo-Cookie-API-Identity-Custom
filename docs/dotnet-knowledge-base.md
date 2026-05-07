# Base de conocimiento .NET

Fecha de actualizacion: 2026-05-07

## Objetivo

Este documento resume tips, best practices y advice para desarrollo con .NET y C#. Toma como punto de partida el articulo de Medium `100 Expert C# Tips to Boost Your Coding Skills` de Sukhpinder Singh, publicado el 2025-03-10, pero reorganiza ese material en una referencia mas util para trabajo real de equipo y lo contrasta con documentacion oficial de Microsoft.

La idea no es coleccionar "100 trucos", sino dejar criterios de decision que sirvan para:

- escribir codigo mas claro y mantenible;
- evitar errores tipicos de performance, concurrencia y diseno;
- alinear decisiones de arquitectura con practicas recomendadas del ecosistema .NET moderno;
- servir como base de revision de PRs, onboarding y discusiones tecnicas.

## Como usar esta guia

- Usar el articulo de Medium como fuente de ideas de lenguaje moderno.
- Usar Microsoft Learn como fuente normativa para practicas de plataforma.
- Favorecer reglas estables y repetibles sobre hacks o micro-optimizaciones locales.
- Cuando haya tradeoffs, priorizar claridad, testabilidad y seguridad antes que "codigo inteligente".

## 1. Principios generales

### 1.1 Preferir claridad sobre cleverness

En C#, muchas features modernas permiten escribir menos codigo, pero menos lineas no siempre implica mejor diseno. Usa features nuevas cuando mejoran legibilidad o eliminan boilerplate real.

Buenos candidatos:

- primary constructors cuando la clase solo transporta dependencias o estado simple;
- collection expressions para inicializaciones triviales;
- pattern matching para reemplazar cascadas de `if`/`else` poco expresivas;
- raw string literals cuando reducen escapes y ruido visual.

Malos candidatos:

- expresiones demasiado densas;
- LINQ encadenado dificil de depurar;
- abuso de records o tuples donde el dominio pide nombres explicitos;
- features nuevas usadas solo por moda.

### 1.2 Mantener consistencia de estilo

La convencion importa porque reduce carga cognitiva. En .NET moderno, la recomendacion practica es:

- definir reglas de estilo en `.editorconfig`;
- activar analyzers;
- sostener el mismo criterio en local, CI y code review;
- modernizar de forma incremental, no caotica.

### 1.3 Escribir codigo para el siguiente mantenedor

La referencia mental correcta no es "funciona?", sino:

- se entiende rapido?
- es facil de testear?
- es facil de modificar sin romper algo?
- deja claro que dependencias y contratos tiene?

## 2. Lenguaje C# moderno

### 2.1 Adoptar features modernas con intencion

El articulo de referencia acierta en algo importante: mantenerse en sintaxis vieja por costumbre empeora la experiencia del codigo. Conviene adoptar gradualmente:

- `required` para forzar inicializacion valida;
- `record` o `record struct` para modelos inmutables orientados a datos;
- pattern matching y `switch` expressions para logica de decision;
- collection expressions para colecciones simples;
- interpolacion de strings y raw strings para mejorar legibilidad;
- `ArgumentNullException.ThrowIfNull(...)` y guard clauses equivalentes.

### 2.2 Nullable Reference Types deben estar siempre habilitados

`<Nullable>enable</Nullable>` no es opcional en codigo nuevo serio. Trata las advertencias de nulabilidad como senales de diseno:

- si algo puede faltar, representalo explicitamente;
- si algo es obligatorio, validalo cerca del boundary;
- evita usar `!` salvo cuando la invariancia este realmente probada.

### 2.3 `var` con criterio

Usa `var` cuando el tipo es obvio desde el lado derecho o el nombre del metodo deja el tipo claro. Evitalo cuando hace mas dificil leer el flujo o entender el contrato.

### 2.4 Inmutabilidad por defecto cuando agrega valor

Preferi objetos inmutables para:

- DTOs;
- options;
- resultados de dominio;
- valores compartidos entre threads.

La mutabilidad deberia ser explicita y justificada, no la configuracion por defecto.

## 3. Diseno de APIs y contratos

### 3.1 Validar argumentos temprano

Errores de contrato deben aparecer cerca del borde de entrada, no varias capas mas abajo. Recomendacion:

- validar `null`, rangos y precondiciones al inicio;
- lanzar excepciones especificas;
- no esconder errores de programacion detras de valores magicos.

### 3.2 Hacer obvias las dependencias

Una clase sana expone sus dependencias por constructor o por parametros explicitos. Senales de mal diseno:

- resolver servicios desde el contenedor dentro del metodo;
- usar estado global;
- crear dependencias concretas con `new` en cualquier parte;
- clases con demasiadas dependencias.

### 3.3 Disenar metodos async de forma consistente

Si la operacion hace I/O, el contrato deberia ser async de punta a punta:

- usar `Task` o `Task<T>`;
- aceptar `CancellationToken` cuando la operacion pueda ser cancelada;
- usar sufijo `Async`;
- no mezclar sync y async arbitrariamente.

## 4. Excepciones y manejo de errores

### 4.1 No usar excepciones para control de flujo normal

Si un caso es esperable, preferi APIs tipo `TryParse`, `TryGetValue` o validaciones previas. Las excepciones son costosas y ademas comunican semanticamente una situacion excepcional.

### 4.2 Capturar solo lo que se puede manejar

Evitar `catch (Exception)` genericos salvo en boundaries de infraestructura:

- middleware global;
- job runner;
- host process;
- integracion externa donde haga falta traducir errores.

Si no podes recuperarte o enriquecer contexto de forma util, no captures.

### 4.3 Rethrow correcto

Si atrapas y relanzas la misma excepcion, usa `throw;` y no `throw ex;`, para no perder stack trace.

### 4.4 Excepciones especificas y mensajes utiles

Usa tipos predefinidos (`ArgumentException`, `InvalidOperationException`, etc.) antes de inventar una excepcion custom. Solo crear una excepcion propia cuando agrega semantica real para la capa consumidora.

## 5. Async, concurrencia y lifecycle

### 5.1 Async all the way

En aplicaciones web, mezclar operaciones sync con pipelines async escala mal. La regla practica:

- I/O a base de datos: async;
- HTTP calls: async;
- Redis/cache: async cuando la libreria lo soporte;
- file I/O: async cuando este en el hot path o afecte throughput.

### 5.2 `async void` casi nunca

En codigo de aplicacion, `async void` debe considerarse incorrecto salvo handlers de eventos muy puntuales. En ASP.NET Core, puede romper el ciclo de request y producir errores dificiles de diagnosticar.

### 5.3 No capturar `HttpContext` ni servicios scoped en background work

Si necesitas trabajo en segundo plano:

- copia solo los datos necesarios durante el request;
- crea un scope nuevo si necesitas servicios scoped;
- no cierres sobre `DbContext`, `HttpContext` ni otros objetos request-scoped.

### 5.4 CancellationToken no es decorativo

Propagalo entre capas cuando la operacion pueda cancelarse. No lo ignores en librerias internas si la llamada puede durar o bloquear recursos.

## 6. Dependency Injection y composicion

### 6.1 Evitar service locator

No resuelvas dependencias con `GetService()` desde logica de negocio cuando podes inyectarlas de forma explicita. Eso esconde contratos y debilita testabilidad.

### 6.2 Evitar estado global

Las guias de Microsoft son claras: evita clases estaticas con estado. Si necesitas una dependencia compartida, registrala con un lifetime correcto y hace explicita la intencion.

### 6.3 Cuidar el tamano de las clases

Muchas dependencias inyectadas suelen indicar demasiadas responsabilidades. Antes de seguir agregando servicios:

- separar orquestacion de reglas;
- extraer adaptadores externos;
- mover logica de validacion o transformacion a componentes propios.

### 6.4 Elegir bien el lifetime

Regla practica:

- `Singleton` para componentes stateless o shared infrastructure thread-safe;
- `Scoped` para trabajo por request o unidad de trabajo;
- `Transient` para objetos livianos sin estado compartido.

Evitar que un singleton dependa de algo scoped salvo a traves de un mecanismo deliberado de creacion de scope.

## 7. ASP.NET Core

### 7.1 Controllers delgados, logica fuera del endpoint

Los endpoints deberian:

- validar input;
- delegar a servicios;
- traducir resultados a HTTP;
- no concentrar reglas de negocio complejas.

### 7.2 Manejo consistente de errores

Centralizar errores en middleware o exception handler. Beneficios:

- respuestas homogeneas;
- menos duplicacion;
- mejor observabilidad;
- menor riesgo de filtrar detalles internos.

### 7.3 No devolver mas datos de los necesarios

En APIs:

- proyectar a DTOs;
- evitar exponer entidades EF directamente;
- paginar listas;
- documentar contratos y status codes.

### 7.4 Seguridad por defecto

Puntos minimos:

- autenticacion y autorizacion explicitas;
- cookies y tokens con configuracion segura;
- validacion de input;
- secretos fuera del repo;
- politicas claras para `401` y `403`;
- proteccion contra exposicion accidental de excepciones internas.

## 8. Datos y EF Core

### 8.1 Consultar solo lo necesario

En lectura, proyecta columnas especificas cuando no necesitas la entidad completa. Leer entidades enteras por comodidad suele costar mas memoria, mas ancho de banda y mas tracking innecesario.

### 8.2 Elegir tracking o no-tracking segun el caso

Para consultas read-only, considera `AsNoTracking()` o una estrategia equivalente. El tracking tiene valor cuando realmente vas a modificar y persistir entidades.

### 8.3 Cuidar el tamano del resultset

Nunca asumir que en produccion habra pocos registros. Aplica:

- filtros;
- paginacion;
- `Take(...)` cuando corresponda;
- streaming cuando el volumen pueda crecer.

### 8.4 Evitar lazy loading por defecto en backends criticos

Puede generar roundtrips invisibles y problemas tipo N+1. Preferi cargas explicitas, eager loading pensado, y profiling de consultas reales.

### 8.5 `DbContext` corto y con scope claro

La practica recomendada para apps web es un contexto corto por unidad de trabajo/request. No reutilizar un `DbContext` mucho mas alla de ese alcance.

## 9. Performance

### 9.1 Optimizar hot paths, no imaginar problemas

No micro-optimizar sin evidencia. Si conviene evitar patrones conocidos de desperdicio:

- concatenacion repetitiva de strings en loops;
- materializar listas innecesarias;
- leer cuerpos grandes completos en memoria;
- asignaciones grandes y frecuentes en paths calientes.

### 9.2 Medir antes de cambiar diseno

Toda optimizacion importante deberia responder a una metrica:

- latencia;
- throughput;
- CPU;
- memoria;
- queries;
- roundtrips;
- allocaciones.

### 9.3 Entender el costo de la memoria

ASP.NET Core recomienda minimizar allocaciones grandes en hot paths. En particular, el LOH puede introducir pausas y comportamiento inconsistente si se lo castiga innecesariamente.

## 10. Configuracion y observabilidad

### 10.1 Configuracion tipada

Preferi options tipadas frente a strings dispersos. Ademas:

- validar configuracion al iniciar;
- separar config por entorno;
- evitar acceso arbitrario a claves sueltas en cualquier capa.

### 10.2 Logging estructurado

Loguear eventos importantes con contexto util:

- operation id / correlation id;
- user id si aplica;
- resource id;
- outcome;
- duracion;
- motivo de error.

Evitar logs ruidosos o con datos sensibles.

### 10.3 Health, tracing y diagnosticos

En servicios reales, la pregunta no es si fallan, sino como los observas cuando fallen. La base minima suele incluir:

- health checks;
- metricas basicas;
- trazas distribuidas si hay multiples servicios;
- logs consistentes.

## 11. Testing

### 11.1 Tests que ejerciten comportamiento, no implementacion

El valor del test esta en detectar regresiones observables. Evita tests fragiles que solo confirman detalles internos.

### 11.2 Integracion donde el riesgo es real

En .NET web apps, vale mucho cubrir con integracion:

- autenticacion/autorizacion;
- serializacion;
- filtros y middleware;
- acceso a base de datos;
- contratos HTTP.

### 11.3 Determinismo

Controlar:

- tiempo (`TimeProvider`);
- datos de prueba;
- IDs;
- side effects externos;
- dependencias de red.

## 12. Checklist corto para PRs .NET

Antes de aprobar un cambio, conviene revisar:

- el contrato async es consistente?
- la nulabilidad esta bien modelada?
- hay validacion temprana de input?
- las dependencias estan explicitas?
- se evito service locator?
- hay riesgo de capturar `HttpContext` o `DbContext` fuera de scope?
- la consulta EF trae solo lo necesario?
- hay paginacion o limites para listas?
- los errores se traducen de forma consistente?
- hay tests para el comportamiento de mayor riesgo?

## 13. Traduccion del articulo de referencia a criterios practicos

El articulo de Medium es valioso como inventario de features y recordatorio de modernizacion. Su mejor uso no es copiar "100 tips" uno por uno, sino traducirlos a estas reglas:

- adoptar sintaxis moderna cuando reduce ruido;
- usar features del lenguaje para hacer contratos mas expresivos;
- no confundir menos boilerplate con mejor diseno;
- combinar tips de lenguaje con practicas de plataforma;
- validar siempre una recomendacion de estilo contra necesidades de performance, observabilidad y mantenimiento.

## 14. Aplicacion a este repositorio

Este repo ya sigue varias practicas sanas:

- `net10.0`, `Nullable` habilitado e `ImplicitUsings` habilitado;
- `TreatWarningsAsErrors` activado;
- middleware de manejo de errores;
- configuracion explicita de autenticacion por cookie;
- tests de integracion;
- separacion razonable entre controllers, stores y data access.

Linea de continuidad recomendada para este codigo base:

- mantener analyzers y estilo como parte de CI;
- seguir evitando logica pesada dentro de controllers;
- conservar lifetimes explicitos y revisar cualquier background work nuevo con mucho cuidado;
- mantener DTOs y contratos HTTP claros;
- medir consultas y paths criticos antes de optimizar.

## Fuentes

- Articulo de referencia:
  - https://medium.com/c-sharp-programming/100-expert-c-tips-to-boost-your-coding-skills-9f64334944c3
- Microsoft Learn:
  - https://learn.microsoft.com/en-us/dotnet/csharp/fundamentals/coding-style/coding-conventions
  - https://learn.microsoft.com/en-us/dotnet/standard/exceptions/best-practices-for-exceptions
  - https://learn.microsoft.com/en-us/aspnet/core/fundamentals/dependency-injection?view=aspnetcore-10.0
  - https://learn.microsoft.com/en-us/dotnet/core/extensions/dependency-injection/guidelines
  - https://learn.microsoft.com/en-us/aspnet/core/fundamentals/best-practices?view=aspnetcore-10.0
  - https://learn.microsoft.com/en-us/ef/core/performance/efficient-querying
  - https://learn.microsoft.com/en-us/dotnet/standard/garbage-collection/implementing-dispose
  - https://learn.microsoft.com/en-us/aspnet/core/fundamentals/configuration/options?view=aspnetcore-9.0
