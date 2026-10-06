using System.Text.Json;
using System.Text.Json.Serialization;
using System.Xml.Linq;
using VertexBPMN.Domain.Interfaces;

namespace VertexBPMN.Application.Import;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum N8nImportDisposition { Migrated, NeedsReview, Unsupported }
