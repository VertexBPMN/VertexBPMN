using System;
using System.Collections.Generic;

namespace VertexBPMN.Domain.Exceptions;

public class BpmnValidationException : Exception
{
    public List<string> Errors { get; }
    public BpmnValidationException(string message, List<string> errors) : base(message)
    {
        Errors = errors;
    }
}