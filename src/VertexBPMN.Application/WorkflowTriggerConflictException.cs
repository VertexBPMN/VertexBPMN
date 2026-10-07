using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Xml.Linq;
using VertexBPMN.Domain.Entities;
using VertexBPMN.Domain.Interfaces;
using VertexBPMN.Domain.Interfaces.Repositories;

namespace VertexBPMN.Application;

public sealed class WorkflowTriggerConflictException(string message) : Exception(message);
