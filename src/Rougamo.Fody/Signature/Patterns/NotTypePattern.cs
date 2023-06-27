﻿using System.Collections.Generic;

namespace Rougamo.Fody.Signature.Patterns
{
    public class NotTypePattern : IIntermediateTypePattern
    {
        public NotTypePattern(IIntermediateTypePattern innerPattern)
        {
            InnerPattern = innerPattern;
        }

        public IIntermediateTypePattern InnerPattern { get; }

        public bool IsAny => InnerPattern.IsAny;

        public bool AssignableMatch => InnerPattern.AssignableMatch;

        public GenericNamePattern SeparateOutMethod()
        {
            return InnerPattern.SeparateOutMethod();
        }

        public DeclaringTypeMethodPattern ToDeclaringTypeMethod()
        {
            var method = SeparateOutMethod();
            return new NotDeclaringTypeMethodPattern(InnerPattern, method);
        }

        public void Compile(List<GenericParameterTypePattern> genericParameters, bool genericIn)
        {
            InnerPattern.Compile(genericParameters, genericIn);
        }

        public bool IsMatch(TypeSignature signature)
        {
            return InnerPattern.IsAny || !InnerPattern.IsMatch(signature);
        }
    }
}
