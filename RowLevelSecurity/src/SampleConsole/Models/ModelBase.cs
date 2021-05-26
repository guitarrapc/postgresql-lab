using System.ComponentModel.DataAnnotations.Schema;

namespace SampleConsole.Models
{
    public abstract class ModelBase
    {
        [Column("tenant_id", Order = 0)]
        public long TenantId { get; init; }
    }
}
