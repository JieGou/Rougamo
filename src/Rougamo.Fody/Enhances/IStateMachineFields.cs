﻿using Mono.Cecil;

namespace Rougamo.Fody.Enhances
{
    internal interface IStateMachineFields
    {
        FieldReference? MoArray { get; }

        FieldReference[] Mos { get; }

        FieldReference MethodContext { get; }

        FieldReference State { get; }

        FieldReference?[] Parameters { get; }

        FieldReference? DeclaringThis { get; set; }

        void SetParameter(int index, FieldDefinition fieldDef);
    }
}
