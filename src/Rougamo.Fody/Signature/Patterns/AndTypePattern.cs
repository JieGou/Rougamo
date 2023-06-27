﻿using System.Collections.Generic;

namespace Rougamo.Fody.Signature.Patterns
{
    public class AndTypePattern : IIntermediateTypePattern
    {
        public AndTypePattern(IIntermediateTypePattern left, IIntermediateTypePattern right)
        {
            Left = left;
            Right = right;
        }

        public IIntermediateTypePattern Left { get; }

        public IIntermediateTypePattern Right { get; }

        public bool IsAny => Left.IsAny && Right.IsAny;

        public bool AssignableMatch => Left.AssignableMatch || Right.AssignableMatch;

        public GenericNamePattern SeparateOutMethod()
        {
            return Right.SeparateOutMethod();
        }

        public DeclaringTypeMethodPattern ToDeclaringTypeMethod()
        {
            var method = SeparateOutMethod();
            return new DeclaringTypeMethodPattern(this, method);
        }

        public void Compile(List<GenericParameterTypePattern> genericParameters, bool genericIn)
        {
            Left.Compile(genericParameters, genericIn);
            Right.Compile(genericParameters, genericIn);
        }

        public bool IsMatch(TypeSignature signature)
        {
            return Left.IsMatch(signature) && Right.IsMatch(signature);
        }
    }
}
