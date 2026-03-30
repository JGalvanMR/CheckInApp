using CheckInApp.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CheckInApp.Services
{
    public interface IReglaEmpleado
    {
        Task<ResultadoRegla> EvaluarAsync(QrResult empleado);
    }
}
