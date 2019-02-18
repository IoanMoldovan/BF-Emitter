using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using static BFEmitter.ParserCodes;

namespace BFEmitter
{
    class Program
    {
        static void Main(string[] args)
        {
            List<IParserCode> parserTree = new List<IParserCode>();

            string code = "";
            string line;
            while ((line = Console.ReadLine()) != null && line != "")
                code += line;

            char prev = (char)0;
            IParserCode workingOn = null;
            Stack<Guid> compOp = new Stack<Guid>();

            for (int i = 0; i < code.Length; i++)
            {
                if (workingOn is CompBeginOp || workingOn is CompEndOp)
                {
                    parserTree.Add(workingOn);
                    workingOn = null;
                }

                if (code[i] != prev && prev != 0 && (!(workingOn is null)))
                {
                    parserTree.Add(workingOn);
                    workingOn = null;
                }

                switch (code[i])
                {
                    case '+':
                        if (workingOn is null) workingOn = new ArithmeticOp();
                        (workingOn as ArithmeticOp).Value++;
                        break;
                    case '-':
                        if (workingOn is null) workingOn = new ArithmeticOp();
                        (workingOn as ArithmeticOp).Value--;
                        break;
                    case '<':
                        if (workingOn is null) workingOn = new ArithmeticPointerOp();
                        (workingOn as ArithmeticPointerOp).Value--;
                        break;
                    case '>':
                        if (workingOn is null) workingOn = new ArithmeticPointerOp();
                        (workingOn as ArithmeticPointerOp).Value++;
                        break;
                    case '[':
                        if (workingOn is null) workingOn = new CompBeginOp();
                        compOp.Push(Guid.NewGuid());
                        (workingOn as CompBeginOp).Tag = compOp.Peek();
                        break;
                    case ']':
                        if (workingOn is null) workingOn = new CompEndOp();
                        (workingOn as CompEndOp).Tag = compOp.Pop();
                        break;
                    case '.':
                        if (workingOn is null) workingOn = new WriteOp();
                        break;
                    case ',':
                        if (workingOn is null) workingOn = new ReadOp();
                        break;
                }

                prev = code[i];
            }

            if (!(workingOn is null)) parserTree.Add(workingOn);
            if (compOp.Count > 0) throw new Exception("UNMATCHED BLOCK");

            // We begin code-generation
            AssemblyBuilder assemblyBuilder = AssemblyBuilder.DefineDynamicAssembly(new AssemblyName("compiled"), AssemblyBuilderAccess.Save);
            ModuleBuilder moduleBuilder = assemblyBuilder.DefineDynamicModule("compiled", "compiled.exe");
            TypeBuilder typeBuilder = moduleBuilder.DefineType("Program", TypeAttributes.Class | TypeAttributes.Public);
            MethodBuilder methodBuilder = typeBuilder.DefineMethod("Main", MethodAttributes.Public | MethodAttributes.Static);

            ILGenerator emitter = methodBuilder.GetILGenerator();

            for (int i = parserTree.Count - 1; i >= 0; i--)
            {
                if (parserTree[i] is CompEndOp)
                {
                    (parserTree[i] as CompEndOp).Label = emitter.DefineLabel();
                    int pos = -1;

                    for (int j = 0; j < parserTree.Count; j++)
                        if (parserTree[j] is CompBeginOp && (parserTree[j] as CompBeginOp).Tag == (parserTree[i] as CompEndOp).Tag)
                        { pos = j; break; }

                    if (pos == -1) throw new Exception("FATAL ERROR! TAG NOT MATCHED!");

                    (parserTree[pos] as CompBeginOp).Label = emitter.DefineLabel();
                    (parserTree[pos] as CompBeginOp).OnFail = (parserTree[i] as CompEndOp).Label;
                    (parserTree[i] as CompEndOp).OnFail = (parserTree[pos] as CompBeginOp).Label;
                }
            }

            // Write pre-Init code
            emitter.DeclareLocal(typeof(byte[]));
            emitter.DeclareLocal(typeof(int));
            emitter.DeclareLocal(typeof(int));

            emitter.Emit(OpCodes.Ldc_I4, 10000);
            emitter.Emit(OpCodes.Newarr, typeof(byte));
            emitter.Emit(OpCodes.Stloc_0);

            emitter.Emit(OpCodes.Ldc_I4_0);
            emitter.Emit(OpCodes.Stloc_1);

            emitter.Emit(OpCodes.Ldc_I4_0);
            emitter.Emit(OpCodes.Stloc_2);

            MethodInfo writeMethod = typeof(Console).GetMethod("Write", new Type[] { typeof(char) });
            MethodInfo readMethod = typeof(Console).GetMethod("Read");

            foreach (IParserCode pCode in parserTree)
            {
                if (pCode is ArithmeticOp)
                {
                    ArithmeticOp aritNorm = pCode as ArithmeticOp;
                    if (aritNorm.Value == 0) continue;
                    emitter.Emit(OpCodes.Ldloc_0);
                    emitter.Emit(OpCodes.Ldloc_1);
                    emitter.Emit(OpCodes.Ldelem_I1);
                    emitter.Emit(OpCodes.Ldc_I4, Math.Abs(aritNorm.Value));
                    if (aritNorm.Value > 0) emitter.Emit(OpCodes.Add);
                    else emitter.Emit(OpCodes.Sub);
                    emitter.Emit(OpCodes.Stloc_2);
                    emitter.Emit(OpCodes.Ldloc_0);
                    emitter.Emit(OpCodes.Ldloc_1);
                    emitter.Emit(OpCodes.Ldloc_2);
                    emitter.Emit(OpCodes.Stelem_I1);
                }
                else if (pCode is ArithmeticPointerOp)
                {
                    ArithmeticPointerOp arit = pCode as ArithmeticPointerOp;
                    if (arit.Value == 0) continue;
                    emitter.Emit(OpCodes.Ldloc_1);
                    emitter.Emit(OpCodes.Ldc_I4, Math.Abs(arit.Value));
                    if (arit.Value > 0) emitter.Emit(OpCodes.Add);
                    else emitter.Emit(OpCodes.Sub);
                    emitter.Emit(OpCodes.Stloc_1);
                }
                else if (pCode is WriteOp)
                {
                    emitter.Emit(OpCodes.Ldloc_0);
                    emitter.Emit(OpCodes.Ldloc_1);
                    emitter.Emit(OpCodes.Ldelem_I1);
                    emitter.EmitCall(OpCodes.Call, writeMethod, new Type[] { typeof(char) });
                }
                else if (pCode is ReadOp)
                {
                    emitter.EmitCall(OpCodes.Call, readMethod, new Type[] { typeof(int) });
                    emitter.Emit(OpCodes.Conv_U1);
                    emitter.Emit(OpCodes.Stloc_2);

                    emitter.Emit(OpCodes.Ldloc_0);
                    emitter.Emit(OpCodes.Ldloc_1);
                    emitter.Emit(OpCodes.Ldloc_2);
                    emitter.Emit(OpCodes.Stelem_I1);

                }
                else if (pCode is CompBeginOp)
                {
                    CompBeginOp beginOp = pCode as CompBeginOp;

                    emitter.Emit(OpCodes.Ldloc_0);
                    emitter.Emit(OpCodes.Ldloc_1);
                    emitter.Emit(OpCodes.Ldelem_I1);
                    emitter.Emit(OpCodes.Ldc_I4_0);
                    emitter.Emit(OpCodes.Ceq);
                    emitter.Emit(OpCodes.Brtrue, beginOp.OnFail);


                    emitter.Emit(OpCodes.Nop);
                    emitter.MarkLabel(beginOp.Label);
                    emitter.Emit(OpCodes.Nop);
                }
                else if (pCode is CompEndOp)
                {
                    CompEndOp endOp = pCode as CompEndOp;

                    emitter.Emit(OpCodes.Ldloc_0);
                    emitter.Emit(OpCodes.Ldloc_1);
                    emitter.Emit(OpCodes.Ldelem_I1);
                    emitter.Emit(OpCodes.Ldc_I4_0);
                    emitter.Emit(OpCodes.Ceq);
                    emitter.Emit(OpCodes.Brfalse, endOp.OnFail);

                    emitter.Emit(OpCodes.Nop);
                    emitter.MarkLabel(endOp.Label);
                    emitter.Emit(OpCodes.Nop);
                }
            }
            emitter.Emit(OpCodes.Ret);
            typeBuilder.CreateType();

            assemblyBuilder.SetEntryPoint(methodBuilder);
            assemblyBuilder.Save("compiled.exe");
        }
    }
}
