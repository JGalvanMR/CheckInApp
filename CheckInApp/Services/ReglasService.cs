using CheckInApp.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CheckInApp.Services
{
    public class ReglasService
    {
        private readonly List<IReglaEmpleado> _reglas;

        public ReglasService()
        {
            _reglas = new List<IReglaEmpleado>
        {
            new ReglaDuplicado(),
            new ReglaExamenMedico(),
            new ReglaDespensa()
        };
        }

        public async Task<ResultadoRegla> EjecutarReglasAsync(QrResult empleado)
        {
            foreach (var regla in _reglas)
            {
                var resultado = await regla.EvaluarAsync(empleado);

                if (!resultado.PuedeContinuar)
                    return resultado;

                if (resultado.EsForzado)
                    return resultado;
            }

            return new ResultadoRegla(); // todo OK
        }
    }
}
