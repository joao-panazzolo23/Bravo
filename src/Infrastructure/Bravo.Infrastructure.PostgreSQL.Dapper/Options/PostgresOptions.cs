using System.ComponentModel.DataAnnotations;

namespace Bravo.Infrastructure.PostgreSQL.Dapper.Options;

internal record PostgresOptions
{
    [Required]
    public required string Connection { get; set; }
}
