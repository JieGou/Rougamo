﻿namespace Rougamo.Fody.Signature.Patterns
{
    public interface ITypePattern
    {
        bool IsAny { get; }

        bool AssignableMatch { get; }

        bool IsMatch(TypeSignature signature);
    }
}
