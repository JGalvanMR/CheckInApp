using CheckInApp.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CheckInApp.Services
{
    public class ReglaDespensa : IReglaEmpleado
    {
        public async Task<ResultadoRegla> EvaluarAsync(QrResult empleado)
        {
            if (empleado.TieneDerechoADespensa)
                return new ResultadoRegla();

            bool continuar = await Application.Current.MainPage.DisplayAlert(
                "⚠️ SIN DERECHO A DESPENSA",
                $"El empleado:\n\n👤 {empleado.Nombre}\n🏢 {empleado.Departamento}\n📋 Nómina: {empleado.Nomina}\n\n" +
                "NO tiene derecho a recibir despensa.\n\n¿Deseas registrar su asistencia de todos modos?",
                "Sí, registrar", "No, cancelar");

            if (!continuar)
            {
                return new ResultadoRegla
                {
                    PuedeContinuar = false,
                    Mensaje = $"⛔ Check-in cancelado para {empleado.Nombre}"
                };
            }

            return new ResultadoRegla
            {
                EsForzado = true,
                Mensaje = $"⚠️ {empleado.Nombre} registrado (SIN DERECHO — FORZADO)"
            };
        }
    }
}
