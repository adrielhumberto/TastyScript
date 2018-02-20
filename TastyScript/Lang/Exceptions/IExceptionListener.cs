﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TastyScript.Lang.Exceptions
{
    public interface IExceptionListener
    {
        void Throw(ExceptionHandler ex);
        void ThrowSilent(ExceptionHandler ex);
    }
    public class ExceptionListener : IExceptionListener
    {
        public void Throw(ExceptionHandler ex)
        {
            TokenParser.Stop = true;
            IO.Output.Print($"\n[ERROR] ({ex.Type.ToString()}) {ex.Message} File: {ex.Line}\n", ConsoleColor.Red);
        }
        public void ThrowSilent(ExceptionHandler ex)
        {
            IO.Output.Print($"[SILENT ERROR] ({ex.Type.ToString()}) {ex.Message} File: {ex.Line}", ConsoleColor.Yellow);
        }
    }
}