using CheckInApp.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CheckInApp.Services
{
    public class ReglaExamenMedico : IReglaEmpleado
    {
        public async Task<ResultadoRegla> EvaluarAsync(QrResult empleado)
        {
            if (empleado.ExamenMedico)
                return new ResultadoRegla();

            bool continuar = await Application.Current.MainPage.DisplayAlert(
                "⚠️ EXAMEN MÉDICO PENDIENTE",
                $"El empleado:\n\n👤 {empleado.Nombre}\n🏢 {empleado.Departamento}\n📋 Nómina: {empleado.Nomina}\n\n" +
                "NO ha realizado su examen médico anual.\n\n" +
                "Tiene derecho a despensa, pero tiene un trámite pendiente con Servicio Médico.\n\n" +
                "¿Deseas forzar el registro?",
                "Sí, registrar", "No, cancelar");

            if (!continuar)
            {
                return new ResultadoRegla
                {
                    PuedeContinuar = false,
                    Mensaje = $"⛔ Registro cancelado para {empleado.Nombre} (Examen médico pendiente)"
                };
            }

            return new ResultadoRegla
            {
                EsForzado = true,
                Mensaje = $"⚠️ {empleado.Nombre} registrado (EXAMEN MÉDICO PENDIENTE — FORZADO)"
            };
        }
    }
}
