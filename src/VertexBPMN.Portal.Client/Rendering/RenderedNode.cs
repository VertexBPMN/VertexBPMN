using System.Collections.Generic;

namespace VertexBPMN.Portal.Client.Rendering;

/// <summary>A single node produced by the allowlist renderer from a released application's ContentJson.</summary>
public abstract record RenderedNode;

/// <summary>
/// A component whose type is on the renderer allowlist and whose version is sanctioned.
/// It was rendered by a known, closed sink passing only that one component type — nothing
/// from the metadata is ever reflected, instantiated or executed.
/// </summary>
public sealed record KnownComponent(string Type, IReadOnlyList<RenderedNode> Children) : RenderedNode;

/// <summary>
/// An unsanctioned/unknown component type. Rendered only as a safe placeholder notice;
/// its payload is never descended into, evaluated or executed.
/// </summary>
public sealed record UnsupportedComponent(string Type) : RenderedNode;

/// <summary>A rendered page within the released application.</summary>
public sealed record RenderedPage(string Id, string Route, IReadOnlyList<RenderedNode> Components);

/// <summary>The fully rendered released application tree.</summary>
public sealed record RenderedApplication(IReadOnlyList<RenderedPage> Pages, int UnsupportedCount);
