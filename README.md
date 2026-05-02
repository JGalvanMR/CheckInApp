# 📦 Nombre del Módulo: CheckInApp

> **Tipo:** Aplicación móvil Android (.NET MAUI 9)
> **Namespace raíz:** `CheckInApp`
> **Versión:** 2.2 (versionCode 22)
> **Package ID:** `com.mrlucky.checkinapp`
> **Empresa objetivo:** Comercializadora GAB S.A. DE C.V.

---

## 🧭 Propósito

CheckInApp es un sistema de control de asistencia y distribución de despensa para el personal de planta de una empresa manufacturera/comercializadora. Opera completamente offline (sin dependencias de red en tiempo de ejecución) y está diseñado para ejecutarse en dispositivos Android industriales equipados con lectores de código de barras físicos (Unitech PA768) o mediante la cámara del dispositivo.

El sistema resuelve el problema de validar, en tiempo real durante un evento de distribución de despensa, qué empleados tienen derecho a recibirla, registrar su asistencia, y generar reportes de auditoría para el Departamento de Recursos Humanos.

---

## ⚙️ Responsabilidades

- Cargar el padrón de empleados desde un archivo Excel con estructura de tres hojas (`TODO EL PERSONAL DE PLANTA`, `CON DERECHO A DESPENSA`, `PARA RETENER DESPENSA`).
- Identificar el derecho a despensa de cada empleado cruzando las listas del Excel al momento de la carga.
- Registrar el check-in de empleados mediante escaneo de código de barras (lector físico o cámara ZXing).
- Aplicar una cadena de reglas de negocio en orden determinístico ante cada escaneo.
- Permitir registro forzado por parte del operador, con trazabilidad del tipo de registro.
- Persistir la asistencia del día en almacenamiento local (JSON) para sobrevivir reinicios de la app.
- Filtrar y buscar empleados en tiempo real dentro de la lista cargada.
- Exportar el reporte de asistencia en cuatro formatos: Excel (.xlsx), TXT, CSV y JSON.
- Mostrar estadísticas en tiempo real: presentes, total, porcentaje de asistencia (entero y decimal de 3 cifras).

---

## 🔄 Flujo de Funcionamiento

### 1. Arranque de la aplicación

```
MauiApp.CreateBuilder()
  └─ Registra IBarcodeService → BarcodeService (Singleton) [Android only]
  └─ Registra MainViewModel (Singleton)
  └─ Registra MainPage (Singleton)
  └─ App.CreateWindow() → AppShell → MainPage
```

Al construirse `MainPage`, el `BindingContext` se asigna con el `MainViewModel` inyectado por DI (no desde XAML). El `BarcodeService` también se inyecta vía constructor y suscribe al evento `BarcodeScanned`.

`MainViewModel` ejecuta `CargarDatos()` en su constructor, intentando restaurar el último estado desde `invitados.json` en `AppDataDirectory`.

---

### 2. Carga del padrón de empleados (`CargarExcel`)

```
Usuario toca "Cargar"
  └─ FilePicker → selección de .xlsx
  └─ XLWorkbook abre el stream
  └─ Lee "CON DERECHO A DESPENSA" → Set<string> de códigos de barras con derecho pleno
  └─ Lee "PARA RETENER DESPENSA"  → Set<string> de códigos con retención por examen médico
  └─ Lee "TODO EL PERSONAL DE PLANTA" fila por fila:
       Columna 1 → Nómina (int)
       Columna 2 → Código interno
       Columna 3 → Código de barras (clave primaria de búsqueda)
       Columna 4 → Nombre
       Columna 5 → Supervisor
       Columna 6 → Departamento
       TieneDerechoADespensa = codigoBarras ∈ codigosConDespensa OR codigosRetencion
       ExamenMedico          = codigoBarras ∉ codigosRetencion
       RetencionPendiente    = codigoBarras ∈ codigosRetencion
  └─ CargarAsistencia() → aplica registros previos del día desde asistencia.json
  └─ Invitados ordenados por Nombre → ObservableCollection
  └─ ActualizarEstadisticas() + FiltrarInvitados()
```

---

### 3. Escaneo y procesamiento (`ProcesarCodigoResultado`)

El evento de escaneo puede originarse desde dos fuentes que convergen en el mismo método:

| Fuente | Ruta |
|--------|------|
| Lector físico (PA768) | `ScanBroadcastReceiver.OnReceive` → `BarcodeService.RaiseBarcode` → `OnBarcodeScanned` |
| Cámara ZXing | `BarcodesDetected` → `ProcesarCodigo` |

Ambas rutas llaman `viewModel.ProcesarCodigoResultadoCommand.ExecuteAsync(codigo)`.

**Pipeline de procesamiento interno:**

```
LimpiarCodigoEscaneado()         // strip prefijos AIM, whitespace, caracteres de control
NormalizarCodigo()               // quita asteriscos, elimina ceros a la izquierda
BuscarEmpleado()                 // busca por CodigoBarras, Codigo o Nomina (normalizados)

SI empleado == null              → StatusMessage "no encontrado", fin
SI !empleado.ExamenMedico        → DisplayAlert "Examen médico pendiente"
    Cancelar                     → fin
    Confirmar                    → Registro forzado (RegistroForzado = true), fin de flujo
SI !empleado.TieneDerechoADespensa → DisplayAlert "Sin derecho a despensa"
    Cancelar                     → fin
    Confirmar                    → Registro forzado, fin de flujo
SI empleado.Asistio              → StatusMessage "ya registró a las HH:mm", muestra detalle, fin
ELSE                             → Registro normal (RegistroForzado = false)

GuardarAsistencia()              // persiste asistencia.json
ActualizarEstadisticas()
InvitadoSeleccionado = empleado  // activa panel de detalle
Vibration.Vibrate(200ms)
FiltrarInvitados()
```

---

### 4. Persistencia

- **Escritura:** `GuardarAsistencia()` serializa `{Nomina: {Hora, Forzado}}` a `asistencia.json` en `AppDataDirectory` tras cada check-in.
- **Lectura:** `CargarAsistencia()` se ejecuta después de cada carga de Excel, aplicando los registros previos sobre la lista nueva.
- **Datos del padrón:** La lista completa de empleados se puede cargar desde `invitados.json` (flujo legacy vía `CargarDatos`), aunque el flujo principal re-carga desde Excel en cada sesión.

---

### 5. Exportación de reportes

El operador elige formato vía `DisplayActionSheet`. Los formatos disponibles son:

| Formato | Contenido |
|---------|-----------|
| `.xlsx` | 3 hojas: Reporte detallado, Resumen Ejecutivo, Alertas (forzados). Formato corporativo con paleta de colores, KPIs, autofilter y freeze de encabezados. |
| `.txt` | Reporte imprimible con tablas de texto fijo, resumen ejecutivo y sección de alertas. |
| `.csv` | Datos tabulares con metadatos comentados y bloque de resumen al pie. |
| `.json` | Estructura completa con `metadata`, `resumen`, `empleados` y `alertas`. |

Todos los archivos se guardan en `FileSystem.CacheDirectory` y se comparten mediante el API nativo `Share.Default.RequestAsync`.

---

## 📐 Reglas de Negocio

### 🔒 Restricciones

| # | Regla | Implementación |
|---|-------|----------------|
| R1 | Un empleado solo puede registrar check-in una vez por sesión | `if (empleado.Asistio)` antes del registro normal |
| R2 | Empleados con examen médico pendiente no pueden registrarse sin confirmación explícita del operador | `if (!empleado.ExamenMedico)` con `DisplayAlert` bloqueante |
| R3 | Empleados sin derecho a despensa no pueden registrarse sin confirmación explícita del operador | `if (!empleado.TieneDerechoADespensa)` con `DisplayAlert` bloqueante |
| R4 | La hoja "TODO EL PERSONAL DE PLANTA" y "CON DERECHO A DESPENSA" son requeridas; sin ellas no se carga el padrón | `if (hojaTodoPersonal == null || hojaConDespensa == null)` → abort |
| R5 | Las filas cuyo campo Nombre contenga la palabra "NOMBRE" (encabezado residual) se descartan | Filtro explícito en el loop de carga |

### ✅ Validaciones

| # | Validación | Implementación |
|---|------------|----------------|
| V1 | Código escaneado se normaliza antes de comparar: se eliminan asteriscos y ceros a la izquierda | `NormalizarCodigo()` |
| V2 | Prefijos de codificación AIM (`]C1`, `]A0`, `]E0`, `[)>`) se eliminan del código | `LimpiarCodigoEscaneado()` |
| V3 | Caracteres de control (`\n`, `\r`, `\t`, `\0`, `chr(3)`, `chr(4)`) se eliminan del código | `LimpiarCodigoEscaneado()` |
| V4 | La búsqueda de empleado intenta tres campos en orden: `CodigoBarras`, `Codigo`, `Nomina` | `BuscarEmpleado()` |
| V5 | Filas del Excel con `CodigoBarras` que contengan "CODIGO" (encabezado residual) se descartan | Filtro explícito en el loop |
| V6 | Celdas numéricas del Excel se leen con `GetFormattedString()` para evitar problemas de tipo en códigos con ceros | Código en `CargarExcel` columnas 2 y 3 |

### 🔁 Agrupaciones / Clasificaciones

| Grupo | Criterio | Resultado en modelo |
|-------|----------|---------------------|
| Con derecho pleno | `codigoBarras ∈ codigosConDespensa` | `TieneDerechoADespensa = true`, `ExamenMedico = true`, `RetencionPendiente = false` |
| Con derecho, examen pendiente | `codigoBarras ∈ codigosRetencion` | `TieneDerechoADespensa = true`, `ExamenMedico = false`, `RetencionPendiente = true` |
| Sin derecho | No aparece en ninguna de las dos listas | `TieneDerechoADespensa = false`, `ExamenMedico = true` (por defecto), `RetencionPendiente = false` |

### ⚙️ Reglas Operativas

| # | Regla |
|---|-------|
| O1 | El flujo de validación es **serial y cortocircuitado**: Examen médico se evalúa **antes** que derecho a despensa. Si el examen médico dispara registro forzado, el flujo termina ahí y no evalúa la regla de despensa. |
| O2 | Un registro forzado por **cualquier causa** (examen o sin derecho) tiene `RegistroForzado = true` y se trata igual en reportes y UI. |
| O3 | La persistencia de asistencia (`asistencia.json`) sobrevive la recarga del Excel: al cargar un nuevo archivo, los check-ins del día se re-aplican automáticamente. |
| O4 | El campo `EstadoDerecho` del modelo es derivado (computed): `"✓ CON DERECHO"` o `"⚠️ SIN DERECHO"` según `TieneDerechoADespensa`. |
| O5 | El reporte Excel incluye una tercera hoja "Alertas — Forzados" **solo si existen** registros forzados. |
| O6 | Los KPI de precisión muestran el porcentaje con 3 decimales (`F3`) para uso de auditoría, mientras que el porcentaje de asistencia usa 0 decimales (`F0`) para visibilidad operativa. |

---

## 🔗 Dependencias

### Paquetes NuGet

| Paquete | Versión | Uso |
|---------|---------|-----|
| `ClosedXML` | 0.105.0 | Lectura y generación de archivos Excel |
| `CommunityToolkit.Mvvm` | 8.4.0 | MVVM: `ObservableObject`, `RelayCommand`, `ObservableProperty` |
| `ZXing.Net.Maui` / `.Controls` | 0.6.0 | Lectura de códigos QR y barras por cámara |
| `Newtonsoft.Json` | 13.0.3 | Serialización/deserialización de persistencia local |
| `Microsoft.Maui.Controls` | (MauiVersion) | Framework UI |
| `Microsoft.Extensions.Logging.Debug` | 9.0.8 | Logging en modo Debug |

### Librerías nativas Android

| Archivo | Propósito |
|---------|-----------|
| `unitechRFID_v2.0.0.5.aar` | SDK del lector RFID del Unitech PA768 |
| `UnitechSDK_1.2.35.jar` | SDK general Unitech |
| `usbserial-3.7.3.aar` | Comunicación USB serial |

### Módulos internos

| Módulo | Dependencia |
|--------|-------------|
| `MainPage` | `MainViewModel`, `IBarcodeService` (opcional) |
| `MainViewModel` | `ClosedXML`, `Newtonsoft.Json`, `Share`, `Vibration`, `FilePicker`, `Application.Current.MainPage` |
| `ScanBroadcastReceiver` | `BarcodeService.Instance` (estático) |
| `MainActivity` | `ScanBroadcastReceiver` |
| `BarcodeService` | `IBarcodeService` |
| Servicios de reglas (`ReglaDespensa`, `ReglaExamenMedico`, `ReglaDuplicado`) | `Application.Current.MainPage` (para `DisplayAlert`) — **acoplamiento UI-servicio** |

### APIs de plataforma

| API | Uso |
|-----|-----|
| `FileSystem.AppDataDirectory` | Persistencia de `invitados.json` y `asistencia.json` |
| `FileSystem.CacheDirectory` | Archivos de exportación temporales |
| `FilePicker` | Selección de Excel |
| `Share.Default` | Compartir reportes exportados |
| `Vibration` | Feedback háptico en check-in |
| `Android.Content.BroadcastReceiver` | Recepción del intent `unitech.scanservice.data` |

---

## ⚠️ Riesgos Técnicos

### RT-01 — Acoplamiento directo UI en capa de servicios
**Severidad: Alta**
Los servicios `ReglaDespensa`, `ReglaExamenMedico` y `ReglaDuplicado` llaman directamente a `Application.Current.MainPage.DisplayAlert()`. Esto viola el principio de separación de responsabilidades: los servicios de negocio tienen dependencia implícita de la capa de presentación. Nota: estos servicios existen en el código pero **no son usados** en el flujo activo — el `MainViewModel` implementa la lógica de alertas directamente. El riesgo persiste si alguien activa `ReglasService` en el futuro.

### RT-02 — Patrón Singleton estático en `BarcodeService`
**Severidad: Alta**
`BarcodeService.Instance` es una propiedad estática que se asigna en el constructor. Si el DI container no instancia `BarcodeService` antes de que `ScanBroadcastReceiver` intente usarlo, `Instance` será `null` y los broadcasts del scanner físico se descartan silenciosamente (sin excepción).

### RT-03 — Deserialización con `dynamic` en `CargarAsistencia`
**Severidad: Media**
```csharp
var asistencia = JsonConvert.DeserializeObject<Dictionary<int, dynamic>>(json);
emp.HoraCheckIn = d.Hora; // acceso a propiedad dinámica
```
Los accesos a propiedades de `dynamic` no son verificados en tiempo de compilación. Un cambio en la estructura del JSON persisido o una corrupción del archivo puede causar `RuntimeBinderException` sin manejo de error específico. El `catch {}` vacío en este método oculta el fallo completamente.

### RT-04 — Dos archivos `MainPage.xaml` en el proyecto
**Severidad: Media**
Existen dos implementaciones de `MainPage.xaml`: una en `CheckInApp/MainPage.xaml` (activa, con bottom-sheet drag) y otra en la raíz del repositorio (`MainPage.xaml` y `MainPage.xaml.txt`). La coexistencia de estos archivos genera confusión en el mantenimiento. El archivo raíz parece ser un diseño alternativo abandonado.

### RT-05 — `catch { continue; }` en carga del Excel
**Severidad: Media**
El loop de procesamiento de filas del Excel envuelve cada fila en `try { ... } catch { continue; }`, descartando silenciosamente cualquier error de parseo. Un archivo Excel con estructura diferente a la esperada puede cargar parcialmente sin ninguna notificación al operador.

### RT-06 — `BindingContext` duplicado en XAML y código
**Severidad: Baja-Media**
`MainPage.xaml` declara `<ContentPage.BindingContext><viewmodels:MainViewModel /></ContentPage.BindingContext>`, pero `MainPage.xaml.cs` sobreescribe `BindingContext` con la instancia inyectada por DI. Esto significa que MAUI instancia un `MainViewModel` adicional en el constructor de XAML que es inmediatamente descartado, desperdiciando recursos y pudiendo causar efectos secundarios si `MainViewModel` tuviera efectos al construirse.

### RT-07 — Métodos legacy sin eliminar
**Severidad: Baja**
`MainViewModel` contiene `CargarExcelLEGACY()`, `ProcesarCodigoResultadoOG()`, `BuscarEmpleadoOG()`, y `LimpiarCodigoEscaneadoOG()` como métodos muertos. Aumentan la superficie de mantenimiento y generan ambigüedad sobre cuál es el flujo activo.

### RT-08 — Ausencia de manejo de permisos en runtime
**Severidad: Media**
La app requiere los permisos `CAMERA`, `READ_EXTERNAL_STORAGE` y `WRITE_EXTERNAL_STORAGE`, pero no existe código explícito de solicitud de permisos en runtime. En Android 6+ (API 23), los permisos peligrosos deben solicitarse en runtime. MAUI puede manejar esto parcialmente de forma automática, pero no está validado explícitamente en el código.

### RT-09 — Dependencia de nombres exactos de hojas Excel
**Severidad: Alta**
Los nombres de las tres hojas del Excel son strings literales codificados en el fuente. Un cambio de mayúsculas, un espacio adicional o una renombración de la hoja hace que la carga falle completamente. No hay un mecanismo de detección flexible o configuración externa.

---

## 🧪 Casos Edge

| # | Escenario | Comportamiento actual |
|---|-----------|----------------------|
| CE-01 | Empleado aparece en AMBAS listas (CON DERECHO y PARA RETENER) | La lógica de `codigosConDespensa OR codigosRetencion` le da derecho, pero `ExamenMedico = false` porque está en retención. Resultado: se le considera con derecho pero con examen pendiente. El Set de retención tiene precedencia sobre el comportamiento de examen. |
| CE-02 | Empleado escanea cuando `Invitados` está vacío (no se cargó Excel) | `BuscarEmpleado` retorna `null`. `StatusMessage` muestra "❌ Empleado no encontrado: [código]". No hay crash, pero el mensaje puede confundir al operador. |
| CE-03 | Se recarga el Excel con empleados distintos mientras hay registros de asistencia del día | `CargarAsistencia` cruza por `Nomina`. Si un empleado del nuevo Excel tiene la misma nómina que uno del anterior, hereda su check-in. Si la nómina no existe en el nuevo archivo, el registro previo se descarta. |
| CE-04 | Código de barras con asteriscos (ej. `*12345*` — formato Code39 estándar) | `NormalizarCodigo` elimina asteriscos y ceros a la izquierda: `*12345*` → `12345`. La búsqueda se hace sobre el código normalizado. Los datos del Excel también deben pasar por la misma normalización, lo cual ocurre en `BuscarEmpleado` al llamar `NormalizarCodigo(i.CodigoBarras)`. |
| CE-05 | Archivo Excel con filas en blanco entre datos | El `try/catch` en el loop captura el error de `GetValue<int>()` en una celda vacía y salta la fila con `continue`. Las filas en blanco se ignoran. |
| CE-06 | El operador cancela la selección de archivo en `FilePicker` | `resultado == null`, el método retorna sin error ni mensaje. `StatusMessage` permanece en su valor anterior. |
| CE-07 | Exportación con lista vacía | La guarda de `Invitados == null || Count == 0` muestra "❌ No hay datos para exportar" y retorna. |
| CE-08 | Dispositivo reiniciado durante el día con check-ins ya registrados | `asistencia.json` persiste. Al abrir la app y cargar el mismo Excel, `CargarAsistencia` restaura todos los check-ins del día. El sistema es resiliente a reinicios. |
| CE-09 | Empleado con nómina `0` o celda de nómina vacía | `GetValue<int>()` retorna `0`. La búsqueda por nómina normalizando `"0"` puede matchear incorrectamente si se escanea un código que normalice a `"0"`. |
| CE-10 | `ToolbarBorder.Y` retorna `0` antes del primer render en `MostrarPanelDetalle` | El código tiene un guard: `if (maxHeight <= 200) maxHeight = 600`. Evita el crash pero puede mostrar el panel con altura incorrecta en el primer escaneo del día. |

---

## 🧱 Suposiciones Detectadas

| # | Suposición |
|---|------------|
| S1 | El archivo Excel siempre tiene exactamente las tres hojas con los nombres `"TODO EL PERSONAL DE PLANTA"`, `"CON DERECHO A DESPENSA"`, `"PARA RETENER DESPENSA"`. |
| S2 | El código de barras (columna 3 de la hoja principal) es el identificador único primario del empleado para el proceso de check-in. |
| S3 | La primera fila de cada hoja del Excel es un encabezado y debe descartarse (`.Skip(1)`). |
| S4 | El dispositivo es el Unitech PA768 y el intent de escaneo tiene la acción `"unitech.scanservice.data"` con el código en el extra `"text"`. |
| S5 | Un solo operador usa la aplicación a la vez en un solo dispositivo — no hay concurrencia de check-ins. |
| S6 | El día de corte de asistencia es el día calendario actual. No hay mecanismo para distinguir asistencias de días distintos más allá de limpiar manualmente la aplicación. |
| S7 | `Application.Current.MainPage` siempre apunta a una página válida durante el procesamiento de escaneos (no nula, no en transición de navegación). |
| S8 | Los empleados en `"PARA RETENER DESPENSA"` tienen derecho técnico a la despensa pero con un trámite pendiente — no es una negación definitiva del derecho. |
| S9 | La versión del keystore (`GABIRA.keystore`) y sus credenciales están disponibles en el entorno de build. Las credenciales están codificadas en el `.csproj` en texto plano. |

---

## 📈 Recomendaciones Técnicas

### REC-01 — Eliminar el `BindingContext` declarado en XAML
**Prioridad: Alta | Esfuerzo: Bajo**
Remover la declaración `<ContentPage.BindingContext>` de `MainPage.xaml` ya que el ViewModel es asignado desde el code-behind vía DI. Esto elimina la instancia redundante del ViewModel.

### REC-02 — Tipar `CargarAsistencia` correctamente
**Prioridad: Alta | Esfuerzo: Bajo**
Reemplazar `dynamic` con un DTO concreto:
```csharp
record AsistenciaEntry(DateTime Hora, bool Forzado);
var asistencia = JsonConvert.DeserializeObject<Dictionary<int, AsistenciaEntry>>(json);
```
Esto hace el acceso seguro en tiempo de compilación y elimina el riesgo de `RuntimeBinderException`.

### REC-03 — Configurar nombres de hojas Excel de forma externalizable
**Prioridad: Alta | Esfuerzo: Medio**
Mover los nombres de hojas a una clase de configuración o archivo `appsettings` para que el equipo de RH pueda adaptarlos sin recompilar:
```csharp
public static class ExcelConfig
{
    public const string HojaTodoPersonal = "TODO EL PERSONAL DE PLANTA";
    public const string HojaConDespensa  = "CON DERECHO A DESPENSA";
    public const string HojaRetencion    = "PARA RETENER DESPENSA";
}
```

### REC-04 — Eliminar código legacy del ViewModel
**Prioridad: Media | Esfuerzo: Bajo**
Remover `CargarExcelLEGACY`, `ProcesarCodigoResultadoOG`, `BuscarEmpleadoOG`, `LimpiarCodigoEscaneadoOG`, `CargarJson` (no expuesto en UI) y los campos comentados de `OnAppearing`. Reducir el ViewModel de ~600 a ~400 líneas mejora la mantenibilidad.

### REC-05 — Agregar logging estructurado en operaciones críticas
**Prioridad: Media | Esfuerzo: Bajo**
Inyectar `ILogger<MainViewModel>` y loggear los check-ins, registros forzados y errores de parseo de Excel. Actualmente los `catch { continue; }` absorben errores sin traza. Con `ILogger` esto es trivial en MAUI:
```csharp
_logger.LogWarning("Fila {Row} del Excel omitida: {Error}", rowNum, ex.Message);
```

### REC-06 — Proteger credenciales del keystore
**Prioridad: Alta | Esfuerzo: Bajo**
Las contraseñas del keystore (`GABIRA`) están en texto plano en el `.csproj`. Migrarlas a variables de entorno o a un archivo `.properties` excluido del control de versiones:
```xml
<AndroidSigningStorePass>$(KEYSTORE_PASS)</AndroidSigningStorePass>
```

### REC-07 — Consolidar los dos diseños de MainPage
**Prioridad: Media | Esfuerzo: Medio**
Eliminar `MainPage.xaml` (raíz) y `MainPage.xaml.txt`. Si existe un diseño de fallback intencional, documentarlo explícitamente. La coexistencia de múltiples versiones del mismo archivo confunde a futuros desarrolladores.

### REC-08 — Solicitud explícita de permisos en runtime
**Prioridad: Media | Esfuerzo: Bajo**
Agregar solicitud de permisos en `OnAppearing` o al primer uso de cámara/almacenamiento, usando la API de MAUI:
```csharp
var status = await Permissions.RequestAsync<Permissions.Camera>();
```

### REC-09 — Separar la lógica de exportación
**Prioridad: Baja | Esfuerzo: Alto**
El método `GenerarXLSX` tiene ~200 líneas dentro del ViewModel. Extraer los generadores de reporte a una clase `ReporteService` con interfaz `IReporteService` para facilitar pruebas unitarias y reutilización.

### REC-10 — Agregar feedback visual cuando el escaneo no encuentra empleado
**Prioridad: Baja | Esfuerzo: Bajo**
Actualmente un escaneo inválido solo actualiza `StatusMessage`. Considerar un `DisplayAlert` o vibración diferenciada (patrón doble) para comunicar el error al operador sin que tenga que mirar la pantalla.

---

## 🧾 Resumen Ejecutivo

CheckInApp es una aplicación Android para tablets/dispositivos industriales que permite al departamento de Recursos Humanos controlar en tiempo real quién recibe su despensa mensual durante el evento de distribución.

**El proceso funciona así:** El responsable de RH carga el archivo Excel oficial del mes, que contiene la lista completa del personal y qué empleados tienen derecho a despensa. Cuando llega cada trabajador, el operador escanea su gafete con el lector de código de barras del dispositivo. La aplicación verifica al instante si tiene derecho, registra la hora de llegada y actualiza los contadores en pantalla.

**Casos especiales:** Si un empleado tiene un trámite médico pendiente o formalmente no tiene derecho a la despensa, el sistema alerta al operador con una pantalla de confirmación — el operador puede autorizar o rechazar el acceso manualmente. Estos casos quedan marcados como "registro forzado" y aparecen resaltados en los reportes para revisión posterior por la gerencia.

**Al final del evento**, el responsable de RH puede exportar un reporte completo en Excel, PDF-texto, CSV o JSON, con el detalle de quién asistió, a qué hora, si tiene derecho, y los casos excepcionales que requieren revisión.

**La aplicación trabaja completamente sin internet**, guarda los registros automáticamente en el dispositivo y los conserva aunque la aplicación se cierre o el dispositivo se reinicie durante el evento.

Los principales puntos de atención para el equipo técnico son: la dependencia crítica de que el archivo Excel mantenga exactamente los nombres de sus hojas, la necesidad de limpiar código obsoleto que podría confundir a futuros mantenedores, y asegurar que las credenciales de firma de la aplicación no estén expuestas en el código fuente.

---

*Documentación generada mediante análisis estático del código fuente. Versión de referencia: CheckInApp v2.2 (versionCode 22).*
