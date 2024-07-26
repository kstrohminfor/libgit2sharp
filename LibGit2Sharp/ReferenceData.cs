using System;
using System.Globalization;
using LibGit2Sharp.Core;

namespace LibGit2Sharp;

/// <summary>
/// Backend's representation of a reference.
/// </summary>
public sealed class ReferenceData
{
    /// <summary>
    /// Reference name.
    /// </summary>
    public string RefName { get; private set; }

    /// <summary>
    /// True if symbolic; otherwise, false.
    /// </summary>
    public bool IsSymbolic { get; private set; }

    /// <summary>
    /// Object ID, if the ref isn't symbolic.
    /// </summary>
    public ObjectId ObjectId { get; private set; }

    /// <summary>
    /// Target name, if the ref is symbolic.
    /// </summary>
    public string SymbolicTarget { get; private set; }

    /// <summary>
    /// Initializes a direct reference.
    /// </summary>
    public ReferenceData(string refName, ObjectId directTarget)
    {
        this.RefName = refName;
        this.IsSymbolic = false;
        this.ObjectId = directTarget;
        this.SymbolicTarget = null;
    }

    /// <summary>
    /// Initializes a symbolic reference.
    /// </summary>
    public ReferenceData(string refName, string symbolicTarget)
    {
        this.RefName = refName;
        this.IsSymbolic = true;
        this.ObjectId = null;
        this.SymbolicTarget = symbolicTarget;
    }

    /// <inheritdoc />
    public override bool Equals(object obj)
    {
        var other = obj as ReferenceData;
        if (other == null)
        {
            return false;
        }

        return other.RefName == this.RefName
            && other.IsSymbolic == this.IsSymbolic
            && other.ObjectId == this.ObjectId
            && other.SymbolicTarget == this.SymbolicTarget;
    }

    /// <inheritdoc />
    public override int GetHashCode()
    {
        unchecked
        {
            var accumulator = this.RefName.GetHashCode();
            accumulator = accumulator * 17 + this.IsSymbolic.GetHashCode();
            if (this.ObjectId != null)
            {
                accumulator = accumulator * 17 + this.ObjectId.GetHashCode();
            }

            if (this.SymbolicTarget != null)
            {
                accumulator = accumulator * 17 + this.SymbolicTarget.GetHashCode();
            }

            return accumulator;
        }
    }

    /// <summary>
    /// Allocates a native git_reference for the <see cref="ReferenceData"/> and returns a pointer.
    /// </summary>
    internal IntPtr MarshalToPtr()
    {
        if (IsSymbolic)
        {
            return Proxy.git_reference__alloc_symbolic(RefName, SymbolicTarget);
        }
        else
        {
            return Proxy.git_reference__alloc(RefName, ObjectId.Oid);
        }
    }

    /// <summary>
    /// Marshals a git_reference into a managed <see cref="ReferenceData"/>
    /// </summary>
    internal static unsafe ReferenceData MarshalFromPtr(git_reference* ptr)
    {
        var name = Proxy.git_reference_name(ptr);
        var type = Proxy.git_reference_type(ptr);
        switch (type)
        {
            case GitReferenceType.Oid:
                var targetOid = Proxy.git_reference_target(ptr);
                return new ReferenceData(name, targetOid);
            case GitReferenceType.Symbolic:
                var targetName = Proxy.git_reference_symbolic_target(ptr);
                return new ReferenceData(name, targetName);
            default:
                throw new LibGit2SharpException(
                    string.Format(
                        CultureInfo.InvariantCulture,
                        "Unable to build a new reference from type '{0}'",
                        type));
        }
    }
}