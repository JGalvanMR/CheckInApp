using CheckInApp.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CheckInApp.Services
{
    public class ReglaDuplicado : IReglaEmpleado
    {
        public Task<ResultadoRegla> EvaluarAsync(QrResult empleado)
        {
            if (!empleado.Asistio)
                return Task.FromResult(new ResultadoRegla());

            return Task.FromResult(new ResultadoRegla
            {
                PuedeContinuar = false,
                Mensaje = $"⚠️ {empleado.Nombre} ya registró a las {empleado.HoraCheckIn:HH:mm}"
            });
        }
    }
}
