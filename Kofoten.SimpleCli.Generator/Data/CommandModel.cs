using System.Collections.Generic;

namespace Kofoten.SimpleCli.Generator.Data;

internal record CommandModel(
    string Namespace,
    string ClassName,
    List<ConstructorParameterModel> ConstructorParameters,
    List<PropertyModel> Properties
);
