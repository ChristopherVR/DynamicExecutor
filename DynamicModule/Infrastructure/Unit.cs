namespace DynamicModule.Infrastructure;
/// <summary>
/// Represents a void type, since <see cref="void"/> is not a valid return type in C#.
/// </summary>
/// See - https://github.com/jbogard/MediatR/blob/c1ad66ef52434a22c10a0de5e060d13b185ef80b/src/MediatR/Unit.cs
public record struct Unit : IEquatable<Unit>, IComparable<Unit>, IComparable
{
    /// <summary>
    /// Default and only value of the <see cref="Unit"/> type.
    /// </summary>
    public static readonly Unit Value;

    /// <summary>
    /// Task from a <see cref="Unit"/> type.
    /// </summary>
    public static readonly Task<Unit> Task = System.Threading.Tasks.Task.FromResult(Value);

    /// <summary>
    /// Compares the current object with another object of the same type.
    /// </summary>
    /// <param name="other">An object to compare with this object.</param>
    /// <returns>
    /// A value that indicates the relative order of the objects being compared.
    /// The return value has the following meanings:
    ///  - Less than zero: This object is less than the <paramref name="other" /> parameter.
    ///  - Zero: This object is equal to <paramref name="other" />.
    ///  - Greater than zero: This object is greater than <paramref name="other" />.
    /// </returns>
    public int CompareTo(Unit other) => 0;

    /// <summary>
    /// Compares the current instance with another object of the same type and returns an integer that indicates whether the current instance precedes, follows, or occurs in the same position in the sort order as the other object.
    /// </summary>
    /// <param name="obj">An object to compare with this instance.</param>
    /// <returns>
    /// A value that indicates the relative order of the objects being compared.
    /// The return value has these meanings:
    ///  - Less than zero: This instance precedes <paramref name="obj" /> in the sort order.
    ///  - Zero: This instance occurs in the same position in the sort order as <paramref name="obj" />.
    ///  - Greater than zero: This instance follows <paramref name="obj" /> in the sort order.
    /// </returns>
    int IComparable.CompareTo(object? obj) => 0;

    /// <summary>
    /// Returns a hash code for this instance.
    /// </summary>
    /// <returns>
    /// A hash code for this instance, suitable for use in hashing algorithms and data structures like a hash table.
    /// </returns>
    public override int GetHashCode() => 0;

    /// <summary>
    /// Determines whether the current object is equal to another object of the same type.
    /// </summary>
    /// <param name="other">An object to compare with this object.</param>
    /// <returns>
    /// <c>true</c> if the current object is equal to the <paramref name="other" /> parameter; otherwise, <c>false</c>.
    /// </returns>
    public bool Equals(Unit other) => true;

    /// <summary>
    /// Returns a <see cref="string" /> that represents this instance.
    /// </summary>
    /// <returns>A <see cref="string" /> that represents this instance.</returns>
    public override string ToString() => "()";

    public static bool operator <(Unit left, Unit right) => left.CompareTo(right) < 0;

    public static bool operator <=(Unit left, Unit right) => left.CompareTo(right) <= 0;

    public static bool operator >(Unit left, Unit right) => left.CompareTo(right) > 0;

    public static bool operator >=(Unit left, Unit right) => left.CompareTo(right) >= 0;
}
