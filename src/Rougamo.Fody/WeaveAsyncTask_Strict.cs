﻿using Mono.Cecil;
using Mono.Cecil.Cil;
using Rougamo.Fody.Enhances;
using Rougamo.Fody.Enhances.Async;
using System;
using System.Collections.Generic;
using System.Linq;
using static Mono.Cecil.Cil.Instruction;

namespace Rougamo.Fody
{
    partial class ModuleWeaver
    {
        private void StrictAsyncTaskMethodWeave(RouMethod rouMethod, TypeDefinition stateMachineTypeDef)
        {
            var actualStateMachineTypeDef = StrictAsyncStateMachineClone(stateMachineTypeDef);
            if (actualStateMachineTypeDef == null) return;

            var actualMethodDef = StrictAsyncSetupMethodClone(rouMethod.MethodDef, stateMachineTypeDef, actualStateMachineTypeDef);
            rouMethod.MethodDef.DeclaringType.Methods.Add(actualMethodDef);

            var moveNextDef = stateMachineTypeDef.Methods.Single(m => m.Name == Constants.METHOD_MoveNext);
            //moveNextDef.Clear();

        }

        private TypeDefinition? StrictAsyncStateMachineClone(TypeDefinition stateMachineTypeDef)
        {
            var typeName = $"$Rougamo_{stateMachineTypeDef.Name}";
            if (stateMachineTypeDef.DeclaringType.NestedTypes.Any(x => x.Name == typeName)) return null;

            var actualTypeDef = stateMachineTypeDef.Clone(typeName);
            actualTypeDef.DeclaringType.NestedTypes.Add(actualTypeDef);

            return actualTypeDef;
        }

        private MethodDefinition StrictAsyncSetupMethodClone(MethodDefinition methodDef, TypeDefinition stateMachineTypeDef, TypeDefinition clonedStateMachineTypeDef)
        {
            var clonedMethodDef = methodDef.Clone($"$Rougamo_{methodDef.Name}");

            StrictAsyncAsyncStateMachineAttributeClone(clonedMethodDef, clonedStateMachineTypeDef);

            var cloneStateMachineTypeRef = clonedStateMachineTypeDef.MakeReference();
            var stateMachine = clonedMethodDef.Body.Variables.Single(x => x.VariableType.Resolve() == stateMachineTypeDef);
            var index = clonedMethodDef.Body.Variables.IndexOf(stateMachine);
            clonedMethodDef.Body.Variables.Remove(stateMachine);
            var clonedStateMachine = new VariableDefinition(cloneStateMachineTypeRef); //clonedMethodDef.Body.CreateVariable(stateMachineTypeRef);
            clonedMethodDef.Body.Variables.Insert(index, clonedStateMachine);

            var fieldMap = new Dictionary<FieldDefinition, FieldReference>();
            foreach (var fieldDef in stateMachineTypeDef.Fields)
            {
                fieldMap[fieldDef] = new FieldReference(fieldDef.Name, fieldDef.FieldType, cloneStateMachineTypeRef);
            }

            foreach (var instruction in clonedMethodDef.Body.Instructions)
            {
                if (instruction.Operand == null) continue;

                if (instruction.Operand is MethodReference methodRef)
                {
                    if (methodRef.Resolve().IsConstructor && methodRef.DeclaringType.Resolve() == stateMachineTypeDef)
                    {
                        var stateMachineCtorDef = clonedStateMachineTypeDef.Methods.Single(x => x.IsConstructor && !x.IsStatic);
                        var stateMachineCtorRef = cloneStateMachineTypeRef.GenericTypeMethodReference(stateMachineCtorDef.ImportInto(ModuleDefinition), ModuleDefinition);
                        instruction.Operand = stateMachineCtorRef;
                    }
                    else if (methodRef is GenericInstanceMethod gim)
                    {
                        var idx = gim.GenericArguments.IndexOf(stateMachineTypeDef);
                        if (idx != -1)
                        {
                            gim.GenericArguments.RemoveAt(idx);
                            gim.GenericArguments.Insert(idx, clonedStateMachineTypeDef);
                        }
                    }
                }
                else if (instruction.Operand is FieldReference fr && fieldMap.TryGetValue(fr.Resolve(), out var fieldRef))
                {
                    instruction.Operand = fieldRef;
                }
                else if (instruction.Operand == stateMachine)
                {
                    instruction.Operand = clonedStateMachine;
                }
            }

            //var isValueType = clonedStateMachineTypeDef.IsValueType;
            //var builderFieldDef = clonedStateMachineTypeDef.Fields.Single(x => x.Name == Constants.FIELD_Builder);
            //var builderTypeRef = builderFieldDef.FieldType;
            //var builderFieldRef = new FieldReference(builderFieldDef.Name, builderTypeRef, stateMachineTypeRef);
            //var builderCreateMethodRef = builderTypeRef.GenericTypeMethodReference(builderTypeRef.Resolve().Methods.Single(x => x.Name == "Create"), ModuleDefinition);
            //var thisFieldDef = clonedStateMachineTypeDef.Fields.Single(x => x.Name = Constants.)

            //var instructions = clonedMethodDef.Body.Instructions;

            //if (isValueType)
            //{
            //    instructions.Add(Create(OpCodes.Ldloca, stateMachine));
            //}
            //else
            //{
            //    var stateMachineCtorDef = clonedStateMachineTypeDef.Methods.Single(x => x.IsConstructor && !x.IsStatic);
            //    var stateMachineCtorRef = stateMachineTypeRef.GenericTypeMethodReference(stateMachineCtorDef.ImportInto(ModuleDefinition), ModuleDefinition);
            //    instructions.Add(Create(OpCodes.Newobj, stateMachineCtorRef));
            //    instructions.Add(Create(OpCodes.Stloc, stateMachine));
            //    instructions.Add(Create(OpCodes.Ldloc, stateMachine));
            //}
            //instructions.Add(Create(OpCodes.Call, builderCreateMethodRef));
            //instructions.Add(Create(OpCodes.Stfld, builderFieldRef));
            //if (isValueType)
            //{
            //    instructions.Add(Create(OpCodes.Ldloca, stateMachine));
            //}
            //else
            //{
            //    instructions.Add(Create(OpCodes.Ldloc, stateMachine));
            //}
            //instructions.Add(Create(OpCodes.Ldarg_0));
            //instructions.Add(Create())

            return clonedMethodDef;
        }

        private void StrictAsyncAsyncStateMachineAttributeClone(MethodDefinition clonedMethodDef, TypeDefinition clonedStateMachineTypeDef)
        {
            var asyncStateMachineAttribute = clonedMethodDef.CustomAttributes.Single(x => x.Is(Constants.TYPE_AsyncStateMachineAttribute));
            clonedMethodDef.CustomAttributes.Remove(asyncStateMachineAttribute);

            asyncStateMachineAttribute = new CustomAttribute(_methodAsyncStateMachineAttributeCtorRef);
            asyncStateMachineAttribute.ConstructorArguments.Add(new CustomAttributeArgument(_typeSystemRef, clonedStateMachineTypeDef));
            clonedMethodDef.CustomAttributes.Add(asyncStateMachineAttribute);
        }
    }
}
