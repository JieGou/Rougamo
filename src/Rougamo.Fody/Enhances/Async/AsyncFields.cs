﻿using Mono.Cecil;
using System.Linq;

namespace Rougamo.Fody.Enhances.Async
{
    internal class AsyncFields : StateMachineFields, IStateMachineFields
    {
        public AsyncFields(
            TypeDefinition stateMachineTypeDef,
            FieldDefinition? moArray, FieldDefinition[] mos,
            FieldDefinition methodContext,
            FieldDefinition state, FieldDefinition builder,
            FieldDefinition? declaringThis, FieldDefinition?[] parameters) : base(stateMachineTypeDef)
        {
            MoArray = MakeReference(moArray);
            Mos = mos.Select(x => MakeReference(x)!).ToArray();
            MethodContext = MakeReference(methodContext)!;
            State = MakeReference(state)!;
            Builder = MakeReference(builder)!;
            Parameters = parameters.Select(x => MakeReference(x)!).ToArray();
            DeclaringThis = MakeReference(declaringThis);
        }

        public FieldReference? MoArray { get; }

        public FieldReference[] Mos { get; }

        public FieldReference MethodContext { get; }

        public FieldReference State { get; }

        public FieldReference Builder { get; }

        public FieldReference?[] Parameters { get; }

        private FieldReference? _declaringThis;
        public FieldReference? DeclaringThis
        {
            get => _declaringThis;
            set
            {
                _declaringThis = value is FieldDefinition fd ? MakeReference(fd) : value;
            }
        }

        private FieldReference? _awaiter;
        public FieldReference? Awaiter
        {
            get => _awaiter;
            set
            {
                _awaiter = value is FieldDefinition fd ? MakeReference(fd) : value;
            }
        }

        public void SetParameter(int index, FieldDefinition fieldDef)
        {
            Parameters[index] = MakeReference(fieldDef);
        }
    }
}
