using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Reflection;
using System.Text.Json.Serialization;

namespace DynamicModule.Extensions;

internal static class PropertyInfoExtensions
{
    /// <summary>
    /// Retrieves the property name by looking for a display name attribute and then fallbacks to the <see cref="PropertyInfo"/> name.
    /// <br/>The order determined is as follow:
    /// <br/> 1: <see cref="JsonPropertyNameAttribute"/>
    /// <br/>2: <see cref="DisplayNameAttribute"/>
    /// <br/>3: <see cref="DisplayAttribute"/>
    /// <br/>4: <see cref="DisplayColumnAttribute"/>
    /// </summary>
    /// <param name="propertyInfo"></param>
    /// <returns></returns>
    internal static string GetDisplayName(this PropertyInfo propertyInfo)
    {
        var displayName = propertyInfo.GetCustomAttribute(typeof(JsonPropertyNameAttribute)) is JsonPropertyNameAttribute js ? js.Name : null;
        displayName ??= propertyInfo.GetCustomAttribute(typeof(DisplayNameAttribute)) is DisplayNameAttribute dn ? dn.DisplayName : null;
        displayName ??= propertyInfo.GetCustomAttribute(typeof(DisplayAttribute)) is DisplayAttribute da ? da.Name : null;
        displayName ??= propertyInfo.GetCustomAttribute(typeof(DisplayColumnAttribute)) is DisplayColumnAttribute dc ? dc.DisplayColumn : null;

        return displayName ?? propertyInfo.Name;
    }
}
