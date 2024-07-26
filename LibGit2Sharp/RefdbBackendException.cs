using System;
using LibGit2Sharp.Core;

namespace LibGit2Sharp;

/// <summary>
/// Exception types that can be thrown from the backend.
/// Exceptions of this type will be converted to libgit2 error codes.
/// </summary>
public sealed class RefdbBackendException : LibGit2SharpException
{
    private readonly GitErrorCode code;

    private RefdbBackendException(GitErrorCode code, string message)
        : base(message, code, GitErrorCategory.Reference)
    {
        this.code = code;
    }

    /// <summary>
    /// Reference was not found.
    /// </summary>
    public static RefdbBackendException NotFound(string referenceName)
    {
        return new RefdbBackendException(GitErrorCode.NotFound, $"could not resolve reference '{referenceName}'");
    }

    /// <summary>
    /// Reference by this name already exists.
    /// </summary>
    public static RefdbBackendException Exists(string referenceName)
    {
        return new RefdbBackendException(GitErrorCode.Exists, $"will not overwrite reference '{referenceName}' without match or force");
    }

    /// <summary>
    /// Conflict between an expected reference value and the reference's actual value.
    /// </summary>
    public static RefdbBackendException Conflict(string referenceName)
    {
        return new RefdbBackendException(GitErrorCode.Conflict, $"conflict occurred while writing reference '{referenceName}'");
    }

    /// <summary>
    /// User is not allowed to alter this reference.
    /// </summary>
    /// <param name="message">Arbitrary message.</param>
    public static RefdbBackendException NotAllowed(string message)
    {
        return new RefdbBackendException(GitErrorCode.Auth, message);
    }

    /// <summary>
    /// Operation is not implemented.
    /// </summary>
    /// <param name="operation">Operation that's not implemented.</param>
    public static RefdbBackendException NotImplemented(string operation)
    {
        return new RefdbBackendException(GitErrorCode.User, $"operation '{operation}' is unsupported by this refdb backend.");
    }

    /// <summary>
    /// Transform an exception into an error code and message, which is logged.
    /// </summary>
    internal static int GetCode(Exception ex)
    {
        Proxy.git_error_set_str(GitErrorCategory.Reference, ex);
        var backendException = ex as RefdbBackendException;
        if (backendException == null)
        {
            return (int)GitErrorCode.Error;
        }

        return (int)backendException.code;
    }
}