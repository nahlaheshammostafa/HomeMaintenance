namespace Tot.Integrations;

/// <summary>
/// A typed reference to a resource at an external provider,
/// e.g. <c>("parking-place", "8841")</c> or <c>("product", "LK-55A1")</c>.
/// </summary>
/// <param name="Type">The resource type as a stable lowercase string.</param>
/// <param name="Id">The provider-assigned identifier.</param>
public sealed record ExternalRef(string Type, string Id);
