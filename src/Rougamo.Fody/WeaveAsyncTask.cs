﻿using Mono.Cecil;
using Mono.Cecil.Cil;
using System.Linq;

namespace Rougamo.Fody
{
    partial class ModuleWeaver
    {
        private void AsyncMethodWeave(RouMethod rouMethod)
        {
            var returnTypeRef = AsyncOnEntry(rouMethod, out var stateTypeDef, out var mosFieldDef, out var contextFieldDef, out var builderDef);
            AsyncStateMachineExtract(stateTypeDef, builderDef, out var moveNextMethodDef, out var setResultIns);
            FieldReference mosFieldRef = mosFieldDef;
            FieldReference contextFieldRef = contextFieldDef;
            if (stateTypeDef.HasGenericParameters)
            {
                // generic return type will get in
                // public async Task<MyClass<T>> Mt<T>()
                var stateTypeRef = new GenericInstanceType(stateTypeDef);
                foreach (var parameter in stateTypeDef.GenericParameters)
                {
                    stateTypeRef.GenericArguments.Add(parameter);
                }
                mosFieldRef = new FieldReference(mosFieldDef.Name, mosFieldDef.FieldType, stateTypeRef);
                contextFieldRef = new FieldReference(contextFieldDef.Name, contextFieldDef.FieldType, stateTypeRef);
            }
            AsyncOnExceptionWithExit(rouMethod, moveNextMethodDef, mosFieldRef, contextFieldRef);
            AsyncOnSuccessWithExit(rouMethod, moveNextMethodDef, mosFieldRef, contextFieldRef, setResultIns, returnTypeRef);
            moveNextMethodDef.Body.OptimizePlus();
            rouMethod.MethodDef.Body.OptimizePlus();
        }

        private TypeReference AsyncOnEntry(RouMethod rouMethod, out TypeDefinition stateTypeDef, out FieldDefinition mosFieldDef, out FieldDefinition contextFieldDef, out TypeDefinition builderDef)
        {
            AsyncFieldDefinition(rouMethod, out stateTypeDef, out mosFieldDef, out contextFieldDef);
            var tStateTypeDef = stateTypeDef;
            var stateMachineVariable = rouMethod.MethodDef.Body.Variables.Single(x => x.VariableType.Resolve() == tStateTypeDef);
            var taskBuilderPreviousIns = GetAsyncTaskMethodBuilderCreatePreviousIns(rouMethod.MethodDef.Body, out builderDef);
            var mosFieldRef = new FieldReference(mosFieldDef.Name, mosFieldDef.FieldType, stateMachineVariable.VariableType);
            var contextFieldRef = new FieldReference(contextFieldDef.Name, contextFieldDef.FieldType, stateMachineVariable.VariableType);
            InitMosField(rouMethod, mosFieldRef, stateMachineVariable, taskBuilderPreviousIns);
            InitMethodContextField(rouMethod, contextFieldRef, stateMachineVariable, taskBuilderPreviousIns);
            ExecuteMoMethod(Constants.METHOD_OnEntry, rouMethod.MethodDef, rouMethod.Mos.Count, stateMachineVariable, mosFieldRef, contextFieldRef, taskBuilderPreviousIns);

            var returnType = rouMethod.MethodDef.ReturnType;
            return returnType.Is(Constants.TYPE_Task) || returnType.Is(Constants.TYPE_ValueTask) ? null : ((GenericInstanceType)returnType).GenericArguments[0];
        }

        private void AsyncFieldDefinition(RouMethod rouMethod, out TypeDefinition stateTypeDef, out FieldDefinition mosFieldDef, out FieldDefinition contextFieldDef)
        {
            stateTypeDef = rouMethod.MethodDef.ResolveAsyncStateMachine();
            var fieldAttributes = FieldAttributes.Public;
            mosFieldDef = new FieldDefinition(Constants.FIELD_RougamoMos, fieldAttributes, _typeIMoArrayRef);
            contextFieldDef = new FieldDefinition(Constants.FIELD_RougamoContext, fieldAttributes, _typeMethodContextRef);
            stateTypeDef.Fields.Add(mosFieldDef);
            stateTypeDef.Fields.Add(contextFieldDef);
        }

        private void AsyncStateMachineExtract(TypeDefinition stateMachineTypeDef, TypeDefinition builderTypeDef, out MethodDefinition moveNextMethodDef, out Instruction setResultIns)
        {
            //builderFieldDef = stateMachineTypeDef.Fields.Single(f => f.FieldType.Resolve() == builderTypeDef);
            moveNextMethodDef = stateMachineTypeDef.Methods.Single(m => m.Name == Constants.METHOD_MoveNext);
            setResultIns = moveNextMethodDef.Body.Instructions.SingleOrDefault(x => x.Operand is MethodReference methodRef
                                        && methodRef.DeclaringType.Resolve() == builderTypeDef && methodRef.Name == Constants.METHOD_SetResult);
        }

        private void AsyncOnExceptionWithExit(RouMethod rouMethod, MethodDefinition moveNextMethodDef, FieldReference mosFieldRef, FieldReference contextFieldRef)
        {
            var instructions = moveNextMethodDef.Body.Instructions;
            var exceptionHandler = OuterExceptionHandler(moveNextMethodDef.Body);
            var ldlocException = exceptionHandler.HandlerStart.Stloc2Ldloc($"[{rouMethod.MethodDef.FullName}] exception handler first instruction is not stloc.s exception");

            var next = exceptionHandler.HandlerStart.Next;
            instructions.InsertBefore(next, Instruction.Create(OpCodes.Ldarg_0));
            instructions.InsertBefore(next, Instruction.Create(OpCodes.Ldfld, contextFieldRef));
            instructions.InsertBefore(next, ldlocException);
            instructions.InsertBefore(next, Instruction.Create(OpCodes.Callvirt, _methodMethodContextSetExceptionRef));
            var splitNop = Instruction.Create(OpCodes.Nop);
            instructions.InsertBefore(next, splitNop);
            ExecuteMoMethod(Constants.METHOD_OnException, moveNextMethodDef, rouMethod.Mos.Count, null, mosFieldRef, contextFieldRef, splitNop);
            ExecuteMoMethod(Constants.METHOD_OnExit, moveNextMethodDef, rouMethod.Mos.Count, null, mosFieldRef, contextFieldRef, next);
        }

        private void AsyncOnSuccessWithExit(RouMethod rouMethod, MethodDefinition moveNextMethodDef, FieldReference mosFieldRef, FieldReference contextFieldRef, Instruction setResultIns, TypeReference returnTypeRef)
        {
            if (setResultIns == null) return; // 100% throw exception

            var instructions = moveNextMethodDef.Body.Instructions;
            var closeThisIns = setResultIns.ClosePreviousLdarg0(rouMethod.MethodDef);
            if (returnTypeRef != null)
            {
                var ldlocResult = setResultIns.Previous.Copy();
                instructions.InsertBefore(closeThisIns, Instruction.Create(OpCodes.Ldarg_0));
                instructions.InsertBefore(closeThisIns, Instruction.Create(OpCodes.Ldfld, contextFieldRef));
                instructions.InsertBefore(closeThisIns, ldlocResult);
                if (returnTypeRef.IsValueType || returnTypeRef.IsEnum(out _) && !returnTypeRef.IsArray || returnTypeRef.IsGenericParameter)
                {
                    instructions.InsertBefore(closeThisIns, Instruction.Create(OpCodes.Box, ldlocResult.GetVariableType(moveNextMethodDef.Body)));
                }
                instructions.InsertBefore(closeThisIns, Instruction.Create(OpCodes.Callvirt, _methodMethodContextSetReturnValueRef));
            }
            var splitNop = Instruction.Create(OpCodes.Nop);
            instructions.InsertBefore(closeThisIns, splitNop);
            ExecuteMoMethod(Constants.METHOD_OnSuccess, moveNextMethodDef, rouMethod.Mos.Count, null, mosFieldRef, contextFieldRef, splitNop);
            ExecuteMoMethod(Constants.METHOD_OnExit, moveNextMethodDef, rouMethod.Mos.Count, null, mosFieldRef, contextFieldRef, closeThisIns);
        }

        private ExceptionHandler OuterExceptionHandler(MethodBody methodBody)
        {
            ExceptionHandler exceptionHandler = null;
            int offset = methodBody.Instructions.First().Offset;
            foreach (var handler in methodBody.ExceptionHandlers)
            {
                if (handler.HandlerType != ExceptionHandlerType.Catch) continue;
                if (handler.TryEnd.Offset > offset)
                {
                    exceptionHandler = handler;
                    offset = handler.TryEnd.Offset;
                }
            }
            return exceptionHandler ?? throw new RougamoException("can not find outer exception handler");
        }
    }
}