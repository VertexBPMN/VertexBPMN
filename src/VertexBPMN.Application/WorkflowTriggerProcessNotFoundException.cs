using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Xml.Linq;
using VertexBPMN.Domain.Entities;
using VertexBPMN.Domain.Interfaces;
using VertexBPMN.Domain.Interfaces.Repositories;

namespace VertexBPMN.Application;

public sealed class WorkflowTriggerProcessNotFoundException(string key)
	: Exception($"Process definition with key '{key}' was not found.");
