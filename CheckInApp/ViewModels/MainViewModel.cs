using CheckInApp.Converters;
using CheckInApp.Models;
using ClosedXML.Excel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.Maui.ApplicationModel.DataTransfer;
using Microsoft.Maui.Media;
using Newtonsoft.Json;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Text;
using System.Windows.Input;

namespace CheckInApp.ViewModels
{
    public partial class MainViewModel : ObservableObject
    {
        // ─── Constantes de empresa ───────────────────────────────────────
        private const string NombreEmpresa = "Comercializadora GAB S.A. DE C.V.";
        private const string DepartamentoRH = "Departamento de Recursos Humanos";
        private const string SistemaVersion = "CheckInApp v2.1";

        private readonly string _dataFilePath;
        private readonly string _asistenciaFilePath;

        [ObservableProperty] private ObservableCollection<QrResult> invitados;
        [ObservableProperty] private ObservableCollection<QrResult> invitadosFiltrados;
        [ObservableProperty] private QrResult invitadoSeleccionado;
        [ObservableProperty] private bool isLoading;
        [ObservableProperty] private string statusMessage;
        [ObservableProperty] private bool isScanning;
        [ObservableProperty] private bool mostrarDetalles;
        [ObservableProperty] private int totalInvitados;
        [ObservableProperty] private int totalAsistentes;
        [ObservableProperty] private string porcentajeAsistencia;
        [ObservableProperty] private string porcentajeAsistenciaDecimal;
        [ObservableProperty] private string textoBusqueda;
        [ObservableProperty] private string filtroSeleccionado;

        public MainViewModel()
        {
            _dataFilePath = Path.Combine(FileSystem.AppDataDirectory, "invitados.json");
            _asistenciaFilePath = Path.Combine(FileSystem.AppDataDirectory, "asistencia.json");

            Invitados = new ObservableCollection<QrResult>();
            InvitadosFiltrados = new ObservableCollection<QrResult>();
            FiltroSeleccionado = "Todos";

            CargarDatos();
        }

        partial void OnInvitadoSeleccionadoChanged(QrResult value)
            => MostrarDetalles = value != null;

        partial void OnInvitadosChanged(ObservableCollection<QrResult> value)
        {
            ActualizarEstadisticas();
            ActualizarListaFiltrada();
        }

        partial void OnTextoBusquedaChanged(string value) => FiltrarInvitados();
        partial void OnFiltroSeleccionadoChanged(string value) => FiltrarInvitados();

        // ════════════════════════════════════════════════════════════════
        //  CARGA DE EXCEL
        // ════════════════════════════════════════════════════════════════
        [RelayCommand]
        private async Task CargarExcel()
        {
            try
            {
                IsLoading = true;
                StatusMessage = "Cargando archivo Excel...";

                var resultado = await FilePicker.PickAsync(new PickOptions
                {
                    PickerTitle = "Selecciona el archivo Excel",
                    FileTypes = new FilePickerFileType(new Dictionary<DevicePlatform, IEnumerable<string>>
            {
                { DevicePlatform.WinUI,    new[] { ".xlsx", ".xls" } },
                { DevicePlatform.macOS,    new[] { ".xlsx", ".xls" } },
                { DevicePlatform.Android,  new[] { "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet" } },
                { DevicePlatform.iOS,      new[] { "org.openxmlformats.spreadsheetml.sheet" } }
            })
                });

                if (resultado != null)
                {
                    using var stream = await resultado.OpenReadAsync();
                    using var workbook = new XLWorkbook(stream);

                    // Nombres actualizados de las hojas
                    var hojaTodoPersonal = workbook.Worksheet("TODO EL PERSONAL DE PLANTA");
                    var hojaConDespensa = workbook.Worksheet("CON DERECHO A DESPENSA");
                    var hojaRetenerDespensa = workbook.Worksheet("PARA RETENER DESPENSA");

                    if (hojaTodoPersonal == null || hojaConDespensa == null)
                    {
                        StatusMessage = "❌ Error: No se encontraron las hojas requeridas en el Excel";
                        return;
                    }

                    // Códigos con derecho pleno
                    var codigosConDespensa = new HashSet<string>();
                    foreach (var row in hojaConDespensa.RowsUsed().Skip(1))
                    {
                        var cb = row.Cell(3).GetString()?.Trim();
                        var nombre = row.Cell(4).GetString()?.Trim();
                        if (!string.IsNullOrWhiteSpace(cb) &&
                            !cb.ToUpper().Contains("CODIGO") &&
                            !string.IsNullOrWhiteSpace(nombre) &&
                            !nombre.ToUpper().Contains("NOMBRE"))
                            codigosConDespensa.Add(cb);
                    }

                    // Códigos con derecho pero con retención por examen médico pendiente
                    var codigosRetencion = new HashSet<string>();
                    if (hojaRetenerDespensa != null)
                    {
                        foreach (var row in hojaRetenerDespensa.RowsUsed().Skip(1))
                        {
                            var cb = row.Cell(3).GetString()?.Trim();
                            var nombre = row.Cell(4).GetString()?.Trim();
                            if (!string.IsNullOrWhiteSpace(cb) &&
                                !cb.ToUpper().Contains("CODIGO") &&
                                !string.IsNullOrWhiteSpace(nombre) &&
                                !nombre.ToUpper().Contains("NOMBRE"))
                                codigosRetencion.Add(cb);
                        }
                    }

                    var data = new List<QrResult>();
                    foreach (var row in hojaTodoPersonal.RowsUsed().Skip(1))
                    {
                        try
                        {
                            var nomina = row.Cell(1).GetValue<int>();
                            var codigo = row.Cell(2).GetFormattedString().Trim();
                            var codigoBarras = row.Cell(3).GetFormattedString().Trim();
                            var nombre = row.Cell(4).GetString()?.Trim();
                            var supervisor = row.Cell(5).GetString()?.Trim();
                            var departamento = row.Cell(6).GetString()?.Trim();

                            // Ya no existe columna 7 de examen médico; se deriva de las listas
                            if (string.IsNullOrWhiteSpace(nombre) ||
                                nombre.ToUpper().Contains("NOMBRE") ||
                                codigoBarras?.ToUpper().Contains("CODIGO") == true)
                                continue;

                            bool tieneDerecho = codigosConDespensa.Contains(codigoBarras)
                                                || codigosRetencion.Contains(codigoBarras);
                            bool examenMedicoOk = !codigosRetencion.Contains(codigoBarras);

                            data.Add(new QrResult
                            {
                                Nomina = nomina,
                                Codigo = codigo,
                                CodigoBarras = codigoBarras,
                                Nombre = nombre,
                                Supervisor = supervisor,
                                Departamento = departamento,
                                ExamenMedico = examenMedicoOk,
                                TieneDerechoADespensa = tieneDerecho,
                                // Propiedad opcional para indicar si requiere forzar registro
                                // (pendiente de examen médico)
                                RetencionPendiente = codigosRetencion.Contains(codigoBarras),
                                FileName = resultado.FileName,
                                Timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")
                            });
                        }
                        catch { continue; }
                    }

                    await CargarAsistencia(data);

                    Invitados.Clear();
                    foreach (var item in data.OrderBy(i => i.Nombre))
                        Invitados.Add(item);

                    var conDespensa = data.Count(x => x.TieneDerechoADespensa);
                    var sinDespensa = data.Count(x => !x.TieneDerechoADespensa);
                    var pendientesMedico = data.Count(x => x.RetencionPendiente);
                    StatusMessage = $"✅ {Invitados.Count} empleados cargados | Con despensa: {conDespensa} | Sin despensa: {sinDespensa} | Pendientes médico: {pendientesMedico}";
                    FiltrarInvitados();
                }
            }
            catch (Exception ex) { StatusMessage = $"❌ Error: {ex.Message}"; }
            finally { IsLoading = false; }
        }
        private async Task CargarExcelLEGACY()
        {
            try
            {
                IsLoading = true;
                StatusMessage = "Cargando archivo Excel...";

                var resultado = await FilePicker.PickAsync(new PickOptions
                {
                    PickerTitle = "Selecciona el archivo Excel",
                    FileTypes = new FilePickerFileType(new Dictionary<DevicePlatform, IEnumerable<string>>
                    {
                        { DevicePlatform.WinUI,    new[] { ".xlsx", ".xls" } },
                        { DevicePlatform.macOS,    new[] { ".xlsx", ".xls" } },
                        { DevicePlatform.Android,  new[] { "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet" } },
                        { DevicePlatform.iOS,      new[] { "org.openxmlformats.spreadsheetml.sheet" } }
                    })
                });

                if (resultado != null)
                {
                    using var stream = await resultado.OpenReadAsync();
                    using var workbook = new XLWorkbook(stream);

                    var hojaTodoPersonal = workbook.Worksheet("TODO EL PERSONAL DE PLANTA ");
                    var hojaConDespensa = workbook.Worksheet("PERSONAL CON DERECHO A DESPENSA");

                    if (hojaTodoPersonal == null || hojaConDespensa == null)
                    {
                        StatusMessage = "❌ Error: No se encontraron las hojas requeridas en el Excel";
                        return;
                    }

                    var codigosConDespensa = new HashSet<string>();
                    foreach (var row in hojaConDespensa.RowsUsed().Skip(1))
                    {
                        var cb = row.Cell(3).GetString()?.Trim();
                        var nombre = row.Cell(4).GetString()?.Trim();
                        if (!string.IsNullOrWhiteSpace(cb) &&
                            !cb.ToUpper().Contains("CODIGO") &&
                            !string.IsNullOrWhiteSpace(nombre) &&
                            !nombre.ToUpper().Contains("NOMBRE"))
                            codigosConDespensa.Add(cb);
                    }

                    var data = new List<QrResult>();
                    foreach (var row in hojaTodoPersonal.RowsUsed().Skip(1))
                    {
                        try
                        {
                            var nomina = row.Cell(1).GetValue<int>();
                            //var codigo = row.Cell(2).GetString()?.Trim();
                            //var codigoBarras = row.Cell(3).GetString()?.Trim();
                            var codigo = row.Cell(2).GetFormattedString().Trim();
                            var codigoBarras = row.Cell(3).GetFormattedString().Trim();
                            var nombre = row.Cell(4).GetString()?.Trim();
                            var supervisor = row.Cell(5).GetString()?.Trim();
                            var departamento = row.Cell(6).GetString()?.Trim();
                            var examenMedico = row.Cell(7).GetString()?.Trim().ToUpper() == "OK";

                            if (string.IsNullOrWhiteSpace(nombre) ||
                                nombre.ToUpper().Contains("NOMBRE") ||
                                codigoBarras?.ToUpper().Contains("CODIGO") == true)
                                continue;

                            data.Add(new QrResult
                            {
                                Nomina = nomina,
                                Codigo = codigo,
                                CodigoBarras = codigoBarras,
                                Nombre = nombre,
                                Supervisor = supervisor,
                                Departamento = departamento,
                                ExamenMedico = examenMedico,
                                TieneDerechoADespensa = codigosConDespensa.Contains(codigoBarras),
                                FileName = resultado.FileName,
                                Timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")
                            });
                        }
                        catch { continue; }
                    }

                    await CargarAsistencia(data);

                    Invitados.Clear();
                    foreach (var item in data.OrderBy(i => i.Nombre))
                        Invitados.Add(item);

                    var conDespensa = data.Count(x => x.TieneDerechoADespensa);
                    var sinDespensa = data.Count(x => !x.TieneDerechoADespensa);
                    StatusMessage = $"✅ {Invitados.Count} empleados cargados | Con despensa: {conDespensa} | Sin despensa: {sinDespensa}";
                    FiltrarInvitados();
                }
            }
            catch (Exception ex) { StatusMessage = $"❌ Error: {ex.Message}"; }
            finally { IsLoading = false; }
        }

        // ════════════════════════════════════════════════════════════════
        //  SCANNER
        // ════════════════════════════════════════════════════════════════
        private string LimpiarCodigoEscaneado(string codigo)
        {
            if (string.IsNullOrWhiteSpace(codigo))
                return string.Empty;

            codigo = codigo.Trim();

            var chars = new[] { '\n', '\r', '\t', '\0', (char)3, (char)4 };
            foreach (var c in chars)
                codigo = codigo.Replace(c.ToString(), "");

            string[] prefijos = { "]C1", "]A0", "]E0", "[)>" };
            foreach (var p in prefijos)
                if (codigo.StartsWith(p))
                    codigo = codigo.Substring(p.Length);

            return codigo.Trim();
        }
        private string NormalizarCodigo(string valor)
        {
            if (string.IsNullOrWhiteSpace(valor))
                return "";

            valor = valor.Replace("*", "").Trim();

            valor = valor.TrimStart('0');

            if (string.IsNullOrEmpty(valor))
                valor = "0";

            return valor;
        }
        private QrResult BuscarEmpleado(string codigo)
        {
            if (string.IsNullOrWhiteSpace(codigo))
                return null;

            codigo = NormalizarCodigo(codigo);

            return Invitados.FirstOrDefault(i =>
                NormalizarCodigo(i.CodigoBarras) == codigo ||
                NormalizarCodigo(i.Codigo) == codigo ||
                NormalizarCodigo(i.Nomina.ToString()) == codigo
            );
        }
        [RelayCommand]
        private async Task ProcesarCodigoResultado(string resultado)
        {
            try
            {
                IsScanning = false;

                var codigo = LimpiarCodigoEscaneado(resultado);
                var empleado = BuscarEmpleado(codigo);

                if (empleado == null)
                {
                    StatusMessage = $"❌ Empleado no encontrado: {codigo}";
                    return;
                }

                #region EXAMEN MEDICO
                if (!empleado.ExamenMedico)
                {
                    bool continuar = await Application.Current.MainPage.DisplayAlert(
                        "⚠️ EXAMEN MÉDICO PENDIENTE",
                        $"El empleado:\n\n👤 {empleado.Nombre}\n🏢 {empleado.Departamento}\n📋 Nómina: {empleado.Nomina}\n\n" +
                        "NO ha realizado su examen médico anual.\n\n" +
                        "Este personal SÍ tiene derecho a despensa, sin embargo tiene un trámite pendiente con Servicio Médico.\n\n" +
                        "¿Deseas forzar el registro?",
                        "Sí, registrar", "No, cancelar");

                    if (!continuar)
                    {
                        StatusMessage = $"⛔ Registro cancelado para {empleado.Nombre} (Examen médico pendiente)";
                        return;
                    }

                    empleado.Asistio = true;
                    empleado.HoraCheckIn = DateTime.Now;
                    empleado.RegistroForzado = true;

                    await GuardarAsistencia();
                    ActualizarEstadisticas();

                    StatusMessage = $"⚠️ {empleado.Nombre} registrado (EXAMEN MÉDICO PENDIENTE — FORZADO)";

                    InvitadoSeleccionado = empleado;

                    Vibration.Vibrate(TimeSpan.FromMilliseconds(200));

                    FiltrarInvitados();
                    return; // 🔴 IMPORTANTE: cortar flujo para que no siga a otras validaciones
                }
                #endregion


                if (!empleado.TieneDerechoADespensa)
                {
                    bool continuar = await Application.Current.MainPage.DisplayAlert(
                        "⚠️ SIN DERECHO A DESPENSA",
                        $"El empleado:\n\n👤 {empleado.Nombre}\n🏢 {empleado.Departamento}\n📋 Nómina: {empleado.Nomina}\n\n" +
                        "NO tiene derecho a recibir despensa.\n\n¿Deseas registrar su asistencia de todos modos?",
                        "Sí, registrar", "No, cancelar");

                    if (!continuar)
                    {
                        StatusMessage = $"⛔ Check-in cancelado para {empleado.Nombre}";
                        return;
                    }

                    empleado.Asistio = true;
                    empleado.HoraCheckIn = DateTime.Now;
                    empleado.RegistroForzado = true;

                    await GuardarAsistencia();
                    ActualizarEstadisticas();

                    StatusMessage = $"⚠️ {empleado.Nombre} registrado (SIN DERECHO — FORZADO)";
                }
                else
                {
                    if (empleado.Asistio)
                    {
                        StatusMessage = $"⚠️ {empleado.Nombre} ya registró a las {empleado.HoraCheckIn:HH:mm}";
                        InvitadoSeleccionado = empleado;
                        return;
                    }

                    empleado.Asistio = true;
                    empleado.HoraCheckIn = DateTime.Now;

                    await GuardarAsistencia();
                    ActualizarEstadisticas();

                    StatusMessage = $"✅ {empleado.Nombre} | {empleado.Departamento} | CON DERECHO";
                }

                InvitadoSeleccionado = empleado;

                Vibration.Vibrate(TimeSpan.FromMilliseconds(200));

                FiltrarInvitados();
            }
            catch (Exception ex)
            {
                StatusMessage = $"❌ Error: {ex.Message}";
            }
        }
        private string LimpiarCodigoEscaneadoOG(string codigo)
        {
            if (string.IsNullOrWhiteSpace(codigo)) return string.Empty;
            var chars = new[] { '\n', '\r', '\t', '\0', (char)3, (char)4 };
            var limpio = codigo.Trim();
            foreach (var c in chars) limpio = limpio.Replace(c.ToString(), "");
            foreach (var p in new[] { "]C1", "]A0", "]E0", "[)>" })
                if (limpio.StartsWith(p)) limpio = limpio.Substring(p.Length);
            return limpio.Trim();
        }

        private QrResult BuscarEmpleadoOG(string codigo)
        {
            if (string.IsNullOrWhiteSpace(codigo)) return null;
            var emp = Invitados.FirstOrDefault(i =>
                i.CodigoBarras == codigo || i.Codigo == codigo || i.Nomina.ToString() == codigo);
            if (emp != null) return emp;
            var sin = codigo.Replace("*", "");
            emp = Invitados.FirstOrDefault(i =>
                i.CodigoBarras?.Replace("*", "") == sin || i.Codigo?.Replace("*", "") == sin);
            if (emp != null) return emp;
            emp = Invitados.FirstOrDefault(i =>
                codigo.Contains(i.CodigoBarras?.Replace("*", "") ?? "") ||
                codigo.Contains(i.Codigo?.Replace("*", "") ?? "") ||
                codigo.Contains(i.Nomina.ToString()));
            if (emp != null) return emp;
            return Invitados.FirstOrDefault(i =>
                i.CodigoBarras?.Contains(codigo) == true ||
                i.Codigo?.Contains(codigo) == true);
        }

        [RelayCommand]
        private async Task ProcesarCodigoResultadoOG(string resultado)
        {
            try
            {
                IsScanning = false;
                var codigo = LimpiarCodigoEscaneado(resultado);
                var empleado = BuscarEmpleado(codigo);

                if (empleado == null)
                {
                    StatusMessage = $"❌ Empleado no encontrado: {codigo}";
                    return;
                }

                if (!empleado.TieneDerechoADespensa)
                {
                    bool continuar = await Application.Current.MainPage.DisplayAlert(
                        "⚠️ SIN DERECHO A DESPENSA",
                        $"El empleado:\n\n👤 {empleado.Nombre}\n🏢 {empleado.Departamento}\n📋 Nómina: {empleado.Nomina}\n\n" +
                        "NO tiene derecho a recibir despensa.\n\n¿Deseas registrar su asistencia de todos modos?",
                        "Sí, registrar", "No, cancelar");

                    if (!continuar)
                    {
                        StatusMessage = $"⛔ Check-in cancelado para {empleado.Nombre}";
                        return;
                    }
                    empleado.Asistio = true;
                    empleado.HoraCheckIn = DateTime.Now;
                    empleado.RegistroForzado = true;
                    await GuardarAsistencia();
                    ActualizarEstadisticas();
                    StatusMessage = $"⚠️ {empleado.Nombre} registrado (SIN DERECHO — FORZADO)";
                }
                else
                {
                    if (empleado.Asistio)
                    {
                        StatusMessage = $"⚠️ {empleado.Nombre} ya registró a las {empleado.HoraCheckIn:HH:mm}";
                        InvitadoSeleccionado = empleado;
                        return;
                    }
                    empleado.Asistio = true;
                    empleado.HoraCheckIn = DateTime.Now;
                    await GuardarAsistencia();
                    ActualizarEstadisticas();
                    StatusMessage = $"✅ {empleado.Nombre} | {empleado.Departamento} | CON DERECHO";
                }

                InvitadoSeleccionado = empleado;
                Vibration.Vibrate(TimeSpan.FromMilliseconds(200));
                FiltrarInvitados();
            }
            catch (Exception ex) { StatusMessage = $"❌ Error: {ex.Message}"; }
        }

        [RelayCommand] private async Task EscanearQR() { IsScanning = true; StatusMessage = "Escaneando..."; }

        // ════════════════════════════════════════════════════════════════
        //  PERSISTENCIA
        // ════════════════════════════════════════════════════════════════
        private async Task GuardarAsistencia()
        {
            try
            {
                var data = Invitados.Where(i => i.Asistio)
                    .ToDictionary(i => i.Nomina, i => new
                    {
                        Hora = i.HoraCheckIn ?? DateTime.Now,
                        Forzado = i.RegistroForzado
                    });
                await File.WriteAllTextAsync(_asistenciaFilePath,
                    JsonConvert.SerializeObject(data, Formatting.Indented));
            }
            catch { }
        }

        private async Task CargarAsistencia(List<QrResult> lista)
        {
            if (!File.Exists(_asistenciaFilePath)) return;
            try
            {
                var json = await File.ReadAllTextAsync(_asistenciaFilePath);
                var asistencia = JsonConvert.DeserializeObject<Dictionary<int, dynamic>>(json);
                foreach (var emp in lista)
                    if (asistencia.TryGetValue(emp.Nomina, out var d))
                    {
                        emp.Asistio = true;
                        emp.HoraCheckIn = d.Hora;
                        emp.RegistroForzado = d.Forzado;
                    }
            }
            catch { }
        }

        [RelayCommand]
        private async Task CargarDatos()
        {
            try
            {
                if (File.Exists(_dataFilePath))
                {
                    var json = await File.ReadAllTextAsync(_dataFilePath);
                    var data = JsonConvert.DeserializeObject<List<QrResult>>(json);
                    await CargarAsistencia(data);
                    Invitados.Clear();
                    foreach (var item in data.OrderBy(i => i.Data?.Nombre))
                        Invitados.Add(item);
                    StatusMessage = $"📋 {Invitados.Count} empleados cargados";
                    ActualizarEstadisticas();
                    FiltrarInvitados();
                }
                else StatusMessage = "Presiona 'Cargar' para comenzar";
            }
            catch (Exception ex) { StatusMessage = $"Error: {ex.Message}"; }
        }

        // ════════════════════════════════════════════════════════════════
        //  FILTROS Y BÚSQUEDA
        // ════════════════════════════════════════════════════════════════
        private void ActualizarListaFiltrada()
        {
            InvitadosFiltrados ??= new ObservableCollection<QrResult>();
            InvitadosFiltrados.Clear();
            if (Invitados == null) return;
            foreach (var i in Invitados) InvitadosFiltrados.Add(i);
        }

        private void FiltrarInvitados()
        {
            try
            {
                if (Invitados == null || Invitados.Count == 0)
                {
                    InvitadosFiltrados?.Clear();
                    return;
                }
                InvitadosFiltrados ??= new ObservableCollection<QrResult>();

                IEnumerable<QrResult> r = FiltroSeleccionado switch
                {
                    "Presentes" => Invitados.Where(i => i.Asistio),
                    "Ausentes" => Invitados.Where(i => !i.Asistio),
                    _ => Invitados
                };

                if (!string.IsNullOrWhiteSpace(TextoBusqueda))
                {
                    var q = TextoBusqueda.ToLower();
                    r = r.Where(i =>
                        i.Nombre?.ToLower().Contains(q) == true ||
                        i.Departamento?.ToLower().Contains(q) == true ||
                        i.Supervisor?.ToLower().Contains(q) == true ||
                        i.Nomina.ToString().Contains(q) ||
                        i.CodigoBarras?.ToLower().Contains(q) == true);
                }

                InvitadosFiltrados.Clear();
                foreach (var i in r.OrderBy(i => i.Nombre)) InvitadosFiltrados.Add(i);
            }
            catch (Exception ex) { Console.WriteLine($"FiltrarInvitados: {ex.Message}"); }
        }

        private void ActualizarEstadisticas()
        {
            TotalInvitados = Invitados.Count;
            TotalAsistentes = Invitados.Count(i => i.Asistio);
            double pct = TotalInvitados > 0 ? (double)TotalAsistentes / TotalInvitados * 100 : 0;
            PorcentajeAsistencia = $"{pct:F0}%";
            PorcentajeAsistenciaDecimal = $"{pct:F3}%";
        }

        [RelayCommand] private void FiltrarTodos() { FiltroSeleccionado = "Todos"; }
        [RelayCommand] private void FiltrarPresentes() { FiltroSeleccionado = "Presentes"; }
        [RelayCommand] private void FiltrarAusentes() { FiltroSeleccionado = "Ausentes"; }
        [RelayCommand] private void LimpiarBusqueda() { TextoBusqueda = string.Empty; FiltroSeleccionado = "Todos"; }
        [RelayCommand] private void LimpiarResultado() { InvitadoSeleccionado = null; StatusMessage = "Listo para escanear"; }

        [RelayCommand]
        private async Task LimpiarAplicacion()
        {
            bool ok = await Application.Current.MainPage.DisplayAlert(
                "⚠️ Limpiar Aplicación",
                "¿Estás seguro? Se eliminarán todos los datos locales.",
                "SÍ, LIMPIAR", "CANCELAR");
            if (!ok) { StatusMessage = "Operación cancelada"; return; }

            IsLoading = true;
            Invitados.Clear();
            InvitadosFiltrados?.Clear();
            if (File.Exists(_dataFilePath)) File.Delete(_dataFilePath);
            if (File.Exists(_asistenciaFilePath)) File.Delete(_asistenciaFilePath);
            InvitadoSeleccionado = null;
            MostrarDetalles = false;
            IsScanning = false;
            TextoBusqueda = string.Empty;
            FiltroSeleccionado = "Todos";
            ActualizarEstadisticas();
            Vibration.Vibrate(TimeSpan.FromMilliseconds(300));
            StatusMessage = "✅ Aplicación limpiada correctamente";
            IsLoading = false;
        }

        // ════════════════════════════════════════════════════════════════
        //  EXPORTAR REPORTE — Despacho de formatos
        // ════════════════════════════════════════════════════════════════
        [RelayCommand]
        private async Task ExportarReporte()
        {
            try
            {
                if (Invitados == null || Invitados.Count == 0)
                {
                    StatusMessage = "❌ No hay datos para exportar";
                    return;
                }

                IsLoading = true;
                StatusMessage = "Preparando reporte...";

                var formato = await Application.Current.MainPage.DisplayActionSheet(
                    "Selecciona el formato de exportación",
                    "Cancelar", null,
                    "📊  Excel (.xlsx) — Reporte completo con formato",
                    "📋  TXT — Reporte imprimible",
                    "📄  CSV — Datos para análisis",
                    "🔷  JSON — Datos estructurados");

                if (string.IsNullOrEmpty(formato) || formato == "Cancelar")
                {
                    StatusMessage = "Exportación cancelada";
                    IsLoading = false;
                    return;
                }

                string archivo = formato switch
                {
                    var f when f.Contains("xlsx") || f.Contains("Excel") => await GenerarXLSX(),
                    var f when f.Contains("TXT") => await GenerarTXT(),
                    var f when f.Contains("CSV") => await GenerarCSV(),
                    var f when f.Contains("JSON") => await GenerarJSON(),
                    _ => string.Empty
                };

                if (!string.IsNullOrEmpty(archivo))
                {
                    await CompartirArchivo(archivo);
                    StatusMessage = "✅ Reporte exportado exitosamente";
                }
            }
            catch (Exception ex) { StatusMessage = $"❌ Error al exportar: {ex.Message}"; }
            finally { IsLoading = false; }
        }

        // ════════════════════════════════════════════════════════════════
        //  REPORTE EXCEL (.xlsx) — Formato profesional HR con ClosedXML
        // ════════════════════════════════════════════════════════════════
        private async Task<string> GenerarXLSX()
        {
            return await Task.Run(() =>
            {
                using var wb = new XLWorkbook();

                // ── Paleta corporativa ──────────────────────────────────
                var azulOscuro = XLColor.FromHtml("#0F2E4D");
                var azulMedio = XLColor.FromHtml("#1A4B7A");
                var azulClaro = XLColor.FromHtml("#DBEAFE");
                var naranja = XLColor.FromHtml("#E07B1A");
                var naranjaBg = XLColor.FromHtml("#FEF3C7");
                var verdeOscuro = XLColor.FromHtml("#15803D");
                var verdeBg = XLColor.FromHtml("#DCFCE7");
                var rojoBg = XLColor.FromHtml("#FEE2E2");
                var rojoOscuro = XLColor.FromHtml("#B91C1C");
                var grisClaro = XLColor.FromHtml("#F8FAFC");
                var grisBorde = XLColor.FromHtml("#E2E8F0");
                var grisMedio = XLColor.FromHtml("#64748B");

                var fecha = DateTime.Now;
                var asistentes = Invitados.Count(x => x.Asistio);
                var conDer = Invitados.Count(x => x.TieneDerechoADespensa);
                var sinDer = Invitados.Count(x => !x.TieneDerechoADespensa);
                var forzados = Invitados.Count(x => x.RegistroForzado);
                double pct = Invitados.Count > 0 ? (double)asistentes / Invitados.Count * 100 : 0;

                // ════════════════════════════════════════════════════
                //  HOJA 1: REPORTE DETALLADO
                // ════════════════════════════════════════════════════
                var ws = wb.Worksheets.Add("Reporte de Asistencia");
                ws.ShowGridLines = false;
                ws.PageSetup.PaperSize = XLPaperSize.LetterPaper;
                ws.PageSetup.PageOrientation = XLPageOrientation.Landscape;
                ws.PageSetup.Margins.Left = 0.5;
                ws.PageSetup.Margins.Right = 0.5;

                int fila = 1;

                // ── Bloque de encabezado corporativo ────────────────
                ws.Row(fila).Height = 14;
                fila++;

                // Empresa
                ws.Cell(fila, 1).Value = NombreEmpresa;
                ws.Range(fila, 1, fila, 10).Merge();
                var cEmpresa = ws.Cell(fila, 1);
                cEmpresa.Style.Font.Bold = true;
                cEmpresa.Style.Font.FontSize = 14;
                cEmpresa.Style.Font.FontColor = azulOscuro;
                cEmpresa.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Left;
                fila++;

                // Departamento
                ws.Cell(fila, 1).Value = DepartamentoRH;
                ws.Range(fila, 1, fila, 10).Merge();
                var cDepto = ws.Cell(fila, 1);
                cDepto.Style.Font.FontSize = 10;
                cDepto.Style.Font.FontColor = grisMedio;
                fila++;

                // Título del reporte
                ws.Cell(fila, 1).Value = "REPORTE DE CONTROL DE ASISTENCIA Y DESPENSA";
                ws.Range(fila, 1, fila, 10).Merge();
                var cTitulo = ws.Cell(fila, 1);
                cTitulo.Style.Font.Bold = true;
                cTitulo.Style.Font.FontSize = 13;
                cTitulo.Style.Font.FontColor = azulMedio;
                fila++;

                // Fecha y hora
                ws.Cell(fila, 1).Value = $"Generado: {fecha:dd/MM/yyyy  HH:mm:ss}     Sistema: {SistemaVersion}";
                ws.Range(fila, 1, fila, 10).Merge();
                ws.Cell(fila, 1).Style.Font.FontSize = 9;
                ws.Cell(fila, 1).Style.Font.FontColor = grisMedio;
                fila += 2;

                // ── KPI cards en fila horizontal ────────────────────
                void KpiCell(int col, string etiqueta, string valor, XLColor fondo, XLColor colorValor)
                {
                    ws.Cell(fila, col).Value = etiqueta;
                    ws.Cell(fila, col).Style.Font.FontSize = 8;
                    ws.Cell(fila, col).Style.Font.FontColor = grisMedio;
                    ws.Cell(fila, col).Style.Font.Bold = true;
                    ws.Cell(fila, col).Style.Fill.BackgroundColor = fondo;
                    ws.Cell(fila, col).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

                    ws.Cell(fila + 1, col).Value = valor;
                    ws.Cell(fila + 1, col).Style.Font.FontSize = 16;
                    ws.Cell(fila + 1, col).Style.Font.Bold = true;
                    ws.Cell(fila + 1, col).Style.Font.FontColor = colorValor;
                    ws.Cell(fila + 1, col).Style.Fill.BackgroundColor = fondo;
                    ws.Cell(fila + 1, col).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

                    ws.Range(fila, col, fila + 1, col + 1).Merge().Style
                        .Border.OutsideBorder = XLBorderStyleValues.Thin;
                    ws.Range(fila, col, fila + 1, col + 1).Style
                        .Border.OutsideBorderColor = grisBorde;
                }
                // Re-hacemos las celdas de KPI sin merge (merge ya no aplica en dos filas independientes)
                void KpiBlock(int col, string etiqueta, string valor, XLColor fondo, XLColor colorValor)
                {
                    var rLabel = ws.Range(fila, col, fila, col + 1);
                    rLabel.Merge();
                    rLabel.FirstCell().Value = etiqueta;
                    rLabel.Style.Font.FontSize = 8;
                    rLabel.Style.Font.Bold = true;
                    rLabel.Style.Font.FontColor = grisMedio;
                    rLabel.Style.Fill.BackgroundColor = fondo;
                    rLabel.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                    rLabel.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
                    rLabel.Style.Border.OutsideBorderColor = grisBorde;

                    var rVal = ws.Range(fila + 1, col, fila + 1, col + 1);
                    rVal.Merge();
                    rVal.FirstCell().Value = valor;
                    rVal.Style.Font.FontSize = 18;
                    rVal.Style.Font.Bold = true;
                    rVal.Style.Font.FontColor = colorValor;
                    rVal.Style.Fill.BackgroundColor = fondo;
                    rVal.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                    rVal.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
                    rVal.Style.Border.OutsideBorderColor = grisBorde;
                }

                ws.Row(fila).Height = 18;
                ws.Row(fila + 1).Height = 32;

                KpiBlock(1, "TOTAL EMPLEADOS", Invitados.Count.ToString(), grisClaro, azulMedio);
                KpiBlock(3, "PRESENTES", asistentes.ToString(), verdeBg, verdeOscuro);
                KpiBlock(5, "AUSENTES", (Invitados.Count - asistentes).ToString(), rojoBg, rojoOscuro);
                KpiBlock(7, "% ASISTENCIA", $"{pct:F1}%", azulClaro, azulMedio);
                KpiBlock(9, "REGISTROS FORZADOS", forzados.ToString(), naranjaBg, naranja);

                fila += 4;

                // ── Tabla de encabezados ─────────────────────────────
                var headers = new[]
                {
                    "NÓMINA", "NOMBRE COMPLETO", "DEPARTAMENTO", "SUPERVISOR",
                    "ASISTIÓ", "HORA CHECK-IN", "DERECHO DESPENSA", "ESTADO", "OBSERVACIONES"
                };
                int[] anchos = { 10, 32, 22, 22, 10, 14, 18, 14, 28 };

                for (int col = 0; col < headers.Length; col++)
                {
                    var c = ws.Cell(fila, col + 1);
                    c.Value = headers[col];
                    c.Style.Font.Bold = true;
                    c.Style.Font.FontColor = XLColor.White;
                    c.Style.Font.FontSize = 10;
                    c.Style.Fill.BackgroundColor = azulMedio;
                    c.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                    c.Style.Alignment.Vertical = XLAlignmentVerticalValues.Distributed;
                    c.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
                    c.Style.Border.OutsideBorderColor = azulOscuro;
                    ws.Column(col + 1).Width = anchos[col];
                }
                ws.Row(fila).Height = 22;
                fila++;

                // ── Filas de datos ───────────────────────────────────
                int filaInicio = fila;
                bool alternar = false;

                foreach (var emp in Invitados.OrderBy(e => e.Nomina))
                {
                    var bgFila = alternar ? grisClaro : XLColor.White;

                    // Si no tiene derecho y asistió: fondo ámbar
                    if (emp.RegistroForzado) bgFila = naranjaBg;
                    // Si asistió normalmente: ligero verde
                    else if (emp.Asistio) bgFila = XLColor.FromHtml("#F0FBF0");

                    string[] valores =
                    {
                        emp.Nomina.ToString(),
                        emp.Nombre      ?? "",
                        emp.Departamento ?? "",
                        emp.Supervisor   ?? "",
                        emp.Asistio ? "SÍ" : "NO",
                        emp.HoraCheckIn.HasValue ? emp.HoraCheckIn.Value.ToString("dd/MM/yyyy HH:mm:ss") : "—",
                        emp.TieneDerechoADespensa ? "CON DERECHO" : "SIN DERECHO",
                        emp.RegistroForzado ? "FORZADO" : (emp.Asistio ? "NORMAL" : "AUSENTE"),
                        emp.RegistroForzado ? "Sin derecho — autorizado por operador" : ""
                    };

                    XLColor[] coloresTxt =
                    {
                        azulOscuro,
                        azulOscuro,
                        grisMedio,
                        grisMedio,
                        emp.Asistio ? verdeOscuro : rojoOscuro,
                        emp.Asistio ? verdeOscuro : grisMedio,
                        emp.TieneDerechoADespensa ? verdeOscuro : rojoOscuro,
                        emp.RegistroForzado ? naranja : (emp.Asistio ? verdeOscuro : grisMedio),
                        naranja
                    };

                    for (int col = 0; col < valores.Length; col++)
                    {
                        var c = ws.Cell(fila, col + 1);
                        c.Value = valores[col];
                        c.Style.Font.FontColor = coloresTxt[col];
                        c.Style.Font.FontSize = 10;
                        c.Style.Fill.BackgroundColor = bgFila;
                        c.Style.Alignment.Horizontal = col == 0 || col == 4 || col == 5 || col == 7
                            ? XLAlignmentHorizontalValues.Center
                            : XLAlignmentHorizontalValues.Left;
                        c.Style.Alignment.Vertical = XLAlignmentVerticalValues.Distributed;
                        c.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
                        c.Style.Border.OutsideBorderColor = grisBorde;

                        if (col == 4 && emp.Asistio) c.Style.Font.Bold = true;
                        if (col == 6 && !emp.TieneDerechoADespensa) c.Style.Font.Bold = true;
                    }
                    ws.Row(fila).Height = 18;
                    fila++;
                    alternar = !alternar;
                }

                // Borde exterior de la tabla completa
                ws.Range(filaInicio - 1, 1, fila - 1, headers.Length)
                    .Style.Border.OutsideBorder = XLBorderStyleValues.Medium;
                ws.Range(filaInicio - 1, 1, fila - 1, headers.Length)
                    .Style.Border.OutsideBorderColor = azulMedio;

                fila += 2;

                // ── Nota al pie ─────────────────────────────────────
                ws.Cell(fila, 1).Value =
                    $"Reporte generado automáticamente por {SistemaVersion} el {fecha:dd/MM/yyyy} a las {fecha:HH:mm:ss}. " +
                    "Los registros marcados como FORZADO fueron autorizados manualmente por el operador de turno.";
                ws.Range(fila, 1, fila, 9).Merge();
                ws.Cell(fila, 1).Style.Font.FontSize = 8;
                ws.Cell(fila, 1).Style.Font.Italic = true;
                ws.Cell(fila, 1).Style.Font.FontColor = grisMedio;
                ws.Cell(fila, 1).Style.Alignment.WrapText = true;
                ws.Row(fila).Height = 26;

                // Freeze encabezados
                ws.SheetView.FreezeRows(filaInicio);

                // AutoFilter en encabezados
                ws.Range(filaInicio - 1, 1, fila - 3, headers.Length).SetAutoFilter();

                // ════════════════════════════════════════════════════
                //  HOJA 2: RESUMEN EJECUTIVO
                // ════════════════════════════════════════════════════
                var wsRes = wb.Worksheets.Add("Resumen Ejecutivo");
                wsRes.ShowGridLines = false;
                wsRes.Column(1).Width = 36;
                wsRes.Column(2).Width = 18;

                int r2 = 1;

                void SeccionResumen(string titulo)
                {
                    r2++;
                    wsRes.Cell(r2, 1).Value = titulo;
                    wsRes.Range(r2, 1, r2, 2).Merge();
                    wsRes.Cell(r2, 1).Style.Font.Bold = true;
                    wsRes.Cell(r2, 1).Style.Font.FontSize = 11;
                    wsRes.Cell(r2, 1).Style.Font.FontColor = XLColor.White;
                    wsRes.Cell(r2, 1).Style.Fill.BackgroundColor = azulMedio;
                    wsRes.Cell(r2, 1).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Left;
                    wsRes.Row(r2).Height = 20;
                    r2++;
                }

                void FilaResumen(string concepto, string valor, bool negrita = false,
                    XLColor? colorVal = null)
                {
                    wsRes.Cell(r2, 1).Value = concepto;
                    wsRes.Cell(r2, 2).Value = valor;
                    wsRes.Cell(r2, 1).Style.Font.FontSize = 10;
                    wsRes.Cell(r2, 2).Style.Font.FontSize = 10;
                    wsRes.Cell(r2, 2).Style.Font.Bold = negrita;
                    wsRes.Cell(r2, 1).Style.Font.FontColor = XLColor.FromHtml("#334155");
                    wsRes.Cell(r2, 2).Style.Font.FontColor = colorVal ?? azulOscuro;
                    wsRes.Cell(r2, 2).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Right;
                    wsRes.Range(r2, 1, r2, 2).Style.Border.BottomBorder = XLBorderStyleValues.Dotted;
                    wsRes.Range(r2, 1, r2, 2).Style.Border.BottomBorderColor = grisBorde;
                    wsRes.Row(r2).Height = 17;
                    r2++;
                }

                // Cabecera hoja resumen
                wsRes.Cell(r2, 1).Value = NombreEmpresa;
                wsRes.Range(r2, 1, r2, 2).Merge();
                wsRes.Cell(r2, 1).Style.Font.Bold = true;
                wsRes.Cell(r2, 1).Style.Font.FontSize = 13;
                wsRes.Cell(r2, 1).Style.Font.FontColor = azulOscuro;
                r2++;
                wsRes.Cell(r2, 1).Value = $"Resumen Ejecutivo — {fecha:MMMM yyyy}";
                wsRes.Range(r2, 1, r2, 2).Merge();
                wsRes.Cell(r2, 1).Style.Font.FontSize = 10;
                wsRes.Cell(r2, 1).Style.Font.FontColor = grisMedio;
                r2 += 2;

                SeccionResumen("COBERTURA GENERAL");
                FilaResumen("Total de empleados registrados", Invitados.Count.ToString("N0"));
                FilaResumen("Empleados con derecho a despensa", conDer.ToString("N0"), false, verdeOscuro);
                FilaResumen("Empleados sin derecho a despensa", sinDer.ToString("N0"), false, rojoOscuro);

                SeccionResumen("ASISTENCIA DEL DÍA");
                FilaResumen("Total de asistentes", asistentes.ToString("N0"), true, verdeOscuro);
                FilaResumen("Ausentes", (Invitados.Count - asistentes).ToString("N0"), true, rojoOscuro);
                FilaResumen("Porcentaje de asistencia", $"{pct:F2}%", true, pct >= 80 ? verdeOscuro : naranja);
                FilaResumen("Registros forzados (sin derecho)", forzados.ToString("N0"), false, forzados > 0 ? naranja : verdeOscuro);

                SeccionResumen("ASISTENTES CON DERECHO A DESPENSA");
                var asistConDer = Invitados.Count(x => x.Asistio && x.TieneDerechoADespensa);
                var asistSinDer = Invitados.Count(x => x.Asistio && !x.TieneDerechoADespensa);
                FilaResumen("Asistentes CON derecho", asistConDer.ToString("N0"), false, verdeOscuro);
                FilaResumen("Asistentes SIN derecho (forzado)", asistSinDer.ToString("N0"), false, naranja);

                SeccionResumen("REGISTRO");
                FilaResumen("Fecha de corte", fecha.ToString("dd/MM/yyyy"));
                FilaResumen("Hora de generación", fecha.ToString("HH:mm:ss"));
                FilaResumen("Sistema", SistemaVersion);

                // Nota ejecutiva
                r2 += 2;
                wsRes.Cell(r2, 1).Value =
                    "Este documento es confidencial y de uso exclusivo del Departamento de Recursos Humanos. " +
                    "Los registros forzados requieren verificación y autorización del responsable de área.";
                wsRes.Range(r2, 1, r2, 2).Merge();
                wsRes.Cell(r2, 1).Style.Font.FontSize = 8;
                wsRes.Cell(r2, 1).Style.Font.Italic = true;
                wsRes.Cell(r2, 1).Style.Font.FontColor = grisMedio;
                wsRes.Cell(r2, 1).Style.Alignment.WrapText = true;
                wsRes.Row(r2).Height = 32;

                // ════════════════════════════════════════════════════
                //  HOJA 3: PERSONAL SIN DERECHO QUE ASISTIÓ
                // ════════════════════════════════════════════════════
                var forzadosLista = Invitados.Where(x => x.RegistroForzado).OrderBy(x => x.Nombre).ToList();
                if (forzadosLista.Any())
                {
                    var wsForzados = wb.Worksheets.Add("Alertas — Forzados");
                    wsForzados.ShowGridLines = false;
                    wsForzados.Column(1).Width = 12;
                    wsForzados.Column(2).Width = 32;
                    wsForzados.Column(3).Width = 24;
                    wsForzados.Column(4).Width = 24;
                    wsForzados.Column(5).Width = 14;

                    int rf = 1;
                    wsForzados.Cell(rf, 1).Value = "⚠ REGISTROS FORZADOS — Personal SIN DERECHO que asistió";
                    wsForzados.Range(rf, 1, rf, 5).Merge();
                    wsForzados.Cell(rf, 1).Style.Font.Bold = true;
                    wsForzados.Cell(rf, 1).Style.Font.FontSize = 12;
                    wsForzados.Cell(rf, 1).Style.Font.FontColor = rojoOscuro;
                    wsForzados.Row(rf).Height = 24;
                    rf += 2;

                    string[] hForzados = { "NÓMINA", "NOMBRE", "DEPARTAMENTO", "SUPERVISOR", "HORA" };
                    int[] aForzados = { 12, 32, 24, 24, 14 };
                    for (int col = 0; col < hForzados.Length; col++)
                    {
                        var c = wsForzados.Cell(rf, col + 1);
                        c.Value = hForzados[col];
                        c.Style.Font.Bold = true;
                        c.Style.Font.FontColor = XLColor.White;
                        c.Style.Fill.BackgroundColor = rojoOscuro;
                        c.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                        wsForzados.Column(col + 1).Width = aForzados[col];
                    }
                    wsForzados.Row(rf).Height = 20;
                    rf++;

                    foreach (var emp in forzadosLista)
                    {
                        string[] vf = {
                            emp.Nomina.ToString(),
                            emp.Nombre ?? "",
                            emp.Departamento ?? "",
                            emp.Supervisor ?? "",
                            emp.HoraCheckIn?.ToString("HH:mm:ss") ?? "—"
                        };
                        for (int col = 0; col < vf.Length; col++)
                        {
                            var c = wsForzados.Cell(rf, col + 1);
                            c.Value = vf[col];
                            c.Style.Fill.BackgroundColor = naranjaBg;
                            c.Style.Font.FontSize = 10;
                            c.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
                            c.Style.Border.OutsideBorderColor = grisBorde;
                        }
                        wsForzados.Row(rf).Height = 17;
                        rf++;
                    }
                }

                // Guardar
                var fileName = $"Asistencia_RH_{fecha:yyyyMMdd_HHmm}.xlsx";
                var filePath = Path.Combine(FileSystem.CacheDirectory, fileName);
                wb.SaveAs(filePath);
                return filePath;
            });
        }

        // ════════════════════════════════════════════════════════════════
        //  REPORTE TXT — Imprimible, estilo HR profesional
        // ════════════════════════════════════════════════════════════════
        private async Task<string> GenerarTXT()
        {
            var sb = new StringBuilder();
            var fecha = DateTime.Now;
            int total = Invitados.Count;
            int asistentes = Invitados.Count(x => x.Asistio);
            int ausentes = total - asistentes;
            int conDer = Invitados.Count(x => x.TieneDerechoADespensa);
            int sinDer = total - conDer;
            int forzados = Invitados.Count(x => x.RegistroForzado);
            double pct = total > 0 ? (double)asistentes / total * 100 : 0;

            const string SEP = "═══════════════════════════════════════════════════════════════════════════";
            const string SEP2 = "───────────────────────────────────────────────────────────────────────────";

            // ── Encabezado ──────────────────────────────────────────
            sb.AppendLine(SEP);
            sb.AppendLine($"  {NombreEmpresa}");
            sb.AppendLine($"  {DepartamentoRH}");
            sb.AppendLine(SEP);
            sb.AppendLine($"  REPORTE DE CONTROL DE ASISTENCIA Y DESPENSA");
            sb.AppendLine($"  Fecha : {fecha:dddd, dd 'de' MMMM 'de' yyyy}");
            sb.AppendLine($"  Hora  : {fecha:HH:mm:ss}   |   Sistema: {SistemaVersion}");
            sb.AppendLine(SEP);
            sb.AppendLine();

            // ── Resumen ejecutivo ───────────────────────────────────
            sb.AppendLine("  RESUMEN EJECUTIVO");
            sb.AppendLine(SEP2);
            sb.AppendLine($"  {"Total de empleados en nómina:",-38} {total,5}");
            sb.AppendLine($"  {"Empleados CON derecho a despensa:",-38} {conDer,5}");
            sb.AppendLine($"  {"Empleados SIN derecho a despensa:",-38} {sinDer,5}");
            sb.AppendLine(SEP2);
            sb.AppendLine($"  {"Total de asistentes el día de hoy:",-38} {asistentes,5}");
            sb.AppendLine($"  {"Total de ausentes:",-38} {ausentes,5}");
            sb.AppendLine($"  {"Porcentaje de asistencia:",-38} {pct,4:F1}%");
            sb.AppendLine($"  {"Registros forzados (sin derecho):",-38} {forzados,5}");
            sb.AppendLine(SEP2);
            sb.AppendLine($"  {"Asistentes CON derecho a despensa:",-38} {Invitados.Count(x => x.Asistio && x.TieneDerechoADespensa),5}");
            sb.AppendLine($"  {"Asistentes SIN derecho (forzados):",-38} {Invitados.Count(x => x.Asistio && !x.TieneDerechoADespensa),5}");
            sb.AppendLine(SEP);
            sb.AppendLine();

            // ── Tabla de detalle ────────────────────────────────────
            sb.AppendLine("  DETALLE DE ASISTENCIA");
            sb.AppendLine(SEP2);
            sb.AppendLine($"  {"NÓM",-6} {"NOMBRE",-28} {"DEPARTAMENTO",-20} {"ASIST",-6} {"HORA",-9} {"DESPENSA",-12} {"OBS",-25}");
            sb.AppendLine($"  {"───",-6} {"──────────────────────────",-28} {"──────────────────",-20} {"─────",-6} {"───────",-9} {"──────────",-12} {"─────────────────────────",-25}");

            foreach (var emp in Invitados.OrderBy(e => e.Nomina))
            {
                var nombre = Truncar(emp.Nombre, 27);
                var depto = Truncar(emp.Departamento, 19);
                var asistio = emp.Asistio ? "SI" : "NO";
                var hora = emp.HoraCheckIn?.ToString("HH:mm") ?? "——";
                var desp = emp.TieneDerechoADespensa ? "CON DER." : "SIN DER.";
                var obs = emp.RegistroForzado ? "⚠ FORZADO/SIN DERECHO" : "";

                sb.AppendLine($"  {emp.Nomina,-6} {nombre,-28} {depto,-20} {asistio,-6} {hora,-9} {desp,-12} {obs,-25}");
            }
            sb.AppendLine(SEP);
            sb.AppendLine();

            // ── Sección de alertas ──────────────────────────────────
            var forzadosLista = Invitados.Where(x => x.RegistroForzado).ToList();
            if (forzadosLista.Any())
            {
                sb.AppendLine("  ⚠  ALERTAS — REGISTROS FORZADOS (REQUIEREN VERIFICACIÓN)");
                sb.AppendLine(SEP2);
                sb.AppendLine($"  Los siguientes empleados NO tienen derecho a despensa pero fueron registrados:");
                sb.AppendLine();
                foreach (var emp in forzadosLista.OrderBy(e => e.Nombre))
                    sb.AppendLine($"  • [{emp.Nomina}] {emp.Nombre} — {emp.Departamento}" +
                                  $"   Hora: {emp.HoraCheckIn:HH:mm:ss}");
                sb.AppendLine(SEP);
                sb.AppendLine();
            }

            // ── Pie de página ───────────────────────────────────────
            sb.AppendLine($"  Documento generado automáticamente por {SistemaVersion}.");
            sb.AppendLine($"  Uso exclusivo del {DepartamentoRH}.");
            sb.AppendLine($"  CONFIDENCIAL — No distribuir sin autorización.");
            sb.AppendLine(SEP);

            var fileName = $"Asistencia_RH_{fecha:yyyyMMdd_HHmm}.txt";
            var filePath = Path.Combine(FileSystem.CacheDirectory, fileName);
            await File.WriteAllTextAsync(filePath, sb.ToString(), Encoding.UTF8);
            return filePath;
        }

        // ════════════════════════════════════════════════════════════════
        //  REPORTE CSV — Limpio, con hoja de resumen al final
        // ════════════════════════════════════════════════════════════════
        private async Task<string> GenerarCSV()
        {
            var sb = new StringBuilder();
            var fecha = DateTime.Now;
            int total = Invitados.Count;
            int asistentes = Invitados.Count(x => x.Asistio);
            double pct = total > 0 ? (double)asistentes / total * 100 : 0;

            // Meta-información
            sb.AppendLine($"# {NombreEmpresa}");
            sb.AppendLine($"# {DepartamentoRH}");
            sb.AppendLine($"# Reporte de Asistencia — {fecha:dd/MM/yyyy HH:mm:ss}");
            sb.AppendLine($"# Sistema: {SistemaVersion}");
            sb.AppendLine();

            // Encabezados de datos
            sb.AppendLine("NOMINA,NOMBRE,DEPARTAMENTO,SUPERVISOR,ASISTIO,HORA_CHECKIN,DERECHO_DESPENSA,TIPO_REGISTRO,OBSERVACIONES");

            foreach (var emp in Invitados.OrderBy(e => e.Nomina))
            {
                var obs = emp.RegistroForzado ? "FORZADO — Sin derecho, autorizado por operador" : "";
                var tipo = emp.RegistroForzado ? "FORZADO" : (emp.Asistio ? "NORMAL" : "AUSENTE");

                sb.AppendLine(string.Join(",",
                    emp.Nomina,
                    Q(emp.Nombre),
                    Q(emp.Departamento),
                    Q(emp.Supervisor),
                    emp.Asistio ? "SI" : "NO",
                    emp.HoraCheckIn.HasValue ? emp.HoraCheckIn.Value.ToString("HH:mm:ss") : "",
                    emp.TieneDerechoADespensa ? "CON DERECHO" : "SIN DERECHO",
                    tipo,
                    Q(obs)
                ));
            }

            // Bloque de resumen al pie
            sb.AppendLine();
            sb.AppendLine("# ──────────────── RESUMEN ────────────────");
            sb.AppendLine($"CONCEPTO,VALOR");
            sb.AppendLine($"Total empleados,{total}");
            sb.AppendLine($"Con derecho a despensa,{Invitados.Count(x => x.TieneDerechoADespensa)}");
            sb.AppendLine($"Sin derecho a despensa,{Invitados.Count(x => !x.TieneDerechoADespensa)}");
            sb.AppendLine($"Total asistentes,{asistentes}");
            sb.AppendLine($"Ausentes,{total - asistentes}");
            sb.AppendLine($"Porcentaje asistencia,{pct:F2}%");
            sb.AppendLine($"Asistentes con derecho,{Invitados.Count(x => x.Asistio && x.TieneDerechoADespensa)}");
            sb.AppendLine($"Asistentes sin derecho (forzados),{Invitados.Count(x => x.Asistio && !x.TieneDerechoADespensa)}");
            sb.AppendLine($"Fecha generación,{fecha:dd/MM/yyyy}");
            sb.AppendLine($"Hora generación,{fecha:HH:mm:ss}");

            var fileName = $"Asistencia_RH_{fecha:yyyyMMdd_HHmm}.csv";
            var filePath = Path.Combine(FileSystem.CacheDirectory, fileName);
            await File.WriteAllTextAsync(filePath, sb.ToString(), Encoding.UTF8);
            return filePath;
        }

        // ════════════════════════════════════════════════════════════════
        //  REPORTE JSON — Estructura limpia para analítica HR
        // ════════════════════════════════════════════════════════════════
        private async Task<string> GenerarJSON()
        {
            var fecha = DateTime.Now;
            int total = Invitados.Count;
            int asist = Invitados.Count(x => x.Asistio);

            var reporte = new
            {
                metadata = new
                {
                    empresa = NombreEmpresa,
                    departamento = DepartamentoRH,
                    sistema = SistemaVersion,
                    fechaCorte = fecha.ToString("yyyy-MM-dd"),
                    horaCorte = fecha.ToString("HH:mm:ss"),
                    generadoEn = fecha.ToString("o")
                },
                resumen = new
                {
                    totalEmpleados = total,
                    conDerechoDespensa = Invitados.Count(x => x.TieneDerechoADespensa),
                    sinDerechoDespensa = Invitados.Count(x => !x.TieneDerechoADespensa),
                    totalAsistentes = asist,
                    totalAusentes = total - asist,
                    porcentajeAsistencia = Math.Round(total > 0 ? (double)asist / total * 100 : 0, 3),
                    asistentesConDerecho = Invitados.Count(x => x.Asistio && x.TieneDerechoADespensa),
                    asistentesSinDerecho = Invitados.Count(x => x.Asistio && !x.TieneDerechoADespensa),
                    registrosForzados = Invitados.Count(x => x.RegistroForzado)
                },
                empleados = Invitados
                    .OrderBy(e => e.Nomina)
                    .Select(e => new
                    {
                        nomina = e.Nomina,
                        nombre = e.Nombre,
                        departamento = e.Departamento,
                        supervisor = e.Supervisor,
                        asistio = e.Asistio,
                        horaCheckIn = e.HoraCheckIn?.ToString("HH:mm:ss"),
                        tieneDerechoADespensa = e.TieneDerechoADespensa,
                        tipoRegistro = e.RegistroForzado ? "FORZADO" : (e.Asistio ? "NORMAL" : "AUSENTE"),
                        observaciones = e.RegistroForzado
                            ? "Sin derecho a despensa — registrado por autorización del operador" : null
                    }),
                alertas = Invitados
                    .Where(x => x.RegistroForzado)
                    .OrderBy(x => x.Nombre)
                    .Select(e => new
                    {
                        nomina = e.Nomina,
                        nombre = e.Nombre,
                        departamento = e.Departamento,
                        hora = e.HoraCheckIn?.ToString("HH:mm:ss"),
                        motivo = "Asistencia registrada sin derecho a despensa — requiere verificación"
                    })
            };

            var json = JsonConvert.SerializeObject(reporte, Formatting.Indented);
            var fileName = $"Asistencia_RH_{fecha:yyyyMMdd_HHmm}.json";
            var filePath = Path.Combine(FileSystem.CacheDirectory, fileName);
            await File.WriteAllTextAsync(filePath, json, Encoding.UTF8);
            return filePath;
        }

        // ════════════════════════════════════════════════════════════════
        //  UTILIDADES
        // ════════════════════════════════════════════════════════════════
        /// <summary>Envuelve un valor en comillas CSV, escapando comillas internas.</summary>
        private static string Q(string v)
            => v == null ? "" : $"\"{v.Replace("\"", "\"\"")}\"";

        /// <summary>Trunca un texto a una longitud máxima añadiendo "…" si es necesario.</summary>
        private static string Truncar(string texto, int max)
        {
            if (string.IsNullOrEmpty(texto)) return "";
            return texto.Length <= max ? texto : texto[..(max - 1)] + "…";
        }

        private async Task CompartirArchivo(string filePath)
        {
            await Share.Default.RequestAsync(new ShareFileRequest
            {
                Title = "Reporte de Asistencia — RH",
                File = new ShareFile(filePath)
            });
        }

        /// <summary>Convierte registros del formato antiguo JSON (con Data anidado).</summary>
        private List<QrResult> ConvertirFormatoAntiguo(List<QrResult> datosAntiguos)
        {
            foreach (var item in datosAntiguos.Where(i => i.Data != null))
            {
                item.Nomina = item.Data.ID;
                item.Nombre = item.Data.Nombre;
                item.Departamento = item.Data.Unidad;
            }
            return datosAntiguos;
        }

        [RelayCommand]
        private async Task CargarJson()
        {
            try
            {
                IsLoading = true;
                StatusMessage = "Cargando archivo JSON...";
                var resultado = await FilePicker.PickAsync(new PickOptions
                {
                    PickerTitle = "Selecciona el archivo JSON",
                    FileTypes = new FilePickerFileType(new Dictionary<DevicePlatform, IEnumerable<string>>
                    {
                        { DevicePlatform.WinUI,   new[] { ".json" } },
                        { DevicePlatform.macOS,   new[] { ".json" } },
                        { DevicePlatform.Android, new[] { "application/json" } },
                        { DevicePlatform.iOS,     new[] { "public.json" } }
                    })
                });
                if (resultado != null)
                {
                    using var stream = await resultado.OpenReadAsync();
                    using var reader = new StreamReader(stream);
                    var json = await reader.ReadToEndAsync();
                    await File.WriteAllTextAsync(_dataFilePath, json);
                    var data = JsonConvert.DeserializeObject<List<QrResult>>(json);
                    await CargarAsistencia(data);
                    Invitados.Clear();
                    foreach (var item in data.OrderBy(i => i.Data?.Nombre))
                        Invitados.Add(item);
                    StatusMessage = $"✅ {Invitados.Count} registros cargados";
                    FiltrarInvitados();
                }
            }
            catch (Exception ex) { StatusMessage = $"❌ Error: {ex.Message}"; }
            finally { IsLoading = false; }
        }  

        public ICommand MostrarDetallesCommand => new Command(() =>
        {
            MostrarDetalles = true;
        });

        public ICommand CerrarDetallesCommand => new Command(() =>
        {
            MostrarDetalles = false;
        });
    }
}
