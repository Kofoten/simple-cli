using System.Collections.Generic;

namespace Kofoten.NativeCli.Generator.Data;

internal record CommandModel(
    string Namespace,
    string Accessibility,
    string ClassName,
    string? Description,
    List<ConstructorParameterModel> ConstructorParameters,
    List<PropertyModel> Properties,
    bool HasDependencyInjection,
    bool HasValidationMethod,
    List<string> Usings);
