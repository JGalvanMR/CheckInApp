using System;
using Newtonsoft.Json;

namespace CheckInApp.Models
{
    public class QrResult
    {
        public string FileName { get; set; }
        public string DecodedData { get; set; }
        public string Timestamp { get; set; }

        [JsonIgnore]
        public InvitadoData Data => !string.IsNullOrEmpty(DecodedData)
            ? JsonConvert.DeserializeObject<InvitadoData>(DecodedData)
            : null;

        [JsonIgnore]
        public bool Asistio { get; set; }

        [JsonIgnore]
        public DateTime? HoraCheckIn { get; set; }

        // Propiedades adicionales para datos del Excel
        public int Nomina { get; set; }
        public string Codigo { get; set; }
        public string CodigoBarras { get; set; }
        public string Nombre { get; set; }
        public string Supervisor { get; set; }
        public string Departamento { get; set; }
        public bool TieneDerechoADespensa { get; set; }
        public bool RegistroForzado { get; set; }
        public bool ExamenMedico { get; set; }
        public bool RetencionPendiente { get; set; }

        [JsonIgnore]
        public string EstadoDerecho => TieneDerechoADespensa ? "✓ CON DERECHO" : "⚠️ SIN DERECHO";
    }

    public class InvitadoData
    {
        [JsonProperty("Evento")]
        public string Evento { get; set; }

        [JsonProperty("ID")]
        public int ID { get; set; }

        [JsonProperty("Nombre")]
        public string Nombre { get; set; }

        [JsonProperty("Unidad")]
        public string Unidad { get; set; }

        [JsonProperty("Fecha")]
        public string Fecha { get; set; }

        [JsonProperty("Hash")]
        public string Hash { get; set; }
    }
}