using System.Diagnostics.CodeAnalysis;
using System.Linq.Expressions;

namespace EventReservation.Domain.Infrastructure;

public static class BuilderFactory
{
    public static Result<TModel> Create<TModel, TBuilder>(Action<TBuilder> configure)
        where TBuilder : IFluentBuilder<TModel>
    {
        if (configure is null)
            return Failure<TModel>(Errors.EmptyConfiguration);

        if (!ConstructorCache<TBuilder>.HasValidConstructor(out var initializationError))
            return Failure<TModel>(initializationError.Value);

        TBuilder builder = ConstructorCache<TBuilder>.CreateInstance();
        configure(builder);
        return builder.Build();
    }

    public static class ConstructorCache<TBuilder>
    {
        private static readonly Func<TBuilder>? _cachedConstructor;
        private static readonly ResultError? _initializationError;

        static ConstructorCache()
        {
            var builderType = typeof(TBuilder);

            var constructor = builderType.GetConstructor(
                System.Reflection.BindingFlags.Instance |
                System.Reflection.BindingFlags.NonPublic |
                System.Reflection.BindingFlags.Public,
                null,
                Type.EmptyTypes,
                null);

            if (constructor is null)
            {
                _initializationError = Errors.MissingParameterlessConstructor(builderType.Name);
                _cachedConstructor = null;
            }
            else
            {
                var newExp = Expression.New(constructor);
                _cachedConstructor = Expression.Lambda<Func<TBuilder>>(newExp).Compile();
                _initializationError = null;
            }
        }

        public static bool HasValidConstructor([NotNullWhen(false)] out ResultError? error)
        {
            error = _initializationError;
            return _cachedConstructor is not null;
        }

        public static TBuilder CreateInstance() =>
            _cachedConstructor is not null
                ? _cachedConstructor()
                : throw new InvalidOperationException(_initializationError?.Message ?? "Builder initialization missing.");
    }

    public static class Errors
    {
        private const string Context = "BUILDER_FACTORY";
        public static ResultError EmptyConfiguration => new(Context, "Configuration action cannot be null.");
        public static ResultError MissingParameterlessConstructor(string builderName) =>
            new(Context, $"The builder type '{builderName}' must have a parameterless constructor (internal or public).");
    }
}