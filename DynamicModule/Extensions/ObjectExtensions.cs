using System.ComponentModel;
using System.Dynamic;
using System.Globalization;
using System.Reflection;
using System.Runtime.Serialization;
using DynamicModule.Enums;

namespace DynamicModule.Extensions;

internal static class ObjectExtensions
{
    private static readonly Type[] _dateTypes = new Type[]
    {
        typeof(DateTime),
        typeof(DateTime?),
        typeof(DateOnly),
        typeof(DateOnly?),
        typeof(DateTimeOffset),
        typeof(DateTimeOffset?),
        typeof(TimeOnly),
        typeof(TimeOnly?),
    };

    /// <summary>
    /// Maps the given <see cref="object"/> to <typeparamref name="T"/>.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="data"></param>
    /// <returns></returns>
    public static T MapTo<T>(this object data)
    {
        if (data is null)
        {
            throw new ArgumentNullException(nameof(data));
        }

        var type = typeof(T);

        if (type.IsValueType || type == typeof(object))
        {
            return (T)data;
        }

        var outputType = data.GetType();

        var actualType = outputType;
        // Determine the type
        if (outputType.IsGenericType)
        {
            // Determine if the underlying type is IEnumerable<>
            if (outputType.GetGenericTypeDefinition() == typeof(IEnumerable<>))
            {
                actualType = outputType.GetGenericArguments()[0];
            }
            else
            {
                // Determine whether the type implements IEnumerable<>
                var enumerableType = outputType
                    .GetInterfaces()
                    .SingleOrDefault(x => x.IsGenericType && x.GetGenericTypeDefinition() == typeof(IEnumerable<>));

                actualType = enumerableType?.GetGenericArguments()[0];
            }

            actualType ??= outputType;
        }

        return (T)MapTo(type, (d) => actualType.GetProperty(d.Name)?.GetValue(data));
    }

    /// <summary>
    /// Maps the given <see cref="object"/> to <typeparamref name="T"/>.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="data"></param>
    /// <returns></returns>
    public static IEnumerable<T> MapTo<T>(this IEnumerable<object?> data)
    {
        if (data is null)
        {
            return Enumerable.Empty<T>();
        }

        var type = typeof(T);

        if (type.IsValueType)
        {
            return data.Cast<T>();
        }

        var outputType = data.GetType();

        var actualType = outputType;
        // Determine the type
        if (outputType.IsGenericType)
        {
            // Determine if the underlying type is IEnumerable<>
            if (outputType.GetGenericTypeDefinition() == typeof(IEnumerable<>))
            {
                actualType = outputType.GetGenericArguments()[0];
            }
            else
            {
                // Determine whether the type implements IEnumerable<>
                var enumerableType = outputType
                    .GetInterfaces()
                    .SingleOrDefault(x => x.IsGenericType && x.GetGenericTypeDefinition() == typeof(IEnumerable<>));

                actualType = enumerableType?.GetGenericArguments()[0];
            }

            actualType ??= outputType;
        }

        IEnumerable<Func<(string Name, string DisplayName), object?>> d = data.Select(y =>
        {
            object? Func((string Name, string DisplayName) d) => actualType.GetProperty(d.Name)?.GetValue(y);
            return (Func<(string Name, string DisplayName), object?>)Func;
        });

        return MapToEnumerable(actualType, d).Cast<T>();
    }

    /// <summary>
    /// Gets all the values in an object and outputs it into a <see cref="IDictionary{string, object}"/>.
    /// This is useful in cases where the <see cref="Type"/> of the object is not visible to the executing <see cref="Assembly"/>.
    /// </summary>
    /// <param name="anonymous"></param>
    /// <returns></returns>
    internal static IDictionary<string, object?> MapObjectToDictionary(this object anonymous)
    {
        IDictionary<string, object?> expandoObject = new ExpandoObject();

        foreach (var prop in anonymous.GetType().GetProperties())
        {
            expandoObject.TryAdd(prop.Name, prop.GetValue(anonymous));
        }

        return expandoObject;
    }

    /// <summary>
    /// Maps the given data in a <see cref="Func{T, TResult}"/> to the <see cref="Type"/> specified.
    /// A <see cref="IFormatProvider"/> can be passed to ensure culture sensitive information is correctly converted. By default Invariant culture will be used.
    /// </summary>
    /// <param name="type"></param>
    /// <param name="getPropertyValue"></param>
    /// <param name="formatProvider"></param>
    /// <returns></returns>
    /// <exception cref="ArgumentNullException"></exception>
    internal static object MapTo(Type type, Func<(string Name, string DisplayName), object?> getPropertyValue, IFormatProvider? formatProvider = default)
    {
        if (type.IsAbstract || type.IsInterface)
        {
            throw new ArgumentException($"Unable to map List data to type {type.Name}. Ensure that this is not an interface or abstract class.");
        }

        formatProvider ??= CultureInfo.InvariantCulture;

        var properties = type.GetProperties();
        ConstructorInfo? ctor = type.GetConstructor(properties.Select(x => x.PropertyType).ToArray());

        IEnumerable<(PropertyInfo PropInfo, object? Value)> objectProperties = GetObjectProperties(properties)
            .Select(y => (y.PropInfo, Value: y.PropInfo.GetPropertyValue(y.Converter, y.Type, getPropertyValue, default, formatProvider)));

        if (ctor is not null)
        {
            object response = ctor.Invoke(objectProperties.Select(y => y.Value).ToArray());

            if (response is null)
            {
                throw new ArgumentNullException(type.Name, "Unable to find a suitable constructor to map dictionary to the given class.");
            }

            return response;
        }

        object? mappedObject = type.GetConstructor(Type.EmptyTypes)?.Invoke(null);

        mappedObject ??= FormatterServices.GetUninitializedObject(type);

        foreach ((PropertyInfo PropInfo, object? Value) in objectProperties)
        {
            PropInfo.SetValue(mappedObject, Value, null);
        }

        return mappedObject;
    }

    internal static IEnumerable<object?> MapToEnumerable(Type type, IEnumerable<Func<(string Name, string DisplayName), object?>> data, IFormatProvider? formatProvider = default)
    {
        if (type.IsAbstract || type.IsInterface)
        {
            throw new ArgumentException($"Unable to map List data to an abstract type {type.Name}.");
        }

        if (type.IsInterface)
        {
            throw new ArgumentException($"Unable to map List data to an interface type {type.Name}.");
        }

        if (!data.Any())
        {
            yield break;
        }

        var properties = type.GetProperties();

        formatProvider ??= CultureInfo.InvariantCulture;
        ConstructorInfo? ctor = type.GetConstructor(properties.Select(y => y.PropertyType).ToArray());

        IEnumerable<(PropertyInfo PropInfo, TypeConverter Converter, InternalType Type)> objectValues = GetObjectProperties(properties);

        if (!objectValues.Any())
        {
            yield break;
        }

        foreach (var y in data)
        {
            if (!objectValues.Any())
            {
                yield return null;
            }

            IEnumerable<(PropertyInfo PropInfo, object? Value)> propsWithValues = objectValues.Select(z => (z.PropInfo, Value: z.PropInfo.GetPropertyValue(z.Converter, z.Type, y, default, formatProvider)));

            object? response = ctor?.Invoke(propsWithValues.Select(y => y.Value).ToArray());

            if (response is not null)
            {
                yield return response;
            }

            object? mappedObject = type.GetConstructor(Type.EmptyTypes)?.Invoke(null);

            mappedObject ??= FormatterServices.GetUninitializedObject(type);

            foreach ((PropertyInfo PropInfo, object? Value) in propsWithValues)
            {
                PropInfo.SetValue(mappedObject, Value, null);
            }

            yield return mappedObject;
        }
    }

    private static object? GetPropertyValue(
        this PropertyInfo propInfo,
        TypeConverter converter,
        InternalType type,
        Func<(string Name, string DisplayName), object?> getPropertyValue,
        Func<string, IFormatProvider>? getPropFormatProvider = default,
        IFormatProvider? formatProvider = default)
    {
        string displayName = propInfo.GetDisplayName();
        object? propertyValue = getPropertyValue((displayName, propInfo.Name));

        object? value;

        IFormatProvider? provider = getPropFormatProvider is not null ? getPropFormatProvider(displayName) : formatProvider;

        if (propertyValue is null)
        {
            return propInfo.PropertyType.GetDefaultValue();
        }

        if (propertyValue is not string propVal)
        {
            return converter.ConvertFrom(propertyValue);
        }

        if (string.IsNullOrEmpty(propVal))
        {
            value = propInfo.PropertyType.GetDefaultValue();
        }
        else if (type is InternalType.Date)
        {
            if (!string.IsNullOrWhiteSpace(propVal))
            {
                if (double.TryParse(propVal, out double oaDate))
                {
                    // OA Date
                    value = DateTime.FromOADate(oaDate);
                }
                else if (propInfo.PropertyType == typeof(DateTime) || propInfo.PropertyType == typeof(DateTime?))
                {
                    value = DateTime.Parse(propVal, provider);
                }
                else if (propInfo.PropertyType == typeof(DateTimeOffset) || propInfo.PropertyType == typeof(DateTimeOffset?))
                {
                    value = DateTimeOffset.Parse(propVal, provider);
                }
                else if (propInfo.PropertyType == typeof(DateOnly) || propInfo.PropertyType == typeof(DateOnly?))
                {
                    value = DateOnly.Parse(propVal, provider);
                }
                else
                {
                    value = propInfo.PropertyType == typeof(TimeOnly) || propInfo.PropertyType == typeof(TimeOnly?)
                        ? TimeOnly.Parse(propVal, provider)
                        : converter.ConvertFromInvariantString(propVal);
                }
            }
            else
            {
                value = propInfo.PropertyType.GetDefaultValue();
            }
        }
        else
        {
            value = type is InternalType.Enum && int.TryParse(propVal, out int res)
                ? Enum.ToObject(propInfo.PropertyType, res)
                : converter.ConvertFromInvariantString(propVal);
        }

        return value;
    }

    private static IEnumerable<(PropertyInfo PropInfo, TypeConverter Converter, InternalType Type)> GetObjectProperties(PropertyInfo[] properties)
    {
        static InternalType GetType(PropertyInfo info)
        {
            if (info.PropertyType.IsEnum)
            {
                return InternalType.Enum;
            }

            if (_dateTypes.Contains(info.PropertyType))
            {
                return InternalType.Date;
            }

            return InternalType.Default;
        }

        return properties
            .Select(x =>
            {
                TypeConverter converter = TypeDescriptor.GetConverter(x.PropertyType);
                return (PropInfo: x, Converter: converter, GetType(x));
            });
    }

    /// <summary>
    /// Returns the default value for this <see cref="Type"/> using Reflection.
    /// <see cref="Activator.CreateInstance(Type)"/> is used if the type is a value type, otherwise <see cref="null"/> will be returned.
    /// </summary>
    /// <param name="type"></param>
    /// <returns></returns>
    internal static object? GetDefaultValue(this Type type)
    {
        if (type.IsValueType)
        {
            return Activator.CreateInstance(type);
        }

        return default;
    }
}
