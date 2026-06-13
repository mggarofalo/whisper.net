// A named output transform: the abstraction the pipeline applies by name to rewrite
// recognized text. It is pure data — a Name to resolve it by, a human Description, and the Prompt
// template handed to the rephrase port. The actual AI execution lives behind IRephraseClient, so this
// type (and the whole framework) stays free of any Infrastructure or network concern.

namespace Logic.AppManagement.OutputTransforms;

public sealed record OutputTransform(string Name, string Description, string Prompt);
