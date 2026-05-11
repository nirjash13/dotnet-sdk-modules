using FluentValidation;

namespace MyModule.Application.MyFeature;

/// <summary>FluentValidation validator for <see cref="MyFeatureRequest"/>.</summary>
internal sealed class MyFeatureValidator : AbstractValidator<MyFeatureRequest>
{
    /// <summary>Initializes validation rules.</summary>
    public MyFeatureValidator()
    {
        // TODO: add validation rules
        // RuleFor(x => x.SomeProperty).NotEmpty().MaximumLength(256);
    }
}
