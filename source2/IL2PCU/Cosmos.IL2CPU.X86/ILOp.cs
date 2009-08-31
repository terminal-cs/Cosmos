﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using CPU = Indy.IL2CPU.Assembler.X86;
using Cosmos.IL2CPU.ILOpCodes;
using System.Reflection;

namespace Cosmos.IL2CPU.X86
{
    public abstract class ILOp : Cosmos.IL2CPU.ILOp
    {
        protected new readonly Assembler Assembler;

        protected ILOp( Cosmos.IL2CPU.Assembler aAsmblr )
            : base( aAsmblr )
        {
            Assembler = ( Assembler )aAsmblr;
        }

		protected uint GetStackCountForLocal(MethodInfo aMethod, LocalVariableInfo aField)
		{
			var xSize = GetFieldStorageSize(aField.LocalType);
			var xResult = xSize / 4;
			if (xSize % 4 == 0)
			{
				xResult++;
			}
			return xResult;
		}

		protected uint GetEBPOffsetForLocal(MethodInfo aMethod, OpVar aOp)
		{
			var xBody = aMethod.MethodBase.GetMethodBody();
			uint xOffset = 0;
			for(int i = 0; i < xBody.LocalVariables.Count;i++){
				if (i == aOp.Value)
				{
					break;
				}
				var xField = xBody.LocalVariables[i];
				xOffset += GetStackCountForLocal(aMethod, xField);
			}
			return xOffset;
		}

		protected string GetLabel(MethodInfo aMethod, ILOpCode aOpCode)
		{
			return MethodInfoLabelGenerator.GenerateLabelName(aMethod.MethodBase) + "__DOT__" + aOpCode.Position.ToString("X8").ToUpper();
		}
    }
}
