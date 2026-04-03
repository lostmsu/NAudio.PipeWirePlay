namespace System.Runtime.CompilerServices;

sealed class IsExternalInit;

[AttributeUsage(AttributeTargets.All, Inherited = false)]
sealed class CompilerFeatureRequiredAttribute(string featureName) : Attribute
{
    public string FeatureName { get; } = featureName;

    public bool IsOptional { get; init; }

    public const string RefStructs = nameof(RefStructs);

    public const string RequiredMembers = nameof(RequiredMembers);
}

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct | AttributeTargets.Field | AttributeTargets.Property, Inherited = false)]
sealed class RequiredMemberAttribute : Attribute;

[AttributeUsage(AttributeTargets.Constructor, Inherited = false)]
sealed class SetsRequiredMembersAttribute : Attribute;
