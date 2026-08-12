using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace VertexBPMN.Domain.Extensions;

/// <summary>
/// Provider-portable helpers for configuring JSON-ish string columns across PostgreSQL, SQL Server and SQLite
/// without hard dependency on relational extension methods (HasColumnType might be missing in trimmed references).
/// </summary>
internal static class PortableColumnTypeExtensions
{
    private const string RelationalColumnTypeAnnotation = "Relational:ColumnType";
    private const string RelationalProviderAnnotation = "Relational:ProviderName";

    /// <summary>
    /// Configures an appropriate provider-specific column type for JSON-like string data.
    /// PostgreSQL => jsonb, SQL Server => nvarchar(max), SQLite/others => TEXT.
    /// Uses reflection to call HasColumnType if available; otherwise sets the annotation directly.
    /// </summary>
    public static PropertyBuilder<string?> HasPortableJsonColumn(this PropertyBuilder<string?> propertyBuilder)
    {
        var model = propertyBuilder.Metadata.DeclaringType?.Model;
        var provider = model?.FindAnnotation(RelationalProviderAnnotation)?.Value?.ToString();

        var columnType = provider switch
        {
            // Npgsql provider name
            "Npgsql.EntityFrameworkCore.PostgreSQL" => "jsonb",
            // SQL Server has no native json type; nvarchar(max) recommended
            "Microsoft.EntityFrameworkCore.SqlServer" => "nvarchar(max)",
            // SQLite stores as TEXT
            "Microsoft.EntityFrameworkCore.Sqlite" => "TEXT",
            _ => "TEXT"
        };

        // Try reflection-based invocation of HasColumnType to avoid compile-time dependency.
        try
        {
            var method = AppDomain.CurrentDomain
                .GetAssemblies()
                .SelectMany(a => a.GetTypes())
                .FirstOrDefault(t => t.Name == "RelationalPropertyBuilderExtensions")?
                .GetMethods()
                .FirstOrDefault(m => m.Name == "HasColumnType" && m.GetParameters().Length == 2 && m.GetParameters()[1].ParameterType == typeof(string));

            if (method != null)
            {
                method.Invoke(null, new object?[] { propertyBuilder, columnType });
            }
            else
            {
                propertyBuilder.HasAnnotation(RelationalColumnTypeAnnotation, columnType);
            }
        }
        catch
        {
            propertyBuilder.HasAnnotation(RelationalColumnTypeAnnotation, columnType);
        }
        return propertyBuilder;
    }
}
