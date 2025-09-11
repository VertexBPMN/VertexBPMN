namespace VertexBPMN.Core.Modeling;

public record struct SentryCondition(
    string Expression, // Jint-kompatible Bedingung
    string VariableRef, // Referenz auf CaseFileItem
    string OnPartEvent, // z.B. complete, occur
    string LogicalOperator // AND, OR
);